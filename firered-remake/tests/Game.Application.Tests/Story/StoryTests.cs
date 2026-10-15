using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain;
using Game.Domain.Events;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Application.Story;
[TestClass, TestCategory("Game")]
public sealed class StoryTests
{
    private const string route = ".equ COUNTER, VAR_TEMP_1\nStart::\n setvar COUNTER, 1\n call Check\n msgbox Greeting\n setflag FLAG_DONE\n end\nCheck::\n call_if_eq COUNTER, 1, Increment\n return\nIncrement::\n setvar COUNTER, 2\n return";
    private PretStoryCompilation Compile(string text) => PretStoryCompiler.Compile([new("fixture.inc", text, new(SourceKind.OriginalFixture, "fixture.inc", "fixture"))]);
    [TestMethod]
    public void compiler_alias_and_call()
    {
        var c = Compile(route);
        var r = StoryRules.Start(StoryState.Empty, "Start", c);
        Assert.IsTrue(r.Status == TransitionStatus.Accepted && r.State.Variables["VAR_TEMP_1"] == 2 && r.State.Wait?.Kind == StoryWaitKind.Dialog, "alias/call failed");
        Assert.IsTrue(!r.State.Flags.ContainsKey("FLAG_DONE"), "executed past dialogue");
        var next = StoryRules.Continue(r.State, r.State.Wait!.CorrelationId, c);
        Assert.IsTrue(next.State.Completed && next.State.Flags["FLAG_DONE"], "resume failed");
    }

    [TestMethod]
    public void vm_rejected_ack_atomic()
    {
        var c = Compile(route);
        var r = StoryRules.Start(StoryState.Empty, "Start", c);
        var before = CanonicalJson.Hash(r.State);
        var bad = StoryRules.Continue(r.State, "wrong", c);
        Assert.IsTrue(bad.Status == TransitionStatus.Rejected && before == CanonicalJson.Hash(bad.State) && bad.Effects.IsEmpty, "bad ack mutated");
    }

    [TestMethod]
    public void vm_unsupported_atomic()
    {
        var c = Compile("X::\n setflag FLAG_MUTATED\n unimplemented_secret\n end");
        var r = StoryRules.Start(StoryState.Empty, "X", c);
        Assert.IsTrue(r.Status == TransitionStatus.Fault && !r.State.Flags.ContainsKey("FLAG_MUTATED") && r.Effects.IsEmpty, "unknown op partially committed");
    }

    [TestMethod]
    public void vm_budget_atomic()
    {
        var c = Compile("X::\n setflag FLAG_MUTATED\n goto X");
        var r = StoryRules.Start(StoryState.Empty, "X", c, 20);
        Assert.IsTrue(r.Diagnostic?.Code == "event_instruction_budget" && r.State.Flags.Count == 0, "unbounded event");
    }

    [TestMethod]
    public void vm_choice_explicit()
    {
        var c = Compile("X::\n msgbox Choose, MSGBOX_YESNO\n goto_if_eq VAR_RESULT, TRUE, Yes\n end\nYes::\n setflag FLAG_YES\n end");
        var r = StoryRules.Start(StoryState.Empty, "X", c);
        Assert.IsTrue(r.State.Wait?.Kind == StoryWaitKind.Choice, "choice not waiting");
        Assert.IsTrue(StoryRules.Continue(r.State, r.State.Wait!.CorrelationId, c).Status == TransitionStatus.Rejected, "choice guessed");
        Assert.IsTrue(StoryRules.Continue(r.State, r.State.Wait!.CorrelationId, c, 1).State.Flags.ContainsKey("FLAG_YES"), "yes failed");
        Assert.IsTrue(!StoryRules.Continue(r.State, r.State.Wait!.CorrelationId, c, 0).State.Flags.ContainsKey("FLAG_YES"), "no failed");
    }

    [TestMethod]
    public void vm_ushort_setvar()
    {
        var c = Compile("X::\n setvar VAR_TEMP_1, 65537\n end");
        var r = StoryRules.Start(StoryState.Empty, "X", c);
        Assert.IsTrue(r.State.Variables["VAR_TEMP_1"] == 1, "source u16 variable semantics lost");
    }

    [TestMethod]
    public void vm_presentation_is_typed()
    {
        var c = Compile("X::\n lockall\n applymovement LOCALID_PLAYER, Path\n waitmovement 0\n releaseall\n end\nPath::\n walk_up\n step_end");
        var r = StoryRules.Start(StoryState.Empty, "X", c);
        Assert.IsTrue(r.Effects.Select(x => x.Opcode).SequenceEqual(new[] { "lockall", "applymovement" }) && r.State.Wait?.Kind == StoryWaitKind.NativeEffect, "native movement was not an authoritative boundary");
        var resumed = StoryRules.Continue(r.State, r.State.Wait!.CorrelationId, c);
        Assert.IsTrue(resumed.Effects.Single().Opcode == "waitmovement" && resumed.State.Wait?.Kind == StoryWaitKind.Presentation, "movement presentation wait missing");
        Assert.IsTrue(c.Movements["Path"].Length == 2, "movement data missing");
    }

