using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Dratini's ordinary level-evolution family and Porygon over the exact 170-species owner.</summary>
public sealed class GameCornerOwnedPokemonCatalog
{
    public const string Profile = "firered-game-corner-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_DRATINI"), new("pret:SPECIES_DRAGONAIR"), new("pret:SPECIES_DRAGONITE"), new("pret:SPECIES_PORYGON")];
    private static readonly int[] Numbers = [147, 148, 149, 137];
    private static readonly ConditionalWeakTable<PokemonCatalog, GameCornerOwnedPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<StoneEvolutionOwnedPokemonCatalog, ImmutableDictionary<SpeciesId, PokemonSpeciesDefinition>> Definitions = new();
    public StoneEvolutionOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;

    private GameCornerOwnedPokemonCatalog(StoneEvolutionOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }

    public static GameCornerOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;

    public static GameCornerOwnedPokemonCatalog Authenticate(StoneEvolutionOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(StoneEvolutionOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 170 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 174 ||
            predecessor.Catalog.Moves.Count != 354 || !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(pair => !candidate.Species.TryGetValue(pair.Key, out var value) || !ReferenceEquals(pair.Value, value)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("game_corner_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!ReferenceEquals(candidate.Species[id], ExpectedSpecies(predecessor, id)))
                throw new ArgumentException("game_corner_owned_source");
        var proof = new GameCornerOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof);
        OwnedBattlePokemonCatalog.RegisterSuccessor(proof.Catalog, predecessor.SharedBattle, Profile);
        return proof;
    }

    public static PokemonSpeciesDefinition ExpectedSpecies(StoneEvolutionOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(StoneEvolutionOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || !AdditionalSpecies.Contains(id))
            throw new ArgumentException("game_corner_owned_species");
        return Definitions.GetValue(predecessor, CreateDefinitions)[id];
    }

    private static ImmutableDictionary<SpeciesId, PokemonSpeciesDefinition> CreateDefinitions(StoneEvolutionOwnedPokemonCatalog predecessor)
    {
        var definitions = ImmutableDictionary.CreateBuilder<SpeciesId, PokemonSpeciesDefinition>();
        for (int index = 0; index < AdditionalSpecies.Length; index++)
        {
            var id = AdditionalSpecies[index];
            var source = predecessor.Mechanics.Catalog.Species[id];
            var raw = predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions;
            if (source.SourceSpeciesId != Numbers[index] || source.Evolutions.IsDefault || !source.Evolutions.IsEmpty || raw.IsDefault)
                throw new ArgumentException("game_corner_owned_canonical_source");
            if (index < 2)
            {
                int level = index == 0 ? 30 : 55;
                var target = AdditionalSpecies[index + 1];
                if (raw.Length != 1 || raw[0].Method != "EVO_LEVEL" || raw[0].SourceMethodId != 4 ||
                    raw[0].Parameter != level.ToString(System.Globalization.CultureInfo.InvariantCulture) || raw[0].ParameterValue != level || raw[0].Target != target)
                    throw new ArgumentException("game_corner_owned_evolution_source");
                // Canonical mechanics keep evolution metadata separate. Cache only this admitted
                // level-edge projection; all source stats, ability slots and learnset objects persist.
                definitions.Add(id, source with { Evolutions = [new(level, target, raw[0].Source)] });
            }
            else
            {
                if (index == 2 && !raw.IsEmpty || index == 3 && (raw.Length != 1 || raw[0].Method != "EVO_TRADE_ITEM" ||
                    raw[0].SourceMethodId != 6 || raw[0].Parameter != "ITEM_UP_GRADE" || raw[0].ParameterValue != 218 ||
                    raw[0].Target != new SpeciesId("pret:SPECIES_PORYGON2")))
                    throw new ArgumentException("game_corner_owned_terminal_source");
                definitions.Add(id, source);
            }
        }
        return definitions.ToImmutable();
    }
}
