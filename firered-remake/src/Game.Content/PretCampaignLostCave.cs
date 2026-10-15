using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            string path = map.SourceRef.Path.Replace("/map.json", "/scripts.inc", StringComparison.Ordinal);
            if (scripts.UnsupportedInstructions.Any(d => d.Path.StartsWith(path, StringComparison.Ordinal)))
                throw new InvalidDataException("lost_cave_script_composition:" + path);
        }
    }

    private static ImmutableArray<FieldPickupDefinition> LostCavePickupDefinitions(PretLostCaveRegion region,EarlyShopCatalog shop,PretStoryCompilation scripts)
    {
        // Adapt the frozen source contract to the shared pickup validator. Item definitions,
        // script programs and persistent flags remain the authenticated originals.
        var balls = region.Contracts.GetProperty("itemPickups").EnumerateArray().Select(row =>
        {
            var script = row.GetProperty("script");
            var instructions = script.GetProperty("lines").EnumerateArray()
                .Select(line => line.GetProperty("text").GetString()!).ToArray();
            var find = instructions.Select(line => Regex.Match(line,
                @"^\s*finditem\s+(ITEM_[A-Z0-9_]+)(?:\s*,\s*(\d+))?\s*$", RegexOptions.CultureInvariant))
                .Single(match => match.Success);
            int quantity = find.Groups[2].Success ? int.Parse(find.Groups[2].Value,
                System.Globalization.CultureInfo.InvariantCulture) : 1;
            return new { map = row.GetProperty("map"), localId = row.GetProperty("localId"),
                @event = row.GetProperty("event"), item = find.Groups[1].Value, quantity,
                sourceRef = script.GetProperty("sourceRef") };
        }).ToArray();
        var contracts = JsonSerializer.SerializeToElement(new { itemPickups = balls,
            hiddenItems = region.Contracts.GetProperty("hiddenItems") });
        var items = JsonSerializer.SerializeToElement(new { declarations = region.Declarations.GetProperty("itemDeclarations") });
        return SourceRegionPickupDefinitions(region.NewMaps, items, contracts, PretLostCaveRegion.MapIdFromSourceSymbol,
            "lost-cave", shop, scripts);
    }
}
