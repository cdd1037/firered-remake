using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>204 opponents, retaining the complete 186-species Power Plant predecessor and 152 owned species.</summary>
public sealed class TrainerTowerOpponentPokemonCatalog
{
    public const string Profile = "firered-trainer-tower-opponents-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        new[] { "POLIWRATH", "VAPOREON", "FLAREON", "FERALIGATR", "LEDIAN", "TOGEPI", "AMPHAROS", "POLITOED",
            "ESPEON", "DUNSPARCE", "GRANBULL", "QWILFISH", "CORSOLA", "MANTINE", "PORYGON2", "SMOOCHUM", "MAGBY", "BLISSEY" }
        .Select(s => new SpeciesId("pret:SPECIES_" + s)).ToImmutableArray();
    private static readonly ConditionalWeakTable<PokemonCatalog, TrainerTowerOpponentPokemonCatalog> Proofs = new();
    private static readonly ConditionalWeakTable<PowerPlantOwnedPokemonCatalog, TrainerTowerOpponentPokemonCatalog> Owners = new();
    private static readonly ConditionalWeakTable<PokemonCatalog, TrainerTowerOpponentPokemonCatalog> MechanicsProofs = new();
    public PowerPlantOpponentPokemonCatalog Predecessor { get; }
    public PowerPlantOwnedPokemonCatalog Owned => Predecessor.Owned;
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public PokemonCatalog Catalog { get; }
    private TrainerTowerOpponentPokemonCatalog(PowerPlantOpponentPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static TrainerTowerOpponentPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static TrainerTowerOpponentPokemonCatalog? For(PowerPlantOwnedPokemonCatalog owned) =>
        Owners.TryGetValue(owned, out var proof) ? proof : null;
    internal static TrainerTowerOpponentPokemonCatalog? ForMechanics(PokemonCatalog? catalog) =>
        catalog is not null && MechanicsProofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static TrainerTowerOpponentPokemonCatalog Authenticate(PowerPlantOpponentPokemonCatalog predecessor,
        ImmutableArray<PokemonSpeciesDefinition> opponentSpecies)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(PowerPlantOpponentPokemonCatalog.For(predecessor.Catalog), predecessor) ||
            predecessor.Catalog.Species.Count != 186 || predecessor.Owned.Catalog.Species.Count != 152 ||
            predecessor.Catalog.Moves.Count != 354 || opponentSpecies.IsDefault || opponentSpecies.Length != 18 ||
            !opponentSpecies.Select(s => s?.Id).SequenceEqual(AdditionalSpecies.Select(s => (SpeciesId?)s)) ||
            opponentSpecies.Any(s => !SilphOwnedPokemonCatalog.MatchesSource(s, predecessor.Mechanics.Catalog.Species[s.Id])))
            throw new ArgumentException("trainer_tower_opponent_predecessor_source");
        var species = predecessor.Catalog.Species;
        foreach (var id in AdditionalSpecies)
        {
            if (species.ContainsKey(id) || predecessor.Owned.Catalog.Species.ContainsKey(id))
                throw new ArgumentException("trainer_tower_opponent_overlap");
            species = species.Add(id, predecessor.Mechanics.Catalog.Species[id]);
        }
        if (species.Count != 204 || !predecessor.Owned.Catalog.Species.Keys.All(species.ContainsKey))
            throw new ArgumentException("trainer_tower_opponent_scope");
        var proof = new TrainerTowerOpponentPokemonCatalog(predecessor, predecessor.Catalog with { Species = species });
        Proofs.Add(proof.Catalog, proof); Owners.GetValue(proof.Owned, _ => proof);
        MechanicsProofs.GetValue(proof.Mechanics.Catalog, _ => proof); return proof;
    }
}
