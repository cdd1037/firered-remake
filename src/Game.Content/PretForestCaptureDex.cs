using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Authenticated forest first-capture pages composed over the unchanged Route 1 document.</summary>
public static class PretForestCaptureDex
{
    public const string SourceCommit = PretCaptureDex.SourceCommit;
    public const string Profile = "firered-forest-capture-dex-v1";
    public const string ComposedProfile = "firered-forest-capture-dex-composed-v1";
    private const string DirectoryName = "content/generated/sprites/pokemon/forest-capture-dex";
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
        var assets = original.Assets.AddRange(extension.Assets);
        Require(entries.Count == 7 && assets.Length == 8 ,
            "Composed forest capture dex scope differs.");
        return extension with
        {
            Entries = entries.ToImmutable(),
            Assets = assets,
            Layout = original.Layout
        };
    }

    /// <summary>Authenticates the five new entries and all dependencies without merging the old entries.</summary>
    public static CaptureDexDocument LoadExtension(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, DirectoryName);
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 ,
            "Forest capture dex manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate forest capture dex output.");
        }
        var document = Parse(Utf8.GetString(outputs["capture-dex.json"]));
        var original = LoadLegacy(project);
        Require(JsonSerializer.Serialize(document.Layout) == JsonSerializer.Serialize(original.Layout),
            "Forest capture dex changed the legacy layout.");
        var family = PretForestPokemon.Load(PretCanonicalPokemonCatalog.Load(project, project), project);
        var dex = PretPokedexMenu.Load(project);
        foreach (var entry in document.Entries.Values)
        {
            Require(dex.Entries[entry.SourceSpeciesId].Name == entry.Name && entry.NationalNumber == entry.SourceSpeciesId,
                "Forest capture dex species identity differs from the existing catalog.");
            var sprite = family.Sprites.Single(s => s.SourceSpeciesId == entry.SourceSpeciesId && s.Side == "front" && s.PaletteVariant == "normal");
            Require(entry.FrontSpritePath == "sprites/pokemon/forest/" + sprite.TexturePath,
                "Forest capture dex must reuse existing normal front art.");
            var asset = document.Assets.Single(a => a.TexturePath == entry.FootprintPath);
            Require(asset.Width == 16 && asset.Height == 16 && asset.X == original.Layout.FootprintX && asset.Y == original.Layout.FootprintY && (outputs[asset.Id + ".png"]).Length > 0, "Forest capture dex footprint identity/pixels differ.");
        }
        return document;
    }

    /// <summary>Read one authenticated merged page asset. Unknown IDs, forged metadata and symlinks reject.</summary>
    public static byte[] ReadTexture(string projectRoot, CaptureDexAsset asset)
    {
        var project = Path.GetFullPath(projectRoot);
        var extension = Parse(Utf8.GetString(ReadPinned(project,DirectoryName + "/capture-dex.json")));
        if (extension.Assets.FirstOrDefault(a => a.Id == asset.Id) is { } expected)
        {
            Require(asset == expected, "Forest capture dex texture metadata differs.");
            return ReadPinned(Path.Combine(project, DirectoryName),asset.TexturePath);
        }
        var original = PretCaptureDex.Load(Utf8.GetString(ReadPinned(project,"content/generated/sprites/pokemon/capture-dex/capture-dex.json")));
        Require(original.Assets.Any(a => a == asset), "Unknown or altered legacy capture dex texture.");
        return PretCaptureDex.ReadTexture(Path.Combine(project, "content/generated/sprites/pokemon/capture-dex"), asset);
    }

    private static CaptureDexDocument LoadLegacy(string projectRoot)
    {
        var original = PretCaptureDex.Load(Utf8.GetString(ReadPinned(projectRoot,"content/generated/sprites/pokemon/capture-dex/capture-dex.json")));
        foreach (var asset in original.Assets) _ = PretCaptureDex.ReadTexture(Path.Combine(projectRoot, "content/generated/sprites/pokemon/capture-dex"), asset);
        return original;
    }

    private static CaptureDexDocument Parse(string json)
    {
        var document = ContentJson.ParseStrict<CaptureDexDocument>(json, ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 && document.Assets.Length == 5 &&
            document.Entries.Keys.Order().SequenceEqual(SpeciesIds),
            "Forest capture dex document scope differs.");
        return document;
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
    private static byte[] ReadPinned(string root,string path)
    {
        var bytes = File.ReadAllBytes(SafeFile(root, path));
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
