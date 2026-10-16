using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record PewterPokemonEncodedName(string Symbol, string Name, ImmutableArray<int> Bytes,
    PokemonSourceRef Source, PokemonSourceRef EncodingSource);

/// <summary>Pewter Pokémon presentation, bound to shared species definitions and independent of gameplay admission.</summary>
public sealed class PretPewterPokemon
{
    public const string Profile = "firered-pewter-pokemon-v1";
    public const string SourceCommit = PokemonFactory.SourceCommit;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public PokemonCatalog Catalog { get; }
    public ImmutableArray<ForestPokemonSprite> Sprites { get; }
    public JsonElement BattleArt { get; }
    public string GeneratedDirectory { get; }

    private PretPewterPokemon(string directory, PokemonCatalog catalog,
        ImmutableArray<ForestPokemonSprite> sprites, JsonElement art)
    {
        GeneratedDirectory = directory; Catalog = catalog;
        Sprites = sprites; BattleArt = art;
    }

    /// <summary>Loads presentation assets against the shared species definitions.</summary>
    public static PretPewterPokemon Load(PretCanonicalPokemonCatalog canonical, string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/sprites/pokemon/pewter");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(GeneratedResourceFile.SafePath(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 && m.GetProperty("composedSpeciesCount").GetInt32() == 29, "Pewter Pokemon manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if (path == "catalog.json") continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate Pewter Pokemon output.");
        }
        var catalog = canonical.Catalog;
        using var art = JsonDocument.Parse(outputs["battle-art.json"]);
        var sprites = ContentJson.ParseStrict<ImmutableArray<ForestPokemonSprite>>(art.RootElement.GetProperty("sprites").GetRawText(), ContentJson.CreateOptions());
        Require(sprites.Length == 28 && sprites.Select(s => (s.SpeciesId, s.Side, s.PaletteVariant)).Distinct().Count() == 28,
            "Pewter Pokemon sprite inventory differs.");
        foreach (var sprite in sprites)
        {
            Require(catalog.Species[new(sprite.SpeciesId)].SourceSpeciesId == sprite.SourceSpeciesId &&
                sprite.Width == 64 && sprite.Height == 64 && sprite.FrameCount == 1 && sprite.SourceFrameIndex == 0 &&
                sprite.TransparentIndex == 0 && !sprite.FlipH && sprite.PaletteRgb555.Length == 16 &&
                (outputs[sprite.TexturePath]).Length > 0, "Pewter Pokemon sprite identity/pixels differ.");
        }
        return new(directory, catalog, sprites, art.RootElement.Clone());
    }

    public ForestPokemonSprite Sprite(SpeciesId species, string side, bool shiny = false) =>
        Sprites.Single(s => s.SpeciesId == species.Value && s.Side == side && s.PaletteVariant == (shiny ? "shiny" : "normal"));
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
