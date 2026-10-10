using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>83 pinned maps; every prior Gym payload, entry and source registry remains exact.</summary>
public static class PretCityServicesRuntimeMaps
{
    public const string DirectoryName = "content/generated/runtime-city-services-maps";
    public const string ManifestSha256 = "bc889d0bfa494a142902951dedff141e43441517059152b5ea47f0da19f4c92e";
    public const string ConverterSha256 = "0e31d661caa9e862dd73aa3d94dc042dbb7b800894a71f178d45b3777abca63e";
    public const string SourceLockSha256 = PretCityServices.LockSha256;

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), DirectoryName), ManifestSha256);

    internal static void ValidatePackage(string directory, ImmutableArray<PretRuntimeMapEntry> entries)
    {
        void Need(bool value, string message) => PretRuntimeMapValidation.Require(value, message);
        // Run the historical 81-map contract unchanged against precisely its prior entries.
        PretVermilionGymRuntimeMaps.ValidatePackage(directory,
            entries.Where(e => e.FullSourceHash != PretCityServices.FullSourceHash).ToImmutableArray());
        foreach (var relative in entries.Select(e => e.Path).Append("city-services-source-lock.json"))
        {
            var path = PretRuntimeMapValidation.SafeFile(directory, relative);
            for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
                Need((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "City services runtime symlink rejected.");
        }
        var bytes = PretRuntimeMapValidation.ReadBounded(PretRuntimeMapValidation.SafeFile(directory, "city-services-source-lock.json"), 256 * 1024);
        Need(PretRuntimeMapValidation.Hash(bytes) == SourceLockSha256, "City services runtime source lock differs.");
        using var document = JsonDocument.Parse(bytes);
        var source = document.RootElement;
        Need(source.GetProperty("commit").GetString() == PretWorldMap.SourceCommit &&
            source.GetProperty("profile").GetString() == PretCityServices.Profile &&
            source.GetProperty("files").GetArrayLength() == 104 && source.GetProperty("maps").GetArrayLength() == 2 &&
            source.GetProperty("maps")[0].GetString() == "VermilionCity_PokemonFanClub" &&
            source.GetProperty("maps")[1].GetString() == "VermilionCity_House1", "City services runtime source registry identity differs.");
        var city = entries.Where(e => e.FullSourceHash == PretCityServices.FullSourceHash).ToArray();
        Need(entries.Length == 83 && city.Length == 2 &&
            city.SingleOrDefault(e => e.StableId == "map.vermilion-city-house-1")?.Path == "city-services/vermilion-city-house1/runtime-map.json" &&
            city.SingleOrDefault(e => e.StableId == "map.vermilion-city-pokemon-fan-club")?.Path == "city-services/vermilion-city-pokemon-fan-club/runtime-map.json",
            "City services runtime map registry differs.");
    }
}
