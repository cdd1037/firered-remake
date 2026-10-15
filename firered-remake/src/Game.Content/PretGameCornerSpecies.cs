using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Small authenticated ownership delta over shared canonical source mechanics.</summary>
public static class PretGameCornerSpecies
{

    private static JsonElement Read(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/game-corner-species", file));
        using var document = JsonDocument.Parse(bytes);
        return document.RootElement.Clone();
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read(root,"pokemon.json");
        if (                        !document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(GameCornerOwnedPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) ||
            !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(GameCornerOpponentPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) ||
            document.GetProperty("trainerOnlySpeciesAdded").GetArrayLength() != 0 ||
            document.GetProperty("movesAdded").GetArrayLength() != 0 || document.GetProperty("evolutions").GetArrayLength() != 2 ||
            document.GetProperty("excludedEvolutions").GetArrayLength() != 1)
            throw new InvalidDataException("game-corner-species_pokemon_scope");
        return document;
    }

    public static GameCornerOwnedPokemonCatalog LoadOwned(string root, StoneEvolutionOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in GameCornerOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, GameCornerOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return GameCornerOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }

    public static GameCornerOpponentPokemonCatalog LoadOpponents(string root, StoneEvolutionOpponentPokemonCatalog predecessor,
        GameCornerOwnedPokemonCatalog owned)
    { return GameCornerOpponentPokemonCatalog.Authenticate(predecessor, owned); }
}
