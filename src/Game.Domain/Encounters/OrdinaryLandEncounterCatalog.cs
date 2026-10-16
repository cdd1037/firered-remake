using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Growth;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.World;

namespace Game.Domain.Encounters;

/// <summary>
/// Load-time admission of ordinary land tables against the supplied maps and Pokémon catalog.
/// Definitions retain the supplied table; no regional table copy or map-name allowlist is added.
/// Special source-origin consumers keep their existing contracts.
/// </summary>
public sealed class OrdinaryLandEncounterCatalog
{
    private readonly ImmutableDictionary<MapId, OrdinaryLandEncounterDefinition> _definitions;

    public OrdinaryLandEncounterCatalog(IReadOnlyDictionary<MapId, EarlyLandEncounterTable> tables,
        IReadOnlyDictionary<MapId, IFieldMap> maps, PokemonCatalog pokemon)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(maps);
        ArgumentNullException.ThrowIfNull(pokemon);
        var definitions = ImmutableDictionary.CreateBuilder<MapId, OrdinaryLandEncounterDefinition>();
        foreach (var (id, table) in tables)
        {
            EarlyLandEncounterRules.ValidateTableDefinition(table);
            if (id != table.Map || !maps.TryGetValue(id, out var map) || map is null || map.Id != id)
                throw new NotSupportedException("Land encounter table requires its supplied field map.");
            if (table.Slots.Any(slot => !pokemon.Species.ContainsKey(slot.Species)))
                throw new ArgumentException("Land encounter species is absent from the supplied Pokémon catalog.");
            if (EarlyLandEncounterRules.RequiresSpecialTable(id))
            {
                EarlyLandEncounterRules.ValidateTable(table);
                // Source-table receipts require the exact registered slots, but their
                // walking terrain still comes from the supplied map like ordinary land.
                // Distinct legacy processes (Safari, roaming regions, etc.) stay separate.
                if (!SourceTableEncounterRegistry.Land.Any(source => source.Map == id)) continue;
            }
            if (map.Width <= 0 || map.Height <= 0)
                throw new ArgumentException("Land encounter map has invalid dimensions.");
            var attributes = ImmutableHashSet.CreateBuilder<(byte Behavior, EncounterTileType Terrain)>();
            for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                var behavior = map.SourceBehaviorAt(x, y);
                var terrain = map.SourceEncounterTypeAt(x, y);
                if (behavior is null or < 0 or > byte.MaxValue || terrain is null or < 0 or > 2)
                    throw new NotSupportedException("Land encounter map contains unsupported source attributes.");
                // Water has its own process. The land process records that behavior without checking.
                attributes.Add(((byte)behavior.Value, terrain == 1 ? EncounterTileType.Land : EncounterTileType.None));
            }
            // Original maps can retain an unused land table. Empty land attributes admit no
            // land checks or pending candidates; the table itself is still a valid definition.
            definitions.Add(id, new(table, attributes.ToImmutable(), pokemon));
        }
        _definitions = definitions.ToImmutable();
    }

    public bool TryGetValue(MapId map, out OrdinaryLandEncounterDefinition definition) =>
        _definitions.TryGetValue(map, out definition!);

    public OrdinaryLandEncounterDefinition this[MapId map] => _definitions[map];
}

/// <summary>Only a validated catalog can create this immutable table/terrain binding.</summary>
public sealed class OrdinaryLandEncounterDefinition
{
    public EarlyLandEncounterTable Table { get; }
    private readonly PokemonCatalog _pokemon;
    private readonly ImmutableHashSet<(byte Behavior, EncounterTileType Terrain)> _attributes;

    internal OrdinaryLandEncounterDefinition(EarlyLandEncounterTable table,
        ImmutableHashSet<(byte Behavior, EncounterTileType Terrain)> attributes, PokemonCatalog pokemon)
    {
        Table = table;
        _pokemon = pokemon;
        _attributes = attributes;
    }

