using System.Buffers.Binary;
using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record PokemonCollectionArtEntry(SpeciesId Species, int SourceSpeciesId,
    PokedexMenuTexture Icon, PokedexMenuTexture Portrait, PokedexMenuTexture Footprint);
public sealed record PokemonCollectionBattleSprite(SpeciesId Species, int SourceSpeciesId, string Side,
    bool Shiny, PokedexMenuTexture Texture, int X, int Y);
public sealed record PokemonCollectionArtDocument(int SchemaVersion, ImmutableArray<PokemonCollectionArtEntry> Entries,
    ImmutableArray<PokemonCollectionBattleSprite> Sprites);

/// <summary>Shared owned-species art additions. Source choices live in import recipes; text uses the shared Dex.</summary>
public sealed class PretPokemonCollectionPresentation
{
    public const string RelativePath = "content/generated/sprites/pokemon/collection/catalog.json";
    public ImmutableArray<PokemonCollectionArtEntry> Entries { get; }
    public ImmutableArray<PokemonCollectionBattleSprite> Sprites { get; }
    public ImmutableDictionary<int, PokedexMenuEntry> DexEntries { get; }
    public ImmutableDictionary<int, CaptureDexEntry> CaptureEntries { get; }
    public ImmutableDictionary<string, byte[]> Textures { get; }

    private PretPokemonCollectionPresentation(ImmutableArray<PokemonCollectionArtEntry> entries,
        ImmutableDictionary<int, PokedexMenuEntry> dexEntries, ImmutableDictionary<string, byte[]> textures,
        ImmutableArray<PokemonCollectionBattleSprite> sprites)
    {
        Entries = entries;
        Sprites = sprites;
        DexEntries = dexEntries;
        Textures = textures;
        CaptureEntries = dexEntries.ToImmutableDictionary(row => row.Key, row =>
        {
            var entry = row.Value;
            return new CaptureDexEntry(entry.SourceSpeciesId, entry.NationalNumber, entry.Name, entry.Category,
                entry.DisplayCategory, entry.HeightDecimeters, entry.WeightHectograms, entry.HeightDisplay,
                entry.WeightDisplay, entry.Description, entry.Footprint!.TexturePath,
                entry.Portrait!.TexturePath, entry.HabitatTitle);
        });
    }

    public static PretPokemonCollectionPresentation Load(string root, GamePokemonCatalog pokemon, PretPokedexMenu dex)
    {
        var owned = pokemon.Catalog;
        var document = ContentJson.ParseStrict<PokemonCollectionArtDocument>(
            File.ReadAllText(SafeFile(root, RelativePath)), ContentJson.CreateOptions());
        if (document.SchemaVersion != 1 || document.Entries.IsDefaultOrEmpty ||
            document.Entries.Any(row => row is null) ||
            document.Entries.Select(row => row.Species).Distinct().Count() != document.Entries.Length ||
            document.Entries.Select(row => row.SourceSpeciesId).Distinct().Count() != document.Entries.Length)
            throw new InvalidDataException("pokemon_collection_art_schema");
        var textures = ImmutableDictionary.CreateBuilder<string, byte[]>(StringComparer.Ordinal);
        var entries = ImmutableDictionary.CreateBuilder<int, PokedexMenuEntry>();
        foreach (var row in document.Entries)
        {
            if (!owned.Species.TryGetValue(row.Species, out var species) || species.SourceSpeciesId != row.SourceSpeciesId ||
                !dex.Entries.TryGetValue(row.SourceSpeciesId, out var metadata))
                throw new InvalidDataException("pokemon_collection_art_species");
            foreach (var (texture, size) in new[] { (row.Icon, 32), (row.Portrait, 64), (row.Footprint, 16) })
            {
                if (texture is null || texture.Width != size || texture.Height != size)
                    throw new InvalidDataException("pokemon_collection_art_dimensions");
                if (!textures.TryGetValue(texture.TexturePath, out var bytes))
                {
                    bytes = File.ReadAllBytes(SafeFile(root, texture.TexturePath));
                    textures.Add(texture.TexturePath, bytes);
                }
                if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
                    !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) ||
                    BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) != size ||
                    BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) != size)
                    throw new InvalidDataException("pokemon_collection_art_png");
            }
            entries.Add(row.SourceSpeciesId, metadata with { Portrait = row.Portrait, Footprint = row.Footprint });
        }
        if (document.Sprites.IsDefault || document.Sprites.Any(row => row is null) ||
            document.Sprites.Select(row => (row.Species, row.Side, row.Shiny)).Distinct().Count() != document.Sprites.Length)
            throw new InvalidDataException("pokemon_collection_battle_schema");
        foreach (var group in document.Sprites.GroupBy(row => row.Species))
        {
            if (group.Count() != 4 || !pokemon.Opponents.Catalog.Species.ContainsKey(group.Key))
                throw new InvalidDataException("pokemon_collection_battle_variants");
            var portrait = document.Entries.SingleOrDefault(row => row.Species == group.Key)?.Portrait;
            if (portrait is not null && group.SingleOrDefault(row => row.Side == "front" && !row.Shiny)?.Texture != portrait)
                throw new InvalidDataException("pokemon_collection_battle_portrait");
            foreach (var row in group)
            {
                if (row.Side is not ("front" or "back") || pokemon.Opponents.Catalog.Species[row.Species].SourceSpeciesId != row.SourceSpeciesId ||
                    row.Texture is null || row.Texture.Width != 64 || row.Texture.Height != 64 ||
                    row.X != (row.Side == "front" ? 144 : 40) || row.Y < -64 || row.Y > 128)
                    throw new InvalidDataException("pokemon_collection_battle_binding");
                var bytes = File.ReadAllBytes(SafeFile(root, row.Texture.TexturePath));
                if (bytes.Length < 33 || !bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
                    !bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8) ||
                    BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)) != 64 ||
                    BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)) != 64)
                    throw new InvalidDataException("pokemon_collection_battle_png");
                textures.TryAdd(row.Texture.TexturePath, bytes);
            }
        }
        return new(document.Entries, entries.ToImmutable(), textures.ToImmutable(), document.Sprites);
    }

    public PokedexMenuPage Present(PokedexMenuPage page) => DexEntries.TryGetValue(page.SourceSpeciesId, out var entry)
        ? page with { Portrait = entry.Portrait, Footprint = page.Caught ? entry.Footprint : null }
        : Sprites.FirstOrDefault(sprite => sprite.SourceSpeciesId == page.SourceSpeciesId && sprite.Side == "front" && !sprite.Shiny) is { } battle
            ? page with { Portrait = battle.Texture } : page;

    private static string SafeFile(string root, string path)
    {
        if (!path.StartsWith("content/generated/", StringComparison.Ordinal))
            throw new InvalidDataException("pokemon_collection_art_path");
        return GeneratedResourceFile.SafePath(root, path);
    }
}
