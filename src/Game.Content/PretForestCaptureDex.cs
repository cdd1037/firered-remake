using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Forest and Route 1 capture art composed with the shared Pokédex display text.</summary>
public static class PretForestCaptureDex
{
    public const string SourceCommit = PretCaptureDex.SourceCommit;
    public const string Profile = "firered-forest-capture-dex-v1";
    public const string ComposedProfile = "firered-forest-capture-dex-composed-v1";
    private const string DirectoryName = "content/generated/sprites/pokemon/forest-capture-dex";
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly int[] SpeciesIds = [10, 11, 13, 14, 25];

    /// <summary>
    /// Validates both page artifacts and existing forest art, then returns seven entries and eight
    /// page assets with canonical display text. Legacy layout, identities and art paths are retained.
    /// All asset readers take the project root, not an art folder.
    /// </summary>
    public static CaptureDexDocument Load(string projectRoot, PretPokedexMenu? pokedexMenu = null,
        PretForestPokemon? forestPokemon = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var dex = pokedexMenu?.ForProjectRoot(project) ?? PretPokedexMenu.Load(project);
        var original = LoadLegacy(project, dex);
        var extension = LoadExtension(project, original, dex, forestPokemon);
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

    /// <summary>Loads five art-bound entries with canonical text without merging the Route 1 entries.</summary>
    public static CaptureDexDocument LoadExtension(string projectRoot, PretPokedexMenu? pokedexMenu = null,
        PretForestPokemon? forestPokemon = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var dex = pokedexMenu?.ForProjectRoot(project) ?? PretPokedexMenu.Load(project);
        return LoadExtension(project, LoadLegacy(project, dex), dex, forestPokemon);
    }

    private static CaptureDexDocument LoadExtension(string project, CaptureDexDocument original,
        PretPokedexMenu dex, PretForestPokemon? forestPokemon)
    {
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
        var document = PretCaptureDex.WithCanonicalText(Parse(Utf8.GetString(outputs["capture-dex.json"])),
            dex, DirectoryName + "/");
        Require(JsonSerializer.Serialize(document.Layout) == JsonSerializer.Serialize(original.Layout),
            "Forest capture dex changed the legacy layout.");
        var family = forestPokemon ?? PretForestPokemon.Load(PretCanonicalPokemonCatalog.Load(project, project), project);
        Require(string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(family.GeneratedDirectory)),
            Path.GetFullPath(Path.Combine(project, "content/generated/sprites/pokemon/forest")),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            "Forest Pokémon art belongs to a different project root.");
        foreach (var entry in document.Entries.Values)
        {
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
        var original = PretCaptureDex.Parse(Utf8.GetString(ReadPinned(project,"content/generated/sprites/pokemon/capture-dex/capture-dex.json")));
        Require(original.Assets.Any(a => a == asset), "Unknown or altered legacy capture dex texture.");
        return PretCaptureDex.ReadTexture(Path.Combine(project, "content/generated/sprites/pokemon/capture-dex"), asset);
    }

    private static CaptureDexDocument LoadLegacy(string projectRoot, PretPokedexMenu dex)
    {
        var original = PretCaptureDex.Load(Utf8.GetString(ReadPinned(projectRoot,"content/generated/sprites/pokemon/capture-dex/capture-dex.json")), dex);
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
        var bytes = File.ReadAllBytes(GeneratedResourceFile.SafePath(root, path));
        return bytes;
    }
}
