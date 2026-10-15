using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Connects compact maps to their original paths and display directories.</summary>
internal static class PretRuntimeMapIntegration
{
    internal static bool Replaces(PretRuntimeMapCatalog runtime, string path)
        => runtime.Entries.Any(e => e.Path == path);

    internal static PretWorldMap LoadBase(PretRuntimeMapCatalog runtime, PretWorldMapSpecification spec, string generated)
        => PretWorldMap.FromRuntime(runtime.Maps[new MapId(spec.StableId)], runtime.BaseMetadata,
            Path.Combine(generated, spec.Directory));
}
