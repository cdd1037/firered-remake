using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record PretRoute1SpeciesName(SpeciesId Id, string Name, PokemonSourceRef Source);
public sealed record PretRoute1MoveName(MoveId Id, string Name);
public sealed record PretRoute1PokemonDocument(int SchemaVersion, ImmutableArray<PokemonSpeciesDefinition> Species, ImmutableArray<PokemonMoveDefinition> Moves,
    ImmutableArray<PretRoute1SpeciesName> SpeciesNames, ImmutableArray<PretRoute1MoveName> MoveNames);
public sealed record PretRoute1PokemonContent(PokemonCatalog Catalog,
    ImmutableSortedDictionary<int, string> SpeciesNames, ImmutableDictionary<MoveId, string> MoveNames);

/// <summary>
/// Authenticates the separate fourteen-species Route 1 artifact. No IO, instance creation, battle
/// implementation, or change to the reviewed nine-starter artifact. Display names do not determine identity.
/// </summary>
public static class PretRoute1PokemonCatalog
{
    public const string Profile = "firered-route1-pokemon-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    public static PokemonCatalog Load(PretCanonicalPokemonCatalog canonical, string generatedJson) => LoadContent(canonical, generatedJson).Catalog;

    public static PretRoute1PokemonContent LoadContent(PretCanonicalPokemonCatalog canonical, string generatedJson)
    {
        var options = canonical.ReferenceOptions();
        options.Converters.Add(new ExactEnumConverter<GrowthRate>());
        var doc = ContentJson.ParseStrict<PretRoute1PokemonDocument>(generatedJson, options);
        if (doc.SchemaVersion != 1 )
            throw Error("profile", "Unknown Route 1 Pokemon profile or source commit.");
        if (doc.Species.IsDefaultOrEmpty ||
            doc.Moves.IsDefaultOrEmpty || doc.SpeciesNames.IsDefaultOrEmpty || doc.MoveNames.IsDefaultOrEmpty)
            throw Error("missing_data", "Explicit nonempty source, species, move and name arrays are required.");

        var speciesIds = new HashSet<SpeciesId>();
        foreach (var species in doc.Species)
        {
            if (species is null || species.LevelUpMoves.IsDefaultOrEmpty || species.Evolutions.IsDefault ||
                !ExpectedSpecies.TryGetValue(species.Id, out int sourceId) || species.SourceSpeciesId != sourceId ||
                !speciesIds.Add(species.Id))
                throw Error("species", "Expected each of the fourteen explicitly pinned species exactly once.");
            int previous = 0;
            foreach (var entry in species.LevelUpMoves)
            {
                if (entry is null || entry.Level is < 1 or > 100 || entry.Level < previous)
                    throw Error("learnset", "Expected ordered level-up entries.");
                previous = entry.Level;
            }
            foreach (var evolution in species.Evolutions)
            {
                if (evolution is null || evolution.Level is < 1 or > 100 || !ExpectedSpecies.ContainsKey(evolution.Target))
                    throw Error("evolution", "Invalid bounded level-evolution reference.");
            }
        }
        if (speciesIds.Count != ExpectedSpecies.Count)
            throw Error("species_missing", "The complete fourteen-species catalog is required.");

        var moveIds = new HashSet<MoveId>();
        foreach (var move in doc.Moves)
        {
            if (move is null || move.BattleEffectImplemented || move.Flags.IsDefault || !moveIds.Add(move.Id))
                throw Error("move", "Invalid/duplicate move or unsupported battle-effect implementation claim.");
        }
        var referencedMoves = doc.Species.SelectMany(x => x.LevelUpMoves).Select(x => x.Move).ToHashSet();
        if (!referencedMoves.SetEquals(moveIds))
            throw Error("move_references", "Move definitions must exactly cover the complete bounded learnsets.");

        var speciesNames = ImmutableSortedDictionary.CreateBuilder<int, string>();
        foreach (var name in doc.SpeciesNames)
        {
            if (name is null || !ExpectedSpecies.TryGetValue(name.Id, out int sourceId) ||
                speciesNames.ContainsKey(sourceId) || string.IsNullOrWhiteSpace(name.Name))
                throw Error("species_name", "Invalid or duplicate species display name.");
            if (name.Id.Value != "pret:" + name.Source.Symbol)
                throw Error("species_name_symbol", "Name provenance must match its species.");
            speciesNames.Add(sourceId, name.Name);
        }
        var moveNames = ImmutableDictionary.CreateBuilder<MoveId, string>();
        foreach (var name in doc.MoveNames)
        {
            if (name is null || !moveIds.Contains(name.Id) || moveNames.ContainsKey(name.Id) || string.IsNullOrWhiteSpace(name.Name))
                throw Error("move_name", "Invalid or duplicate move display name.");
            moveNames.Add(name.Id, name.Name);
        }
        if (speciesNames.Count != speciesIds.Count || moveNames.Count != moveIds.Count)
            throw Error("names_missing", "Every imported species and move requires a nonblank display name.");
        var catalog = new PokemonCatalog(canonical.Catalog.SourceCommit, doc.Species.ToImmutableDictionary(x => x.Id),
            doc.Moves.ToImmutableDictionary(x => x.Id));
        PokemonFactory.ValidateDefinitions(catalog);
        return new(catalog, speciesNames.ToImmutable(), moveNames.ToImmutable());
    }

    private static ContentValidationException Error(string code, string message) =>
        ContentJson.Error("route1_pokemon_" + code, "$", message);

    // Explicit source IDs: the five Route 1-family species do not follow starter ID 9.
    private static readonly IReadOnlyDictionary<SpeciesId, int> ExpectedSpecies =
        new Dictionary<SpeciesId, int>
        {
            [new("pret:SPECIES_BULBASAUR")] = 1,[new("pret:SPECIES_IVYSAUR")] = 2,[new("pret:SPECIES_VENUSAUR")] = 3,[new("pret:SPECIES_CHARMANDER")] = 4,[new("pret:SPECIES_CHARMELEON")] = 5,[new("pret:SPECIES_CHARIZARD")] = 6,[new("pret:SPECIES_SQUIRTLE")] = 7,[new("pret:SPECIES_WARTORTLE")] = 8,[new("pret:SPECIES_BLASTOISE")] = 9,[new("pret:SPECIES_PIDGEY")] = 16,[new("pret:SPECIES_PIDGEOTTO")] = 17,[new("pret:SPECIES_PIDGEOT")] = 18,[new("pret:SPECIES_RATTATA")] = 19,[new("pret:SPECIES_RATICATE")] = 20
        };

}
