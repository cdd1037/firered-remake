using System.Collections.Immutable;
using System.Reflection;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Controlled location only; retain the checkpoint's owned individuals, progression and
/// receipts. Both services enter their actual generated scripts through public interaction.</summary>
[TestClass, TestCategory("Game")]
public sealed class DayCareSummaryTests : WildBattleFixture
{
    private CampaignSession Start(bool fourIsland)
    {
        var state = checkpoint.State;
        Assert.IsNull(state.Route5DayCare?.Resident, "fixture requires empty Route 5 service");
        Assert.AreEqual(0, state.FourIslandDayCare?.Count ?? 0, "fixture requires empty Four Island service");
        Assert.IsTrue(state.Party.Count(p => !p.IsEgg) >= 3, "bounded service fixture has usable reserves");
        var map = content.Maps[new(fourIsland ? "map.four-island-pokemon-day-care" : "map.route-5-pokemon-day-care")];
        // Four Island's woman stands behind the source MB_COUNTER at (2,3).
        // Interact reaches across it from the actual walkable floor at (2,4).
        int x = fourIsland ? 2 : 4, y = fourIsland ? 4 : 5;
        Assert.IsFalse(map.GetTile(x, y)!.Blocked);
        Assert.AreEqual(3, map.GetTile(x, y)!.Elevation);
        if (fourIsland)
        {
            Assert.IsTrue(map.GetTile(x, y - 1)!.Blocked, "source counter is not a standing cell");
            Assert.IsTrue(map.SourceBehaviorAt(x, y - 1) == 0x80, "shared counter interaction reaches the woman");
        }
        // Relocation is controlled; initialize all derived entry state through the
        // same map-entry authority as ordinary doors, including regional direction,
        // source actor movement/facing, object slots and entry callbacks. Null
        // arrival deliberately avoids inventing a roamer-warp RNG transition.
        state = state with {
            World = state.World with { Player = new(map.Id, x, y, map.GetTile(x, y)!.Elevation, Direction.North) },
            SelectedObjectSlot = 0, IsSurfing = false, IsCycling = false
        };
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        state = (CampaignState)typeof(CampaignSession).GetMethod("EnterMap", hidden)!
            .Invoke(validator, [state, null, Game.Domain.Roaming.RoamerTransition.Warp])!;
        state = (CampaignState)typeof(CampaignSession).GetMethod("Pump", hidden)!.Invoke(validator, [state])!;
        Assert.AreEqual(checkpoint.State.World.Rng, state.World.Rng, "controlled map entry preserves RNG");
        Assert.IsTrue(state.FourIslandMovementDirection == (fourIsland ? Direction.North : (Direction?)null));
        var sourceActor = content.MapDefinition(map.Id).Actors.Single(a => a.SourceLocalId == 1);
        var actor = state.Actors[sourceActor.LocalId];
        Assert.AreEqual(sourceActor.MovementType, actor.MovementType);
        Assert.AreEqual("MOVEMENT_TYPE_FACE_DOWN", sourceActor.MovementType);
        Assert.AreEqual(Direction.South, actor.Facing);
        Assert.IsTrue(actor.Active && !actor.Invisible);
        Assert.IsNull(validator.ValidatePlayerSave(state), "strict controlled starting state");
        return CampaignSession.RestoreCurrent(content, state, checkpoint.Generation);
    }

    private void DrainService(CampaignSession session, bool stopAtSelection = false, int choice = 0)
    {
        for (int n = 0; n < 200; n++)
        {
            if (session.State.Script.Wait?.Kind == StoryWaitKind.PartySelection)
            {
                Assert.IsTrue(stopAtSelection, "service selection must remain explicit");
                return;
            }
            if (session.State.Script.Completed && session.State.Script.Wait is null &&
                session.State.Dialogue is null && session.State.EntryPrograms.IsEmpty) return;
            Send(session, new("ack", Choice: session.State.Script.Wait?.Kind == StoryWaitKind.Choice ? choice : null));
        }
        Assert.Fail("bounded source Day-Care script did not finish");
    }

