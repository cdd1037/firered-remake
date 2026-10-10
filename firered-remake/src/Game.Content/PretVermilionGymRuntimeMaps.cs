using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>81 pinned maps; the prior 80 compact payloads, entries and source registry remain exact.</summary>
public static class PretVermilionGymRuntimeMaps
{
    public const string DirectoryName = "content/generated/runtime-vermilion-gym-maps";
    public const string ManifestSha256 = "b1c90f99af973dfeea896b6fea0ffeec34116a0126dfa72334847735acb17bc0";
    public const string ConverterSha256 = "d23e157602855a1fec5a344f91898d1fbf339db3f434b3d2d36f5e57f1e2ca22";
    public const string SourceLockSha256 = PretVermilionGymRegion.LockSha256;

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), DirectoryName), ManifestSha256);

    internal static void ValidatePackage(string directory, ImmutableArray<PretRuntimeMapEntry> entries)
    {
        // Retain the prior ship lock, source identity and symlink requirements.
        PretSsAnneRuntimeMaps.ValidatePackage(directory, entries);
        void Need(bool value, string message) => PretRuntimeMapValidation.Require(value, message);
        var path = PretRuntimeMapValidation.SafeFile(directory, "vermilion-gym-source-lock.json");
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            Need((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Vermilion Gym runtime source lock symlink rejected.");
        var bytes = PretRuntimeMapValidation.ReadBounded(path, 256 * 1024);
        Need(PretRuntimeMapValidation.Hash(bytes) == SourceLockSha256, "Vermilion Gym runtime source lock differs.");
        using var document = JsonDocument.Parse(bytes);
        var source = document.RootElement;
        Need(source.GetProperty("commit").GetString() == PretWorldMap.SourceCommit &&
            source.GetProperty("profile").GetString() == PretVermilionGymRegion.Profile &&
            source.GetProperty("files").GetArrayLength() == 121 && source.GetProperty("maps").GetArrayLength() == 1 &&
            source.GetProperty("maps")[0].GetString() == "VermilionCity_Gym", "Vermilion Gym runtime source registry identity differs.");
        var gym = entries.Where(e => e.FullSourceHash == PretVermilionGymRegion.FullSourceHash).ToArray();
        Need(entries.Length == 81 && gym.Length == 1 && gym[0].StableId == "map.vermilion-city-gym" &&
            gym[0].Path == "vermilion-gym-region/vermilion-city-gym/runtime-map.json", "Vermilion Gym runtime map registry differs.");
    }
}
