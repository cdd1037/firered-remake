using System.Collections.Immutable;
using System.Text.Json;

namespace Game.Content;

/// <summary>The independently pinned 80-map package. Historical 54-map payloads and base registry remain exact.</summary>
public static class PretSsAnneRuntimeMaps
{
    public const string DirectoryName = "content/generated/runtime-ssanne-maps";
    public const string ManifestSha256 = "9a27bcfeef98d5a9adb160509a254a08a0add4f1aff3f9800d45f68c574007ac";
    public const string ConverterSha256 = "cb72a9f40a8cab66f67a6f17778fa857c847c32747a5ef1ddae675e3596e639e";
    public const string SourceLockSha256 = PretSsAnneRegion.LockSha256;

    public static PretRuntimeMapCatalog Load(string projectRoot) =>
        PretRuntimeMapCatalog.Load(Path.Combine(Path.GetFullPath(projectRoot), DirectoryName), ManifestSha256);

    internal static void ValidatePackage(string directory, ImmutableArray<PretRuntimeMapEntry> entries)
    {
        void Need(bool value, string message) => PretRuntimeMapValidation.Require(value, message);
        foreach (var relative in entries.Select(e => e.Path).Concat(["manifest.json", "source-registry.json", "ssanne-source-lock.json"]))
        {
            var path = PretRuntimeMapValidation.SafeFile(directory, relative);
            for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
                Need((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "S.S. Anne runtime symlink rejected.");
        }
        var bytes = PretRuntimeMapValidation.ReadBounded(Path.Combine(directory, "ssanne-source-lock.json"), 256 * 1024);
        Need(PretRuntimeMapValidation.Hash(bytes) == SourceLockSha256, "S.S. Anne runtime source lock differs.");
        using var document = JsonDocument.Parse(bytes);
        var source = document.RootElement;
        Need(source.GetProperty("commit").GetString() == PretWorldMap.SourceCommit &&
            source.GetProperty("profile").GetString() == PretSsAnneRegion.Profile &&
            source.GetProperty("files").GetArrayLength() == 288, "S.S. Anne runtime source registry identity differs.");
        var expectedMaps = source.GetProperty("maps").EnumerateArray().Select(n => n.GetString()!).ToHashSet(StringComparer.Ordinal);
        var shipMaps = entries.Where(e => e.FullSourceHash == PretSsAnneRegion.FullSourceHash).ToArray();
        Need(shipMaps.Length == 26 && expectedMaps.Count == 26, "S.S. Anne runtime source map registry differs.");
    }
}