    public void ValidateState(EarlyLandEncounterState state, EarlyLandEncounterTable table)
    {
        if (!ReferenceEquals(Table, table))
            throw new ArgumentException("Encounter table differs from its loaded definition.");
        EarlyLandEncounterRules.ValidateState(state);
        if (state.RulesVersion != EarlyLandEncounterRules.FiveIslandRulesVersion)
            throw new NotSupportedException("Catalog-bound land encounters require the current process.");
        if (state.Pending is not { } pending) return;
        var candidate = pending.Candidate;
        var slot = Table.Slots[candidate.Slot];
        if (candidate.Map != Table.Map || candidate.Species != slot.Species ||
            candidate.Level < slot.MinimumLevel || candidate.Level > slot.MaximumLevel ||
            !_attributes.Contains((pending.MetatileBehavior, EncounterTileType.Land)))
            throw new ArgumentException("Pending candidate disagrees with its loaded land definition.");
    }

    public EarlyLandWildPokemonResult GeneratePokemon(EarlyLandEncounterState state, RngState rng, uint trainerId)
    {
        ValidateState(state, Table);
        var candidate = state.Pending?.Candidate ?? throw new InvalidOperationException("ordinary_land_pending");
        var generated = Route1WildPokemonFactory.GenerateOrdinary(_pokemon,
            candidate.Species, candidate.Level, rng, trainerId);
        return new(generated.Pokemon, generated.GlobalRng, generated.RandomTrace);
    }

    /// <summary>Runtime proof of the exact pending slot; active battles are not save payloads.</summary>
    public OrdinaryLandBattleBinding BindBattle(EarlyLandEncounterState state)
    {
        ValidateState(state, Table);
        var candidate = state.Pending?.Candidate ?? throw new InvalidOperationException("ordinary_land_pending");
        return new(_pokemon, candidate);
    }

    internal void ValidateCheck(EarlyLandEncounterCheck check)
    {
        if (check.Map != Table.Map || check.EncounterType is not (EncounterTileType.None or EncounterTileType.Land) ||
            !_attributes.Contains((check.MetatileBehavior, check.EncounterType)))
            throw new ArgumentException("Encounter check disagrees with its loaded map attributes.");
    }
}

/// <summary>Created only from a validated ordinary table and pending land candidate.</summary>
public sealed class OrdinaryLandBattleBinding
{
    private readonly PokemonCatalog _pokemon;
    private readonly EarlyLandEncounterCandidate _candidate;

    internal OrdinaryLandBattleBinding(PokemonCatalog pokemon, EarlyLandEncounterCandidate candidate)
    {
        _pokemon = pokemon;
        _candidate = candidate;
    }

    public bool Matches(PokemonInstance pokemon) => pokemon is not null &&
        pokemon.Species == _candidate.Species && pokemon.Level == _candidate.Level &&
        pokemon.SourceSpeciesId == _pokemon.Species[_candidate.Species].SourceSpeciesId;

    public bool SourceInitialInstance(GamePokemonCatalog owned, PokemonInstance pokemon)
    {
        if (owned is null || !ReferenceEquals(_pokemon, owned.Catalog) || !Matches(pokemon) ||
            SourcePokemonValidator.ValidatePermanent(owned.Mechanics, pokemon) is not null) return false;
        var source = owned.Mechanics.Catalog.Species[pokemon.Species];
        return pokemon.NpcTradeOrigin is null && pokemon.ContestConditions is null && pokemon.StatCalculationEvs is null &&
            pokemon.Evs == SixStats.Zero && pokemon.CurrentHp == pokemon.Stats.MaxHp && pokemon.Friendship == source.Friendship &&
            pokemon.Experience == GrowthRules.ExperienceAtLevel(source.GrowthRate, pokemon.Level) &&
            pokemon.Moves.SequenceEqual(PokemonMoveRules.InitialMovesCore(owned.Mechanics.Catalog, source, pokemon.Level));
    }

    public string HeldItem(GamePokemonCatalog owned, int roll)
    {
        if (owned is null || !ReferenceEquals(_pokemon, owned.Catalog) || roll is < 0 or > 99)
            throw new ArgumentException("ordinary_land_held_item");
        var source = owned.Mechanics.SpeciesMechanics[_candidate.Species];
        return source.ItemCommon == source.ItemRare ? source.ItemCommon :
            roll < 45 ? "ITEM_NONE" : roll < 95 ? source.ItemCommon : source.ItemRare;
    }
}
