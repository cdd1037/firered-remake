using System.Collections.Immutable;
using Game.Domain.Battle;
using System.Reflection;
using Game.Application;
using Game.Domain.Ids;
using FireRed.Tests.Application.WildBattles;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogItemUseTests:WildBattleFixture
{
    [TestMethod]
    [DataRow("POTION",false)]
    [DataRow("ETHER",true)]
    public void BattleMenuSuccessUsesTypedReceiptAndAcknowledgementsDoNotRepeat(string symbol,bool pp)
    {
        var target=Owned("RATTATA",20,102,"GROWL");
        target=target with{Pokemon=target.Pokemon with{CurrentHp=target.Pokemon.Stats.MaxHp-10,
            Moves=target.Pokemon.Moves.SetItem(0,target.Pokemon.Moves[0] with{CurrentPp=0})}};
        var session=Controlled([Owned("PIDGEY",20,101,"GROWL"),target],Owned("PIDGEY",4,201,"GROWL"));
        var item=new ItemId("pret:ITEM_"+symbol);
        var state=session.State with{Inventory=session.State.Inventory! with{BagItems=[new(item,2)]}};
        typeof(CampaignSession).GetField("_commands",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState,CampaignCommand>(state,session.RulesVersion,content.ContentVersion,session.Revision));
        Send(session,new("battle_medicine",Text:item.Value,PartyIndex:1,MoveSlot:pp?0:-1){TargetInstanceId=target.InstanceId});
        var used=CampaignQuestLogItemUseCapture.Observe(state,session.State,content);
        Assert.IsNotNull(used);Assert.AreEqual(item,used.Item);Assert.AreEqual(target.Pokemon.SourceSpeciesId,used.TargetSpeciesId);
        var recordBefore=state with{Script=state.Script with{Flags=state.Script.Flags.SetItem("FLAG_SYS_CAN_LINK_WITH_RS",true)}};
        var recordAfter=session.State with{Script=session.State.Script with{Flags=session.State.Script.Flags.SetItem("FLAG_SYS_CAN_LINK_WITH_RS",true)}};
        var original=System.Text.Json.JsonSerializer.Serialize(recordAfter);
        var log=CampaignQuestLogRecorder.Record(null,recordBefore,recordAfter,new("battle_medicine"),content,recordingOpen:false)!;
        Assert.AreEqual("UsedItemOnMonAtThisLocation",log.Scenes.Single().Actions.Single().TextKey);
        Assert.AreEqual(original,System.Text.Json.JsonSerializer.Serialize(recordAfter));
        Assert.IsNull(CampaignQuestLogItemUseCapture.Observe(session.State,session.State,content));
        var enemyEvents=session.State.WildBattle!.SourceEvents.Select(e=>e is SourceMedicineAppliedEvent medicine?
            medicine with{Actor=new BattlerSlot(1),TargetSide=BattleSide.Opponent}:e).ToImmutableArray();
        var enemyUse=session.State with{WildBattle=session.State.WildBattle with{SourceEvents=enemyEvents}};
        Assert.IsNull(CampaignQuestLogItemUseCapture.Observe(state,enemyUse,content));
        var accepted=session.State;Drain(session);
        Assert.IsNull(CampaignQuestLogItemUseCapture.Observe(accepted,session.State,content));
        Assert.AreEqual(1,session.State.Inventory!.BagItems.Single(s=>s.Item==item).Quantity);
    }
    [TestMethod]
    public void PokeDollUsesTerminalReceiptAndNeverRecordsOrdinaryRun()
    {
        var session=Controlled([Owned("PIDGEY",20,101,"GROWL")],Owned("PIDGEY",4,201,"GROWL"));
        var item=new ItemId("pret:ITEM_POKE_DOLL");
        var state=session.State with{Inventory=session.State.Inventory! with{BagItems=[new(item,1)]}};
        typeof(CampaignSession).GetField("_commands",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState,CampaignCommand>(state,session.RulesVersion,content.ContentVersion,session.Revision));
        Send(session,new("battle_medicine",Text:item.Value,PartyIndex:0){TargetInstanceId=state.Party[0].InstanceId});
        var receipt=CampaignQuestLogItemUseCapture.Observe(state,session.State,content);
        Assert.IsNotNull(receipt);Assert.AreEqual(item,receipt.Item);Assert.IsNull(receipt.TargetSpeciesId);
        Assert.IsNull(session.State.WildBattle);
        Assert.AreEqual(Game.Domain.WildBattle.WildBattleOutcome.Escaped,session.State.Exploration!.Battles[^1].Outcome);
        var recordBefore=state with{Script=state.Script with{Flags=state.Script.Flags.SetItem("FLAG_SYS_CAN_LINK_WITH_RS",true)}};
        var recordAfter=session.State with{Script=session.State.Script with{Flags=session.State.Script.Flags.SetItem("FLAG_SYS_CAN_LINK_WITH_RS",true)}};
        var log=CampaignQuestLogRecorder.Record(null,recordBefore,recordAfter,new("battle_medicine"),content,recordingOpen:false)!;
        Assert.AreEqual("UsedTheItem",log.Scenes.Single().Actions.Single().TextKey);
        Assert.IsNull(CampaignQuestLogItemUseCapture.Observe(session.State,session.State,content));
        var accepted=session.State;Drain(session);
        Assert.IsNull(CampaignQuestLogItemUseCapture.Observe(accepted,session.State,content));
        Assert.IsNull(session.State.WildBattle);
        var runner=Controlled([Owned("PIDGEY",20,301,"GROWL")],Owned("PIDGEY",4,401,"GROWL"));
        var beforeRun=runner.State;Send(runner,new("battle_menu",Text:"run"));
        Assert.IsNull(CampaignQuestLogItemUseCapture.Observe(beforeRun,runner.State,content));
    }
    [TestMethod]
    public void StatBoosterReceiptDoesNotInventAnItemCaptionAbsentFromItsSourceCallback()
    {
        var session=Controlled([Owned("PIDGEY",20,101,"GROWL")],Owned("PIDGEY",4,201,"GROWL"));
        var item=new ItemId("pret:ITEM_X_ATTACK");
        var state=session.State with{Inventory=session.State.Inventory! with{BagItems=[new(item,1)]}};
        typeof(CampaignSession).GetField("_commands",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState,CampaignCommand>(state,session.RulesVersion,content.ContentVersion,session.Revision));
        Send(session,new("battle_medicine",Text:item.Value,PartyIndex:0){TargetInstanceId=state.Party[0].InstanceId});
        Assert.IsTrue(session.State.WildBattle!.SourceEvents.OfType<SourceMedicineAppliedEvent>().Any());
        Assert.IsNull(CampaignQuestLogItemUseCapture.Observe(state,session.State,content));
    }
    [TestMethod]
    public void FieldFluteRequiresWakeEffectAndPowderDisplayRecordsOnlyOnOpening()
    {
        var before=CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog).State;
        var noWake=before with{ItemMenu=new("bag","message",MessageSymbol:"gText_PlayedPokeFluteHadNoEffect")};
        Assert.IsNull(CampaignQuestLogItemUseCapture.Observe(before,noWake,content));
        var awake=before with{PokeFlute=new(CampaignPokeFlutePhase.PlayedText,"test:wake")};
        Assert.AreEqual("pret:ITEM_POKE_FLUTE",CampaignQuestLogItemUseCapture.Observe(before,awake,content)!.Item.Value);
        Assert.IsNull(CampaignQuestLogItemUseCapture.Observe(awake,awake,content));
        var powder=before with{ItemMenu=new("bag","message",MessageSymbol:"gText_PowderQty")};
        Assert.AreEqual("pret:ITEM_POWDER_JAR",CampaignQuestLogItemUseCapture.Observe(before,powder,content)!.Item.Value);
        Assert.IsNull(CampaignQuestLogItemUseCapture.Observe(powder,powder,content));
    }
}
