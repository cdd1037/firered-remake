
namespace Game.Content;

/// <summary>Runtime maps for CeladonGym.</summary>
public static class PretCeladonGymRuntimeMaps
{
    public const string ManifestPath = "content/generated/maps/catalogs/celadon-gym-maps.json";

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), ManifestPath));
}
