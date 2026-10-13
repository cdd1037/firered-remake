using System.Collections.Immutable;
using Game.Domain.Growth;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Encounters;

public sealed record EventIslandPokemonResult(PokemonInstance Pokemon, RngState Rng, ImmutableArray<RandomTrace> Trace,
    EventIslandWildOrigin Origin)
{
    public bool ModernFatefulEncounter => Origin.ModernFatefulEncounter;
    public string HeldItem => "ITEM_NONE";
}
/// <summary>CreateEnemyEventMon → CreateEventMon: player OT, random PID/IVs, then modern fateful metadata.
/// Uses the existing explicit low16/high16 generation profile; target-compiler ordering is not claimed.</summary>
public static class EventIslandPokemonFactory
{
    public static EventIslandPokemonResult Create(EventIslandOwnedPokemonCatalog owned, EventIslandEncounterKind kind,
        uint originalTrainerId, RngState rng)
    {
        ValidateOwned(owned);
        var origin = EventIslandWildBattleContract.Origin(kind);
        var created = SourcePokemonFactory.Create(owned.Mechanics, new(EventIslandWildBattleContract.Species(kind),
            EventIslandWildBattleContract.Level(kind), originalTrainerId, IvPolicy.Random, PersonalityPolicy.Random), rng);
        if (!SourceInitialInstance(owned, origin, created.Pokemon)) throw new InvalidOperationException("event_island_generated_identity");
        return new(created.Pokemon, created.Rng, created.Trace, origin);
    }
    public static Route1WildPokemonResult Generate(EventIslandOwnedPokemonCatalog owned, EventIslandEncounterKind kind,
        RngState rng, uint originalTrainerId)
    {
        var made = Create(owned, kind, originalTrainerId, rng);
        return new(made.Pokemon, made.Rng, made.Trace);
    }
    public static bool SourceInitialInstance(EventIslandOwnedPokemonCatalog owned, EventIslandWildOrigin origin, PokemonInstance pokemon)
    {
        if (owned is null || !ReferenceEquals(EventIslandOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !EventIslandWildBattleContract.Matches(origin, pokemon) || SourcePokemonValidator.ValidatePermanent(owned.Mechanics, pokemon) is not null)
            return false;
        var source = owned.Mechanics.Catalog.Species[pokemon.Species];
        return pokemon.Evs == SixStats.Zero && pokemon.CurrentHp == pokemon.Stats.MaxHp && pokemon.Friendship == source.Friendship &&
            pokemon.Experience == GrowthRules.ExperienceAtLevel(source.GrowthRate, pokemon.Level) &&
            pokemon.Moves.SequenceEqual(PokemonMoveRules.InitialMovesCore(owned.Mechanics.Catalog, source, pokemon.Level));
    }
    /// <summary>Apply event provenance to the actual caught party member. Never regenerates PID, IVs, HP, PP, OT or status.</summary>
    public static PartyPokemon PreserveCaughtMetadata(EventIslandOwnedPokemonCatalog owned, EventIslandWildOrigin origin, PartyPokemon caught)
    {
        ValidateOwned(owned); ArgumentNullException.ThrowIfNull(caught);
        if (!EventIslandWildBattleContract.Matches(origin, caught.Pokemon) ||
            SourcePokemonValidator.ValidatePermanent(owned.Mechanics, caught.Pokemon) is not null)
            throw new ArgumentException("event_island_caught_identity");
        return caught with { ModernFatefulEncounter = origin.ModernFatefulEncounter };
    }
    private static void ValidateOwned(EventIslandOwnedPokemonCatalog owned)
    {
        if (owned is null || !ReferenceEquals(EventIslandOwnedPokemonCatalog.For(owned.Catalog), owned))
            throw new ArgumentException("event_island_owned_proof");
    }
}
