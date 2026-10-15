
namespace Game.Content;

/// <summary>Runtime maps for CityServices.</summary>
public static class PretCityServicesRuntimeMaps
{
    public const string ManifestPath = "content/generated/maps/catalogs/city-services-maps.json";

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), ManifestPath));
}
