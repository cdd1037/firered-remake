using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Fail-closed source authentication for Tower additions. Dependencies are shared with prior packages.</summary>
internal static class PokemonTowerPresentationAssets
{
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string PresentationLockSha256 = "9023708d49b8f755ff104cb7d59e2e7527b4c3d4db60ce123bc6feddca0519b3";
    internal const string PresentationManifestSha256 = "2629efa8a9b8cb8eedb0c9b388025ed169452969f800aa9c580dc6198a7570c4";
    internal const string ArtLockSha256 = "765c7c322e28ab46facb80b23f4048802c33a6774822fb7765c76b118e6967b2";
    internal const string ArtManifestSha256 = "1d2d211a98b9c40dcc8d553d3d4234943cd14155769cab2417c03431bb3f6bec";
    internal sealed record Package(JsonElement Manifest, ImmutableDictionary<string, byte[]> Files);
    private static byte[] Read(string[] roots, string relative, string sha256, int expected)
    {
        if (Path.IsPathRooted(relative) || relative.Contains('\\') || relative.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Tower presentation path.");
        foreach (string root in roots)
        {
            var file = new FileInfo(Path.Combine(root, relative));
            if (file.LinkTarget is not null) throw new InvalidDataException("Tower presentation symlink rejected.");
            for (var parent = file.Directory; parent is not null; parent = parent.Parent)
                if (parent.LinkTarget is not null) throw new InvalidDataException("Tower presentation directory symlink rejected.");
            if (!file.Exists) continue;
            if (file.Length != expected || file.Length is <= 0 or > 3000000) throw new InvalidDataException("Tower presentation size differs: " + relative);
            byte[] bytes = File.ReadAllBytes(file.FullName);
            if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != sha256) throw new InvalidDataException("Tower presentation bytes differ: " + relative);
            return bytes;
        }
        throw new FileNotFoundException("Tower presentation dependency missing.", relative);
    }
    // Optional dependency roots are only for isolated verification before the files are landed together.
    internal static Package Load(string root, bool art = false, params string[] dependencyRoots)
    {
        string[] roots = [root, .. dependencyRoots];
        string stem = art ? "pokemon-tower-art" : "pokemon-tower-presentation";
        string lockSha = art ? ArtLockSha256 : PresentationLockSha256;
        using var locked = JsonDocument.Parse(Read([root], "scripts/" + stem + ".lock.json", lockSha, art ? 8820 : 11875));
        using var manifest = JsonDocument.Parse(Read([root], "content/generated/" + stem + "/manifest.json", art ? ArtManifestSha256 : PresentationManifestSha256, art ? 20659 : 2655));
        var l = locked.RootElement; var m = manifest.RootElement;
        if (l.GetProperty("schemaVersion").GetInt32() != 1 || l.GetProperty("repository").GetString() != "pret/pokefirered" ||
            l.GetProperty("commit").GetString() != SourceCommit || m.GetProperty("schemaVersion").GetInt32() != 1 ||
            m.GetProperty("sourceCommit").GetString() != SourceCommit || m.GetProperty("profile").GetString() != "firered-" + stem + "-v1" ||
            m.GetProperty("lockSha256").GetString() != lockSha || l.GetProperty("files").GetArrayLength() != (art ? 25 : 33) ||
            l.GetProperty("inputs").GetArrayLength() != (art ? 2 : 6)) throw new InvalidDataException("Tower presentation provenance differs.");
        foreach (var row in l.GetProperty("files").EnumerateArray())
            Read(roots, row.GetProperty("localPath").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        var files = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        foreach (var row in l.GetProperty("inputs").EnumerateArray())
        {
            string relative = row.GetProperty("path").GetString()!;
            var data = Read(roots, relative, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
            if (relative.EndsWith(".png", StringComparison.Ordinal)) files.Add(relative, data);
        }
        foreach (var row in m.GetProperty("outputs").EnumerateArray())
        {
            string relative = row.GetProperty("path").GetString()!;
            files.Add(relative, Read([root], "content/generated/" + stem + "/" + relative,
                row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32()));
        }
        if (m.GetProperty("outputs").GetArrayLength() != (art ? 26 : 2) || files.Count != (art ? 26 : 5))
            throw new InvalidDataException("Tower presentation output scope differs.");
        if (art && (m.GetProperty("sprites").GetArrayLength() != 8 || m.GetProperty("icons").GetArrayLength() != 2 ||
            m.GetProperty("footprints").GetArrayLength() != 2 || m.GetProperty("portraits").GetArrayLength() != 1))
            throw new InvalidDataException("Tower art catalog differs.");
        return new(m.Clone(), files.ToImmutable());
    }
}
