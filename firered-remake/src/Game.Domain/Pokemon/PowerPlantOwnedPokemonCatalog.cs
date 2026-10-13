using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;
namespace Game.Domain.Pokemon;

/// <summary>Power Plant wild/static species and the source Thunder Stone closure over Event Island.</summary>
public sealed class PowerPlantOwnedPokemonCatalog
{
    public const string Profile = "firered-power-plant-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_ELECTABUZZ"), new("pret:SPECIES_JOLTEON"), new("pret:SPECIES_ZAPDOS")];
    private static readonly ConditionalWeakTable<PokemonCatalog, PowerPlantOwnedPokemonCatalog> Proofs = new();
    public EventIslandOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;
    private PowerPlantOwnedPokemonCatalog(EventIslandOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static PowerPlantOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static PowerPlantOwnedPokemonCatalog Authenticate(EventIslandOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(EventIslandOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 149 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 152 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var value) || !ReferenceEquals(p.Value, value)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("power_plant_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!ReferenceEquals(candidate.Species[id], ExpectedSpecies(predecessor, id)))
                throw new ArgumentException("power_plant_owned_source");
        var proof = new PowerPlantOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public static PokemonSpeciesDefinition ExpectedSpecies(EventIslandOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(EventIslandOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || !AdditionalSpecies.Contains(id))
            throw new ArgumentException("power_plant_owned_species");
        var source = predecessor.Mechanics.Catalog.Species[id];
        if (!predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions.IsEmpty || !source.Evolutions.IsEmpty)
            throw new ArgumentException("power_plant_owned_terminal_source");
        return source;
    }
}
