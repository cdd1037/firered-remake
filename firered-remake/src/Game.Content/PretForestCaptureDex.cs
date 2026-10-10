using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Authenticated forest first-capture pages composed over the unchanged Route 1 document.</summary>
public static class PretForestCaptureDex
{
    public const string SourceCommit = PretCaptureDex.SourceCommit;
    public const string Profile = "firered-forest-capture-dex-v1";
    public const string ComposedProfile = "firered-forest-capture-dex-composed-v1";
    public const string LockSha256 = "5650ddf48839ac26aba3c8c87b17f04f9ee35d7df50bbcdd89fc3ffdd57b71bb";
    public const string ManifestSha256 = "c802b1f36e4e8d851e8082116ed2c4e47099b325f90725066c61a5988bee58d3";
    public const string GeneratedSha256 = "1bbd7697c985340f787836d626b4b6c1ff8b71885ba03c3e86c7c28001bddcb1";
    public const string RawManifestSha256 = "9ddc313ec093d48cdffefd58dc7e50cecc994ac8e06d6202f6e4b3258515c904";
    public const string FullSourceHash = "7048e311d148f350d6cb03a6c5f367c8892ca68ad1927a0a0f9777d34d82d130";
    private const string DirectoryName = "content/generated/forest-capture-dex";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly int[] SpeciesIds = [10, 11, 13, 14, 25];

    /// <summary>
    /// Authenticates legacy and extension documents, all new source bytes and pixels, and existing
    /// forest catalog/art, then returns seven entries and eight page assets. The legacy layout and
    /// two entries are retained verbatim. All asset readers take the project root, not an art folder.
    /// </summary>
    public static CaptureDexDocument Load(string projectRoot)
    {
        var original = LoadLegacy(projectRoot);
        var extension = LoadExtension(projectRoot);
        var entries = original.Entries.ToBuilder();
        foreach (var entry in extension.Entries) entries.Add(entry.Key, entry.Value);
        var inputs = original.Inputs.ToDictionary(i => i.Path, StringComparer.Ordinal);
        foreach (var input in extension.Inputs)
        {
            Require(!inputs.TryGetValue(input.Path, out var previous) || previous == input,
                "Legacy and forest capture dex source inputs disagree.");
            inputs[input.Path] = input;
        }
        var inventory = inputs.Values.OrderBy(i => i.Path, StringComparer.Ordinal).ToImmutableArray();
        var assets = original.Assets.AddRange(extension.Assets);
        Require(entries.Count == 7 && assets.Length == 8 && inventory.Length == 43,
            "Composed forest capture dex scope differs.");
        return new(1, ComposedProfile, SourceCommit, SourceSetHash(inventory), inventory,
            entries.ToImmutable(), assets, original.Layout, original.Limitations.AddRange(extension.Limitations));
    }

