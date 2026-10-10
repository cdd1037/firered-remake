using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>113 pinned maps; every prior RockTunnel payload, entry and source registry remains exact.</summary>
public static class PretCeladonApproachRuntimeMaps
{
    public const string DirectoryName = "content/generated/celadon-approach-runtime-maps";
    public const string ManifestSha256 = "c18a66b9b2ce6adbfb1ad6e6e789e3d14e361fd70e362a29b9f6c4725d316c2c";
    public const string ConverterSha256 = "f9561b7e9e2c9d4d5336b50ad2753c45eee043fd663c34ad438acd80fc0d0912";
    public const string SourceLockSha256 = PretCeladonApproachRegion.LockSha256;

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), DirectoryName), ManifestSha256);

    internal static void ValidatePackage(string directory, ImmutableArray<PretRuntimeMapEntry> entries)
    {
        void Need(bool value, string message) => PretRuntimeMapValidation.Require(value, message);
        // Run the historical 98-map contract unchanged against precisely its prior entries.
        PretRockTunnelRuntimeMaps.ValidatePackage(directory,
            entries.Where(e => e.FullSourceHash != PretCeladonApproachRegion.FullSourceHash).ToImmutableArray());
        foreach (var relative in entries.Select(e => e.Path).Append("celadon-approach-region-source-lock.json"))
        {
            var path = PretRuntimeMapValidation.SafeFile(directory, relative);
            for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
                Need((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Celadon approach runtime symlink rejected.");
        }
        var bytes = PretRuntimeMapValidation.ReadBounded(PretRuntimeMapValidation.SafeFile(directory, "celadon-approach-region-source-lock.json"), 256 * 1024);
        Need(PretRuntimeMapValidation.Hash(bytes) == SourceLockSha256, "Celadon approach runtime source lock differs.");
        using var document = JsonDocument.Parse(bytes);
        var source = document.RootElement;
        Need(source.GetProperty("commit").GetString() == PretWorldMap.SourceCommit &&
            source.GetProperty("profile").GetString() == PretCeladonApproachRegion.Profile &&
            source.GetProperty("files").GetArrayLength() == 418 && source.GetProperty("maps").GetArrayLength() == 15,
            "Celadon approach runtime source registry identity differs.");
        var regional = entries.Where(e => e.FullSourceHash == PretCeladonApproachRegion.FullSourceHash).ToArray();
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["map.celadon-city-pokemon-center-1f"] = "celadon-approach-region/celadon-city-pokemon-center-1f/runtime-map.json",
            ["map.celadon-city-pokemon-center-2f"] = "celadon-approach-region/celadon-city-pokemon-center-2f/runtime-map.json",
            ["map.celadon-city"] = "celadon-approach-region/celadon-city/runtime-map.json",
            ["map.lavender-town-house-1"] = "celadon-approach-region/lavender-town-house-1/runtime-map.json",
            ["map.lavender-town-mart"] = "celadon-approach-region/lavender-town-mart/runtime-map.json",
            ["map.lavender-town-pokemon-center-1f"] = "celadon-approach-region/lavender-town-pokemon-center-1f/runtime-map.json",
            ["map.lavender-town-pokemon-center-2f"] = "celadon-approach-region/lavender-town-pokemon-center-2f/runtime-map.json",
            ["map.lavender-town-volunteer-pokemon-house"] = "celadon-approach-region/lavender-town-volunteer-pokemon-house/runtime-map.json",
            ["map.route-7-east-entrance"] = "celadon-approach-region/route-7-east-entrance/runtime-map.json",
            ["map.route-7"] = "celadon-approach-region/route-7/runtime-map.json",
            ["map.route-8-west-entrance"] = "celadon-approach-region/route-8-west-entrance/runtime-map.json",
            ["map.route-8"] = "celadon-approach-region/route-8/runtime-map.json",
            ["map.underground-path-east-entrance"] = "celadon-approach-region/underground-path-east-entrance/runtime-map.json",
            ["map.underground-path-east-west-tunnel"] = "celadon-approach-region/underground-path-east-west-tunnel/runtime-map.json",
            ["map.underground-path-west-entrance"] = "celadon-approach-region/underground-path-west-entrance/runtime-map.json",
        };
        Need(entries.Length == 113 && regional.Length == 15 && regional.All(e => expected.GetValueOrDefault(e.StableId) == e.Path),
            "Celadon approach runtime map registry differs.");
    }
}
