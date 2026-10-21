using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Ids;
namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    public bool IsRoute22StablePosition(MapId map,int x,int y)=>(map.Value!="map.route-23"||Maps[map].GetTile(x,y) is not null);

}
