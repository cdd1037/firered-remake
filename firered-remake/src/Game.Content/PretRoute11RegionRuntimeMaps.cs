using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>91 pinned maps; every prior city-services payload, entry and source registry remains exact.</summary>
public static class PretRoute11RegionRuntimeMaps
{
    public const string DirectoryName = "content/generated/route11-runtime-maps";
    public const string ManifestSha256 = "2e41c5d0b82be9904dcbcf88bc5a423b09f16ea31683c82e909b7ed7e006a6b1";
    public const string ConverterSha256 = "6c6e620d847165a5f5530f2679c607a2c9a3ae206bd43a1790c71e36f99064c8";
    public const string SourceLockSha256 = PretRoute11Region.LockSha256;

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), DirectoryName), ManifestSha256);

    internal static void ValidatePackage(string directory, ImmutableArray<PretRuntimeMapEntry> entries)
    {
        void Need(bool value, string message) => PretRuntimeMapValidation.Require(value, message);
        // Run the historical 83-map contract unchanged against precisely its prior entries.
        PretCityServicesRuntimeMaps.ValidatePackage(directory,
            entries.Where(e => e.FullSourceHash != PretRoute11Region.FullSourceHash).ToImmutableArray());
        foreach (var relative in entries.Select(e => e.Path).Append("route11-region-source-lock.json"))
        {
            var path = PretRuntimeMapValidation.SafeFile(directory, relative);
            for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
                Need((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Route11/Cave runtime symlink rejected.");
        }
        var bytes = PretRuntimeMapValidation.ReadBounded(PretRuntimeMapValidation.SafeFile(directory, "route11-region-source-lock.json"), 256 * 1024);
        Need(PretRuntimeMapValidation.Hash(bytes) == SourceLockSha256, "Route11/Cave runtime source lock differs.");
        using var document = JsonDocument.Parse(bytes);
        var source = document.RootElement;
        Need(source.GetProperty("commit").GetString() == PretWorldMap.SourceCommit &&
            source.GetProperty("profile").GetString() == PretRoute11Region.Profile &&
            source.GetProperty("files").GetArrayLength() == 256 && source.GetProperty("maps").GetArrayLength() == 8,
            "Route11/Cave runtime source registry identity differs.");
        var regional = entries.Where(e => e.FullSourceHash == PretRoute11Region.FullSourceHash).ToArray();
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["map.route-11"] = "route11-region/route-11/runtime-map.json",
            ["map.digletts-cave-south-entrance"] = "route11-region/digletts-cave-south-entrance/runtime-map.json",
            ["map.digletts-cave-b1f"] = "route11-region/digletts-cave-b1f/runtime-map.json",
            ["map.digletts-cave-north-entrance"] = "route11-region/digletts-cave-north-entrance/runtime-map.json",
            ["map.route-2-east-building"] = "route11-region/route-2-east-building/runtime-map.json",
            ["map.route-2-house"] = "route11-region/route-2-house/runtime-map.json",
            ["map.route-11-east-entrance-1f"] = "route11-region/route-11-east-entrance-1f/runtime-map.json",
            ["map.route-11-east-entrance-2f"] = "route11-region/route-11-east-entrance-2f/runtime-map.json",
        };
        Need(entries.Length == 91 && regional.Length == 8 && regional.All(e => expected.GetValueOrDefault(e.StableId) == e.Path),
            "Route11/Cave runtime map registry differs.");
    }
}
