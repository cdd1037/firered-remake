using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>98 pinned maps; every prior Route11 payload, entry and source registry remains exact.</summary>
public static class PretRockTunnelRuntimeMaps
{
    public const string ManifestPath = "content/generated/maps/catalogs/rocktunnel-maps.json";

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), ManifestPath));
}
