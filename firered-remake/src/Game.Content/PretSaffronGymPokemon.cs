using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Authenticated Slowbro80 projection, with no owned-catalog construction or promotion API.</summary>
public static class PretSaffronGymPokemon
{
    public const string Profile = "firered-saffron-gym-pokemon-v1";
    public static SilphPokemonDocument LoadDocument(string root, string? dependencyProjectRoot = null)
    {
        var source = SaffronGymSource.Catalogs(root); var d = source.Parse<SilphPokemonDocument>("pokemon.json");
        SilphSourcePackage.Need(d.SchemaVersion == 1 && d.Species.IsEmpty &&
            d.OpponentSpecies.Select(s => s.SourceSpeciesId).SequenceEqual([80]) && d.SpeciesNames.Length == 1 && d.EncodedNames.Length == 1 &&
            d.Machines.Length == 1 && d.HeldItems.Length == 1 && d.SourceEvolutions.Length == 1 && !d.Limitations.IsDefaultOrEmpty , "Saffron Gym enemy-only projection differs.");
        var slowbro = d.OpponentSpecies[0];         SilphSourcePackage.Need(            d.RequiredMoves.ToHashSet().SetEquals(slowbro.LevelUpMoves.Select(m => m.Move)) ,
            "Saffron Gym canonical Slowbro differs.");
        SilphSourcePackage.Need(d.EncodedNames[0].Bytes[^1] == 255, "Saffron Gym source name terminator differs.");
        return d;
    }
    public static ImmutableArray<PokemonSpeciesDefinition> LoadOpponents(string root, string? dependencyProjectRoot = null) => LoadDocument(root, dependencyProjectRoot).OpponentSpecies;
}
