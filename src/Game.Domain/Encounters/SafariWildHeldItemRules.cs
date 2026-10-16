using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>Exact SetWildMonHeldItem slots for the15 authenticated Safari ground species.</summary>
public static class SafariWildHeldItemRules
{
    public const string SourceCommit = EarlyLandEncounterRules.SourceCommit;
    public static EarlyLandWildHeldItemResult Assign(SpeciesId species, RngState globalRng)
    {
        if (!SafariLandEncounterTables.WildSpecies.Contains(species))
            throw new NotSupportedException("Safari held-item admission is restricted to the ground table species.");
        // Source draws before comparing the slots, including NONE/NONE.
        var draw = Gen3Rng.Next(globalRng, "wild-battle.held-item", 100);
        var item = species.Value switch
        {
            "pret:SPECIES_PARAS" or "pret:SPECIES_PARASECT" when draw.Trace.Result >= 95 => "ITEM_BIG_MUSHROOM",
            "pret:SPECIES_PARAS" or "pret:SPECIES_PARASECT" when draw.Trace.Result >= 45 => "ITEM_TINY_MUSHROOM",
            "pret:SPECIES_CHANSEY" when draw.Trace.Result >= 95 => "ITEM_LUCKY_EGG",
            "pret:SPECIES_VENOMOTH" when draw.Trace.Result >= 95 => "ITEM_SILVER_POWDER",
            _ => "ITEM_NONE"
        };
        return new(item, draw.State, [draw.Trace]);
    }
}
