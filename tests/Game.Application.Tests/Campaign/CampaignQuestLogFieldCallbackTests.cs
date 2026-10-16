using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Ids;
using FireRed.Tests.Application.FieldItems;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogFieldCallbackTests
{
    [TestMethod]
    public void StoneCaptionNamesOriginalRecipientDespiteAtomicEvolution()
    {
        var (session,item)=Start("MOON_STONE");var original=session.State.Party[0];
        Send(session,new("field_evolution",Text:item.Value,PartyIndex:0));
        Assert.AreNotEqual(original.Pokemon.SourceSpeciesId,session.State.Party[0].Pokemon.SourceSpeciesId);
        var caption=Last(session);Assert.AreEqual("UsedItemOnMonAtThisLocation",caption.TextKey);
        Assert.AreEqual(CurrentCampaignFixture.Catalog.SourceSpeciesName(original.Pokemon.SourceSpeciesId),caption.Arguments[1]);
        var recorded=session.State.QuestLog;
        for(int i=0;i<40&&session.State.Evolution is not null;i++)Send(session,session.State.Evolution.Stage==CampaignEvolutionStage.LearnMove?new("evolution_learn",MoveSlot:-1):new("ack"));
        Assert.IsNull(session.State.Evolution);
        Assert.AreEqual(recorded,session.State.QuestLog);
        Send(session,new("field_evolution",Text:item.Value,PartyIndex:0));
        Assert.AreEqual(recorded,session.State.QuestLog);
        Assert.AreEqual(1,Quantity(session,item));
    }

    [TestMethod]
    [DataRow("PP_UP","field_medicine")]
    [DataRow("PP_MAX","field_medicine")]
    [DataRow("RARE_CANDY","field_rare_candy")]
    public void RemainingSuccessfulPartyCallbacksRecordOnePaidTarget(string symbol,string kind)
    {
        var (session,item)=Start(symbol);var before=session.State;
        Send(session,new(kind,Text:item.Value,PartyIndex:0,MoveSlot:kind=="field_medicine"?0:-1){TargetInstanceId=before.Party[0].InstanceId});
        Assert.AreEqual(1,Quantity(session,item));Assert.AreEqual("UsedItemOnMonAtThisLocation",Last(session).TextKey);
        Assert.AreEqual(CurrentCampaignFixture.Catalog.SourceSpeciesName(before.Party[0].Pokemon.SourceSpeciesId),Last(session).Arguments[1]);
        Assert.AreEqual(before.World.Rng,session.State.World.Rng);
        Assert.IsNull(CampaignQuestLogItemUseCapture.Observe(session.State,session.State,CurrentCampaignFixture.Catalog,new(kind,Text:item.Value,PartyIndex:0)));
    }

    [TestMethod]
    [DataRow("REPEL")]
    [DataRow("SUPER_REPEL")]
    [DataRow("MAX_REPEL")]
    public void RepelRecordsAtPaidSoundCompletionRatherThanOpeningOrMessageAck(string symbol)
    {
        var (session,item)=Start(symbol);var before=session.State;
        Send(session,new("field_repel",Text:item.Value));
        Assert.IsNull(session.State.QuestLog);Assert.AreEqual(2,Quantity(session,item));
        Send(session,new("repel_presented",Text:session.State.Repel!.WaitId));
        Assert.AreEqual("UsedTheItem",Last(session).TextKey);Assert.AreEqual(1,Quantity(session,item));
        var log=session.State.QuestLog;Send(session,new("menu_ack"));
        Send(session,new("field_repel",Text:item.Value));
        Assert.AreEqual(log,session.State.QuestLog);Assert.AreEqual(1,Quantity(session,item));
        Assert.AreEqual(before.World.Rng,session.State.World.Rng);
    }

    [TestMethod]
    [DataRow("field_unrelated")]
    [DataRow("bag_unrelated")]
    [DataRow("item_unrelated")]
    public void UnrelatedConsumptionDoesNotInventUsedItemCaption(string kind)
    {
        var (session,item)=Start("POTION");var before=session.State;
        var after=before with{Inventory=before.Inventory! with{BagItems=before.Inventory!.BagItems.RemoveAll(s=>s.Item==item)}};
        Assert.IsNull(CampaignQuestLogRecorder.Record(null,before,after,new(kind,PartyIndex:0),CurrentCampaignFixture.Catalog,recordingOpen:false));
    }

    private static (CampaignSession,ItemId) Start(string symbol)
    {
        var catalog=CurrentCampaignFixture.Catalog;var saved=CurrentCampaignFixture.Checkpoint;
        var state=CurrentFixturePokemon.BringToLead(catalog,saved.State,35);var item=new ItemId("pret:ITEM_"+symbol);
        var pokemonCatalog=catalog.PokemonContent!.Catalog;var mon=state.Party[0];mon=mon with{HeldItem="ITEM_NONE",Pokemon=mon.Pokemon with{Moves=mon.Pokemon.Moves.Select(m=>m with{PpUps=0,MaximumPp=pokemonCatalog.Moves[m.Move].Pp,CurrentPp=pokemonCatalog.Moves[m.Move].Pp}).ToImmutableArray()}};
        state=state with{Party=state.Party.SetItem(0,mon),QuestLog=null,Inventory=state.Inventory! with{BagItems=state.Inventory!.BagItems.RemoveAll(s=>s.Item==item).Add(new(item,2))},
            Script=state.Script with{Variables=state.Script.Variables.SetItem(CampaignSession.RepelCounterVariable,0)}};
        Assert.IsNull(CampaignSession.CreateCurrent(catalog).ValidatePlayerSave(state));
        var session=CampaignSession.RestoreCurrent(catalog,state,saved.Generation);Send(session,new("bag_open"));return(session,item);
    }
    private static int Quantity(CampaignSession s,ItemId item)=>s.State.Inventory!.BagItems.Where(x=>x.Item==item).Sum(x=>x.Quantity);
    private static CampaignQuestLogAction Last(CampaignSession s)=>s.State.QuestLog!.Scenes.SelectMany(x=>x.Actions).Last(x=>x.TextKey is not null);
    private static void Send(CampaignSession s,CampaignCommand c){var r=s.Execute("quest-field-callback:"+(s.Revision+1),s.Revision,c);Assert.AreEqual(CommandStatus.Accepted,r.Status,r.Error);}
}
