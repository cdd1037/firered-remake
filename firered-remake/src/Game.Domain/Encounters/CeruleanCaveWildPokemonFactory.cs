using System.Collections.Immutable;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Encounters;

/// <summary>Authenticated current ownership and exact source-slot admission over the existing creation algorithms.</summary>
public static class CeruleanCaveWildPokemonFactory
{
    public static ImmutableArray<SpeciesId> WildSpecies { get; } = CeruleanCaveEncounterTables.Land
        .SelectMany(t => t.Slots).Select(s => s.Species)
        .Concat(CeruleanCaveEncounterTables.Water.SelectMany(t => t.Slots).Select(s => s.Species))
        .Concat(CeruleanCaveEncounterTables.OldRod.SelectMany(t => t.Slots).Select(s => s.Species))
        .Distinct().ToImmutableArray();

    public static EarlyLandWildPokemonResult Generate(CeruleanCaveOwnedPokemonCatalog owned,
        EarlyLandEncounterTable table, EarlyLandEncounterState encounter, RngState rng, uint playerTrainerId)
    {
        ValidateOwned(owned);
        if (encounter.RulesVersion != EarlyLandEncounterRules.FiveIslandRulesVersion)
            throw new ArgumentException("cerulean_cave_wild_profile");
        EarlyLandEncounterRules.ValidateState(encounter, table);
        var candidate = encounter.Pending?.Candidate ?? throw new InvalidOperationException("cerulean_cave_wild_pending");
        var result = CeruleanCaveEncounterTables.IsCave(table.Map)
            ? GenerateSourceSlot(owned, new(candidate.Map, CeruleanCaveEncounterKind.Land, candidate.Slot), candidate.Level, rng, playerTrainerId)
            : Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, candidate.Species, candidate.Level, rng, playerTrainerId);
        return new(result.Pokemon, result.GlobalRng, result.RandomTrace);
    }

    public static Route1WildPokemonResult GenerateSourceSlot(CeruleanCaveOwnedPokemonCatalog owned,
        CeruleanCaveWildOrigin origin, byte level, RngState rng, uint playerTrainerId)
    {
        ValidateOwned(owned);
        var slot = CeruleanCaveWildBattleContract.SourceSlot(origin);
        if (slot is null || origin.Kind == CeruleanCaveEncounterKind.Mewtwo ||
            level < slot.MinimumLevel || level > slot.MaximumLevel || !owned.Catalog.Species.ContainsKey(slot.Species))
            throw new ArgumentException("cerulean_cave_wild_source_slot");
        var result = Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, slot.Species, level, rng, playerTrainerId);
        if (!SourceInitialInstance(owned, origin, result.Pokemon))
            throw new InvalidOperationException("cerulean_cave_wild_generated_identity");
        return result;
    }

    /// <summary>setwildbattle MEWTWO,70 calls CreateMon directly: two PID and two IV draws, with no nature retry.</summary>
    public static Route1WildPokemonResult GenerateMewtwo(CeruleanCaveOwnedPokemonCatalog owned,
        RngState rng, uint playerTrainerId)
    {
        ValidateOwned(owned);
        var created = PokemonFactory.Create(owned.Catalog, new(new("pret:SPECIES_MEWTWO"), 70,
            playerTrainerId, IvPolicy.Random, PersonalityPolicy.Random), rng);
        if (created.Trace.Length != 4 || !SourceInitialInstance(owned, CeruleanCaveWildBattleContract.MewtwoOrigin, created.Pokemon))
            throw new InvalidOperationException("cerulean_cave_mewtwo_creation");
        return new(created.Pokemon, created.Rng, created.Trace);
    }

    public static bool SourceInitialInstance(CeruleanCaveOwnedPokemonCatalog owned,
        CeruleanCaveWildOrigin origin, PokemonInstance pokemon)
    {
        if (owned is null || !ReferenceEquals(CeruleanCaveOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            pokemon is null || !CeruleanCaveWildBattleContract.Matches(origin, pokemon) ||
            !owned.Catalog.Species.TryGetValue(pokemon.Species, out var species)) return false;
        // Mechanics is the independently authenticated complete source; ownership authenticates inherited identity.
        var source = owned.Mechanics.Catalog.Species[pokemon.Species];
        var moves = PokemonMoveRules.InitialMovesCore(owned.Mechanics.Catalog, source, pokemon.Level);
        return SourcePokemonValidator.ValidatePermanent(owned.Mechanics, pokemon) is null &&
            pokemon.SourceSpeciesId == source.SourceSpeciesId && pokemon.Evs == SixStats.Zero &&
            pokemon.CurrentHp == pokemon.Stats.MaxHp && pokemon.Friendship == source.Friendship &&
            pokemon.Experience == GrowthRules.ExperienceAtLevel(source.GrowthRate, pokemon.Level) &&
            pokemon.Moves.SequenceEqual(moves) && species.SourceSpeciesId == source.SourceSpeciesId;
    }

    public static string HeldItem(SpeciesId species, int roll)
    {
        if (roll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        var source = CeruleanCaveEncounterTables.HeldItems.SingleOrDefault(s => s.Species == species)
            ?? throw new NotSupportedException("cerulean_cave_wild_held_species");
        return source.Common == source.Rare ? source.Common : roll < 45 ? "ITEM_NONE" : roll < 95 ? source.Common : source.Rare;
    }

    public static EarlyLandWildHeldItemResult AssignHeldItem(SpeciesId species, RngState rng)
    {
        if (!WildSpecies.Contains(species)) throw new NotSupportedException("cerulean_cave_wild_held_species");
        var draw = Gen3Rng.Next(rng, "wild-battle.held-item", 100);
        return new(HeldItem(species, draw.Trace.Result), draw.State, [draw.Trace]);
    }

    internal static void ValidateOwned(CeruleanCaveOwnedPokemonCatalog owned)
    {
        if (owned is null || !ReferenceEquals(CeruleanCaveOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(CelioOwnedPokemonCatalog.For(owned.Predecessor.Catalog), owned.Predecessor) ||
            owned.Catalog.Species.Count != 139)
            throw new ArgumentException("cerulean_cave_owned_proof");
    }
}
