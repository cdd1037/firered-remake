using Game.Domain.Ids;
namespace Game.Domain.Encounters;

/// <summary>New CeladonApproach land candidates share the original nature/PID rejection and two-IV-draw generator.</summary>
public static class CeladonApproachWildPokemonFactory
{
    public const string RulesVersion = "firered-celadon-approach-wild-generation-v1";
    public const string EncounterRulesVersion = "firered-celadon-approach-walking-candidates-v1";
    public static string HeldItem(SpeciesId species, int roll) => roll is < 0 or >= 100 ? throw new ArgumentOutOfRangeException(nameof(roll)) : species.Value switch
    {
        "pret:SPECIES_GROWLITHE" when roll is >= 45 and <= 94 => "ITEM_RAWST_BERRY",
        "pret:SPECIES_MEOWTH" when roll >= 95 => "ITEM_NUGGET",
        _ => "ITEM_NONE"
    };
}
