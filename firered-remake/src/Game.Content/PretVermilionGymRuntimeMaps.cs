using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>81 pinned maps; the prior 80 compact payloads, entries and source registry remain exact.</summary>
public static class PretVermilionGymRuntimeMaps
{
    public const string DirectoryName = "content/generated/runtime-vermilion-gym-maps";

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), DirectoryName));
}
