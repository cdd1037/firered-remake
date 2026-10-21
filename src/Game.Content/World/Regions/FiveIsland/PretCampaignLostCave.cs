using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    private static void ValidateLostCaveScripts(PretLostCaveRegion region, PretStoryCompilation scripts)
    {
        foreach (var map in region.NewMaps.Values)
        {
            string path = "data/maps/" + map.SourceMapName + "/scripts.inc";
            if (scripts.UnsupportedInstructions.Any(d => d.Path.StartsWith(path, StringComparison.Ordinal)))
                throw new InvalidDataException("lost_cave_script_composition:" + path);
        }
    }

    private static ImmutableArray<FieldPickupDefinition> LostCavePickupDefinitions(PretLostCaveRegion region,Func<string, MapId> resolveMap,EarlyShopCatalog shop,PretStoryCompilation scripts)
    {
        return SourceRegionPickupDefinitions(region.NewMaps, region.Items, region.Contracts, resolveMap,
            "lost-cave", shop, scripts);
    }
}
