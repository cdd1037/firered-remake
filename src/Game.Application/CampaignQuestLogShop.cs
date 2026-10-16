using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

// Source shop.c keeps exactly two temporary entries and publishes them on exit.
// This value belongs to the live recorder only; it is never part of a save.
public sealed record CampaignQuestLogShopEntry(bool Buying,ItemId LastItem,int Quantity,int Money,bool Multiple);
public sealed record CampaignQuestLogShopCapture(ImmutableArray<CampaignQuestLogShopEntry> Entries)
{
    public static (CampaignQuestLogShopCapture? Capture,ImmutableArray<CampaignQuestLogAction> Events) Observe(
        CampaignQuestLogShopCapture? capture,CampaignState before,CampaignState after,ICampaignCatalog catalog)
    {
        if(before.Shop is null)return (after.Shop is null?null:new([]),[]);
        capture??=new([]);
        if(before.Shop is {Stage:CampaignShopStage.Confirmation,Item:{} item} shop&&
            after.Shop is {Stage:CampaignShopStage.Message,MessageSymbol:"gText_HereYouGoThankYou" or "gText_TurnedOverItemsWorthYen"})
        {
            bool buying=shop.Mode=="buy";
            var definition=catalog.Inventory.Items.GetValueOrDefault(item)??catalog.Machines?.Machines.FirstOrDefault(m=>m.Machine.Item.Id==item)?.Machine.Item
                ??throw new InvalidDataException("quest_log_shop_item_missing: "+item.Value);
            int index=-1;
            for(int i=0;i<capture.Entries.Length;i++)if(capture.Entries[i].Buying==buying)index=i;
            var previous=index<0?new CampaignQuestLogShopEntry(buying,item,0,0,false):capture.Entries[index];
            var entry=previous with{LastItem=item,Quantity=Math.Min(999,previous.Quantity+shop.Quantity),
                Money=(int)Math.Min(999999,(long)previous.Money+(buying?definition.Price:definition.Price/2)*(long)shop.Quantity),Multiple=index>=0};
            capture=new(index<0?capture.Entries.Add(entry):capture.Entries.SetItem(index,entry));
        }
        if(after.Shop is not null)return(capture,[]);
        var source=((ICampaignQuestLogCatalog)catalog).QuestLogSource;
        string map=source.MapName(before.World.Player.MapId.Value);
        var events=ImmutableArray.CreateBuilder<CampaignQuestLogAction>();
        foreach(var entry in capture.Entries)
        {
            var itemDefinition=catalog.Inventory.Items.GetValueOrDefault(entry.LastItem)??catalog.Machines?.Machines.FirstOrDefault(m=>m.Machine.Item.Id==entry.LastItem)?.Machine.Item
                ??throw new InvalidDataException("quest_log_shop_item_missing: "+entry.LastItem.Value);
            string name=itemDefinition.Name;
            string money=entry.Money.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if(entry.Buying)events.Add(entry.Quantity<2?
                CampaignQuestLogRecorder.Event(CampaignQuestLogEvent.Bought,"BoughtItem",map,name):
                CampaignQuestLogRecorder.Event(CampaignQuestLogEvent.Bought,"BoughtItemsIncludingItem",map,name,money));
            else if(entry.Multiple)events.Add(CampaignQuestLogRecorder.Event(CampaignQuestLogEvent.Sold,"SoldItemsIncludingItem",map,name,money));
            else
            {
                string quantity=entry.Quantity==1?source.Texts["JustOne"]:source.Expand("Num",before.PlayerName,["","","","",entry.Quantity.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
                events.Add(CampaignQuestLogRecorder.Event(CampaignQuestLogEvent.Sold,"SoldNumOfItem",before.PlayerName,map,name,quantity));
            }
        }
        return(null,events.ToImmutable());
    }
}
