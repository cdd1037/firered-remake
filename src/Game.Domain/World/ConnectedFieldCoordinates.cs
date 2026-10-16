using Game.Domain.Ids;
namespace Game.Domain.World;

/// <summary>Translate the source map's virtual border cells through its direct camera connections.</summary>
public static class ConnectedFieldCoordinates
{
    public static (MapId Map,int X,int Y)? Resolve(IReadOnlyDictionary<MapId,ConnectedFieldMap> maps,MapId map,int x,int y)
    {
        if(!maps.TryGetValue(map,out var current))return null;
        if(x>=0&&y>=0&&x<current.Width&&y<current.Height)return(map,x,y);
        if(x< -7||y< -7||x>=current.Width+8||y>=current.Height+7)return null;
        foreach(var connection in current.Connections)
        {
            if(!maps.TryGetValue(connection.DestinationMapId,out var other))continue;
            var (ox,oy)=Origin(current,other,connection);
            int tx=x-ox,ty=y-oy;
            if(tx>=0&&ty>=0&&tx<other.Width&&ty<other.Height)return(other.Id,tx,ty);
        }
        return null;
    }
    public static (int X,int Y) Origin(ConnectedFieldMap current,ConnectedFieldMap other,ConnectedFieldConnection connection)=>connection.Direction switch
    {
        Direction.North=>(connection.Offset,-other.Height),Direction.South=>(connection.Offset,current.Height),
        Direction.West=>(-other.Width,connection.Offset),_=>(current.Width,connection.Offset)
    };
}
