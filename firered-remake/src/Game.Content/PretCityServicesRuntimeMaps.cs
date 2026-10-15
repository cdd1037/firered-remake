
namespace Game.Content;

/// <summary>Runtime maps for CityServices.</summary>
public static class PretCityServicesRuntimeMaps
{
    public const string DirectoryName = "content/generated/runtime-city-services-maps";

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), DirectoryName));
}
