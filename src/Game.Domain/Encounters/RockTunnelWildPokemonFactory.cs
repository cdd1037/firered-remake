using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.Encounters;

/// <summary>New RockTunnel land candidates share the original nature/PID rejection and two-IV-draw generator.</summary>
public static class RockTunnelWildPokemonFactory
{
    public const string RulesVersion = "firered-rocktunnel-wild-generation-v1";
    public const string EncounterRulesVersion = "firered-rocktunnel-walking-candidates-v1";
    public static EarlyLandWildHeldItemResult AssignHeldItem(SpeciesId species, RngState globalRng)
    {
        if (!RockTunnelLandEncounterTables.WildSpecies.Contains(species))
            throw new NotSupportedException("Only the nine authenticated RockTunnel land species are admitted.");
        var draw = Gen3Rng.Next(globalRng, "wild-battle.held-item", 100);
        return new(species.Value == "pret:SPECIES_ONIX" && draw.Trace.Result >= 95 ? "ITEM_HARD_STONE" : "ITEM_NONE", draw.State, [draw.Trace]);
    }
}
