using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>The three event-island species over Seven Island. Deoxys uses authenticated FireRed Attack stats.</summary>
public sealed class EventIslandOwnedPokemonCatalog
{
    public const string Profile = "firered-event-island-owned-pokemon-v1";
    public static ImmutableArray<SpeciesId> AdditionalSpecies { get; } =
        [new("pret:SPECIES_HO_OH"), new("pret:SPECIES_LUGIA"), new("pret:SPECIES_DEOXYS")];
    private static readonly ConditionalWeakTable<PokemonCatalog, EventIslandOwnedPokemonCatalog> Proofs = new();
    public SevenIslandOwnedPokemonCatalog Predecessor { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Predecessor.Mechanics;
    public OwnedBattlePokemonCatalog SharedBattle => OwnedBattlePokemonCatalog.For(Catalog)!;
    private EventIslandOwnedPokemonCatalog(SevenIslandOwnedPokemonCatalog predecessor, PokemonCatalog catalog)
    { Predecessor = predecessor; Catalog = catalog; }
    public static EventIslandOwnedPokemonCatalog? For(PokemonCatalog? catalog) =>
        catalog is not null && Proofs.TryGetValue(catalog, out var proof) ? proof : null;
    public static EventIslandOwnedPokemonCatalog Authenticate(SevenIslandOwnedPokemonCatalog predecessor, PokemonCatalog candidate)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(candidate);
        if (!ReferenceEquals(SevenIslandOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || predecessor.Catalog.Species.Count != 146 ||
            candidate.SourceCommit != PokemonFactory.SourceCommit || candidate.Species is null || candidate.Species.Count != 149 ||
            !ReferenceEquals(candidate.Moves, predecessor.Catalog.Moves) ||
            predecessor.Catalog.Species.Any(p => !candidate.Species.TryGetValue(p.Key, out var value) || !ReferenceEquals(p.Value, value)) ||
            !candidate.Species.Keys.Except(predecessor.Catalog.Species.Keys).ToHashSet().SetEquals(AdditionalSpecies))
            throw new ArgumentException("event_island_owned_scope");
        foreach (var id in AdditionalSpecies)
            if (!SevenIslandOwnedPokemonCatalog.MatchesSource(candidate.Species[id], ExpectedSpecies(predecessor, id)))
                throw new ArgumentException("event_island_owned_source");
        var proof = new EventIslandOwnedPokemonCatalog(predecessor, candidate with { });
        Proofs.Add(proof.Catalog, proof); return proof;
    }
    public static PokemonSpeciesDefinition ExpectedSpecies(SevenIslandOwnedPokemonCatalog predecessor, SpeciesId id)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        if (!ReferenceEquals(SevenIslandOwnedPokemonCatalog.For(predecessor.Catalog), predecessor) || !AdditionalSpecies.Contains(id))
            throw new ArgumentException("event_island_owned_species");
        var source = predecessor.Mechanics.Catalog.Species[id];
        if (!predecessor.Mechanics.SpeciesMechanics[id].SourceEvolutions.IsEmpty || !source.Evolutions.IsEmpty ||
            source.Ability1 != "ABILITY_PRESSURE" || source.Ability2 != "ABILITY_NONE")
            throw new ArgumentException("event_island_owned_terminal_source");
        if (id != AdditionalSpecies[2]) return source;
        var form = predecessor.Mechanics.FormStatOverrides.Single(f => f.Species == id);
        if (source.SourceSpeciesId != 410 || form.Scope != "firered-ordinary-non-link-stat-read" ||
            form.BaseStats != new SixStats(50, 180, 20, 150, 180, 20) || form.Source.Symbol != "sDeoxysBaseStats" ||
            form.RuleSource.Symbol != "GetDeoxysStat") throw new ArgumentException("event_island_deoxys_form_source");
        // Every current permanent path (growth, validation, machines, storage) sees the same effective form.
        // The separately authenticated canonical mechanics catalog retains the source normal-form row.
        return source with { BaseStats = form.BaseStats };
    }
}
