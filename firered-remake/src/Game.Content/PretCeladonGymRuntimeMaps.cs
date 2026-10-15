
namespace Game.Content;

/// <summary>Runtime maps for CeladonGym.</summary>
public static class PretCeladonGymRuntimeMaps
{
    public const string DirectoryName = "content/generated/celadon-gym-runtime-maps";

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), DirectoryName));
}
