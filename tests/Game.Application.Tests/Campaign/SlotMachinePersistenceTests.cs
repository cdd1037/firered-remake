using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Slots;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class SlotMachinePersistenceTests
{
    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(0xffffff, 0xffffff)]
    public void PublicJackpotSettlementAndExitRemainSaveable(int previous, int expected)
    {
        var c = CurrentCampaignFixture.Catalog;
        var session = CampaignSession.CreateCurrent(c);
        var state = CurrentCampaignFixture.Checkpoint.State;
        var stats = (state.GameStats ?? ImmutableSortedDictionary<string, int>.Empty).SetItem("GAME_STAT_SLOT_JACKPOTS", previous);
        state = state with { GameStats = stats };
        var program = c.FindProgram(SlotMachineSource.Common)!;
        int index = Enumerable.Range(0, program.Instructions.Length).First(i => program.Instructions[i].Opcode == "playslotmachine");
        var sign = SlotMachineSource.Signs[0];
        var map = new MapId(GameCornerCoinSource.Map);
        int x = sign.X + 1, y = sign.Y;
        state = state with
        {
            World = state.World with { Player = new(map, x, y, c.Maps[map].GetTile(x, y)!.Elevation, sign.Facing) },
            Script = StoryState.Empty with { Flags = state.Script.Flags, Variables = state.Script.Variables,
                EffectSequence = state.Script.EffectSequence },
            Dialogue = null, ItemMenu = null, MonPicture = null, Motions = [], PresentationTicks = 0,
            PresentationEffects = [], EntryPrograms = [], ArrivalFacing = null,
            IsSurfing = false, IsCycling = false, BicycleSlope = false, MapEdits = null
        };
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        state = (CampaignState)typeof(CampaignSession).GetMethod("EnterMap", hidden)!
            .Invoke(session, [state, null, Game.Domain.Roaming.RoamerTransition.Warp])!;
        state = (CampaignState)typeof(CampaignSession).GetMethod("Pump", hidden)!.Invoke(session, [state])!;
        Assert.IsNull(session.ValidatePlayerSave(state));
        // Controlled final-reel boundary: the public task still evaluates the original reel table,
        // writes the statistic, pays coins, owns the callback and exits through source commands.
        const string callback = "slot-persistence-test";
        ImmutableArray<int> positions = [5, 9, 18];
        Assert.AreEqual(SlotReward.Seven, SlotMachineCore.Evaluate(c.GameCornerSlots!.Rules, 3, positions).BestReward);
        var active = state with
        {
            SlotMachine = new(sign.Id, 0, callback, CampaignSlotPhase.Betting, new(SlotReward.None, 0),
                new(positions.Select(p => new SlotReel(p, 0, false, 21)).ToImmutableArray(), 3, false))
                { MainStep = 4, CurrentReel = 2, Bet = 3 },
            Script = state.Script with { Locked = true, Completed = false, Wait = new(StoryWaitKind.SlotMachine, callback),
                Stack = [new(SlotMachineSource.Common, index + 1)],
                Variables = state.Script.Variables.SetItem("VAR_0x8004", sign.Id).SetItem("VAR_RESULT", 0) }
        };
        Assert.IsTrue(CampaignSlotMachineTransactions.OwnsCallback(active, c));
        typeof(CampaignSession).GetField("_commands", hidden)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(active, session.RulesVersion, c.ContentVersion, 0));
        int ticks = 0;
        void Tick(CampaignSlotKeys held = CampaignSlotKeys.None)
        {
            var slot = session.State.SlotMachine!;
            var outcome = session.Execute("slot-test:" + ticks++, session.Revision, new("slot_tick")
                { SlotInput = new(callback, slot.Frame + 1, held & ~slot.PreviousKeys, held) });
            Assert.AreEqual(CommandStatus.Accepted, outcome.Status, outcome.Error);
        }
        Tick();
        Assert.AreEqual(SlotReward.Seven, session.State.SlotMachine!.Result!.BestReward);
        Assert.AreEqual(expected, session.State.GameStats!["GAME_STAT_SLOT_JACKPOTS"]);
        while (session.State.SlotMachine is {} slot && ticks < 5000)
        {
            if (slot.Phase == CampaignSlotPhase.Betting && slot.MainStep == 0) Tick(CampaignSlotKeys.B);
            else if (slot.Phase == CampaignSlotPhase.Quit && slot.MainStep == 2) Tick(CampaignSlotKeys.A);
            else if (slot.Phase == CampaignSlotPhase.Payout && slot.MainStep == 2 && slot.FanfareCounter < 0)
                Tick(slot.PreviousKeys == CampaignSlotKeys.Start ? CampaignSlotKeys.None : CampaignSlotKeys.Start);
            else Tick();
        }
        Assert.IsTrue(ticks < 5000, "The payout/exit callback did not settle.");
        Assert.IsNull(session.State.SlotMachine);
        Assert.IsTrue(session.State.Script.Completed);
        Assert.IsNull(session.ValidatePlayerSave(session.State));
        var loaded = SaveCodec.Decode<CampaignState>(SaveCodec.Encode(session.State, session.Revision,
            session.RulesVersion, c.ContentVersion), session.RulesVersion, c.ContentVersion, validate: session.ValidatePlayerSave);
        var restored = CampaignSession.RestoreCurrent(c, loaded.State, loaded.Generation);
        Assert.AreEqual(expected, restored.State.GameStats!["GAME_STAT_SLOT_JACKPOTS"]);
    }

    [TestMethod]
    [DataRow("GAME_STAT_SLOT_JACKPOTS", -1)]
    [DataRow("GAME_STAT_SLOT_JACKPOTS", 0x1000000)]
    [DataRow("GAME_STAT_UNKNOWN", 1)]
    public void InvalidStatisticsRemainRejected(string stat, int value)
    {
        var session = CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog);
        var state = CurrentCampaignFixture.Checkpoint.State;
        Assert.AreEqual("save_game_stat_invalid", session.ValidatePlayerSave(state with
            { GameStats = state.GameStats!.SetItem(stat, value) }));
    }
}
