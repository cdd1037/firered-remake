using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.World;
namespace Game.Application;
public sealed partial class CampaignSession
{
    private int? SourceBehavior(CampaignState state,int x,int y)=>state.MapEdits?.TryGetValue(x+":"+y,out var edit)==true
        ?(_content.MetatileDefinition(state.World.Player.MapId,edit.SourceMetatileId)??throw new InvalidDataException("metatile_definition_missing")).Behavior:_content.Maps[state.World.Player.MapId].SourceBehaviorAt(x,y);
    private bool TryApplyParcelEffect(CampaignState state,StoryEffect effect,out CampaignState next)
    {
        next=state;if(_content.QuestItems is not {} catalog)return false;
        var args=effect.Args;
        int Value(string token)=>token.StartsWith("VAR_",StringComparison.Ordinal)?state.Script.Variables.GetValueOrDefault(token):int.TryParse(token,out var n)?n:token.StartsWith("0x",StringComparison.OrdinalIgnoreCase)?Convert.ToInt32(token[2..],16):_content.TryConstant(token,out n)?n:throw new InvalidDataException("parcel_value_missing: "+token);
        CampaignState Buffer(string variable,string value)
        {
            int capacity=variable=="STR_VAR_1"?31:variable is "STR_VAR_2" or "STR_VAR_3"?19:throw new InvalidDataException("parcel_string_buffer_invalid");
            if(value.Length>capacity)throw new InvalidDataException("parcel_string_buffer_overflow");
            return state with{TextBuffers=state.TextBuffers.SetItem(variable,value)};
        }
        if(effect.Opcode=="loadword")
        {
            if(Value(args[0])!=0)throw new InvalidDataException("script_local_word_slot_unsupported");
            _=_content.TextPages(args[1]);next=state with{MessagePointer=args[1]};return true;
        }
        if(effect.Opcode=="setmetatile")
        {
            int x=Value(args[0]),y=Value(args[1]),id=Value(args[2]);
            if(state.World.Player.MapId.Value!="map.viridian-mart"||x!=1||(y,id) is not ((3,0x2bf) or (4,0x2c0))||Value(args[3])!=1||_content.MetatileDefinitions?.ContainsKey(id)!=true)throw new InvalidDataException("metatile_edit_profile_unsupported");
            var cell=_content.Maps[state.World.Player.MapId].GetTile(x,y)??throw new InvalidDataException("metatile_edit_outside_map");
            next=state with{MapEdits=(state.MapEdits??ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty).SetItem(x+":"+y,new(x,y,id,3,cell.Elevation))};return true;
        }
        if(effect.Opcode=="special"&&args[0]=="SetUnlockedPokedexFlags")
        {next=state with{GcnLinkFlags=(ushort)(state.GcnLinkFlags|0x31)};return true;}
        if(effect.Opcode=="bufferstdstring")
        {
            var text=args[1] switch{"STDSTRING_ITEMS_POCKET"=>"gText_ItemsPocket","STDSTRING_KEY_ITEMS_POCKET"=>"gText_KeyItemsPocket","STDSTRING_POKEBALLS_POCKET"=>"gText_PokeBallsPocket",_=>throw new InvalidDataException("parcel_standard_string_unsupported")};
            next=Buffer(args[0],string.Join("",_content.TextPages(text)));return true;
        }
        if(effect.Opcode is not ("checkitemspace" or "additem" or "removeitem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural"))return false;
        bool buffer=effect.Opcode.StartsWith("bufferitem",StringComparison.Ordinal);int sourceId=Value(args[buffer?1:0]);
        var item=catalog.Items.SingleOrDefault(x=>x.SourceItemId==sourceId);
        if(item is null)return false;
        var inventory=state.Inventory??throw new InvalidDataException("inventory_missing");
        if(buffer)
        {
            var name=item.Name;if(effect.Opcode=="bufferitemnameplural"&&item.SourceItemSymbol==QuestItemRules.PokeBallSymbol&&Value(args[2])>=2)name+="S";
            next=Buffer(args[0],name);return true;
        }
        if(effect.Opcode=="checkitemtype")
        {
            next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",Value(item.Pocket))}};return true;
        }
        int quantity=args.Length>1?Value(args[1]):1;
        if(quantity is <1 or >255)throw new InvalidDataException("parcel_script_quantity_out_of_profile");
        if(effect.Opcode=="checkitemspace")
        {
            var error=ValidateQuestInventory(catalog,inventory);if(error is not null)throw new InvalidDataException(error);
            // CheckBagHasSpace probes the same insertion rules; Check() tests possession.
            // The tentative immutable insertion is discarded and consumes no item or RNG.
            bool hasSpace=QuestChange(catalog,inventory,item.SourceItemSymbol,quantity,true).Succeeded;
            next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",hasSpace?1:0)}};return true;
        }
        var result=effect.Opcode=="additem"?QuestChange(catalog,inventory,item.SourceItemSymbol,quantity,true):QuestChange(catalog,inventory,item.SourceItemSymbol,quantity,false);
        if(!result.Succeeded)
        {
            // Source additem/removeitem report capacity/quantity failure through VAR_RESULT;
            // Std_ObtainItem owns its failure branch and the original caller's scene ordering.
            if(result.Error is not ("inventory_stack_full" or "inventory_destination_full" or "inventory_insufficient_quantity"))throw new InvalidDataException("parcel_inventory_transaction: "+result.Error);
            next=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",0)}};return true;
        }
        next=state with{Inventory=result.State,Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_RESULT",1)}};return true;
    }
    private IReadOnlyDictionary<Game.Domain.Ids.MapId,ConnectedFieldMap> EditedFieldMaps(CampaignState state)
    {
        var maps=_content.ConnectedFieldMaps!;
        if(state.MapEdits is not {Count:>0})return maps;
        var current=maps[state.World.Player.MapId];var cells=current.Cells.ToBuilder();
        foreach(var edit in state.MapEdits.Values)
        {
            var definition=_content.MetatileDefinition(state.World.Player.MapId,edit.SourceMetatileId)??throw new InvalidDataException("metatile_definition_missing");
            cells[edit.Y*current.Width+edit.X]=new(definition.Behavior,edit.Collision,edit.Elevation);
        }
        var result=maps.ToDictionary(x=>x.Key,x=>x.Value);result[current.Id]=current with{Cells=cells.ToImmutable()};return result;
    }
    private string? ValidateParcelState(CampaignState state)
    {
        if(_content.QuestItems is not {} catalog)
            return state.MapEdits is not null||state.GcnLinkFlags!=0||state.MessagePointer is not null||state.Inventory?.KeyItems is not null||state.Inventory?.Balls is not null?"save_parcel_content_missing":null;
        var inventoryError=ValidateQuestInventory(catalog,state.Inventory);if(inventoryError is not null)return inventoryError;
        if(state.MessagePointer is not null)return "save_message_pointer_active";
        bool dex=state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET");
        int mart=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_VIRIDIAN_CITY_MART"),lab=state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB");
        int parcel=QuestQuantity(catalog,state.Inventory!,QuestItemRules.ParcelSymbol);
        if(!dex&&QuestQuantity(catalog,state.Inventory!,QuestItemRules.PokeBallSymbol)!=0)return "save_balls_before_pokedex";
        if(mart is <0 or >2||mart==0&&(parcel!=0||dex||lab>4)||mart==1&&(parcel!=1||dex||lab!=5)||mart==2&&(parcel!=0||!dex||lab!=6&&!(_content.NationalDexEnabled&&lab is 8 or 9)))return "save_parcel_progress_inconsistent";
        if(state.GcnLinkFlags!=(dex?0x31:0))return "save_pokedex_link_flags_inconsistent";
        if(state.Script.Flags.GetValueOrDefault("FLAG_HIDE_POKEDEX")!=dex)return "save_pokedex_objects_flag_inconsistent";
        if(state.World.Player.MapId.Value=="map.professor-oaks-lab")
        {
            foreach(var definition in _content.MapDefinition(state.World.Player.MapId).Actors.Where(a=>a.HideFlag=="FLAG_HIDE_POKEDEX"))
            {
                if(!state.Actors.TryGetValue(definition.LocalId,out var actor)||actor is null)return "save_pokedex_actor_missing";
                var player=state.World.Player;
                bool inView=actor.X+7>=player.X-2&&actor.X+7<=player.X+17&&actor.Y+7>=player.Y&&actor.Y+7<=player.Y+16;
                if(actor.Active!=(!dex&&inView)||actor.Invisible||actor.X!=definition.X||actor.Y!=definition.Y||actor.TemplateX!=definition.X||actor.TemplateY!=definition.Y)return "save_pokedex_actor_inconsistent";
            }
        }
        if(state.World.Player.MapId.Value=="map.viridian-mart"&&!dex)
        {
            if(state.MapEdits is null||state.MapEdits.Count!=2||!state.MapEdits.TryGetValue("1:3",out var top)||!state.MapEdits.TryGetValue("1:4",out var bottom)||top!=new FieldMetatileEdit(1,3,0x2bf,3,3)||bottom!=new FieldMetatileEdit(1,4,0x2c0,3,0))return "save_mart_counter_edits_invalid";
        }
        else if(state.MapEdits is {Count:>0}&&!(_content.VermilionGymEnabled&&state.World.Player.MapId.Value==VermilionGymMap)&&!(_content.RocketHideoutEnabled&&RocketMap(state.World.Player.MapId))&&!(_content.SilphEnabled&&SilphFieldRules.IsSilphMap(state.World.Player.MapId))&&!(_content.CinnabarEnabled&&CinnabarFieldMap(state.World.Player.MapId))&&!(_content.VictoryRoadEnabled&&VictoryRoadFieldMap(state.World.Player.MapId))&&!(_content.PokemonLeagueEnabled&&PokemonLeagueRoomIndex(state.World.Player.MapId)>=0))return "save_map_edits_unexpected";
        return null;
    }
}
