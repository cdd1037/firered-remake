using System.Collections.Immutable;
using System.Reflection;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Roaming;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Controlled locations and encounter modifiers; generation, capture and persistence use public commands.</summary>
[TestClass, TestCategory("Game")]
public sealed class MtEmberEncounterFlowTests : WildBattleFixture
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private CampaignSession At(string name, int x, int y, Direction direction)
    {
        var session = CampaignSession.CreateCurrent(content);
        var state = FieldFixture();
        var map = content.Maps[new(name)];
        state = state with {
            World = state.World with { Player = new(map.Id, x, y, map.GetTile(x, y)!.Elevation, direction),
                Rng = state.World.Rng with { Seed = 3 } },
            Script = StoryState.Empty with { Flags = state.Script.Flags,
                Variables = state.Script.Variables.SetItem("VAR_REPEL_STEP_COUNT", 0),
                EffectSequence = state.Script.EffectSequence },
            Inventory = state.Inventory! with { Balls = [new(new("pret:ITEM_MASTER_BALL"), 1)] },
            Dialogue = null, ItemMenu = null, EntryPrograms = [], Motions = [], MapEdits = null,
            PresentationEffects = [], PresentationTicks = 0, IsSurfing = false, IsCycling = false, FlashLevel = 0
        };
        state = (CampaignState)typeof(CampaignSession).GetMethod("EnterMap", Private)!
            .Invoke(session, [state, null, RoamerTransition.Warp])!;
        state = (CampaignState)typeof(CampaignSession).GetMethod("Pump", Private)!.Invoke(session, [state])!;
        state = state with { Exploration = state.Exploration! with {
            EarlyEncounter = new(new(0), (ushort)map.SourceBehaviorAt(x, y)!, ushort.MaxValue, 21,
                RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion) } };
        typeof(CampaignSession).GetField("_commands", Private)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, checkpoint.Generation));
        Assert.IsNull(session.ValidatePlayerSave(session.State));
        return session;
    }

    private void CaptureAndContinue(CampaignSession session, bool rockSmash)
    {
        Drain(session);
        var battle = session.State.WildBattle!;
        var initial = battle.State.Opponent.Pokemon;
        long instance = session.State.NextPokemonId;
        Assert.AreEqual(rockSmash, battle.RockSmashReceipt is not null);
        Assert.AreEqual(!rockSmash, battle.SourceTableReceipt is not null);
        Send(session, new("battle_ball", Text: "pret:ITEM_MASTER_BALL")); Drain(session);
        for (int n = 0; n < 100 && (!session.State.Script.Completed || session.State.Dialogue is not null ||
            !session.State.EntryPrograms.IsEmpty); n++) Send(session, new("ack"));
        Assert.IsNull(session.State.WildBattle);
        var caught = session.State.Exploration!.Captured!.Value.Single(capture => capture.InstanceId == instance);
        Assert.AreEqual(initial.SourceSpeciesId, caught.SourceSpeciesId);
        Assert.AreEqual(initial.Personality, caught.Personality);
        Assert.AreEqual(initial.Ivs, caught.Ivs);
        Assert.AreEqual("MAPSEC_MT_EMBER", caught.MetLocation);
        Assert.AreEqual(rockSmash, caught.RockSmash is not null);
        Assert.AreEqual(!rockSmash, caught.SourceTable is not null);
        StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow("map.mt-ember-summit-path-1f", 11, 2)]
    [DataRow("map.mt-ember-summit-path-2f", 8, 38)]
    [DataRow("map.mt-ember-summit-path-3f", 2, 3)]
    public void EachSummitPathPublicTurnGeneratesSourceLandBattleCapturesAndContinues(string map, int x, int y)
    {
        var session = At(map, x, y, Direction.South);
        Send(session, new("turn", Direction.North));
        Assert.IsNotNull(session.State.WildBattle);
        var receipt = session.State.WildBattle.SourceTableReceipt;
        Assert.IsNotNull(receipt);
        Assert.AreEqual(SourceTableEncounterKind.Land, receipt.Origin.Kind);
        Assert.AreEqual(map, receipt.Origin.Map.Value);
        var actual = session.State.WildBattle.State.Opponent.Pokemon;
        Assert.IsTrue(content.LandEncounters![new(map)].Slots.Any(slot => slot.Species == actual.Species &&
            slot.MinimumLevel <= actual.Level && slot.MaximumLevel >= actual.Level));
        CaptureAndContinue(session, rockSmash: false);
    }

    [TestMethod]
    public void SecondFloorRockSmashGeneratesItsOwnBattleOriginCapturesAndContinues()
    {
        const string map = "map.mt-ember-summit-path-2f";
        var session = At(map, 8, 38, Direction.West);
        int partyIndex = FieldRockSmashSourceRules.FirstPartyMember(session.State.Party);
        Send(session, new("field_rock_smash", PartyIndex: partyIndex));
        for (int n = 0; n < 100 && session.State.WildBattle is null; n++) Send(session, new("ack"));
        Assert.IsNotNull(session.State.WildBattle);
        Assert.IsNotNull(session.State.WildBattle.RockSmashReceipt);
        Assert.AreEqual(map, session.State.WildBattle.RockSmashReceipt.Origin.Map.Value);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_TEMP_17"));
        Assert.IsFalse(session.State.Actors.Values.Single(actor => actor.SourceLocalId == 6).Active);
        CaptureAndContinue(session, rockSmash: true);
    }
}
