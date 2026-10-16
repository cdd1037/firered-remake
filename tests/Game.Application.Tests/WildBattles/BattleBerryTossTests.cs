using System.Collections.Immutable;
using System.Reflection;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.WildBattle;
using Game.Domain.Trainers;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class BattleBerryTossTests : WildBattleFixture
{
    private static readonly ItemId Berry = new("pret:ITEM_ORAN_BERRY");

    // Controlled selection owners exercise the same transaction without claiming a natural route replay.
    private CampaignSession Selection(string owner, int quantity = 3)
    {
        var session = Controlled([Owned("SNORLAX", 30, 1, "TACKLE"), Owned("PIDGEY", 20, 2, "TACKLE")],
            Owned("PIDGEY", 20, 99, "TACKLE"));
        var state = session.State;
        var battle = state.WildBattle!;
        if(owner.StartsWith("trainer", StringComparison.Ordinal)) battle=battle with {Frame=battle.Frame with {BattleKind=WildBattleCalledBattleKind.Trainer, WildEncounter=null}};
        state = state with { Inventory = state.Inventory! with {
            Berries = [new(Berry, quantity)],
            KeyItems = (state.Inventory!.KeyItems ?? []).RemoveAll(s => s.Item == SsAnneItemRules.BerryPouchId).Add(new(SsAnneItemRules.BerryPouchId, 1)) },
            Script = state.Script with { Flags = state.Script.Flags.SetItem("FLAG_SYS_GOT_BERRY_POUCH", true) } };
        if (owner.Contains("double", StringComparison.Ordinal))
        {
            var entry=SourceTwinsTrainerEncounter.Begin(catalog,content.Route8Twins!,
                state.Party.Select(p=>new WildBattleCalledPartyMember(p.Pokemon,p.HeldItem,p.Status,p.IsEgg)).ToImmutableArray(),state.World.Rng,
                false,false,false);
            Assert.IsTrue(entry.Succeeded,entry.Fault);
            var frame=entry.State!;
            // No natural wild-double encounter is claimed. The shared selection owner also
            // accepts this valid four-slot wild-kind fixture without a singles encounter policy.
            if(owner=="wild-double")frame=frame with {BattleKind=WildBattleCalledBattleKind.Wild};
            Assert.IsNull(SourceBattleIdentityRules.Validate(frame), "controlled double selection frame");
            // First actor already selected: Toss belongs to actor 2 and must retain that pending choice.
            var pending = new SourceActionSelection(frame.Get(new(0)).Occupant!.Value, SourceActionKind.Move, 0, new(1));
            state = state with { WildBattle = null, DoubleBattle = new("toss-double", content.Route8Twins!.Trainer.Id, "test-actor", frame,
                CampaignDoubleBattleStage.Combat, [], 0, [pending], [30,20], 0, 0, [], [], [], [], 0) };
        }
        else if (owner == "trainer-single")
            state = state with { WildBattle = null, ForestTrainerBattle = new("toss-trainer", new("test-trainer"), "test-actor",
                battle, CampaignForestTrainerStage.Combat, [30,20], [], 0, 0, [0]) };
        Set(session, state);
        return session;
    }

    private void Set(CampaignSession session, CampaignState state) => typeof(CampaignSession)
        .GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));
    private static CampaignCommand Open(CampaignState state) => new("battle_toss_open", Text: Berry.Value,
        PartyIndex: state.DoubleBattle?.SelectingSlot ?? 0) { TargetInstanceId = state.Party[state.DoubleBattle is null ? 0 : 1].InstanceId };
    private void Reject(CampaignSession session, CampaignCommand command)
    {
        var before = session.State;
        var result = session.Execute("toss-reject:" + commands++, session.Revision, command);
        Assert.AreEqual(CommandStatus.Rejected, result.Status, command.Kind);
        Assert.AreSame(before, session.State);
    }

    [TestMethod]
    [DataRow("wild-single")]
    [DataRow("trainer-single")]
    [DataRow("wild-double")]
    [DataRow("trainer-double")]
    public void TossIsModalAndDoesNotSpendBattleAction(string owner)
    {
        var s = Selection(owner); var before = s.State;
        Send(s, Open(s.State));
        var wait = s.State.ItemMenu!.Toss!.WaitId;
        Reject(s, new("battle_move", MoveSlot:0));
        Send(s, new("bag_toss_quantity", Quantity:3, Text:wait));
        Reject(s, new("bag_toss_select", Text:wait));
        Send(s, new("bag_toss_select", Text:s.State.ItemMenu!.Toss!.WaitId));
        Send(s, new("bag_toss_confirm", Choice:0, Text:s.State.ItemMenu!.Toss!.WaitId));
        Assert.AreSame(before.Inventory, s.State.Inventory);
        wait = s.State.ItemMenu!.Toss!.WaitId;
        Send(s, new("bag_toss_ack", Text:wait));
        Assert.IsNull(s.State.ItemMenu);
        Assert.AreEqual(0, OpeningInventoryRules.Quantity(s.State.Inventory!.Berries ?? [], Berry));
        Assert.AreEqual(before, s.State with { Inventory = before.Inventory });
        Reject(s, new("bag_toss_ack", Text:wait));
    }

    [TestMethod]
    [DataRow("wild-single")]
    [DataRow("trainer-single")]
    [DataRow("wild-double")]
    [DataRow("trainer-double")]
    public void QuantityAndConfirmationCancelRetainInventoryAndOwner(string owner)
    {
        var s=Selection(owner); var before=s.State;
        Send(s,Open(s.State));
        Send(s,new("bag_toss_back",Text:s.State.ItemMenu!.Toss!.WaitId));
        Assert.AreEqual(before,s.State);
        Send(s,Open(s.State));
        Send(s,new("bag_toss_select",Text:s.State.ItemMenu!.Toss!.WaitId));
        Send(s,new("bag_toss_confirm",Choice:1,Text:s.State.ItemMenu!.Toss!.WaitId));
        Assert.AreEqual(before,s.State);
    }

    [TestMethod]
    public void SingleRemainingBerrySkipsQuantityAndRejectsChangedOwner()
    {
        var s=Selection("wild-single",1);
        Reject(s,Open(s.State) with {TargetInstanceId=999});
        Send(s,Open(s.State));
        Assert.AreEqual(CampaignBagTossPhase.Confirm,s.State.ItemMenu!.Toss!.Phase);
        var unchanged=s.State;
        Set(s,s.State with {WildBattle=s.State.WildBattle! with {Frame=s.State.WildBattle.Frame with {Revision=s.State.WildBattle.Frame.Revision+1}}});
        Reject(s,new("bag_toss_confirm",Choice:0,Text:s.State.ItemMenu!.Toss!.WaitId));
        Set(s,unchanged);
        Send(s,new("bag_toss_confirm",Choice:0,Text:s.State.ItemMenu!.Toss!.WaitId));
        Send(s,new("bag_toss_ack",Text:s.State.ItemMenu!.Toss!.WaitId));
        Assert.IsTrue(s.State.Inventory!.Berries!.Value.IsEmpty);
    }

    [TestMethod]
    [DataRow(CampaignBattlePhase.Events)]
    [DataRow(CampaignBattlePhase.Results)]
    public void ExecutionAndResultsCannotOpenToss(CampaignBattlePhase phase)
    {
        var s=Selection("wild-single");Set(s,s.State with {WildBattle=s.State.WildBattle! with {Phase=phase}});
        Reject(s,Open(s.State));
    }
    [TestMethod]
    public void ForcedReplacementAndOtherPocketsCannotOpenToss()
    {
        var s=Selection("wild-single");
        Reject(s,Open(s.State) with {Text="pret:ITEM_POTION"});
        Set(s,s.State with {WildBattle=s.State.WildBattle! with {Replacement=CampaignReplacementStage.Select}});
        Reject(s,Open(s.State));
    }
    [TestMethod]
    public void DoubleExecutionResultsAndPokedudeDoNotOwnOrdinaryPouch()
    {
        var s=Selection("trainer-double");var initial=s.State;
        Set(s,initial with {DoubleBattle=initial.DoubleBattle! with {Stage=CampaignDoubleBattleStage.Finished}});
        Reject(s,Open(s.State));
        Set(s,initial with {DoubleBattle=initial.DoubleBattle! with {Frame=initial.DoubleBattle.Frame with {
            FourTurn=new(0,SourceFourActionPhase.Actions,[])}}});
        Reject(s,Open(s.State));
        s=Selection("wild-single");
        Set(s,s.State with {WildBattle=s.State.WildBattle! with {Frame=s.State.WildBattle.Frame with {Pokedude=new(1)}}});
        Reject(s,Open(s.State));
    }

}
