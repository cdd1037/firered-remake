using System.Collections.Immutable;
using Game.Application;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class TeachyTvFrameInvariantTests
{
    private static CampaignTeachyTvBattle Start(int lesson)
    {
        var content = CurrentCampaignFixture.Catalog;
        var result = CampaignTeachyTvBattleRules.Start(content.PokemonContent!.Pokemon.Mechanics,
            new("teachy-invariants", content.TeachyTv!.Lessons[lesson]), 12345, "RED", "MAPSEC_PALLET_TOWN",
            new RngState(0x12345678), new());
        Assert.IsTrue(result.Accepted, result.Error);
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State.Frame));
        return result.State;
    }

    private static (SourceBattleFrame Frame, ImmutableArray<SourceBattleEvent> Events) Finish(int lesson)
    {
        var content = CurrentCampaignFixture.Catalog;
        var battle = Start(lesson);
        var receipts = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        receipts.AddRange(battle.Events);
        for (int tick = 0; tick < 30000 && battle.Stage != CampaignTeachyTvBattleStage.Finished; tick++)
        {
            Assert.AreNotEqual(CampaignTeachyTvBattleStage.Boundary, battle.Stage, battle.Boundary);
            string command = battle.Voiceover.Pending is not null
                ? battle.VoiceoverPrinted ? "teachy_tv_battle_confirm" : "teachy_tv_battle_presented"
                : battle.Stage == CampaignTeachyTvBattleStage.Events ? "teachy_tv_battle_presented" : "teachy_tv_battle_tick";
            var previous = battle.Events;
            var result = CampaignTeachyTvBattleRules.Decide(content.PokemonContent!.Pokemon.Mechanics,
                content.PresetDoubleItems!, content.TeachyTv!.Lessons[lesson].Battle!, battle, command, battle.WaitId);
            Assert.IsTrue(result.Accepted, result.Error);
            battle = result.State;
            if (battle.Events != previous) receipts.AddRange(battle.Events);
        }
        Assert.AreEqual(CampaignTeachyTvBattleStage.Finished, battle.Stage, "authored controller stream must terminate");
        return (battle.Frame, receipts.ToImmutable());
    }

    [TestMethod]
    [DataRow(0, WildBattleOutcome.Won, SourceWildBattleOutcome.Won)]
    [DataRow(3, WildBattleOutcome.Caught, SourceWildBattleOutcome.Caught)]
    public void RealTerminalCommandsRetainSharedOutcomeAndRejectMissingOrForgedMetadata(
        int lesson, WildBattleOutcome outcome, SourceWildBattleOutcome sourceOutcome)
    {
        // Authenticated lesson inputs drive the existing commands. The pinned
        // OldMan_Pokedude_CaughtMessage sets B_OUTCOME_CAUGHT before endlinkbattle;
        // normal faint/reward commands retain their own Won outcome.
        var (frame, events) = Finish(lesson);
        Assert.AreEqual(outcome, frame.Outcome);
        Assert.AreEqual(sourceOutcome, frame.WildEncounter!.SourceOutcome);
        Assert.IsNull(SourceFourActionTurns.ValidateCapability(CurrentCampaignFixture.Catalog.PokemonContent!.Pokemon.Mechanics.Catalog, frame));
        Assert.AreEqual(outcome, events.OfType<SourceEndedEvent>().Single().Outcome);
        if (lesson == 3)
        {
            Assert.AreEqual("STRINGID_GOTCHAPKMNCAUGHT2", events.OfType<SourcePokedudeCaughtEvent>().Single().Message);
            Assert.IsFalse(events.OfType<SourceCaptureResultEvent>().Any(), "tutorial capture never invokes the ordinary catch formula");
        }
        else Assert.IsTrue(events.OfType<SourceRewardAppliedEvent>().Any());
        Assert.AreEqual("pokedude_frame_context", SourceBattleIdentityRules.Validate(frame with {
            WildEncounter = frame.WildEncounter with { SourceOutcome = null } }));
        foreach (var forged in new[] { SourceWildBattleOutcome.Lost, (SourceWildBattleOutcome)999 })
            Assert.AreEqual("source_wild_terminal_outcome", SourceBattleIdentityRules.Validate(frame with {
                WildEncounter = frame.WildEncounter with { SourceOutcome = forged } }));
        RejectEncounterProvenance(frame);
    }

    [TestMethod]
    public void OngoingTutorialCannotBorrowTerminalMetadataOrEncounterProvenance()
    {
        var frame = Start(0).Frame;
        Assert.IsNull(frame.WildEncounter!.SourceOutcome);
        Assert.AreEqual("source_wild_terminal_outcome", SourceBattleIdentityRules.Validate(frame with {
            WildEncounter = frame.WildEncounter with { SourceOutcome = SourceWildBattleOutcome.Won } }));
        Assert.AreEqual("pokedude_frame_context", SourceBattleIdentityRules.Validate(frame with { WildEncounter = null }));
        RejectEncounterProvenance(frame);
    }

    private static void RejectEncounterProvenance(SourceBattleFrame frame)
    {
        var encounter = frame.WildEncounter!;
        foreach (var forged in new[] {
            encounter with { RulesVersion = "forged" }, encounter with { ScriptedSnorlax = true },
            encounter with { FirstSeviiWild = true }, encounter with { LostelleHypno = true },
            encounter with { TowerBattleKind = PokemonTowerBattleKind.OrdinaryWild } })
            Assert.AreEqual("pokedude_frame_context", SourceBattleIdentityRules.Validate(frame with { WildEncounter = forged }));
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void ActualAbortKeepsDrawMetadataAndClearsPendingActionsWithoutRngOrPartyChanges(bool queued)
    {
        var catalog = CurrentCampaignFixture.Catalog.PokemonContent!.Pokemon.Mechanics.Catalog;
        var frame = Start(0).Frame;
        if (queued)
        {
            var selections = frame.Slots.Take(frame.BattlerCount).Select(cell =>
                new SourceActionSelection(cell.Occupant!.Value, SourceActionKind.Move, 0, cell.Slot.Opposite)).ToImmutableArray();
            var started = SourceFourActionTurns.BeginActions(catalog, frame, frame.Revision, selections);
            Assert.IsTrue(started.Succeeded, started.Fault); frame = started.State;
            Assert.IsNotNull(frame.FourTurn); Assert.IsFalse(frame.Queue.IsEmpty);
        }
        string before = CanonicalJson.Hash(frame);
        var aborted = SourcePokedudeBattle.Abort(catalog, frame, frame.Revision);
        Assert.IsTrue(aborted.Succeeded, aborted.Fault);
        Assert.AreEqual(WildBattleOutcome.Draw, aborted.State.Outcome);
        Assert.AreEqual(SourceWildBattleOutcome.Drew, aborted.State.WildEncounter!.SourceOutcome);
        Assert.AreEqual(WildBattleOutcome.Draw, ((SourceEndedEvent)aborted.Events.Single()).Outcome);
        Assert.IsNull(SourceBattleIdentityRules.Validate(aborted.State));
        Assert.AreEqual(frame.Rng, aborted.State.Rng); Assert.IsTrue(aborted.Trace.IsEmpty);
        Assert.AreEqual(frame.Parties, aborted.State.Parties); Assert.AreEqual(frame.Slots, aborted.State.Slots);
        Assert.AreEqual(frame.Revision + 1, aborted.State.Revision); Assert.AreEqual(frame.EventSequence + 1, aborted.State.EventSequence);
        Assert.IsTrue(aborted.State.Queue.IsEmpty); Assert.AreEqual(0, aborted.State.NextAction);
        Assert.IsNull(aborted.State.FourTurn); Assert.IsNull(aborted.State.PreparedMedicines);
        Assert.IsTrue(aborted.State.PendingSwitches.IsEmpty); Assert.IsTrue(aborted.State.Reservations.IsEmpty);
        Assert.AreEqual(before, CanonicalJson.Hash(frame));
        var replay = SourcePokedudeBattle.Abort(catalog, aborted.State, frame.Revision);
        Assert.IsFalse(replay.Succeeded); Assert.AreEqual("pokedude_abort_phase", replay.Fault);
        Assert.AreSame(aborted.State, replay.State); Assert.IsTrue(replay.Events.IsEmpty); Assert.IsTrue(replay.Trace.IsEmpty);
        Assert.AreEqual("pokedude_frame_context", SourceBattleIdentityRules.Validate(aborted.State with {
            WildEncounter = aborted.State.WildEncounter with { SourceOutcome = null } }));
    }
}
