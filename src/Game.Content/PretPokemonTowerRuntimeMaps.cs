using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>127 source maps. Original 120 entries, source registries and payload bytes remain exact.</summary>
public static class PretPokemonTowerRuntimeMaps
{
    public const string ManifestPath = "content/generated/maps/catalogs/pokemon-tower-maps.json";
    public static PretRuntimeMapCatalog Load(string projectRoot)
    {
        var directory = Path.Combine(Path.GetFullPath(projectRoot), ManifestPath);
        PretRocketHideoutRuntimeMaps.RequireNoLinks(Path.GetDirectoryName(directory)!, Path.GetFileName(directory));
        return PretRuntimeMapCatalog.Load(directory);
    }
}