    private void ChooseParty(CampaignSession session)
    {
        Send(session, new("interact"));
        DrainService(session, stopAtSelection: true, choice: 1);
        Assert.AreEqual(CampaignRoute5DayCareStage.Party, session.State.DayCareInteraction?.Stage);
        Assert.AreEqual(StoryWaitKind.PartySelection, session.State.Script.Wait?.Kind);
        Assert.IsFalse(session.CanReorderFieldSummaryMoves, "the global stable-field gate remains closed");
    }

    private static CampaignCommand Swap(CampaignState state, int index = 1, int source = 0, int destination = 3) =>
        new("party_summary_swap", PartyIndex: index, MoveSlot: source, Choice: destination)
        { TargetInstanceId = state.Party[index].InstanceId };

    private void RejectUnchanged(CampaignSession session, CampaignCommand command, long? expectedRevision = null)
    {
        var before = session.State; long revision = session.Revision;
        string serialized = CanonicalJson.Text(before);
        var result = session.Execute("daycare-summary-reject:" + commands++, expectedRevision ?? revision, command);
        Assert.AreEqual(CommandStatus.Rejected, result.Status, result.Error);
        Assert.AreSame(before, session.State); Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(serialized, CanonicalJson.Text(session.State));
    }

    private CampaignSession Continue(CampaignSession session)
    {
        Assert.IsNull(session.ValidatePlayerSave(session.State), "strict service-result save");
        var saved = SaveCodec.Decode<CampaignState>(
            SaveCodec.Encode(session.State, session.Revision, session.RulesVersion, content.ContentVersion),
            session.RulesVersion, content.ContentVersion, validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(content, saved.State, saved.Generation);
        Assert.AreEqual(CanonicalJson.Text(session.State), CanonicalJson.Text(restored.State),
            "Continue preserves the actual serialized payload");
        return restored;
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void NormalSummarySwapKeepsScriptWaitAndIdentityThenDepositPersistsThroughContinue(bool fourIsland)
    {
        var session = Start(fourIsland);
        ChooseParty(session);
        var before = session.State;
        var mon = before.Party[1];
        Assert.AreEqual(4, mon.Pokemon.Moves.Length, "occupied source/destination fixture");
        Assert.IsTrue(mon.Pokemon.Moves.Any(m => m.PpUps > 0), "full PP-Up metadata fixture");
        Assert.AreEqual("save_event_active", session.ValidatePlayerSave(before));
        var expectedMoves = mon.Pokemon.Moves.SetItem(0, mon.Pokemon.Moves[3]).SetItem(3, mon.Pokemon.Moves[0]);
        Send(session, Swap(before));
        var after = session.State;
        CollectionAssert.AreEqual(expectedMoves.ToArray(), after.Party[1].Pokemon.Moves.ToArray());
        CollectionAssert.AreEqual(before.Party.Select(p => p.InstanceId).ToArray(), after.Party.Select(p => p.InstanceId).ToArray());
        Assert.AreSame(before.Script, after.Script);
        Assert.AreSame(before.DayCareInteraction, after.DayCareInteraction);
        Assert.AreEqual(before.World.Rng, after.World.Rng);
        Assert.AreEqual(before.NextPokemonId, after.NextPokemonId);
        Assert.AreEqual(CanonicalJson.Text(before with { Party = after.Party }), CanonicalJson.Text(after),
            "only the selected owned move slots may change");
        Assert.AreEqual(CampaignRoute5DayCareStage.Party, after.DayCareInteraction!.Stage);
        Assert.AreEqual("save_event_active", session.ValidatePlayerSave(after));
        RejectUnchanged(session, Swap(after), session.Revision - 1);

        Send(session, new("daycare_deposit", PartyIndex: 1) { TargetInstanceId = mon.InstanceId });
        DrainService(session);
        var resident = fourIsland ? session.State.FourIslandDayCare!.First! : session.State.Route5DayCare!.Resident!;
        Assert.AreEqual(mon.InstanceId, resident.Pokemon.InstanceId);
        CollectionAssert.AreEqual(expectedMoves.Select(m => m with { CurrentPp = m.MaximumPp }).ToArray(),
            resident.Pokemon.Pokemon.Moves.ToArray(), "existing deposit projection restores PP, retaining new order and PP Ups");
        Assert.IsFalse(session.State.Party.Any(p => p.InstanceId == mon.InstanceId));
        Assert.AreEqual(before.World.Rng, session.State.World.Rng);
        Assert.AreEqual(before.NextPokemonId, session.State.NextPokemonId);
        Assert.IsNull(session.State.DayCareInteraction);
        string owned = CanonicalJson.Text(resident);
        session = Continue(session);
        Assert.AreEqual(owned, CanonicalJson.Text(fourIsland ? session.State.FourIslandDayCare!.First : session.State.Route5DayCare!.Resident));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InspectionAndCallbackCancelPreservePartyRngAndServiceState(bool fourIsland)
    {
        var session = Start(fourIsland);
        ChooseParty(session);
        var before = session.State; long revision = session.Revision;
        string serialized = CanonicalJson.Text(before);
        for (int page = 0; page <= 2; page++)
            for (int index = 0; index < before.Party.Length; index++)
            {
                _ = PokemonSummaryRules.SeekPartyIndex(before.Party, index, (PokemonSummaryPage)page, -1);
                _ = PokemonSummaryRules.SeekPartyIndex(before.Party, index, (PokemonSummaryPage)page, 1);
            }
        Assert.AreSame(before, session.State); Assert.AreEqual(revision, session.Revision);
        Assert.AreEqual(serialized, CanonicalJson.Text(session.State));
        Send(session, new("daycare_cancel"));
        DrainService(session);
        Assert.AreEqual(CanonicalJson.Text(before.Party), CanonicalJson.Text(session.State.Party));
        Assert.AreEqual(before.World.Rng, session.State.World.Rng);
        Assert.AreEqual(before.NextPokemonId, session.State.NextPokemonId);
        Assert.AreEqual(before.Route5DayCare, session.State.Route5DayCare);
        Assert.AreEqual(before.FourIslandDayCare, session.State.FourIslandDayCare);
        Assert.IsNull(session.State.DayCareInteraction);
        _ = Continue(session);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InvalidTargetsEggsAndUnrelatedCommandsRemainAtomicInsideTheCallback(bool fourIsland)
    {
        var session = Start(fourIsland);
        Send(session, new("interact"));
        RejectUnchanged(session, Swap(session.State)); // Dialogue is not the party callback.
        DrainService(session, stopAtSelection: true, choice: 1);
        var swap = Swap(session.State);
        RejectUnchanged(session, swap with { TargetInstanceId = null });
        RejectUnchanged(session, swap with { TargetInstanceId = long.MaxValue });
        RejectUnchanged(session, swap with { PartyIndex = -1 });
        RejectUnchanged(session, swap with { PartyIndex = session.State.Party.Length });
        RejectUnchanged(session, swap with { MoveSlot = -1 });
        RejectUnchanged(session, swap with { Choice = 4 });
        RejectUnchanged(session, swap with { HeldInstanceId = swap.TargetInstanceId });
        RejectUnchanged(session, swap with { StorageLocation = new(PokemonStorageMoveLocation.PartyBox, 1) });
        int egg = Array.FindIndex(session.State.Party.ToArray(), p => p.IsEgg);
        Assert.IsTrue(egg >= 0, "fixture retains its authenticated egg");
        RejectUnchanged(session, Swap(session.State, egg, 0, 1));
        RejectUnchanged(session, new("party_swap", PartyIndex: 0, Choice: 1));
        RejectUnchanged(session, new("move", Direction: Direction.West));
        RejectUnchanged(session, new("storage_summary_swap", MoveSlot: 0, Choice: 1));
        RejectUnchanged(session, new("ack"));
        Send(session, new("daycare_cancel")); DrainService(session);
        _ = Continue(session);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ClockOnlyCommitKeepsSuspendedCallbackAndNewRevisionCanSwap(bool fourIsland)
    {
        var session = Start(fourIsland); ChooseParty(session);
        var before = session.State; long revision = session.Revision;
        Send(session, new("play_time_tick", Choice: 60));
        Assert.AreEqual(CanonicalJson.Text(before with { PlayTime = session.State.PlayTime }), CanonicalJson.Text(session.State));
        Assert.AreSame(before.Script, session.State.Script);
        RejectUnchanged(session, Swap(session.State), revision);
        Send(session, Swap(session.State));
        var expectedMoves = before.Party[1].Pokemon.Moves.SetItem(0, before.Party[1].Pokemon.Moves[3])
            .SetItem(3, before.Party[1].Pokemon.Moves[0]);
        CollectionAssert.AreEqual(expectedMoves.ToArray(), session.State.Party[1].Pokemon.Moves.ToArray());
        Assert.AreSame(before.Script, session.State.Script);
        Assert.AreEqual(before.World.Rng, session.State.World.Rng);
        Send(session, new("daycare_cancel")); DrainService(session);
        session = Continue(session);
        CollectionAssert.AreEqual(expectedMoves.ToArray(), session.State.Party[1].Pokemon.Moves.ToArray(),
            "canceling the deposit callback does not undo a confirmed Summary reorder");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ForgedActorWaitInstructionAndServiceCannotAdmitSummaryMutation(bool fourIsland)
    {
        var session = Start(fourIsland); ChooseParty(session);
        var before = session.State;
        var actor = before.Actors[before.LastTalked!];
        var frame = before.Script.Stack[^1];
        CampaignState[] forged = [
            before with { LastTalked = null },
            before with { Actors = before.Actors.SetItem(actor.LocalId, actor with { Active = false }) },
            before with { Script = before.Script with { Locked = false } },
            before with { Script = before.Script with { QuestLogState = 1 } },
            before with { Script = before.Script with { Wait = before.Script.Wait! with { Kind = StoryWaitKind.Dialog } } },
            before with { Script = before.Script with { Stack = before.Script.Stack.SetItem(before.Script.Stack.Length - 1,
                frame with { InstructionIndex = frame.InstructionIndex - 1 }) } },
            before with { DayCareInteraction = null },
            before with { DayCareInteraction = before.DayCareInteraction! with { Stage = CampaignRoute5DayCareStage.FieldScript } },
            before with { DayCareInteraction = before.DayCareInteraction! with { Stage = CampaignRoute5DayCareStage.Residents } },
            before with { DayCareInteraction = before.DayCareInteraction! with { Service = fourIsland ? CampaignDayCareService.Route5 : CampaignDayCareService.FourIsland } }
        ];
        var decide = typeof(CampaignSession).GetMethod("Decide", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<CampaignState, CampaignCommand, Decision<CampaignState>>>(session);
        foreach (var state in forged)
        {
            string serialized = CanonicalJson.Text(state);
            var rejected = decide(state, Swap(before));
            Assert.AreEqual(CommandStatus.Rejected, rejected.Status, rejected.Error);
            Assert.AreSame(state, rejected.State); Assert.AreEqual(serialized, CanonicalJson.Text(state));
        }
        Assert.AreSame(before, session.State, "forgery probes never replace the public session");
        Send(session, Swap(before)); // Positive control through the authenticated public command.
        Send(session, new("daycare_cancel")); DrainService(session);
        _ = Continue(session);
    }

    [TestMethod]
    public void FourIslandResidentSelectionCannotBorrowThePartySummaryPermission()
    {
        var session = Start(true);
        for (int n = 0; n < 2; n++)
        {
            ChooseParty(session);
            Send(session, new("daycare_deposit", PartyIndex: 1) { TargetInstanceId = session.State.Party[1].InstanceId });
            DrainService(session);
        }
        Assert.AreEqual(2, session.State.FourIslandDayCare!.Count);
        session = Continue(session);
        Send(session, new("interact")); DrainService(session, stopAtSelection: true, choice: 1);
        Assert.AreEqual(CampaignRoute5DayCareStage.Residents, session.State.DayCareInteraction?.Stage);
        RejectUnchanged(session, Swap(session.State));
        Send(session, new("daycare_cancel")); DrainService(session);
        _ = Continue(session);
    }
}
