using Game.Domain.Ids;
using Game.Domain.World;
namespace Game.Domain.Items;

/// <summary>Berry Forest's exact source setescapewarp over the Cinnabar escape contract.</summary>
public static class FirstSeviiEscapeRopeRules
{
    public static bool IsForestDestination(EscapeRopeDestination? value)=>value is{MapId.Value:"map.three-island-bond-bridge",X:12,Y:6};
    public static EscapeRopeDestination ForestDestination=>new(new("map.three-island-bond-bridge"),12,6);
    public static string? ValidateDestination(EscapeRopeDestination? value)=>IsForestDestination(value)?null:CinnabarEscapeRopeRules.ValidateDestination(value);
    public static EscapeRopeDestination? RecordWarp(EscapeRopeDestination? previous,TilePosition from,MapId to)
    {
        if(ValidateDestination(previous) is {} error)throw new ArgumentException(error,nameof(previous));
        var result=CinnabarEscapeRopeRules.RecordWarp(IsForestDestination(previous)?null:previous,from,to);
        return result??previous;
    }
    public static EscapeRopeUseResult Use(FirstSeviiShopCatalog shop,InventoryState inventory,MapId current,EscapeRopeDestination? destination,PotionUseContext context)
    {
        EscapeRopeUseResult Fail(string error)=>new(inventory,null,false,0,error);
        if(shop.ValidateInventory(inventory) is {} invalid)return Fail(invalid);
        if(current.Value!="map.three-island-berry-forest")
        {
            var old=CinnabarEscapeRopeRules.Use(shop.Predecessor,shop.InventoryForPredecessor(inventory),current,
                IsForestDestination(destination)?null:destination,context);
            return old.Succeeded?old with{Inventory=shop.RestoreFromPredecessor(inventory,old.Inventory)}:Fail(old.Error!);
        }
        if(context!=PotionUseContext.Field)return Fail("escape_rope_field_only");
        if(!IsForestDestination(destination))return Fail("escape_rope_destination_mismatch");
        var removed=EarlyInventoryAddRules.Remove(shop.Catalog,inventory,EscapeRopeRules.Item,1);
        if(!removed.Succeeded)return Fail(removed.Error!);
        // Original Bond Bridge block(12,6)=0x3341: collision0/elevation3.
        return new(removed.State,new(destination!.MapId,12,6,3,Direction.South),true,1,null);
    }
}
