
namespace Game.Content;

/// <summary>Runtime maps for CeladonApproach.</summary>
public static class PretCeladonApproachRuntimeMaps
{
    public const string DirectoryName = "content/generated/celadon-approach-runtime-maps";

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), DirectoryName));
}
