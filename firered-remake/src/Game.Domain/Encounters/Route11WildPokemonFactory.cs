using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.Encounters;

/// <summary>New Route11 land candidates share the original nature/PID rejection and two-IV-draw generator.</summary>
public static class Route11WildPokemonFactory
{
    public const string RulesVersion = "firered-route11-wild-generation-v1";
    public const string EncounterRulesVersion = "firered-route11-walking-candidates-v1";
    public static EarlyLandWildPokemonResult Generate(PokemonCatalog catalog, EarlyLandEncounterTable table,
        EarlyLandEncounterState encounter, RngState globalRng, uint playerTrainerId)
    {
        EarlyLandEncounterRules.ValidateState(encounter, table);
        var pending = encounter.Pending ?? throw new InvalidOperationException("A verified Route11 land candidate is required.");
        PokemonFactory.ValidateCatalog(catalog);
        var expected = Route11LandEncounterTables.All.SingleOrDefault(t => t.Map == table.Map);
        if (catalog.Species.Count != 79 || encounter.RulesVersion != EncounterRulesVersion || expected is null ||
            table with { Slots = expected.Slots } != expected || !table.Slots.SequenceEqual(expected.Slots) ||
            !catalog.Species.ContainsKey(pending.Candidate.Species))
            throw new NotSupportedException("An authenticated Route11 catalog and exact land table are required.");
        if (globalRng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unknown global RNG algorithm.");
        var result = Route1WildPokemonFactory.GenerateOrdinary(catalog, pending.Candidate.Species,
            pending.Candidate.Level, globalRng, playerTrainerId);
        return new(result.Pokemon, result.GlobalRng, result.RandomTrace);
    }
    public static EarlyLandWildHeldItemResult AssignHeldItem(SpeciesId species, RngState globalRng)
    {
        if (!Route11LandEncounterTables.WildSpecies.Contains(species))
            throw new NotSupportedException("Only the five authenticated Route11 land species are admitted.");
        var draw = Gen3Rng.Next(globalRng, "wild-battle.held-item", 100);
        return new("ITEM_NONE", draw.State, [draw.Trace]);
    }
}