    /// <summary>Authenticates the five new entries and all dependencies without merging the old entries.</summary>
    public static CaptureDexDocument LoadExtension(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var raw = Path.Combine(project, "content/raw/forest-capture-dex");
        var directory = Path.Combine(project, DirectoryName);
        _ = ReadPinned(raw, "manifest.json", RawManifestSha256);
        using var sourceLock = JsonDocument.Parse(ReadPinned(project, "scripts/forest-capture-dex.lock.json", LockSha256));
        using var manifest = JsonDocument.Parse(ReadPinned(directory, "manifest.json", ManifestSha256));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("profile").GetString() == Profile &&
            m.GetProperty("sourceCommit").GetString() == SourceCommit && m.GetProperty("tree").GetString() == PretForestPokemon.SourceTree &&
            m.GetProperty("fullSourceHash").GetString() == FullSourceHash && m.GetProperty("inputCount").GetInt32() == 23 &&
            m.GetProperty("outputs").GetArrayLength() == 11 && !m.GetProperty("runtimeShadersUsed").GetBoolean() &&
            m.GetProperty("legacyDocumentSha256").GetString() == PretCaptureDex.GeneratedSha256 &&
            m.GetProperty("forestPokemonManifestSha256").GetString() == PretForestPokemon.ManifestSha256,
            "Forest capture dex manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory, path, output.GetProperty("sha256").GetString()!,
                output.GetProperty("byteLength").GetInt32())), "Duplicate forest capture dex output.");
        }
        Require(Hash(outputs["capture-dex.json"]) == GeneratedSha256, "Forest capture dex document differs.");
        var document = Parse(Utf8.GetString(outputs["capture-dex.json"]));
        using var json = JsonDocument.Parse(outputs["capture-dex.json"]);
        Require(JsonElement.DeepEquals(json.RootElement.GetProperty("inputs"), sourceLock.RootElement.GetProperty("files")),
            "Forest capture dex source inventory differs from its lock.");
        foreach (var input in document.Inputs)
        {
            var bytes = ReadPinned(raw, input.Path, input.Sha256, input.ByteLength);
            Require(Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) == input.GitBlobSha1,
                "Forest capture dex source Git blob differs: " + input.Path);
        }
        Require(SourceSetHash(document.Inputs) == FullSourceHash, "Forest capture dex source-set hash differs.");
        var original = LoadLegacy(project);
        Require(JsonSerializer.Serialize(document.Layout) == JsonSerializer.Serialize(original.Layout),
            "Forest capture dex changed the legacy layout.");
        var family = PretForestPokemon.Load(project);
        foreach (var entry in document.Entries.Values)
        {
            Require(family.SpeciesNames[entry.SourceSpeciesId] == entry.Name && entry.NationalNumber == entry.SourceSpeciesId,
                "Forest capture dex species identity differs from the existing catalog.");
            var sprite = family.Sprites.Single(s => s.SourceSpeciesId == entry.SourceSpeciesId && s.Side == "front" && s.PaletteVariant == "normal");
            Require(entry.FrontSpritePath == "forest-pokemon/" + sprite.TexturePath,
                "Forest capture dex must reuse existing normal front art.");
            var asset = document.Assets.Single(a => a.TexturePath == entry.FootprintPath);
            Require(asset.Width == 16 && asset.Height == 16 && asset.X == original.Layout.FootprintX && asset.Y == original.Layout.FootprintY &&
                Hash(outputs[asset.TexturePath]) == asset.Sha256 && outputs[asset.Id + ".rgba"].Length == 16 * 16 * 4 &&
                Hash(outputs[asset.Id + ".rgba"]) == asset.RgbaSha256, "Forest capture dex footprint identity/pixels differ.");
        }
        return document;
    }

    /// <summary>Read one authenticated merged page asset. Unknown IDs, forged metadata and symlinks reject.</summary>
    public static byte[] ReadTexture(string projectRoot, CaptureDexAsset asset)
    {
        var project = Path.GetFullPath(projectRoot);
        var extension = Parse(Utf8.GetString(ReadPinned(project, DirectoryName + "/capture-dex.json", GeneratedSha256)));
        if (extension.Assets.FirstOrDefault(a => a.Id == asset.Id) is { } expected)
        {
            Require(asset == expected, "Forest capture dex texture metadata differs.");
            return ReadPinned(Path.Combine(project, DirectoryName), asset.TexturePath, asset.Sha256);
        }
        var original = PretCaptureDex.Load(Utf8.GetString(ReadPinned(project, "content/generated/capture-dex/capture-dex.json", PretCaptureDex.GeneratedSha256)));
        Require(original.Assets.Any(a => a == asset), "Unknown or altered legacy capture dex texture.");
        return PretCaptureDex.ReadTexture(Path.Combine(project, "content/generated/capture-dex"), asset);
    }

    private static CaptureDexDocument LoadLegacy(string projectRoot)
    {
        var original = PretCaptureDex.Load(Utf8.GetString(ReadPinned(projectRoot, "content/generated/capture-dex/capture-dex.json", PretCaptureDex.GeneratedSha256)));
        foreach (var asset in original.Assets) _ = PretCaptureDex.ReadTexture(Path.Combine(projectRoot, "content/generated/capture-dex"), asset);
        return original;
    }

    private static CaptureDexDocument Parse(string json)
    {
        Require(Hash(Utf8.GetBytes(json)) == GeneratedSha256, "Forest capture dex catalog hash differs.");
        var document = ContentJson.ParseStrict<CaptureDexDocument>(json, ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.Profile == Profile && document.SourceCommit == SourceCommit &&
            document.FullSourceHash == FullSourceHash && document.Inputs.Length == 23 && document.Assets.Length == 5 &&
            document.Entries.Keys.Order().SequenceEqual(SpeciesIds) && !document.Limitations.IsDefaultOrEmpty,
            "Forest capture dex document scope differs.");
        return document;
    }

    private static string SourceSetHash(IEnumerable<PretPokemonInput> inputs) => Hash(Utf8.GetBytes(string.Concat(
        inputs.OrderBy(i => i.Path, StringComparer.Ordinal).Select(i => i.Path + "\0" + i.Sha256 + "\n"))));
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static byte[] ReadPinned(string root, string path, string hash, int? length = null)
    {
        var bytes = File.ReadAllBytes(SafeFile(root, path));
        Require(Hash(bytes) == hash && (length is null || bytes.Length == length), "Pinned forest capture dex bytes differ: " + path);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(s => s is "" or "." or ".."))
            throw new InvalidDataException("Unsafe forest capture dex path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Forest capture dex directory symlink rejected.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Forest capture dex file symlink rejected: " + path);
        }
        return result;
    }
}
