using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.Encounters;

/// <summary>Exact Old Rod table continuation. Frame task admission remains in the shared fishing owner.</summary>
public static class SevenIslandOldRodEncounterRules
{
    public const string RulesVersion = CeruleanCaveOldRodEncounterRules.RulesVersion;
    public static ImmutableArray<OldRodEncounterTable> SourceAdditions => SevenIslandEncounterTables.OldRod;
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } = [.. CeruleanCaveOldRodEncounterRules.Tables, .. SourceAdditions, .. SeafoamEncounterTables.OldRod, .. CapeBrinkEncounterTables.OldRod, .. MemorialPillarEncounterTables.OldRod, .. LostCaveEncounterTables.OldRod];
    public static ImmutableArray<MapId> MapsWithoutFishingMons { get; } = [
        new("map.seven-island"), new("map.seven-island-harbor"), new("map.seven-island-house-room1"),
        new("map.seven-island-mart"), new("map.seven-island-pokemon-center-1f"),
        new("map.seven-island-sevault-canyon"), new("map.seven-island-sevault-canyon-entrance"),
        new("map.seven-island-sevault-canyon-house"), new("map.seven-island-sevault-canyon-tanoby-key"),
        new("map.seven-island-tanoby-ruins-monean-chamber"), new("map.seven-island-tanoby-ruins-liptoo-chamber"),
        new("map.seven-island-tanoby-ruins-weepth-chamber"), new("map.seven-island-tanoby-ruins-dilford-chamber"),
        new("map.seven-island-tanoby-ruins-scufib-chamber"), new("map.seven-island-tanoby-ruins-rixy-chamber"),
        new("map.seven-island-tanoby-ruins-viapois-chamber")];
    public static FishingTaskProfile TaskProfile { get; } = FishingTaskProfile.ForRod(RulesVersion, FishingRod.Old, Tables, CeruleanCaveOldRodEncounterRules.TaskProfile.EmptyMaps.Select(m => m.Map) .Concat(MapsWithoutFishingMons));
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t => t.Map == map) || MapsWithoutFishingMons.Contains(map) || CeruleanCaveOldRodEncounterRules.IsAdmittedMap(map);
    public static OldRodFishingResult Start(GamePokemonCatalog pokemon, OldRodCastContext context,
        RngState rng, bool cancelBeforeCast = false)
    {
        ValidatePokemon(pokemon);
        return TaskProfile.Start(context, rng, cancelBeforeCast);
    }

    public static OldRodFishingResult Advance(GamePokemonCatalog pokemon, OldRodFishingState state,
        OldRodFrameInput input, RngState rng)
    {
        ValidatePokemon(pokemon);
        return TaskProfile.Advance(state, input, rng);
    }

    public static void ValidateState(GamePokemonCatalog pokemon, OldRodFishingState state)
    {
        ValidatePokemon(pokemon);
        TaskProfile.ValidateState(state);
    }

    public static void ValidateTable(OldRodEncounterTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var expected = Tables.SingleOrDefault(t => t.Map == table.Map);
        if (expected is null || table.Slots.IsDefault || table with { Slots = expected.Slots } != expected || !table.Slots.SequenceEqual(expected.Slots))
            throw new ArgumentException("seven_island_old_rod_source");
    }
    public static void ValidateCandidate(OldRodEncounterCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var table = Tables.SingleOrDefault(t => t.Map == candidate.Map);
        if (table is null || candidate.SlotIndex is < 0 or > 1) throw new ArgumentException("seven_island_old_rod_candidate_scope");
        var slot = table.Slots[candidate.SlotIndex];
        if (candidate.Species != slot.Species || candidate.Level < slot.MinimumLevel || candidate.Level > slot.MaximumLevel)
            throw new ArgumentException("seven_island_old_rod_candidate_source");
    }
    public static OldRodEncounterResult GenerateCandidate(GamePokemonCatalog pokemon,
        OldRodEncounterTable table, OldRodFishingState fishing, RngState rng)
    {
        ValidateTable(table);
        ValidateState(pokemon, fishing);
        if (fishing.Status != OldRodFishingStatus.EncounterReady || fishing.Map != table.Map)
            throw new InvalidOperationException("The matching fishing task must finish successfully first.");
        return OldRodEncounterRules.GenerateCandidateCore(table, rng);
    }

    public static OldRodPokemonResult GeneratePokemon(GamePokemonCatalog pokemon,
        OldRodEncounterCandidate candidate, RngState rng, uint playerTrainerId)
    {
        ValidatePokemon(pokemon);
        ValidateCandidate(candidate);
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unknown global RNG algorithm.");
        var result = Route1WildPokemonFactory.GenerateOrdinary(pokemon.Catalog, candidate.Species,
            candidate.Level, rng, playerTrainerId);
        return new(result.Pokemon, result.GlobalRng, result.RandomTrace);
    }

    private static void ValidatePokemon(GamePokemonCatalog pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
    }
}
