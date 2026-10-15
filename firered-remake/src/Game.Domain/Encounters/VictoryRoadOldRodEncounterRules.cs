using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Authenticated first-Sevii continuation of the existing Old Rod task and ordinary creation.</summary>
public static class VictoryRoadOldRodEncounterRules
{
    public const string RulesVersion = "firered-victory-road-old-rod-v1";
    public static ImmutableArray<OldRodEncounterTable> SourceAdditions { get; } = VictoryRoadEncounterTables.FishingSource.Select(t => t with {Slots=t.Slots.Take(2).ToImmutableArray()}).ToImmutableArray();
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } =
        [.. FirstSeviiOldRodEncounterRules.Tables, .. SourceAdditions];

    public static ImmutableArray<MapId> MapsWithoutFishingMons { get; } = [new("map.victory-road-1f"), new("map.victory-road-2f"), new("map.victory-road-3f"), new("map.indigo-plateau-exterior")];
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t=>t.Map==map) || MapsWithoutFishingMons.Contains(map) || FirstSeviiOldRodEncounterRules.IsAdmittedMap(map);

    public static OldRodFishingResult Start(GamePokemonCatalog pokemon, OldRodCastContext context,
        RngState rng, bool cancelBeforeCast = false)
    {
        ValidatePokemon(pokemon);
        return OldRodFishingRules.StartCore(context, rng, cancelBeforeCast, cinnabarRules: true, firstSeviiRules: true, victoryRoadRules: true);
    }

    public static OldRodFishingResult Advance(GamePokemonCatalog pokemon, OldRodFishingState state,
        OldRodFrameInput input, RngState rng)
    {
        ValidatePokemon(pokemon);
        return OldRodFishingRules.AdvanceCore(state, input, rng, cinnabarRules: true, firstSeviiRules: true, victoryRoadRules: true);
    }

    public static void ValidateState(GamePokemonCatalog pokemon, OldRodFishingState state)
    {
        ValidatePokemon(pokemon);
        OldRodFishingRules.ValidateStateCore(state, cinnabarRules: true, firstSeviiRules: true, victoryRoadRules: true);
    }

    public static void ValidateTable(OldRodEncounterTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var expected = Tables.SingleOrDefault(t => t.Map == table.Map);
        if (expected is null || table.Slots.IsDefault || table with { Slots = expected.Slots } != expected ||
            !table.Slots.SequenceEqual(expected.Slots))
            throw new ArgumentException("victory_road_old_rod_table_source");
    }

    public static void ValidateCandidate(OldRodEncounterCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var table = Tables.SingleOrDefault(t => t.Map == candidate.Map);
        if (table is null || candidate.SlotIndex is < 0 or > 1)
            throw new ArgumentException("victory_road_old_rod_candidate_scope");
        var slot = table.Slots[candidate.SlotIndex];
        if (candidate.Species != slot.Species || candidate.Level < slot.MinimumLevel || candidate.Level > slot.MaximumLevel)
            throw new ArgumentException("victory_road_old_rod_candidate_source");
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
