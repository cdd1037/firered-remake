using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Seven original direct stone targets over the exact 163-species Super Rod owner.</summary>
public sealed class StoneEvolutionOwnedPokemonCatalog
{
    public const string Profile = "firered-stone-evolution-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        new[] { "NINETALES", "ARCANINE", "POLIWRATH", "CLOYSTER", "EXEGGUTOR", "VAPOREON", "FLAREON" }
        .Select(name => new SpeciesId("pret:SPECIES_" + name)).ToImmutableArray();
    private static readonly (int Number, string From, string Stone, int Item)[] Expected = [
        (38, "VULPIX", "FIRE", 95), (59, "GROWLITHE", "FIRE", 95), (62, "POLIWHIRL", "WATER", 97),
        (91, "SHELLDER", "WATER", 97), (103, "EXEGGCUTE", "LEAF", 98), (134, "EEVEE", "WATER", 97), (136, "EEVEE", "FIRE", 95)
    ];
    private static readonly ConditionalWeakTable<PokemonCatalog, StoneEvolutionOwnedPokemonCatalog> Proofs = new();
    public SuperRodOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;
    private StoneEvolutionOwnedPokemonCatalog(SuperRodOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static StoneEvolutionOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static bool IsAuthenticated(PokemonCatalog? catalog) => For(catalog) is not null;
    public static StoneEvolutionOwnedPokemonCatalog Authenticate(SuperRodOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(SuperRodOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 163 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 170 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var value) || !ReferenceEquals(p.Value, value)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("stone_evolution_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!ReferenceEquals(candidate.Species[id], ExpectedSpecies(predecessor, id)))
                throw new ArgumentException("stone_evolution_owned_source");
        var proof = new StoneEvolutionOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof);
        OwnedBattlePokemonCatalog.RegisterSuccessor(proof.Catalog, predecessor.SharedBattle, Profile);
        return proof;
    }
    public static PokemonSpeciesDefinition ExpectedSpecies(SuperRodOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(SuperRodOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || !AdditionalSpecies.Contains(id))
            throw new ArgumentException("stone_evolution_owned_species");
        var row = Expected[AdditionalSpecies.IndexOf(id)];
        var expected = predecessor.Mechanics.Catalog.Species[id];
        var raw = predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions;
        var from = new SpeciesId("pret:SPECIES_" + row.From);
        if (expected.SourceSpeciesId != row.Number || expected.Evolutions.IsDefault || !expected.Evolutions.IsEmpty || raw.IsDefault || !raw.IsEmpty ||
            !predecessor.Catalog.Species.ContainsKey(from) ||
            predecessor.Mechanics.SpeciesMechanics[from].SourceEvolutions.Count(e => e.Method == "EVO_ITEM" &&
                e.SourceMethodId == 7 && e.Parameter == "ITEM_" + row.Stone + "_STONE" && e.ParameterValue == row.Item && e.Target == id) != 1)
            throw new ArgumentException("stone_evolution_owned_terminal_source");
        return expected;
    }
}
