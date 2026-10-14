using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // Current construction authenticates the latest shop. These predecessors remain
    // typed source proofs and preserve each later key-item extension when narrowing.
    private SeafoamShopCatalog SeafoamShopProof => PokeDollInventoryCatalog.For(ShopCatalog)?.Predecessor ?? SeafoamShopCatalog.For(ShopCatalog) ?? throw new InvalidDataException("seafoam_shop_proof_missing");
    private InventoryState SeafoamPredecessorInventory(InventoryState inventory) => SeafoamShopProof.InventoryForPredecessor(SeafoamInventoryView(inventory));
    private NpcTradeShopCatalog NpcTradeShopProof => SeafoamShopProof.Predecessor;
    private InventoryState NpcTradePredecessorInventory(InventoryState inventory) => NpcTradeShopProof.InventoryForPredecessor(SeafoamPredecessorInventory(inventory));
    private FossilLabShopCatalog? FossilLabShopProof => _content.FossilLabShop;
    private InventoryState FossilLabPredecessorInventory(InventoryState inventory) => FossilLabShopProof?.InventoryForPredecessor(NpcTradePredecessorInventory(inventory))??inventory;
    private TrainerTowerShopCatalog? TrainerTowerShopProof => FossilLabShopProof?.Predecessor ?? TrainerTowerShopCatalog.For(ShopCatalog);
    private InventoryState TrainerTowerPredecessorInventory(InventoryState inventory) => TrainerTowerShopProof?.InventoryForPredecessor(FossilLabPredecessorInventory(inventory))??inventory;
    private PowerPlantShopCatalog PowerPlantShopProof => TrainerTowerShopProof?.Predecessor ?? PowerPlantShopCatalog.For(ShopCatalog) ?? throw new InvalidDataException("power_plant_shop_proof_missing");
    private InventoryState PowerPlantPredecessorInventory(InventoryState inventory) => PowerPlantShopProof.InventoryForPredecessor(TrainerTowerPredecessorInventory(inventory));
    private EventIslandShopCatalog EventIslandShopProof => PowerPlantShopProof.Predecessor;
    private SevenIslandShopCatalog SevenIslandShopProof => EventIslandShopProof.Predecessor;
    private InventoryState EventIslandPredecessorInventory(InventoryState inventory) => EventIslandShopProof.InventoryForPredecessor(PowerPlantPredecessorInventory(inventory));
    private FiveIslandShopCatalog FiveIslandShopProof => SevenIslandShopProof.Predecessor;
    private InventoryState SevenIslandPredecessorInventory(InventoryState inventory) => SevenIslandShopProof.InventoryForPredecessor(EventIslandPredecessorInventory(inventory));
    private SixIslandShopCatalog SixIslandShopProof => FiveIslandShopProof.Predecessor;
    private FourIslandShopCatalog FourIslandShopProof => SixIslandShopProof.Predecessor;
    private SeviiRubyShopCatalog SeviiRubyShopProof => FourIslandShopProof.Predecessor;
    private VictoryRoadShopCatalog VictoryRoadShopProof => SeviiRubyShopProof.Predecessor;
    private FirstSeviiShopCatalog FirstSeviiShopProof => VictoryRoadShopProof.Predecessor;

    private InventoryState FiveIslandPredecessorInventory(InventoryState inventory) => FiveIslandShopProof.InventoryForPredecessor(SevenIslandPredecessorInventory(inventory));
    private InventoryState SixIslandPredecessorInventory(InventoryState inventory) => SixIslandShopProof.InventoryForPredecessor(FiveIslandPredecessorInventory(inventory));
    private InventoryState FourIslandPredecessorInventory(InventoryState inventory) => FourIslandShopProof.InventoryForPredecessor(SixIslandPredecessorInventory(inventory));
    private InventoryState VictoryRoadPredecessorInventory(InventoryState inventory) => VictoryRoadShopProof.InventoryForPredecessor(SeviiRubyShopProof.InventoryForPredecessor(FourIslandPredecessorInventory(inventory)));
    private InventoryState FirstSeviiPredecessorInventory(InventoryState inventory) => FirstSeviiShopProof.InventoryForPredecessor(VictoryRoadPredecessorInventory(inventory));

    private InventoryState MergeFirstSeviiPredecessorInventory(InventoryState original, InventoryState changed)
    {
        var view = ShopInventoryView(original);
        changed = FirstSeviiShopProof.RestoreFromPredecessor(VictoryRoadPredecessorInventory(view), changed);
        changed = VictoryRoadShopProof.RestoreFromPredecessor(SeviiRubyShopProof.InventoryForPredecessor(FourIslandPredecessorInventory(view)), changed);
        changed = SeviiRubyShopProof.RestoreFromPredecessor(FourIslandPredecessorInventory(view), changed);
        changed = FourIslandShopProof.RestoreFromPredecessor(SixIslandPredecessorInventory(view), changed);
        changed = SixIslandShopProof.RestoreFromPredecessor(FiveIslandPredecessorInventory(view), changed);
        changed = FiveIslandShopProof.RestoreFromPredecessor(SevenIslandPredecessorInventory(view), changed);
        changed = SevenIslandShopProof.RestoreFromPredecessor(EventIslandPredecessorInventory(view), changed);
        changed = EventIslandShopProof.RestoreFromPredecessor(PowerPlantPredecessorInventory(view), changed);
        changed = PowerPlantShopProof.RestoreFromPredecessor(TrainerTowerPredecessorInventory(view), changed);
        if(TrainerTowerShopProof is {} tower)changed=tower.RestoreFromPredecessor(FossilLabPredecessorInventory(view),changed);
        if(FossilLabShopProof is {} fossil)changed=fossil.RestoreFromPredecessor(NpcTradePredecessorInventory(view),changed);
        changed = NpcTradeShopProof.RestoreFromPredecessor(SeafoamPredecessorInventory(view), changed);
        changed = RestoreSeafoamInventory(view, SeafoamShopProof.RestoreFromPredecessor(SeafoamInventoryView(view), changed));
        return MergeShopInventory(original, changed);
    }
    private bool TryApplyFirstSeviiItemEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;
        if(state.FieldPickup is not null)return false;
        if(effect.Opcode is not("checkitem" or "checkitemspace" or "additem" or "removeitem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")||!InFirstSeviiScript(state))return false;
        var a=effect.Args;bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);
        int id=SeviiValue(state,a[buffer?1:0]);var item=ShopCatalog.Items.SingleOrDefault(i=>i.SourceItemId==id);
        if(item is null||item.SourceItemSymbol is not("ITEM_METEORITE" or "ITEM_TRI_PASS" or "ITEM_IAPAPA_BERRY" or "ITEM_MOON_STONE" or "ITEM_FULL_RESTORE"))return false;
        if(!(FirstSeviiSource(effect.Source.Path)||effect.Source.Path is "data/scripts/obtain_item.inc" or "data/event_scripts.s"))
            throw new InvalidDataException("first_sevii_gift_source");
        if(buffer)
        {
            if(a[0] is not("STR_VAR_1" or "STR_VAR_2" or "STR_VAR_3"))throw new InvalidDataException("first_sevii_buffer");
            next=state with{TextBuffers=state.TextBuffers.SetItem(a[0],item.Name)};return true;
        }
        CampaignState Result(int value)=>state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",value)}};
        if(effect.Opcode=="checkitemtype"){next=Result(SeviiValue(state,item.Pocket));return true;}
        var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
        if(ValidateCampaignInventory(inventory) is {} error)throw new InvalidDataException(error);
        int quantity=a.Length>1?SeviiValue(state,a[1]):1;
        if(quantity!=1)throw new InvalidDataException("first_sevii_gift_quantity");
        var view=ShopInventoryView(inventory);
        if(effect.Opcode=="checkitem")
        {next=Result(OpeningInventoryRules.Quantity((item.Pocket switch { "POCKET_KEY_ITEMS" => view.KeyItems??[], "POCKET_BERRY_POUCH" => view.Berries??[], _ => view.BagItems }),item.Id)>=quantity?1:0);return true;}
        bool remove=effect.Opcode=="removeitem";
        if(remove&&(item.Id!=FirstSeviiShopCatalog.MeteoriteId||effect.Source.Symbol!="TwoIsland_JoyfulGameCorner_EventScript_GiveDaddyMeteorite"))
            throw new InvalidDataException("first_sevii_gift_removal_context");
        var result=remove?EarlyInventoryAddRules.Remove(ShopCatalog,view,item.Id,quantity):effect.Opcode=="checkitemspace"?
            EarlyInventoryAddRules.CheckSpace(ShopCatalog,view,item.Id,quantity):EarlyInventoryAddRules.Add(ShopCatalog,view,item.Id,quantity);
        if(!result.Succeeded&&result.Error is not("inventory_stack_full" or "inventory_destination_full" or "inventory_key_items_full" or "inventory_insufficient_quantity"))throw new InvalidDataException(result.Error);
        var changed=inventory;
        if(result.Succeeded&&effect.Opcode!="checkitemspace")
        {
            changed=MergeShopInventory(inventory,result.State);
            if(item.Pocket=="POCKET_KEY_ITEMS")
            {
                int count=OpeningInventoryRules.Quantity(result.State.KeyItems??[],item.Id);
                var keys=changed.KeyItems??[];int index=-1;
                for(int i=0;i<keys.Length;i++)if(keys[i].Item==item.Id){index=i;break;}
                changed=changed with {KeyItems=count==0?keys.RemoveAll(x=>x.Item==item.Id):index>=0?keys.SetItem(index,new(item.Id,count)):keys.Add(new(item.Id,count))};
            }
        }
        next=Result(result.Succeeded?1:0) with{Inventory=changed,Script=Result(result.Succeeded?1:0).Script with{
            Flags=result.Succeeded&&effect.Opcode=="additem"&&item.Pocket=="POCKET_BERRY_POUCH"?
                state.Script.Flags.SetItem("FLAG_SYS_GOT_BERRY_POUCH",true):state.Script.Flags}};
        return true;
    }
    private string? ValidateFirstSeviiProgress(CampaignState state)
    {
        bool Flag(string key)=>state.Script.Flags.GetValueOrDefault(key);
        int Var(string key)=>state.Script.Variables.GetValueOrDefault(key);
        int Count(ItemId item)=>OpeningInventoryRules.Quantity(state.Inventory?.KeyItems??[],item);
        int center=Var("VAR_MAP_SCENE_ONE_ISLAND_POKEMON_CENTER_1F");
        bool rescued=Flag("FLAG_RESCUED_LOSTELLE"),received=Flag("FLAG_GOT_MOON_STONE_FROM_JOYFUL_GAME_CORNER"),pending=Flag("FLAG_NO_ROOM_FOR_JOYFUL_GAME_CORNER_MOON_STONE");
        if(!_content.FirstSeviiEnabled)return center!=0||rescued||received||pending||Flag("FLAG_SYS_SEVII_MAP_123")||
            Count(FirstSeviiShopCatalog.TriPassId)!=0||Count(FirstSeviiShopCatalog.MeteoriteId)!=0?"save_first_sevii_profile_missing":null;
        if(state.Seagallop is not null||state.SeagallopMenu is not null)return "save_seagallop_active";
        if(center<0||center>(CelioCompleted(state)?7:_content.SeviiRubyEnabled?5:3)||Var("VAR_MAP_SCENE_ONE_ISLAND_HARBOR") is <0 or >3||
            Var("VAR_MAP_SCENE_TWO_ISLAND_JOYFUL_GAME_CORNER") is <0 or >4||Var("VAR_MAP_SCENE_THREE_ISLAND") is <0 or >4||
            Var("VAR_MAP_SCENE_TWO_ISLAND") is <0 or >2)return "save_first_sevii_scene";
        if((center>0||FirstSeviiMap(state.World.Player.MapId.Value))&&!Flag("FLAG_BADGE07_GET"))return "save_first_sevii_without_badge";
        if(Count(FirstSeviiShopCatalog.TriPassId)!=(center>0&&center<5?1:0)||Count(FirstSeviiShopCatalog.MeteoriteId)!=(center==1?1:0)||
            Flag("FLAG_SYS_SEVII_MAP_123")!=(center>0))return "save_first_sevii_key_progress";
        if(center>=2&&!rescued||received&&(!rescued||center<2)||pending&&center<2)
            return "save_first_sevii_delivery_progress";
        if(Var("VAR_MAP_SCENE_TWO_ISLAND_JOYFUL_GAME_CORNER")==4&&!received)return "save_first_sevii_revisit_progress";
        if(rescued!=Flag("FLAG_HIDE_LOSTELLE_IN_BERRY_FOREST"))return "save_first_sevii_lostelle_visibility";
        if(Flag("FLAG_SYS_PC_STORAGE_DISABLED")&&Flag("FLAG_SEVII_DETOUR_FINISHED"))return "save_first_sevii_pc_progress";
        if(Var("VAR_MAP_SCENE_CINNABAR_ISLAND")>=3&&center<3)return "save_first_sevii_return_progress";
        return null;
    }
}
