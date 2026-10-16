using FireRed.Tests.Application.FieldItems;
using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogBerryMedicineTests
{
    [TestMethod]
    [DataRow("LEPPA_BERRY","empty",10)]
    [DataRow("LEPPA_BERRY","partial",3)]
    [DataRow("LEPPA_BERRY","full",0)]
    [DataRow("MAX_ETHER","empty",127)]
    [DataRow("MAX_ETHER","partial",3)]
    [DataRow("MAX_ETHER","full",0)]
    public void SourceSelectedPpMedicineUsesMatchingContainerAndRecordsOnlySuccessfulUse(string symbol,string depletion,int amount)
    {
        var catalog=CurrentCampaignFixture.Catalog;var saved=CurrentCampaignFixture.Checkpoint;
        var basis=CurrentFixturePokemon.BringToLead(catalog,saved.State,130);var mon=basis.Party[0];
        int slot=Enumerable.Range(0,mon.Pokemon.Moves.Length).OrderByDescending(i=>mon.Pokemon.Moves[i].MaximumPp).First();
        var move=mon.Pokemon.Moves[slot];int pp=depletion=="empty"?0:depletion=="partial"?move.MaximumPp-3:move.MaximumPp;
        mon=mon with{Pokemon=mon.Pokemon with{Moves=mon.Pokemon.Moves.SetItem(slot,move with{CurrentPp=pp})}};
        var item=new ItemId("pret:ITEM_"+symbol);var definition=catalog.Inventory.Items[item];bool berry=definition.Pocket=="POCKET_BERRY_POUCH";
        var inventory=basis.Inventory!;var pocket=berry?inventory.Berries??[]:inventory.BagItems;
        pocket=pocket.RemoveAll(s=>s.Item==item).Add(new(item,2));
        var state=basis with{Party=basis.Party.SetItem(0,mon),QuestLog=null,
            Inventory=berry?inventory with{Berries=pocket}:inventory with{BagItems=pocket}};
        Assert.IsTrue(state.Script.Flags.GetValueOrDefault("FLAG_SYS_CAN_LINK_WITH_RS"));
        var validator=CampaignSession.CreateCurrent(catalog);Assert.IsNull(validator.ValidatePlayerSave(state));
        var session=CampaignSession.RestoreCurrent(catalog,state,saved.Generation);int sequence=0;
        void Send(CampaignCommand command){var result=session.Execute("quest-berry:"+(++sequence),session.Revision,command);Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);}
        Assert.AreEqual(EarlyShopUseSupport.FieldEther,catalog.EarlyShop!.Items.Single(i=>i.Id==item).UseSupport);
        Assert.AreEqual(FieldMedicineEffect.RestoreMovePp,AuthenticatedFieldMedicine.Resolve(catalog.EarlyShop,item).Effect);
        Send(new("bag_open"));
        if(!berry)Send(new("berry_open"));
        var wrongContainer=session.State;
        var rejected=session.Execute("quest-berry:wrong-container",session.Revision,new("field_medicine",Text:item.Value,PartyIndex:0,MoveSlot:slot));
        Assert.AreEqual(CommandStatus.Rejected,rejected.Status);Assert.AreEqual("berry_item_context_invalid",rejected.Error);
        Assert.AreSame(wrongContainer,session.State);
        if(berry)Send(new("berry_open"));else Send(new("menu_back"));var before=session.State;
        Send(new("field_medicine",Text:item.Value,PartyIndex:0,MoveSlot:slot));
        Assert.AreEqual(Math.Min(move.MaximumPp,pp+amount),session.State.Party[0].Pokemon.Moves[slot].CurrentPp);
        Assert.AreEqual(before.World.Rng,session.State.World.Rng);
        var remaining=berry?session.State.Inventory!.Berries??[]:session.State.Inventory!.BagItems;
        Assert.AreEqual(amount==0?2:1,remaining.Single(s=>s.Item==item).Quantity);
        if(amount==0){Assert.AreSame(before.QuestLog,session.State.QuestLog);Assert.AreEqual("gText_WontHaveEffect",session.State.ItemMenu!.MessageSymbol);}
        else Assert.AreEqual("UsedItemOnMonAtThisLocation",session.State.QuestLog!.Scenes[^1].Actions.First(a=>a.Event==CampaignQuestLogEvent.UsedItem).TextKey);
        var recording=session.State.QuestLog;Send(new("menu_ack"));
        Assert.AreEqual(berry?"berries":"bag",session.State.ItemMenu!.View);Assert.AreSame(recording,session.State.QuestLog);
    }
}
