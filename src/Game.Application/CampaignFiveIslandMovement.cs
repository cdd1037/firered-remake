using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private IReadOnlyDictionary<MapId,IFieldMap> FiveIslandMovementMaps(IReadOnlyDictionary<MapId,IFieldMap> maps, CampaignState state)
    {
        var changed = maps.ToDictionary(p => p.Key, p => p.Value);
        foreach (var pair in maps.Where(p => FiveIslandEditedMap(p.Key)))
        {
            var edits = FiveIslandExpectedEdits(state, pair.Key);
            if (!edits.IsEmpty) changed[pair.Key] = new RocketEditedMap(pair.Value, edits, _content);
        }
        return changed;
    }
}
