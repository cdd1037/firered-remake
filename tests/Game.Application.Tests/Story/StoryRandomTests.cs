using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Randomness;

namespace FireRed.Tests.Application.Story;

[TestClass, TestCategory("Game")]
public sealed class StoryRandomTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static PretStoryCompilation Compile(string source) => PretStoryCompiler.Compile(
        [new("fixture.inc", source, new(SourceKind.OriginalFixture, "fixture.inc", "fixture"))]);

    [TestMethod]
    [DataRow("3", 3)]
    [DataRow("0x4", 4)]
    [DataRow("VAR_TEMP_1", 3)]
    public void CompilerAndInterpreterResolveMaximumAndPreserveSingleDraw(string operand, int maximum)
    {
        var catalog = Compile($"Start::\n setvar VAR_TEMP_1, 3\n random {operand}\n copyvar VAR_CHOSEN, VAR_RESULT\n end");
        Assert.AreEqual(StoryOp.Random, catalog.FindProgram("Start")!.Instructions[1].Op);
        var start = StoryRules.Start(StoryState.Empty, "Start", catalog);
        Assert.AreEqual(TransitionStatus.Accepted, start.Status);
        Assert.AreEqual(StoryWaitKind.NativeEffect, start.State.Wait!.Kind);
        Assert.IsFalse(start.State.Variables.ContainsKey("VAR_CHOSEN"));
        var rng = new RngState(0x12345678, 7);
        var expected = Gen3Rng.Next(rng, "story.random", maximum);
        var applied = StoryRandomRules.Apply(start.State, rng, start.Effects.Single());
        Assert.AreEqual(expected.State, applied.Rng);
        Assert.AreEqual(expected.Trace.Result, applied.Story.Variables["VAR_RESULT"]);
        var restored = JsonSerializer.Deserialize<StoryState>(JsonSerializer.Serialize(applied.Story))!;
        var end = StoryRules.Continue(restored, restored.Wait!.CorrelationId, catalog);
        Assert.AreEqual(expected.Trace.Result, end.State.Variables["VAR_CHOSEN"]);
        Assert.IsTrue(end.State.Completed);
    }

    [TestMethod]
    public void ZeroMaximumAndInvalidArityFailAtomically()
    {
        foreach (string instruction in new[] { "random 0", "random 65536" })
        {
            var result = StoryRules.Start(StoryState.Empty, "Start", Compile($"Start::\n setflag FLAG_CHANGED\n {instruction}\n end"));
            Assert.AreEqual(TransitionStatus.Fault, result.Status);
            Assert.IsFalse(result.State.Flags.ContainsKey("FLAG_CHANGED"));
            Assert.IsTrue(result.Effects.IsEmpty);
        }
    }

    [TestMethod]
    public void CompilerRejectsInvalidRandomArity()
    {
        foreach (string instruction in new[] { "random", "random 3, 4" })
            Assert.Throws<ContentValidationException>(() => Compile($"Start::\n {instruction}\n end"));
    }

    [TestMethod]
    public void DomainUsesExactModuloAndRejectsStaleEffectAndExhaustedRng()
    {
        var catalog = Compile("Start::\n random 3\n end");
        var start = StoryRules.Start(StoryState.Empty, "Start", catalog);
        var effect = start.Effects.Single();
        for (uint seed = 0; seed < 100; seed++)
        {
            var result = StoryRandomRules.Apply(start.State, new(seed), effect);
            uint next = unchecked(seed * 0x41C64E6Du + 0x6073u);
            Assert.AreEqual((int)((next >> 16) % 3), result.Story.Variables["VAR_RESULT"]);
            Assert.AreEqual(new RngState(next, 1), result.Rng);
        }
        Assert.Throws<InvalidDataException>(() => StoryRandomRules.Apply(start.State, new(0), effect with { Sequence = effect.Sequence + 1 }));
        Assert.Throws<OverflowException>(() => StoryRandomRules.Apply(start.State, new(0, ulong.MaxValue), effect));
    }

    [TestMethod]
    [DataRow("CeruleanCity_EventScript_Lass", 3)]
    [DataRow("CeruleanCity_EventScript_Slowbro", 4)]
    [DataRow("SSAnne_Kitchen_EventScript_Chef4", 3)]
    public void GeneratedNpcProgramsReachEveryRandomBranch(string program, int maximum)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        Assert.AreEqual(StoryOp.Random, catalog.FindProgram(program)!.Instructions.Single(i => i.Opcode == "random").Op);
        var branches = new HashSet<int>();
        for (uint seed = 0; seed < 100; seed++)
        {
            var rng = new RngState(seed);
            var step = StoryRules.Start(StoryState.Empty, program, catalog);
            int waits = 0;
            while (!step.State.Completed)
            {
                Assert.AreEqual(TransitionStatus.Accepted, step.Status, step.Diagnostic?.Message);
                Assert.IsTrue(++waits < 30);
                var story = step.State;
                foreach (var effect in step.Effects.Where(e => e.Opcode == "random"))
                {
                    var drawn = StoryRandomRules.Apply(story, rng, effect);
                    story = drawn.Story;
                    rng = drawn.Rng;
                    branches.Add(story.Variables["VAR_RESULT"]);
                }
                step = StoryRules.Continue(story, story.Wait!.CorrelationId, catalog);
            }
            Assert.AreEqual(1UL, rng.Cursor);
        }
        Assert.AreEqual(maximum, branches.Count);
    }

    [TestMethod]
    public void StoryAdmissionPreflightDoesNotConsumeExtraDrawsAndReplayIsDeterministic()
    {
        var original = CurrentCampaignFixture.Catalog;
        Assert.IsNotNull(original.ConnectedFieldMaps);
        var catalog = (PretCampaignCatalog)typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        var field = typeof(PretCampaignCatalog).GetField("_scripts", Private)!;
        var scripts = (PretStoryCompilation)field.GetValue(original)!;
        var program = Compile("RandomTest::\n random 3\n copyvar VAR_FIRST, VAR_RESULT\n random 4\n copyvar VAR_SECOND, VAR_RESULT\n end").FindProgram("RandomTest")!;
        field.SetValue(catalog, scripts with { Programs = scripts.Programs.SetItem(program.Id, program) });
        var basis = CurrentCampaignFixture.Checkpoint.State with
        { Script = StoryState.Empty, Dialogue = null, PresentationEffects = [], Motions = [], EntryPrograms = [] };
        var session = CampaignSession.CreateCurrent(catalog);
        typeof(CampaignSession).GetField("_commands", Private)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(basis, session.RulesVersion, catalog.ContentVersion));
        CampaignState Run()
        {
            var entered = (CampaignState)typeof(CampaignSession).GetMethod("StartStory", Private)!.Invoke(session, [basis, program.Id])!;
            return (CampaignState)typeof(CampaignSession).GetMethod("Pump", Private)!.Invoke(session, [entered])!;
        }
        var first = Gen3Rng.Next(basis.World.Rng, "story.random", 3);
        var second = Gen3Rng.Next(first.State, "story.random", 4);
        var result = Run();
        Assert.AreEqual(second.State, result.World.Rng);
        Assert.AreEqual(first.Trace.Result, result.Script.Variables["VAR_FIRST"]);
        Assert.AreEqual(second.Trace.Result, result.Script.Variables["VAR_SECOND"]);
        Assert.AreEqual(result.World.Rng, Run().World.Rng);
        Assert.AreSame(basis, session.State);
    }
}
