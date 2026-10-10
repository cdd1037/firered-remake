using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>98 pinned maps; every prior Route11 payload, entry and source registry remains exact.</summary>
public static class PretRockTunnelRuntimeMaps
{
    public const string DirectoryName = "content/generated/rocktunnel-runtime-maps";
    public const string ManifestSha256 = "a0fb56162cc143d71e991559d637b2e6cb0d47f3cf90adc2b5fd660fcef78486";
    public const string ConverterSha256 = "eb2212ea855f72d8f39d09f5a6f78cf50b425c10c807634a4133577cc725d669";
    public const string SourceLockSha256 = PretRockTunnelRegion.LockSha256;

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), DirectoryName), ManifestSha256);

    internal static void ValidatePackage(string directory, ImmutableArray<PretRuntimeMapEntry> entries)
    {
        void Need(bool value, string message) => PretRuntimeMapValidation.Require(value, message);
        // Run the historical 91-map contract unchanged against precisely its prior entries.
        PretRoute11RegionRuntimeMaps.ValidatePackage(directory,
            entries.Where(e => e.FullSourceHash != PretRockTunnelRegion.FullSourceHash).ToImmutableArray());
        foreach (var relative in entries.Select(e => e.Path).Append("rocktunnel-region-source-lock.json"))
        {
            var path = PretRuntimeMapValidation.SafeFile(directory, relative);
            for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
                Need((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "RockTunnel runtime symlink rejected.");
        }
        var bytes = PretRuntimeMapValidation.ReadBounded(PretRuntimeMapValidation.SafeFile(directory, "rocktunnel-region-source-lock.json"), 256 * 1024);
        Need(PretRuntimeMapValidation.Hash(bytes) == SourceLockSha256, "RockTunnel runtime source lock differs.");
        using var document = JsonDocument.Parse(bytes);
        var source = document.RootElement;
        Need(source.GetProperty("commit").GetString() == PretWorldMap.SourceCommit &&
            source.GetProperty("profile").GetString() == PretRockTunnelRegion.Profile &&
            source.GetProperty("files").GetArrayLength() == 296 && source.GetProperty("maps").GetArrayLength() == 7,
            "RockTunnel runtime source registry identity differs.");
        var regional = entries.Where(e => e.FullSourceHash == PretRockTunnelRegion.FullSourceHash).ToArray();
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["map.route-9"] = "rocktunnel-region/route-9/runtime-map.json",
            ["map.route-10"] = "rocktunnel-region/route-10/runtime-map.json",
            ["map.rock-tunnel-1f"] = "rocktunnel-region/rock-tunnel-1f/runtime-map.json",
            ["map.rock-tunnel-b1f"] = "rocktunnel-region/rock-tunnel-b1f/runtime-map.json",
            ["map.route-10-pokemon-center-1f"] = "rocktunnel-region/route-10-pokemon-center-1f/runtime-map.json",
            ["map.route-10-pokemon-center-2f"] = "rocktunnel-region/route-10-pokemon-center-2f/runtime-map.json",
            ["map.lavender-town"] = "rocktunnel-region/lavender-town/runtime-map.json",
        };
        Need(entries.Length == 98 && regional.Length == 7 && regional.All(e => expected.GetValueOrDefault(e.StableId) == e.Path),
            "RockTunnel runtime map registry differs.");
    }
}
