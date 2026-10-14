using System.Collections.Immutable;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Encounters;

/// <summary>Source CreateScriptedWildMon and ordinary nature rejection. Held assignment is a separate battle-entry boundary.</summary>
public static class SeafoamPokemonFactory
{
    public static Route1WildPokemonResult GenerateArticuno(SeafoamOwnedPokemonCatalog owned, RngState rng, uint originalTrainerId)
    {
        ValidateOwned(owned);
        var made = SourcePokemonFactory.Create(owned.Mechanics, new(SeafoamOwnedPokemonCatalog.ArticunoId, 50,
            originalTrainerId, IvPolicy.Random, PersonalityPolicy.Random), rng);
        if (made.Trace.Length != 4 || !SourceInitialInstance(owned, SeafoamWildBattleContract.ArticunoOrigin, made.Pokemon))
            throw new InvalidOperationException("seafoam_articuno_generated_identity");
        return new(made.Pokemon, made.Rng, made.Trace);
    }
    public static Route1WildPokemonResult GenerateSourceSlot(SeafoamOwnedPokemonCatalog owned, SeafoamWildOrigin origin,
        byte level, RngState rng, uint originalTrainerId)
    {
        ValidateOwned(owned);
        var slot = SeafoamWildBattleContract.SourceSlot(origin);
        if (origin is null || origin.Kind == SeafoamEncounterKind.Articuno || slot is null || !owned.Catalog.Species.ContainsKey(slot.Species) ||
            level < Math.Min(slot.MinimumLevel, slot.MaximumLevel) || level > Math.Max(slot.MinimumLevel, slot.MaximumLevel))
            throw new ArgumentException("seafoam_wild_source_slot");
        var made = Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, slot.Species, level, rng, originalTrainerId);
        if (!SourceInitialInstance(owned, origin, made.Pokemon)) throw new InvalidOperationException("seafoam_wild_generated_identity");
        return made;
    }
    public static bool SourceInitialInstance(SeafoamOwnedPokemonCatalog owned, SeafoamWildOrigin origin, PokemonInstance pokemon)
    {
        if (owned is null || !ReferenceEquals(SeafoamOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !SeafoamWildBattleContract.Matches(origin, pokemon) || SourcePokemonValidator.ValidatePermanent(owned.Mechanics, pokemon) is not null)
            return false;
        var source = owned.Mechanics.Catalog.Species[pokemon.Species];
        return pokemon.NpcTradeOrigin is null && pokemon.ContestConditions is null && pokemon.StatCalculationEvs is null &&
            pokemon.Evs == SixStats.Zero && pokemon.CurrentHp == pokemon.Stats.MaxHp && pokemon.Friendship == source.Friendship &&
            pokemon.Experience == GrowthRules.ExperienceAtLevel(source.GrowthRate, pokemon.Level) &&
            pokemon.Moves.SequenceEqual(PokemonMoveRules.InitialMovesCore(owned.Mechanics.Catalog, source, pokemon.Level));
    }
    public static string HeldItem(SpeciesId species, int roll)
    {
        if (roll is < 0 or > 99) throw new ArgumentOutOfRangeException(nameof(roll));
        var row = SeafoamEncounterTables.HeldItems.SingleOrDefault(r => r.Species == species)
            ?? throw new ArgumentException("seafoam_held_species");
        return row.Common == row.Rare ? row.Common : roll <= 44 ? "ITEM_NONE" : roll <= 94 ? row.Common : row.Rare;
    }
    public static EarlyLandWildHeldItemResult AssignHeldItem(SeafoamWildOrigin origin, RngState rng)
    {
        var slot = SeafoamWildBattleContract.SourceSlot(origin) ?? throw new ArgumentException("seafoam_held_origin");
        if (origin.Kind == SeafoamEncounterKind.Articuno) return new("ITEM_NONE", rng, []);
        var draw = Gen3Rng.Next(rng, "wild-battle.held-item", 100);
        return new(HeldItem(slot.Species, draw.Trace.Result), draw.State, [draw.Trace]);
    }
    public static PartyPokemon PreserveCaughtMetadata(SeafoamOwnedPokemonCatalog owned, SeafoamWildOrigin origin, PartyPokemon caught)
    {
        ValidateOwned(owned); ArgumentNullException.ThrowIfNull(caught);
        if (!SeafoamWildBattleContract.Matches(origin, caught.Pokemon) ||
            SourcePokemonValidator.ValidatePermanent(owned.Mechanics, caught.Pokemon) is not null)
            throw new ArgumentException("seafoam_caught_identity");
        return caught with { ModernFatefulEncounter = false };
    }
    private static void ValidateOwned(SeafoamOwnedPokemonCatalog owned)
    {
        if (owned is null || !ReferenceEquals(SeafoamOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            owned.Catalog.Species.Count != 162 || owned.Catalog.Moves.Count != 354)
            throw new ArgumentException("seafoam_owned_proof");
    }
}