    [TestMethod]
    public void compiler_map_tables()
    {
        var c = Compile("Map::\n map_script MAP_SCRIPT_ON_TRANSITION, OnEnter\n map_script MAP_SCRIPT_ON_FRAME_TABLE, OnFrame\n .byte 0\nOnFrame::\n map_script_2 VAR_SCENE, 1, Start\n .2byte 0\nOnEnter::\n end\nStart::\n end");
        Assert.IsTrue(c.MapHeaders["Map"].Length == 2 && c.MapConditions["OnFrame"][0].Target == "Start", "map table parsed incorrectly");
    }

    [TestMethod]
    public void compiler_arity_strict()
    {
        try
        {
            Compile("X::\n setvar VAR_X\n end");
        }
        catch (ContentValidationException)
        {
            return;
        }

        throw new Exception("bad arity accepted");
    }

    [TestMethod]
    public void vm_checkpoint_replay()
    {
        var c = Compile(route);
        var a = StoryRules.Start(StoryState.Empty, "Start", c);
        var bytes = SaveCodec.Encode(a.State, 1, "story-test", "fixture");
        var loaded = SaveCodec.Decode<StoryState>(bytes, "story-test", "fixture").State;
        var x = StoryRules.Continue(a.State, a.State.Wait!.CorrelationId, c);
        var y = StoryRules.Continue(loaded, loaded.Wait!.CorrelationId, c);
        Assert.IsTrue(CanonicalJson.Hash(x) == CanonicalJson.Hash(y), "restored event differs");
    }

    [TestMethod]
    public void vm_missing_label_atomic()
    {
        var c = Compile("X::\n setflag FLAG_MUTATED\n goto Missing");
        var r = StoryRules.Start(StoryState.Empty, "X", c);
        Assert.IsTrue(r.Status == TransitionStatus.Fault && r.State.Flags.Count == 0, "missing label changed state");
    }

    [TestMethod]
    public void compiler_nurse_case_whitespace()
    {
        var c = Compile("X::\n switch VAR_RESULT\n case 1 Done\n end\nDone::\n setflag FLAG_DONE\n end");
        var r = StoryRules.Start(StoryState.Empty with { Variables = StoryState.Empty.Variables.Add("VAR_RESULT", 1) }, "X", c);
        Assert.IsTrue(r.State.Flags.GetValueOrDefault("FLAG_DONE"), "source whitespace macro separator was lost");
    }

    [TestMethod]
    public void vm_specialvar_destination_identity()
    {
        var c = Compile("X::\n setvar VAR_RESULT, 9\n specialvar VAR_RESULT, IsPlayerNotInTrainerTowerLobby\n copyvar VAR_AFTER, VAR_RESULT\n end");
        var r = StoryRules.Start(StoryState.Empty, "X", c);
        Assert.IsTrue(r.State.Wait?.Kind == StoryWaitKind.NativeEffect && r.Effects.Single().Args[0] == "VAR_RESULT", "native return destination was captured as its old value");
        var next = StoryRules.Continue(r.State with { Variables = r.State.Variables.SetItem("VAR_RESULT", 1) }, r.State.Wait!.CorrelationId, c);
        Assert.IsTrue(next.State.Variables["VAR_AFTER"] == 1, "later instruction did not read native return");
    }

    [TestMethod]
    public void vm_nurse_menu_index_and_cancel()
    {
        var c = Compile("X::\n multichoice 19, 8, MULTICHOICE_YES_NO, FALSE | (TRUE << 1)\n end")with
        {
            Constants = ImmutableDictionary<string, int>.Empty.Add("SCR_MENU_CANCEL", 127)
        };
        var r = StoryRules.Start(StoryState.Empty, "X", c);
        Assert.IsTrue(r.State.Wait?.Kind == StoryWaitKind.MenuChoice, "index menu conflated with Boolean yes/no");
        foreach (var choice in new[]
        {
            0,
            1,
            127
        }

        )
        {
            var n = StoryRules.Continue(r.State, r.State.Wait!.CorrelationId, c, choice);
            Assert.IsTrue(n.Status == TransitionStatus.Accepted && n.State.Variables["VAR_RESULT"] == choice, "source result changed");
        }

        var bad = StoryRules.Continue(r.State, r.State.Wait!.CorrelationId, c, 2);
        Assert.IsTrue(bad.Status == TransitionStatus.Rejected && bad.State == r.State, "invalid index committed");
    }
}
