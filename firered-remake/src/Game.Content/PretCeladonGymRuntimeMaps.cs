using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>114 pinned maps; all 113 preceding entries, payloads and source registries remain exact.</summary>
public static class PretCeladonGymRuntimeMaps
{
    public const string DirectoryName = "content/generated/celadon-gym-runtime-maps";
    public const string ManifestSha256 = "50df37627109ea609f2b2cab2591849a81554077bccfe996e31bc573c4a9a9d4";
    public const string ConverterSha256 = "c9176716345af4321af5128821065b9a95ca2f414eff56d8b783fb53b1537aca";
    public const string SourceLockSha256 = PretCeladonGymRegion.LockSha256;

    public static PretRuntimeMapCatalog Load(string projectRoot)
    {
        var directory = Path.Combine(Path.GetFullPath(projectRoot), DirectoryName);
        RequireNoLinks(directory, "manifest.json");
        return PretRuntimeMapCatalog.Load(directory, ManifestSha256);
    }

    internal static void ValidatePackage(string directory, ImmutableArray<PretRuntimeMapEntry> entries)
    {
        void Need(bool value, string message) => PretRuntimeMapValidation.Require(value, message);
        // Keep the historical 113-map contract unchanged and pass it only its previous entries.
        PretCeladonApproachRuntimeMaps.ValidatePackage(directory,
            entries.Where(e => e.FullSourceHash != PretCeladonGymRegion.FullSourceHash).ToImmutableArray());
        foreach (var relative in entries.Select(e => e.Path).Concat(new[]
        {
            "manifest.json", "source-registry.json", "ssanne-source-lock.json", "vermilion-gym-source-lock.json",
            "city-services-source-lock.json", "route11-region-source-lock.json", "rocktunnel-region-source-lock.json",
            "celadon-approach-region-source-lock.json", "celadon-gym-region-source-lock.json"
        })) RequireNoLinks(directory, relative);
        var bytes = PretRuntimeMapValidation.ReadBounded(
            PretRuntimeMapValidation.SafeFile(directory, "celadon-gym-region-source-lock.json"), 256 * 1024);
        Need(PretRuntimeMapValidation.Hash(bytes) == SourceLockSha256, "Celadon Gym runtime source lock differs.");
        using var document = JsonDocument.Parse(bytes);
        var source = document.RootElement;
        Need(source.GetProperty("schemaVersion").GetInt32() == 1 && source.GetProperty("repository").GetString() == "pret/pokefirered" &&
            source.GetProperty("commit").GetString() == PretWorldMap.SourceCommit && source.GetProperty("tree").GetString() == PretCeladonApproachSource.Tree &&
            source.GetProperty("profile").GetString() == PretCeladonGymRegion.Profile && source.GetProperty("gameVersion").GetString() == "FIRERED" &&
            source.GetProperty("revision").GetInt32() == 0 && source.GetProperty("files").GetArrayLength() == 99 &&
            source.GetProperty("maps").GetArrayLength() == 1 && source.GetProperty("maps")[0].GetString() == "CeladonCity_Gym",
            "Celadon Gym runtime source registry identity differs.");
        var regional = entries.Where(e => e.FullSourceHash == PretCeladonGymRegion.FullSourceHash).ToArray();
        Need(entries.Length == 114 && regional.Length == 1 && regional[0].StableId == "map.celadon-city-gym" &&
            regional[0].Path == "celadon-gym-region/celadon-city-gym/runtime-map.json",
            "Celadon Gym runtime map registry differs.");
    }

    private static void RequireNoLinks(string directory, string relative)
    {
        var path = PretRuntimeMapValidation.SafeFile(directory, relative);
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            PretRuntimeMapValidation.Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0,
                "Celadon Gym runtime symlink rejected.");
    }
}
