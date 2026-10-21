using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private IReadOnlyDictionary<MapId,IFieldMap> FiveIslandMovementMaps(IReadOnlyDictionary<MapId,IFieldMap> maps, CampaignState state)
    {
        return new CampaignMapView<IFieldMap>(maps,(id,map) =>
        {
            if(!FiveIslandEditedMap(id))return map;
            var edits=FiveIslandExpectedEdits(state,id);
            return edits.IsEmpty ? map : new RocketEditedMap(map,edits,_content);
        });
    }
}
