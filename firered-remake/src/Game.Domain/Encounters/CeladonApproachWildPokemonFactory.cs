using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.Encounters;

/// <summary>New CeladonApproach land candidates share the original nature/PID rejection and two-IV-draw generator.</summary>
public static class CeladonApproachWildPokemonFactory
{
    public const string RulesVersion = "firered-celadon-approach-wild-generation-v1";
    public const string EncounterRulesVersion = "firered-celadon-approach-walking-candidates-v1";
    public static EarlyLandWildPokemonResult Generate(PokemonCatalog catalog, EarlyLandEncounterTable table,
        EarlyLandEncounterState encounter, RngState globalRng, uint playerTrainerId, bool celadonGymRules = false)
    {
        EarlyLandEncounterRules.ValidateState(encounter, table);
        var pending = encounter.Pending ?? throw new InvalidOperationException("A verified CeladonApproach land candidate is required.");
        PokemonFactory.ValidateCatalog(catalog);
        var expected = CeladonApproachLandEncounterTables.All.SingleOrDefault(t => t.Map == table.Map);
        if (catalog.Species.Count != (celadonGymRules ? 98 : 94) || encounter.RulesVersion != (celadonGymRules ? EarlyLandEncounterRules.CeladonGymRulesVersion : EncounterRulesVersion) || expected is null ||
            table with { Slots = expected.Slots } != expected || !table.Slots.SequenceEqual(expected.Slots) ||
            !catalog.Species.ContainsKey(pending.Candidate.Species))
            throw new NotSupportedException("An authenticated CeladonApproach catalog and exact land table are required.");
        if (globalRng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unknown global RNG algorithm.");
        var result = Route1WildPokemonFactory.GenerateOrdinary(catalog, pending.Candidate.Species,
            pending.Candidate.Level, globalRng, playerTrainerId);
        return new(result.Pokemon, result.GlobalRng, result.RandomTrace);
    }
    public static string HeldItem(SpeciesId species, int roll) => roll is < 0 or >= 100 ? throw new ArgumentOutOfRangeException(nameof(roll)) : species.Value switch
    {
        "pret:SPECIES_GROWLITHE" when roll is >= 45 and <= 94 => "ITEM_RAWST_BERRY",
        "pret:SPECIES_MEOWTH" when roll >= 95 => "ITEM_NUGGET",
        _ => "ITEM_NONE"
    };
    public static EarlyLandWildHeldItemResult AssignHeldItem(SpeciesId species, RngState globalRng)
    {
        if (!CeladonApproachLandEncounterTables.WildSpecies.Contains(species))
            throw new NotSupportedException("Only the five authenticated CeladonApproach land species are admitted.");
        var draw = Gen3Rng.Next(globalRng, "wild-battle.held-item", 100);
        return new(HeldItem(species, draw.Trace.Result), draw.State, [draw.Trace]);
    }
}
