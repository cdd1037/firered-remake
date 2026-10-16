using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogHeldItemTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PublicGiveAndTakeRecordTheCorrectSourceCallback(bool fromParty)
    {
        var catalog=CurrentCampaignFixture.Catalog;var saved=CurrentCampaignFixture.Checkpoint;
        var state=saved.State;var item=new Game.Domain.Ids.ItemId("pret:ITEM_POTION");
        state=state with{QuestLog=null,Party=state.Party.SetItem(0,state.Party[0] with{HeldItem="ITEM_NONE",Mail=255}),
            Inventory=state.Inventory! with{BagItems=state.Inventory!.BagItems.RemoveAll(s=>s.Item==item).Add(new(item,2))}};
        var session=CampaignSession.RestoreCurrent(catalog,state,saved.Generation);int count=0;long target=state.Party[0].InstanceId;
        void Send(CampaignCommand command){var result=session.Execute("quest-held:"+(++count),session.Revision,command);Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);}
        if(fromParty){Send(new("party_item_open",PartyIndex:0){TargetInstanceId=target});Send(new("held_bag_open",PartyIndex:0){TargetInstanceId=target});}
        else Send(new("bag_open"));
        Send(new("field_give",Text:item.Value,PartyIndex:0){TargetInstanceId=target});
        Assert.AreEqual(fromParty?CampaignQuestLogEvent.GaveHeldItem:CampaignQuestLogEvent.GaveHeldItemBag,
            session.State.QuestLog!.Scenes.SelectMany(s=>s.Actions).Last(a=>a.TextKey is not null).Event);
        Assert.AreEqual(1,session.State.Inventory!.BagItems.Single(s=>s.Item==item).Quantity);
        Assert.AreEqual(state.World.Rng,session.State.World.Rng);
        Send(new("menu_back"));Send(new("party_item_open",PartyIndex:0){TargetInstanceId=target});
        Send(new("field_take",PartyIndex:0){TargetInstanceId=target});
        Assert.AreEqual(CampaignQuestLogEvent.TookHeldItem,session.State.QuestLog!.Scenes.SelectMany(s=>s.Actions).Last(a=>a.TextKey is not null).Event);
        Assert.AreEqual(2,session.State.Inventory!.BagItems.Single(s=>s.Item==item).Quantity);
    }

    [TestMethod]
    [DataRow("field_give","bag",false,CampaignQuestLogHeldItemOrigin.Bag)]
    [DataRow("field_give","bag",true,CampaignQuestLogHeldItemOrigin.Party)]
    [DataRow("pc_give","withdraw",false,CampaignQuestLogHeldItemOrigin.Pc)]
    [DataRow("field_take","held",true,CampaignQuestLogHeldItemOrigin.Party)]
    public void ExplicitAcceptedMenuOperationPreservesOriginalEntry(string kind,string view,bool partyEntry,CampaignQuestLogHeldItemOrigin origin)
    {
        var state=CurrentCampaignFixture.Checkpoint.State;
        var target=state.Party[0].InstanceId;
        state=state with{ItemMenu=new(kind=="pc_give"?"pc":kind=="field_take"?"party":"bag",view){TargetPartyIndex=partyEntry?0:null,TargetInstanceId=partyEntry?target:null}};
        var receipt=CampaignQuestLogHeldItemCapture.Observe(state,state,new(kind,PartyIndex:0){TargetInstanceId=target});
        Assert.IsNotNull(receipt);Assert.AreEqual(target,receipt.InstanceId);Assert.AreEqual(origin,receipt.Origin);
    }

    [TestMethod]
    [DataRow(MailStationeryOrigin.Bag,false,CampaignQuestLogHeldItemOrigin.Bag)]
    [DataRow(MailStationeryOrigin.Bag,true,CampaignQuestLogHeldItemOrigin.Party)]
    [DataRow(MailStationeryOrigin.Pc,false,CampaignQuestLogHeldItemOrigin.Pc)]
    public void ConfirmedMailGiveKeepsItsOriginalEntry(MailStationeryOrigin origin,bool partyEntry,CampaignQuestLogHeldItemOrigin expected)
    {
        var state=CurrentCampaignFixture.Checkpoint.State;var target=state.Party[0].InstanceId;
        state=state with{ItemMenu=new(origin==MailStationeryOrigin.Pc?"pc":"bag",origin==MailStationeryOrigin.Pc?"withdraw":"bag")
            {TargetInstanceId=partyEntry?target:null},MailComposer=new("quest-mail",0,CampaignMailComposePhase.ConfirmDone,0,target,121,[],false){Origin=origin}};
        var command=new CampaignCommand("mail_compose_confirm",Choice:0,Text:state.MailComposer.WaitId);
        var after=state with{MailComposer=null};
        var receipt=CampaignQuestLogHeldItemCapture.Observe(state,after,command);
        Assert.IsNotNull(receipt);Assert.AreEqual(expected,receipt.Origin);Assert.AreEqual(target,receipt.InstanceId);
        Assert.IsNull(CampaignQuestLogHeldItemCapture.Observe(state,after,command with{Choice=1}));
        Assert.IsNull(CampaignQuestLogHeldItemCapture.Observe(state with{MailComposer=state.MailComposer with{Phase=CampaignMailComposePhase.ConfirmCancel}},after,command));
    }

    [TestMethod]
    [DataRow("battle_ack")]
    [DataRow("battle_move")]
    [DataRow("storage_item_take")]
    [DataRow("storage_item_give")]
    [DataRow("mail_send_pc")]
    [DataRow("mail_erase_confirm")]
    public void UnrelatedHeldItemDeltasNeverInventMenuCaptions(string kind)
    {
        var catalog=CurrentCampaignFixture.Catalog;
        var before=CurrentCampaignFixture.Checkpoint.State;
        before=before with{QuestLog=null,Party=before.Party.SetItem(0,before.Party[0] with{HeldItem="ITEM_ORAN_BERRY"})};
        var after=before with{Party=before.Party.SetItem(0,before.Party[0] with{HeldItem="ITEM_NONE"})};
        var command=new CampaignCommand(kind,PartyIndex:0){TargetInstanceId=before.Party[0].InstanceId};
        Assert.IsNull(CampaignQuestLogHeldItemCapture.Observe(before,after,command));
        Assert.IsNull(CampaignQuestLogRecorder.Record(null,before,after,command,catalog,recordingOpen:false));
    }
}
