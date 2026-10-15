using System.Text;
using System.Text.Json;
namespace Game.Content;

/// <summary>Reuse the owning generated map's event data for cross-region entrances.</summary>
internal static class GeneratedRegionEvents
{
    public static byte[] Read(string projectRoot, string sourcePath)
    {
        string relative = sourcePath switch
        {
            "data/maps/CeruleanCity/map.json" => "content/generated/maps/regions/cerulean/cerulean-city.json",
            "data/maps/VermilionCity/map.json" => "content/generated/maps/regions/vermilion/vermilion-city.json",
            "data/maps/CeladonCity/map.json" => "content/generated/maps/regions/celadon-approach/celadon-city.json",
            "data/maps/FuchsiaCity/map.json" => "content/generated/maps/regions/fuchsia-route/fuchsia-city.json",
            "data/maps/LavenderTown/map.json" => "content/generated/maps/regions/rocktunnel/lavender-town.json",
            "data/maps/Route2/map.json" => "content/generated/maps/regions/forest/route-2.json",
            "data/maps/Route9/map.json" => "content/generated/maps/regions/rocktunnel/route-9.json",
            "data/maps/Route12/map.json" => "content/generated/maps/regions/snorlax/route-12.json",
            "data/maps/Route16/map.json" => "content/generated/maps/regions/snorlax/route-16.json",
            _ => throw new InvalidDataException("No generated map owns the requested region events: " + sourcePath)
        };
        using var document = JsonDocument.Parse(File.ReadAllBytes(PretRuntimeMapValidation.SafeFile(projectRoot, relative)));
        return Encoding.UTF8.GetBytes(document.RootElement.GetProperty("events").GetRawText());
    }
}
