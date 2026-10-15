using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>The independently pinned 80-map package. Historical 54-map payloads and base registry remain exact.</summary>
public static class PretSsAnneRuntimeMaps
{
    public const string DirectoryName = "content/generated/runtime-ssanne-maps";

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), DirectoryName));
}
