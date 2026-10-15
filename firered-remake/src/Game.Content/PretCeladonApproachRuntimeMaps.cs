
namespace Game.Content;

/// <summary>Runtime maps for CeladonApproach.</summary>
public static class PretCeladonApproachRuntimeMaps
{
    public const string ManifestPath = "content/generated/maps/catalogs/celadon-approach-maps.json";

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), ManifestPath));
}
