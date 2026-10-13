using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Authenticated Ruby continuation of the existing Old Rod task and ordinary creation.</summary>
public static class SeviiRubyOldRodEncounterRules
{
    public const string RulesVersion = "firered-sevii-ruby-old-rod-v1";
    public static ImmutableArray<OldRodEncounterTable> SourceAdditions { get; } = SeviiRubyEncounterTables.FishingSource.Select(t => t with {Slots=t.Slots.Take(2).ToImmutableArray()}).ToImmutableArray();
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } =
        [.. VictoryRoadOldRodEncounterRules.Tables, .. SourceAdditions];

    public static ImmutableArray<MapId> MapsWithoutFishingMons { get; } = [
        new("map.mt-ember-exterior"),new("map.mt-ember-ruby-path-1f"),new("map.mt-ember-ruby-path-b1f"),
        new("map.mt-ember-ruby-path-b2f"),new("map.mt-ember-ruby-path-b3f"),new("map.mt-ember-ruby-path-b4f"),
        new("map.mt-ember-ruby-path-b5f"),new("map.mt-ember-ruby-path-b1f-stairs"),new("map.mt-ember-ruby-path-b2f-stairs"),
        new("map.one-island-kindle-road-ember-spa")];
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t=>t.Map==map) || MapsWithoutFishingMons.Contains(map) || VictoryRoadOldRodEncounterRules.IsAdmittedMap(map);

    public static OldRodFishingResult Start(SeviiRubyOwnedPokemonCatalog pokemon, OldRodCastContext context,
        RngState rng, bool cancelBeforeCast = false)
    {
        ValidatePokemon(pokemon);
        return OldRodFishingRules.StartCore(context, rng, cancelBeforeCast, cinnabarRules: true, firstSeviiRules: true, victoryRoadRules: true, seviiRubyRules: true);
    }

    public static OldRodFishingResult Advance(SeviiRubyOwnedPokemonCatalog pokemon, OldRodFishingState state,
        OldRodFrameInput input, RngState rng)
    {
        ValidatePokemon(pokemon);
        return OldRodFishingRules.AdvanceCore(state, input, rng, cinnabarRules: true, firstSeviiRules: true, victoryRoadRules: true, seviiRubyRules: true);
    }

    public static void ValidateState(SeviiRubyOwnedPokemonCatalog pokemon, OldRodFishingState state)
    {
        ValidatePokemon(pokemon);
        OldRodFishingRules.ValidateStateCore(state, cinnabarRules: true, firstSeviiRules: true, victoryRoadRules: true, seviiRubyRules: true);
    }

    public static void ValidateTable(OldRodEncounterTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var expected = Tables.SingleOrDefault(t => t.Map == table.Map);
        if (expected is null || table.Slots.IsDefault || table with { Slots = expected.Slots } != expected ||
            !table.Slots.SequenceEqual(expected.Slots))
            throw new ArgumentException("sevii_ruby_old_rod_table_source");
    }

    public static void ValidateCandidate(OldRodEncounterCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var table = Tables.SingleOrDefault(t => t.Map == candidate.Map);
        if (table is null || candidate.SlotIndex is < 0 or > 1)
            throw new ArgumentException("sevii_ruby_old_rod_candidate_scope");
        var slot = table.Slots[candidate.SlotIndex];
        if (candidate.Species != slot.Species || candidate.Level < slot.MinimumLevel || candidate.Level > slot.MaximumLevel)
            throw new ArgumentException("sevii_ruby_old_rod_candidate_source");
    }

    public static OldRodEncounterResult GenerateCandidate(SeviiRubyOwnedPokemonCatalog pokemon,
        OldRodEncounterTable table, OldRodFishingState fishing, RngState rng)
    {
        ValidateTable(table);
        ValidateState(pokemon, fishing);
        if (fishing.Status != OldRodFishingStatus.EncounterReady || fishing.Map != table.Map)
            throw new InvalidOperationException("The matching fishing task must finish successfully first.");
        return OldRodEncounterRules.GenerateCandidateCore(table, rng);
    }

    public static OldRodPokemonResult GeneratePokemon(SeviiRubyOwnedPokemonCatalog pokemon,
        OldRodEncounterCandidate candidate, RngState rng, uint playerTrainerId)
    {
        ValidatePokemon(pokemon);
        ValidateCandidate(candidate);
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unknown global RNG algorithm.");
        var result = Route1WildPokemonFactory.GenerateOrdinary(pokemon.Catalog, candidate.Species,
            candidate.Level, rng, playerTrainerId);
        return new(result.Pokemon, result.GlobalRng, result.RandomTrace);
    }

    private static void ValidatePokemon(SeviiRubyOwnedPokemonCatalog pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        if (!ReferenceEquals(SeviiRubyOwnedPokemonCatalog.For(pokemon.Catalog), pokemon))
            throw new ArgumentException("sevii_ruby_old_rod_owned_proof");
    }
}
