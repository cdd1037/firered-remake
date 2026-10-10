using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>127 source maps. Original 120 entries, source registries and payload bytes remain exact.</summary>
public static class PretPokemonTowerRuntimeMaps
{
    public const string DirectoryName = "content/generated/pokemon-tower-runtime-maps";
    public const string ManifestSha256 = "e27a05c831cc9ac286ddaefc37bf1f1f8550fa62ce2b3cdf02832bc13d571918";
    public const string ConverterSha256 = "5ac335edfa72b4cea771f8c5b4d2a8222fb3971863247380c9bc752c3ca5b227";
    public static PretRuntimeMapCatalog Load(string projectRoot)
    {
        var directory = Path.Combine(Path.GetFullPath(projectRoot), DirectoryName);
        PretRocketHideoutRuntimeMaps.RequireNoLinks(directory, "manifest.json");
        return PretRuntimeMapCatalog.Load(directory, ManifestSha256);
    }
    internal static void ValidatePackage(string directory, ImmutableArray<PretRuntimeMapEntry> entries)
    {
        var prior = entries.Where(e => e.FullSourceHash != PretPokemonTowerRegion.FullSourceHash).ToImmutableArray();
        PretRocketHideoutRuntimeMaps.ValidatePackage(directory, prior);
        foreach (var e in entries) PretRocketHideoutRuntimeMaps.RequireNoLinks(directory, e.Path);
        foreach (var path in new[] { "rocket-baseline-manifest.json", "pokemon-tower-region-source-lock.json", "source-registry.json" })
            PretRocketHideoutRuntimeMaps.RequireNoLinks(directory, path);
        var baselineBytes = PretRuntimeMapValidation.ReadBounded(Path.Combine(directory, "rocket-baseline-manifest.json"), 256 * 1024);
        PretRuntimeMapValidation.Require(PretRuntimeMapValidation.Hash(baselineBytes) == PretRocketHideoutRuntimeMaps.ManifestSha256, "Tower baseline manifest differs.");
        var baseline = PretRuntimeMapValidation.Decode<PretRuntimeMapManifest>(baselineBytes);
        PretRuntimeMapValidation.Require(prior.OrderBy(e => e.Path, StringComparer.Ordinal).SequenceEqual(baseline.Maps.OrderBy(e => e.Path, StringComparer.Ordinal)), "Tower changed a prior map registry entry.");
        var lockBytes = PretRuntimeMapValidation.ReadBounded(Path.Combine(directory, "pokemon-tower-region-source-lock.json"), 256 * 1024);
        PretRuntimeMapValidation.Require(PretRuntimeMapValidation.Hash(lockBytes) == PretPokemonTowerRegion.LockSha256, "Tower runtime source lock differs.");
        using var source = JsonDocument.Parse(lockBytes);
        PretRuntimeMapValidation.Require(source.RootElement.GetProperty("commit").GetString() == PretWorldMap.SourceCommit &&
            source.RootElement.GetProperty("tree").GetString() == PretCeladonApproachSource.Tree && source.RootElement.GetProperty("files").GetArrayLength() == 244, "Tower source registry differs.");
        var regional = entries.Where(e => e.FullSourceHash == PretPokemonTowerRegion.FullSourceHash).ToArray();
        var expected = Enumerable.Range(1, 7).Select(f => "map.pokemon-tower-" + f + "f").ToHashSet(StringComparer.Ordinal);
        PretRuntimeMapValidation.Require(entries.Length == 127 && regional.Length == 7 && regional.Select(e => e.StableId).ToHashSet(StringComparer.Ordinal).SetEquals(expected) &&
            regional.All(e => e.Path == "pokemon-tower-region/" + e.StableId[4..] + "/runtime-map.json"), "Tower seven-map registry differs.");
    }
}
