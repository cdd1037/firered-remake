using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Domain.World;

public sealed record ItemfinderUnderfoot(string Map,int X,int Y,int Elevation,string Item,int SourceItemId,string Flag,int SourceFlagId,int Quantity);
public sealed record ItemfinderSourceContract(ImmutableArray<ItemfinderUnderfoot> Underfoot,ImmutableDictionary<string,string> Texts);
public interface IItemfinderSourceCatalog { ItemfinderSourceContract? ItemfinderSource { get; } }
public sealed record ItemfinderTarget(MapId Map,int X,int Y,string Flag,string Item,bool Underfoot);
public sealed record ItemfinderResponse(ItemfinderTarget? Target,int Dx,int Dy,Direction? Direction,int Dings);

/// <summary>src/itemfinder.c: source-order scan, exact underfoot precedence, range and nearest tie-break.</summary>
public static class ItemfinderRules
{
    public static ItemId Item { get; } = new("pret:ITEM_ITEMFINDER");
    public static ItemfinderResponse Scan(ICampaignCatalog catalog,TilePosition player,IReadOnlyDictionary<string,bool> flags,
        ItemfinderSourceContract source)
    {
        ItemfinderTarget? best=null;int bx=0,by=0;
        bool Available(string flag)=>!flags.GetValueOrDefault(flag);
        void Consider(ItemfinderTarget target,int dx,int dy)
        {
            if(best is null||Math.Abs(bx)+Math.Abs(by)>Math.Abs(dx)+Math.Abs(dy)||
                Math.Abs(bx)+Math.Abs(by)==Math.Abs(dx)+Math.Abs(dy)&&(Math.Abs(by)>Math.Abs(dy)||Math.Abs(by)==Math.Abs(dy)&&by<dy))
            {best=target;bx=dx;by=dy;}
        }
        var definition=catalog.MapDefinition(player.MapId);
        // The source ignores elevation for Itemfinder. Physical actor collision governs access to an occupied tile.
        foreach(var item in source.Underfoot.Where(i=>i.Map==definition.SourceMapSymbol))
            if(item.X==player.X&&item.Y==player.Y&&Available(item.Flag))
                return new(new(player.MapId,item.X,item.Y,item.Flag,item.Item,true),0,0,null,3);
        foreach(var bg in definition.BackgroundTriggers)
            if(bg.HiddenItem is {} item&&bg.HiddenFlag is {} flag&&!bg.Underfoot&&!source.Underfoot.Any(i=>i.Map==definition.SourceMapSymbol&&i.Flag==flag)&&Available(flag))
            {
                int dx=bg.X-player.X,dy=bg.Y-player.Y;
                if(dx is >=-7 and <=7&&dy is >=-5 and <=5)Consider(new(player.MapId,bg.X,bg.Y,flag,item,false),dx,dy);
            }
        if(catalog.ConnectedFieldMaps is {} maps&&maps.TryGetValue(player.MapId,out var map))
            for(int x=player.X-7;x<=player.X+7;x++)for(int y=player.Y-5;y<=player.Y+5;y++)
            {
                if(x>=0&&x<map.Width&&y>=0&&y<map.Height)continue;
                foreach(var connection in map.Connections)
                {
                    if(!maps.TryGetValue(connection.DestinationMapId,out var destination))continue;
                    bool vertical=connection.Direction is Direction.North or Direction.South;
                    int axis=(vertical?x:y)-connection.Offset,span=vertical?destination.Width:destination.Height;
                    if(axis<0||axis>=span||connection.Direction switch
                        {Direction.North=>y>=0,Direction.South=>y<map.Height,Direction.West=>x>=0,Direction.East=>x<map.Width,_=>true})continue;
                    int tx=connection.Direction switch{Direction.West=>destination.Width+x,Direction.East=>x-map.Width,_=>x-connection.Offset};
                    int ty=connection.Direction switch{Direction.North=>destination.Height+y,Direction.South=>y-map.Height,_=>y-connection.Offset};
                    var bg=catalog.MapDefinition(destination.Id).BackgroundTriggers.FirstOrDefault(b=>b.X==tx&&b.Y==ty&&b.HiddenItem is not null);
                    if(bg is {Underfoot:false,HiddenFlag:{} flag,HiddenItem:{} item}&&Available(flag)&&!source.Underfoot.Any(i=>i.Map==catalog.MapDefinition(destination.Id).SourceMapSymbol&&i.Flag==flag))
                        Consider(new(destination.Id,tx,ty,flag,item,false),x-player.X,y-player.Y);
                    break; // GetMapConnectionAtPos returns the first matching source connection.
                }
            }
        Direction? direction=bx==0&&by==0?null:Math.Abs(bx)>Math.Abs(by)?bx<0?Direction.West:Direction.East:by<0?Direction.North:Direction.South;
        return new(best,bx,by,direction,best is null?0:Math.Max(Math.Abs(bx),Math.Abs(by))>3?2:4);
    }
}
