using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Encounters;

/// <summary>Authenticated ordinary Kanto Good Rod admission; task execution remains in the shared rod owner.</summary>
public static class GoodRodEncounterRules
{
    public const string RulesVersion = "firered-kanto-good-rod-v1";
    public static ImmutableArray<OldRodEncounterTable> Tables { get; } = [..GoodRodEncounterTables.Tables, ..SeafoamEncounterTables.GoodRod, ..CapeBrinkEncounterTables.GoodRod];
    public static ImmutableArray<MapId> MapsWithoutFishingMons => OldRodEncounterRules.MapsWithoutFishingMons;
    public static bool IsAdmittedMap(MapId map) => Tables.Any(t => t.Map == map) || MapsWithoutFishingMons.Contains(map);

    public static OldRodFishingResult Start(OwnedBattlePokemonCatalog owned, OldRodCastContext context,
        RngState rng, bool cancelBeforeCast = false)
    {
        ValidateOwned(owned);
        return OldRodFishingRules.StartCore(context, rng, cancelBeforeCast, cinnabarRules: false, goodRodRules: true);
    }

    public static OldRodFishingResult Advance(OwnedBattlePokemonCatalog owned, OldRodFishingState state,
        OldRodFrameInput input, RngState rng)
    {
        ValidateOwned(owned);
        return OldRodFishingRules.AdvanceCore(state, input, rng, cinnabarRules: false, goodRodRules: true);
    }

    public static void ValidateState(OwnedBattlePokemonCatalog owned, OldRodFishingState state)
    {
        ValidateOwned(owned);
        OldRodFishingRules.ValidateStateCore(state, cinnabarRules: false, goodRodRules: true);
    }

    public static void ValidateTable(OldRodEncounterTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var expected = Tables.SingleOrDefault(t => t.Map == table.Map);
        if (expected is null || table.Slots.IsDefault || table with { Slots = expected.Slots } != expected ||
            !table.Slots.SequenceEqual(expected.Slots)) throw new ArgumentException("good_rod_table_source");
    }

    public static void ValidateCandidate(OldRodEncounterCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ValidateOrigin(new(FishingRod.Good, candidate.Map, candidate.SlotIndex, candidate.Species, candidate.Level));
    }

    public static void ValidateOrigin(FishingEncounterOrigin origin)
    {
        if (origin is null || origin.Rod != FishingRod.Good || FishingBattleContract.SourceSlot(origin) is null)
            throw new ArgumentException("good_rod_candidate_source");
    }

    public static int ChooseSourceSlot(int roll)
    {
        if (roll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return roll < 60 ? 2 : roll < 80 ? 3 : 4;
    }

    public static OldRodEncounterResult GenerateCandidate(OwnedBattlePokemonCatalog owned,
        OldRodEncounterTable table, OldRodFishingState fishing, RngState rng)
    {
        ValidateTable(table);
        ValidateState(owned, fishing);
        if (fishing.Status != OldRodFishingStatus.EncounterReady || fishing.Map != table.Map)
            throw new InvalidOperationException("The matching Good Rod fishing task must finish successfully first.");
        var slotDraw = Gen3Rng.Next(rng, "fishing.slot", 100);
        var slot = table.Slots.Single(s => s.Index == ChooseSourceSlot(slotDraw.Trace.Result));
        int lo = Math.Min(slot.MinimumLevel, slot.MaximumLevel), hi = Math.Max(slot.MinimumLevel, slot.MaximumLevel);
        var levelDraw = Gen3Rng.Next(slotDraw.State, "fishing.level", hi - lo + 1);
        return new(new(table.Map, slot.Index, slot.Species, (byte)(lo + levelDraw.Trace.Result)),
            levelDraw.State, [slotDraw.Trace, levelDraw.Trace]);
    }

    public static OldRodPokemonResult GeneratePokemon(OwnedBattlePokemonCatalog owned,
        OldRodEncounterCandidate candidate, RngState rng, uint playerTrainerId)
    {
        ValidateOwned(owned);
        ValidateCandidate(candidate);
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unknown global RNG algorithm.");
        if (!owned.Catalog.Species.ContainsKey(candidate.Species)) throw new ArgumentException("good_rod_owned_species");
        var made = Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, candidate.Species, candidate.Level, rng, playerTrainerId);
        return new(made.Pokemon, made.GlobalRng, made.RandomTrace);
    }

    private static void ValidateOwned(OwnedBattlePokemonCatalog owned)
    {
        if (owned is null || !ReferenceEquals(OwnedBattlePokemonCatalog.For(owned.Catalog), owned) ||
            OwnedBattlePokemonCatalog.SourceRoot(owned) is null)
            throw new ArgumentException("good_rod_owned_proof");
    }
}
