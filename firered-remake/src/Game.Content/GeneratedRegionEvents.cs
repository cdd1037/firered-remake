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
            "data/maps/CeruleanCity/map.json" => "content/generated/cerulean-region/cerulean-city/runtime-map.json",
            "data/maps/VermilionCity/map.json" => "content/generated/vermilion-region/vermilion-city/runtime-map.json",
            "data/maps/CeladonCity/map.json" => "content/generated/celadon-approach-region/celadon-city/runtime-map.json",
            "data/maps/FuchsiaCity/map.json" => "content/generated/fuchsia-route-region/fuchsia-city/runtime-map.json",
            "data/maps/LavenderTown/map.json" => "content/generated/rocktunnel-region/lavender-town/runtime-map.json",
            "data/maps/Route2/map.json" => "content/generated/forest-region/route-2/runtime-map.json",
            "data/maps/Route9/map.json" => "content/generated/rocktunnel-region/route-9/runtime-map.json",
            "data/maps/Route12/map.json" => "content/generated/snorlax-region/route-12/runtime-map.json",
            "data/maps/Route16/map.json" => "content/generated/snorlax-region/route-16/runtime-map.json",
            _ => throw new InvalidDataException("No generated map owns the requested region events: " + sourcePath)
        };
        using var document = JsonDocument.Parse(File.ReadAllBytes(PretRuntimeMapValidation.SafeFile(projectRoot, relative)));
        return Encoding.UTF8.GetBytes(document.RootElement.GetProperty("events").GetRawText());
    }
}
