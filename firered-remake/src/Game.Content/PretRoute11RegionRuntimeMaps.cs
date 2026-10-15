using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>91 pinned maps; every prior city-services payload, entry and source registry remains exact.</summary>
public static class PretRoute11RegionRuntimeMaps
{
    public const string DirectoryName = "content/generated/route11-runtime-maps";

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), DirectoryName));
}
