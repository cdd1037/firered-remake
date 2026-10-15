using System.Collections.Immutable;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Encounters;

public sealed record PowerPlantPokemonResult(PokemonInstance Pokemon, RngState Rng, ImmutableArray<RandomTrace> Trace,
    PowerPlantWildOrigin Origin)
{
    public bool ModernFatefulEncounter => false;
    public string HeldItem => "ITEM_NONE";
}
/// <summary>Source CreateScriptedWildMon and ordinary land generation. Held assignment remains at battle entry.</summary>
public static class PowerPlantPokemonFactory
{
    public static PowerPlantPokemonResult Create(GamePokemonCatalog owned, PowerPlantEncounterKind kind,
        uint originalTrainerId, RngState rng)
    {
        ValidateOwned(owned);
        var origin = PowerPlantWildBattleContract.Origin(kind);
        var created = SourcePokemonFactory.Create(owned.Mechanics, new(PowerPlantWildBattleContract.Species(kind),
            PowerPlantWildBattleContract.Level(kind), originalTrainerId, IvPolicy.Random, PersonalityPolicy.Random), rng);
        if (!SourceInitialInstance(owned, origin, created.Pokemon)) throw new InvalidOperationException("power_plant_generated_identity");
        return new(created.Pokemon, created.Rng, created.Trace, origin);
    }
    public static Route1WildPokemonResult GenerateStatic(GamePokemonCatalog owned, PowerPlantEncounterKind kind,
        RngState rng, uint originalTrainerId)
    {
        var made = Create(owned, kind, originalTrainerId, rng);
        return new(made.Pokemon, made.Rng, made.Trace);
    }
    public static Route1WildPokemonResult GenerateLand(GamePokemonCatalog owned, PendingEarlyLandEncounter pending,
        RngState rng, uint originalTrainerId)
    {
        ValidateOwned(owned); ArgumentNullException.ThrowIfNull(pending);
        var candidate = pending.Candidate;
        var origin = new PowerPlantWildOrigin(candidate.Map, PowerPlantEncounterKind.Land, candidate.Slot);
        return GenerateSourceSlot(owned, origin, candidate.Level, rng, originalTrainerId, candidate.Species);
    }
    public static Route1WildPokemonResult GenerateSourceSlot(GamePokemonCatalog owned, PowerPlantWildOrigin origin,
        byte level, RngState rng, uint originalTrainerId) => GenerateSourceSlot(owned, origin, level, rng, originalTrainerId, null);
    private static Route1WildPokemonResult GenerateSourceSlot(GamePokemonCatalog owned, PowerPlantWildOrigin origin,
        byte level, RngState rng, uint originalTrainerId, SpeciesId? expectedSpecies)
    {
        ValidateOwned(owned);
        var slot = PowerPlantWildBattleContract.SourceSlot(origin);
        if (origin.Kind != PowerPlantEncounterKind.Land || slot is null ||
            expectedSpecies is {} expected && expected != slot.Species || level < slot.MinimumLevel || level > slot.MaximumLevel)
            throw new ArgumentException("power_plant_pending_source");
        var made = Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, slot.Species, level, rng, originalTrainerId);
        if (!SourceInitialInstance(owned, origin, made.Pokemon)) throw new InvalidOperationException("power_plant_generated_identity");
        return made;
    }
    public static bool SourceInitialInstance(GamePokemonCatalog owned, PowerPlantWildOrigin origin, PokemonInstance pokemon)
    {
        if (owned is null || !PowerPlantWildBattleContract.Matches(origin, pokemon) || SourcePokemonValidator.ValidatePermanent(owned.Mechanics, pokemon) is not null)
            return false;
        var source = owned.Mechanics.Catalog.Species[pokemon.Species];
        return pokemon.Evs == SixStats.Zero && pokemon.CurrentHp == pokemon.Stats.MaxHp && pokemon.Friendship == source.Friendship &&
            pokemon.Experience == GrowthRules.ExperienceAtLevel(source.GrowthRate, pokemon.Level) &&
            pokemon.Moves.SequenceEqual(PokemonMoveRules.InitialMovesCore(owned.Mechanics.Catalog, source, pokemon.Level));
    }
    /// <summary>SetWildMonHeldItem always consumes its separate draw for ordinary battles, even NONE/NONE.</summary>
    public static string HeldItem(SpeciesId species, int roll)
    {
        if (roll is < 0 or > 99) throw new ArgumentOutOfRangeException(nameof(roll));
        var row = PowerPlantEncounterTables.HeldItems.SingleOrDefault(r => r.Species == species)
            ?? throw new ArgumentException("power_plant_held_species");
        return row.Common == row.Rare ? row.Common : roll <= 44 ? "ITEM_NONE" : roll <= 94 ? row.Common : row.Rare;
    }
    public static PartyPokemon PreserveCaughtMetadata(GamePokemonCatalog owned, PowerPlantWildOrigin origin, PartyPokemon caught)
    {
        ValidateOwned(owned); ArgumentNullException.ThrowIfNull(caught);
        if (!PowerPlantWildBattleContract.Matches(origin, caught.Pokemon) ||
            SourcePokemonValidator.ValidatePermanent(owned.Mechanics, caught.Pokemon) is not null)
            throw new ArgumentException("power_plant_caught_identity");
        return caught with { ModernFatefulEncounter = false };
    }
    private static void ValidateOwned(GamePokemonCatalog owned)
    {
        if (owned is null)
            throw new ArgumentException("power_plant_owned_proof");
    }
}
