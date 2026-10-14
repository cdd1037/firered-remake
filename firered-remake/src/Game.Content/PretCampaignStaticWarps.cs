using Game.Domain.Ids;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    // Current source objects are authenticated during composition. Dynamic exits,
    // story gates, collision, direction and partial-region geometry stay with
    // their existing owners; this answers only static destination completeness.
    private bool IsCurrentStaticWarpResolved(MapId map, int index)
    {
        if (!Maps.TryGetValue(map, out var source) || index < 0 || index >= source.FieldWarps.Length)
            return false;
        var warp = source.FieldWarps[index];
        return warp.Index == index && Maps.TryGetValue(warp.DestinationMapId, out var target) &&
            warp.DestinationWarpId >= 0 && warp.DestinationWarpId < target.FieldWarps.Length &&
            target.FieldWarps[warp.DestinationWarpId].Index == warp.DestinationWarpId;
    }
}
