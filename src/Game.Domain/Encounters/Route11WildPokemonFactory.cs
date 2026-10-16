using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.Encounters;

/// <summary>New Route11 land candidates share the original nature/PID rejection and two-IV-draw generator.</summary>
public static class Route11WildPokemonFactory
{
    public const string RulesVersion = "firered-route11-wild-generation-v1";
    public const string EncounterRulesVersion = "firered-route11-walking-candidates-v1";
    public static EarlyLandWildHeldItemResult AssignHeldItem(SpeciesId species, RngState globalRng)
    {
        if (!Route11LandEncounterTables.WildSpecies.Contains(species))
            throw new NotSupportedException("Only the five authenticated Route11 land species are admitted.");
        var draw = Gen3Rng.Next(globalRng, "wild-battle.held-item", 100);
        return new("ITEM_NONE", draw.State, [draw.Trace]);
    }
}
