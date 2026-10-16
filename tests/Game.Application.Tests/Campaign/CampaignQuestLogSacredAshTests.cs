using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Ids;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogSacredAshTests
{
    [TestMethod]
    [DataRow("field_sacred_ash")]
    [DataRow("field_medicine")]
    public void WholePartyItemNamesFirstActuallyRevivedMemberAndDoesNotRepeatOnNoEffect(string commandKind)
    {
        var catalog=CurrentCampaignFixture.Catalog;var saved=CurrentCampaignFixture.Checkpoint;
        var item=new ItemId("pret:ITEM_SACRED_ASH");var state=saved.State;
        Assert.IsTrue(state.Party.Length>=4);
        var party=state.Party.Select((p,i)=>p with{Pokemon=p.Pokemon with{CurrentHp=i is 1 or 3?0:p.Pokemon.Stats.MaxHp}}).ToImmutableArray();
        state=state with{Party=party,QuestLog=null,Inventory=state.Inventory! with{BagItems=state.Inventory!.BagItems.RemoveAll(s=>s.Item==item).Add(new(item,2))}};
        Assert.IsNull(CampaignSession.CreateCurrent(catalog).ValidatePlayerSave(state));
        var session=CampaignSession.RestoreCurrent(catalog,state,saved.Generation);int sequence=0;
        void Send(CampaignCommand command){var result=session.Execute("quest-ash:"+(++sequence),session.Revision,command);Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);}
        Send(new("bag_open"));Send(new(commandKind,Text:item.Value,PartyIndex:0));
        Assert.IsTrue(session.State.Party.All(p=>p.Pokemon.CurrentHp==p.Pokemon.Stats.MaxHp));
        Assert.AreEqual(state.World.Rng,session.State.World.Rng);
        var recording=session.State.QuestLog!;var caption=recording.Scenes[^1].Actions.Single(a=>a.Event==CampaignQuestLogEvent.UsedItem);
        Assert.AreEqual("UsedItemOnMonAtThisLocation",caption.TextKey);
        Assert.AreEqual(catalog.SourceSpeciesName(party[1].Pokemon.SourceSpeciesId),caption.Arguments[1]);
        Assert.AreNotEqual(catalog.SourceSpeciesName(party[0].Pokemon.SourceSpeciesId),caption.Arguments[1]);
        for(int i=0;session.State.ItemMenu!.View=="message";i++){Assert.IsTrue(i<10);Send(new("menu_ack"));Assert.AreSame(recording,session.State.QuestLog);}
        Assert.AreEqual(1,session.State.Inventory!.BagItems.Single(s=>s.Item==item).Quantity);
        Send(new(commandKind,Text:item.Value,PartyIndex:0));
        Assert.AreEqual("gText_WontHaveEffect",session.State.ItemMenu!.MessageSymbol);
        Assert.AreEqual(1,session.State.Inventory!.BagItems.Single(s=>s.Item==item).Quantity);
        Assert.AreSame(recording,session.State.QuestLog);
    }
}
