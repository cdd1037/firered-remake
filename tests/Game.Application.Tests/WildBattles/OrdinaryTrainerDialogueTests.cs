using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class OrdinaryTrainerDialogueTests
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static ForestTrainerBattleDefinition[] Definitions(CampaignSession session) =>
        ((IEnumerable<ForestTrainerBattleDefinition>)typeof(CampaignSession)
            .GetProperty("OrdinaryTrainerScripts", Members)!.GetValue(session)!)
        .Where(definition => definition.Mode == 0).OrderBy(definition => definition.ScriptEntry, StringComparer.Ordinal).ToArray();

    private static CampaignState Call(CampaignSession session, string method, params object[] arguments)
    {
        try
        {
            return (CampaignState)typeof(CampaignSession).GetMethod(method, Members)!.Invoke(session, arguments)!;
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }

    private static void Install(CampaignSession session, PretCampaignCatalog catalog, CampaignState state) =>
        typeof(CampaignSession).GetField("_commands", Members)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, catalog.ContentVersion));

    // An explicit field-script unit fixture, not a saved game, a fabricated battle result,
    // or evidence of reaching this map naturally. Already-fought interaction needs no party.
    private static CampaignState DefeatedNpc(CampaignSession session, PretCampaignCatalog catalog,
        ForestTrainerBattleDefinition definition)
    {
        var basis = session.State;
        var map = catalog.Maps.Keys.Single(id =>
            catalog.MapDefinition(id).Actors.Any(actor => actor.ScriptId == definition.ScriptEntry));
        var template = catalog.MapDefinition(map).Actors.Single(actor => actor.ScriptId == definition.ScriptEntry);
        var actor = new CampaignActor(template.LocalId, template.SourceLocalId, template.GraphicsId,
            template.X, template.Y, template.Elevation, Direction.South, template.MovementType,
            template.ScriptId, template.HideFlag, true, false, template.X, template.Y);
        return basis with {
            World = basis.World with { Player = new(map, actor.X, actor.Y + 1, actor.Elevation, Direction.North) },
            Actors = ImmutableSortedDictionary<string, CampaignActor>.Empty.WithComparers(StringComparer.Ordinal)
                .Add(actor.LocalId, actor),
            ObjectSlots = ImmutableSortedDictionary<int, string>.Empty.Add(0, "LOCALID_PLAYER").Add(1, actor.LocalId),
            SelectedObjectSlot = 1, LastTalked = actor.LocalId,
            BattledTrainers = basis.BattledTrainers.Clear().Add(definition.Trainer.Value),
            Script = StoryState.Empty with {
                Fame = FameCheckerRules.Reset(),
                Variables = StoryState.Empty.Variables.Add("VAR_LAST_TALKED", actor.SourceLocalId)
            },
            EntryPrograms = [], ArrivalFacing = null, Dialogue = null, Motions = [],
            PresentationTicks = 0, PresentationEffects = [], VsSeekerProgress = CampaignVsSeekerProgress.Empty
        };
    }

    private static CampaignState ReachDialogue(CampaignSession session, PretCampaignCatalog catalog,
        ForestTrainerBattleDefinition definition, CampaignState initial)
    {
        var state = Call(session, "Advance", initial, StoryRules.Start(initial.Script, definition.ScriptEntry, catalog));
        for (int budget = 0; state.Script.Wait?.Kind == StoryWaitKind.NativeEffect; budget++)
        {
            Assert.IsTrue(budget < 20, definition.ScriptEntry);
            state = Call(session, "Advance", state,
                StoryRules.Continue(state.Script, state.Script.Wait.CorrelationId, catalog));
        }
        return state;
    }

    private static void AssertDialogue(PretCampaignCatalog catalog, ForestTrainerBattleDefinition definition,
        CampaignState initial, CampaignState pending)
    {
        string context = definition.ScriptEntry;
        Assert.IsNull(pending.ForestTrainerBattle, context);
        Assert.AreEqual(StoryWaitKind.Dialog, pending.Script.Wait!.Kind, context);
        Assert.IsNotNull(pending.Dialogue, context);
        Assert.AreEqual(definition.PostBattleText, pending.Dialogue.TextSymbol, context);
        Assert.IsTrue(pending.Dialogue.CloseOnCompletion && pending.Script.Locked, context);
        Assert.IsFalse(pending.Dialogue.Pages.IsDefaultOrEmpty, context);
        CollectionAssert.AreEqual(catalog.TextPages(definition.PostBattleText).ToArray(),
            pending.Dialogue.Pages.ToArray(), context);

        // Rematch-capable trainers actually reach the not-ready rematch continuation.
        // Do not substitute VAR_RESULT=0 or assume that dialogue stays in the first program.
        var program = catalog.FindProgram(definition.ScriptEntry)!;
        bool checksRematch = program.Instructions.Any(instruction => instruction.Opcode == "specialvar" &&
            instruction.Args.SequenceEqual(["VAR_RESULT", "ShouldTryRematchBattle"]));
        string[] expectedEffects = checksRematch
            ? ["trainerbattle_single", "specialvar", "trainerbattle_rematch", "msgbox"]
            : ["trainerbattle_single", "msgbox"];
        CollectionAssert.AreEqual(expectedEffects, pending.PresentationEffects.Select(effect => effect.Opcode).ToArray(), context);
        Assert.AreEqual(checksRematch ? 0 : 1, pending.Script.Variables["VAR_RESULT"], context);
        var frame = pending.Script.Stack[^1];
        var suspended = catalog.FindProgram(frame.ProgramId)!.Instructions[frame.InstructionIndex - 1];
        Assert.AreEqual(StoryOp.Dialog, suspended.Op, context);
        Assert.AreEqual("msgbox", suspended.Opcode, context);
        CollectionAssert.AreEqual(new[] { definition.PostBattleText, "MSGBOX_AUTOCLOSE" }, suspended.Args.ToArray(), context);
        Assert.AreEqual(checksRematch, frame.ProgramId != definition.ScriptEntry, context);

        var expectedFame = initial.Script.Fame;
        foreach (var fact in program.Instructions.Where(instruction => instruction.Op == StoryOp.FameChecker))
        {
            Assert.AreEqual(2, fact.Args.Length, context);
            Assert.IsTrue(catalog.TryConstant(fact.Args[0], out int person), context);
            int factIndex = int.Parse(fact.Args[1], System.Globalization.CultureInfo.InvariantCulture);
            var entry = expectedFame.GetValueOrDefault(person, new FameEntry(0, 0));
            expectedFame = expectedFame.SetItem(person, new(entry.PickState == 2 ? 2 : 1,
                entry.FlavorFlags | (1 << factIndex)));
            Assert.AreEqual(person, pending.Script.Variables["VAR_0x8004"], context);
            Assert.AreEqual(1, pending.Script.Variables["VAR_0x8005"], context);
        }
        CollectionAssert.AreEqual(expectedFame.ToArray(), pending.Script.Fame.ToArray(), context);
        CollectionAssert.AreEqual(initial.Script.Flags.ToArray(), pending.Script.Flags.ToArray(), context);
        AssertNoRewards(initial, pending, context);
    }

    private static void AssertNoRewards(CampaignState initial, CampaignState actual, string context)
    {
        Assert.AreEqual(initial.World.Rng, actual.World.Rng, context);
        Assert.AreEqual(initial.Money, actual.Money, context);
        Assert.AreEqual(initial.Party, actual.Party, context);
        Assert.AreEqual(initial.Inventory, actual.Inventory, context);
        Assert.AreEqual(initial.ForestTrainerHistory, actual.ForestTrainerHistory, context);
        CollectionAssert.AreEqual(initial.BattledTrainers.ToArray(), actual.BattledTrainers.ToArray(), context);
        Assert.IsNull(actual.ForestTrainerBattle, context);
    }

    private static void AssertFinished(CampaignState pending, CampaignState finished, string context)
    {
        Assert.IsTrue(finished.Script.Completed && !finished.Script.Locked &&
            finished.Script.Stack.IsEmpty && finished.Script.Wait is null, context);
        Assert.IsNull(finished.Dialogue, context);
        Assert.IsTrue(finished.PresentationEffects.IsEmpty, context);
        CollectionAssert.AreEqual(pending.Script.Variables.ToArray(), finished.Script.Variables.ToArray(), context);
        CollectionAssert.AreEqual(pending.Script.Flags.ToArray(), finished.Script.Flags.ToArray(), context);
        CollectionAssert.AreEqual(pending.Script.Fame.ToArray(), finished.Script.Fame.ToArray(), context);
        AssertNoRewards(pending, finished, context);
    }

    [TestMethod]
    public void EveryAdmittedModeZeroTrainerKeepsActualPostDialogueProgressionAndPageUnlock()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog);
        var definitions = Definitions(session);
        Assert.IsTrue(definitions.Length > 0, "The current registry must exercise ordinary field dialogue.");
        foreach (var definition in definitions)
        {
            var initial = DefeatedNpc(session, catalog, definition);
            var pending = ReachDialogue(session, catalog, definition, initial);
            AssertDialogue(catalog, definition, initial, pending);
            var dialogue = pending.Dialogue ?? throw new InvalidOperationException("The ordinary script did not produce dialogue.");
            for (int page = 0; page < dialogue.Pages.Length; page++)
            {
                var before = pending with { Dialogue = dialogue with { Page = page } };
                var released = Call(session, "ReleaseForestTrainerPostDialogue", before, before);
                Assert.AreEqual(page + 1 < dialogue.Pages.Length, released.Script.Locked, definition.ScriptEntry);
            }
            var last = pending with { Dialogue = dialogue with { Page = dialogue.Pages.Length - 1 } };
            var next = last with { Dialogue = null, Motions = [], PresentationTicks = 0, PresentationEffects = [] };
            next = Call(session, "ReleaseForestTrainerPostDialogue", last, next);
            next = Call(session, "Advance", next,
                StoryRules.Continue(next.Script, next.Script.Wait!.CorrelationId, catalog));
            AssertFinished(pending, next, definition.ScriptEntry);
        }
    }

    [TestMethod]
    [DataRow("ViridianForest_EventScript_Rick", -1, -1)]
    [DataRow("Route3_EventScript_Ben", -1, -1)]
    [DataRow("CeladonCity_Gym_EventScript_Lisa", 5, 2)]
    [DataRow("CeladonCity_Gym_EventScript_Tamia", 5, 3)]
    [DataRow("CeruleanCity_Gym_EventScript_Luis", 3, 2)]
    [DataRow("FuchsiaCity_Gym_EventScript_Kirk", 6, 2)]
    [DataRow("SaffronCity_Gym_EventScript_Tyron", 7, 5)]
    [DataRow("VermilionCity_Gym_EventScript_Dwayne", 4, 4)]
    public void PublicAcknowledgementsFinishActualNpcScriptWithoutReawardingBattle(string script, int person, int fact)
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog);
        var definition = Definitions(session).Single(candidate => candidate.ScriptEntry == script);
        var initial = DefeatedNpc(session, catalog, definition);
        Assert.IsNull(catalog.MapDefinition(initial.World.Player.MapId).Programs.OnFrameTable,
            "This representative tests acknowledgement, not an unrelated map-entry scene.");
        Install(session, catalog, initial);
        var entered = Call(session, "Pump", Call(session, "StartStory", initial, script));
        Install(session, catalog, entered);
        AssertDialogue(catalog, definition, initial, entered);
        if (person >= 0)
            Assert.AreEqual(new FameEntry(1, 1 << fact), entered.Script.Fame[person], script);
        int pages = entered.Dialogue!.Pages.Length;
        for (int page = 0; page < pages; page++)
        {
            var before = session.State;
            Assert.AreEqual(page, before.Dialogue!.Page, script);
            var result = session.Execute("post-dialogue:" + session.Revision, session.Revision, new("ack"));
            Assert.AreEqual(CommandStatus.Accepted, result.Status, result.Error);
            if (page + 1 < pages)
            {
                Assert.IsTrue(session.State.Script.Locked, script);
                Assert.AreSame(before.Script, session.State.Script, script);
                Assert.AreEqual(page + 1, session.State.Dialogue!.Page, script);
            }
        }
        AssertFinished(entered, session.State, script);
        var finished = session.State;
        var extra = session.Execute("post-dialogue-extra", session.Revision, new("ack"));
        Assert.AreEqual(CommandStatus.Rejected, extra.Status);
        Assert.AreEqual("no_pending_event", extra.Error);
        Assert.AreSame(finished, session.State, "A redundant acknowledgement cannot mutate the completed interaction.");
    }

    [TestMethod]
    public void AutocloseRequiresCompletedDialogueAndActualSuspendedInstruction()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(catalog);
        var definition = Definitions(session).Single(candidate => candidate.ScriptEntry == "ViridianForest_EventScript_Rick");
        var initial = DefeatedNpc(session, catalog, definition);
        var pending = ReachDialogue(session, catalog, definition, initial);
        var dialogue = pending.Dialogue ?? throw new InvalidOperationException("The ordinary script did not produce dialogue.");
        var last = pending with {
            Dialogue = dialogue with { Page = dialogue.Pages.Length - 1 },
            BattledTrainers = pending.BattledTrainers.Clear()
        };
        Assert.IsFalse(Call(session, "ReleaseForestTrainerPostDialogue", last, last).Script.Locked,
            "Actual source autoclose does not depend on a duplicated trainer-text lookup.");
        var frame = last.Script.Stack[^1];
        var program = catalog.FindProgram(frame.ProgramId)!;
        CampaignState[] invalid = [
            last with { Dialogue = null },
            last with { Dialogue = last.Dialogue! with { CloseOnCompletion = false } },
            last with { Script = last.Script with { Stack = [] } },
            last with { Script = last.Script with { Stack = [frame with { ProgramId = "MissingProgram" }] } },
            last with { Script = last.Script with { Stack = [frame with { InstructionIndex = 0 }] } },
            last with { Script = last.Script with { Stack = [frame with { InstructionIndex = program.Instructions.Length + 1 }] } },
            last with { Script = last.Script with { Stack = [frame with { InstructionIndex = 1 }] } }
        ];
        foreach (var candidate in invalid)
            Assert.AreSame(candidate, Call(session, "ReleaseForestTrainerPostDialogue", candidate, candidate));
    }
}
