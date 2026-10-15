using System.Collections.Immutable;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Encounters;

/// <summary>Authenticated current ownership and exact source-slot admission over the existing creation algorithms.</summary>
public static class SevenIslandWildPokemonFactory
{
    public static ImmutableArray<SpeciesId> WildSpecies { get; } = SevenIslandEncounterTables.Land
        .SelectMany(t => t.Slots).Select(s => s.Species)
        .Concat(SevenIslandEncounterTables.Water.SelectMany(t => t.Slots).Select(s => s.Species))
        .Concat(SevenIslandEncounterTables.OldRod.SelectMany(t => t.Slots).Select(s => s.Species))
        .Distinct().ToImmutableArray();

    public static EarlyLandWildPokemonResult Generate(GamePokemonCatalog owned,
        EarlyLandEncounterTable table, EarlyLandEncounterState encounter, RngState rng, uint playerTrainerId)
    {
        ValidateOwned(owned);
        if (encounter.RulesVersion != EarlyLandEncounterRules.FiveIslandRulesVersion)
            throw new ArgumentException("seven_island_wild_profile");
        EarlyLandEncounterRules.ValidateState(encounter, table);
        var candidate = encounter.Pending?.Candidate ?? throw new InvalidOperationException("seven_island_wild_pending");
        var result = SevenIslandEncounterTables.Land.Any(t => t.Map == table.Map)
            ? GenerateSourceSlot(owned, new(candidate.Map, SevenIslandEncounterKind.Land, candidate.Slot), candidate.Level, rng, playerTrainerId)
            : Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, candidate.Species, candidate.Level, rng, playerTrainerId);
        return new(result.Pokemon, result.GlobalRng, result.RandomTrace);
    }

    public static Route1WildPokemonResult GenerateSourceSlot(GamePokemonCatalog owned,
        SevenIslandWildOrigin origin, byte level, RngState rng, uint playerTrainerId)
    {
        ValidateOwned(owned);
        var slot = SevenIslandWildBattleContract.SourceSlot(origin);
        if (slot is null ||
            level < slot.MinimumLevel || level > slot.MaximumLevel || !owned.Catalog.Species.ContainsKey(slot.Species))
            throw new ArgumentException("seven_island_wild_source_slot");
        var result = slot.Species.Value == "pret:SPECIES_UNOWN"
            ? UnownWildPokemonFactory.GenerateSourceSlot(owned.Mechanics, origin.Map, origin.Slot, level, rng, playerTrainerId)
            : Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, slot.Species, level, rng, playerTrainerId);
        if (!SourceInitialInstance(owned, origin, result.Pokemon))
            throw new InvalidOperationException("seven_island_wild_generated_identity");
        return result;
    }

    public static bool SourceInitialInstance(GamePokemonCatalog owned,
        SevenIslandWildOrigin origin, PokemonInstance pokemon)
    {
        if (owned is null || pokemon is null || !SevenIslandWildBattleContract.Matches(origin, pokemon) ||
            !owned.Catalog.Species.TryGetValue(pokemon.Species, out var species)) return false;
        // Mechanics is the independently authenticated complete source; ownership authenticates inherited identity.
        var source = owned.Mechanics.Catalog.Species[pokemon.Species];
        var moves = PokemonMoveRules.InitialMovesCore(owned.Mechanics.Catalog, source, pokemon.Level);
        return SourcePokemonValidator.ValidatePermanent(owned.Mechanics, pokemon) is null &&
            pokemon.SourceSpeciesId == source.SourceSpeciesId &&
            (pokemon.Species.Value != "pret:SPECIES_UNOWN" || UnownFormRules.Form(pokemon.Personality) == UnownFormRules.SourceSlotForm(origin.Map, origin.Slot)) &&
            pokemon.Evs == SixStats.Zero &&
            pokemon.CurrentHp == pokemon.Stats.MaxHp && pokemon.Friendship == source.Friendship &&
            pokemon.Experience == GrowthRules.ExperienceAtLevel(source.GrowthRate, pokemon.Level) &&
            pokemon.Moves.SequenceEqual(moves) && species.SourceSpeciesId == source.SourceSpeciesId;
    }

    public static string HeldItem(SpeciesId species, int roll)
    {
        if (roll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        var source = SevenIslandEncounterTables.HeldItems.SingleOrDefault(s => s.Species == species)
            ?? throw new NotSupportedException("seven_island_wild_held_species");
        return source.Common == source.Rare ? source.Common : roll < 45 ? "ITEM_NONE" : roll < 95 ? source.Common : source.Rare;
    }

    public static EarlyLandWildHeldItemResult AssignHeldItem(SpeciesId species, RngState rng)
    {
        if (!WildSpecies.Contains(species)) throw new NotSupportedException("seven_island_wild_held_species");
        var draw = Gen3Rng.Next(rng, "wild-battle.held-item", 100);
        return new(HeldItem(species, draw.Trace.Result), draw.State, [draw.Trace]);
    }

    internal static void ValidateOwned(GamePokemonCatalog owned)
    {
        if (owned is null)
            throw new ArgumentException("seven_island_owned_proof");
    }
}
