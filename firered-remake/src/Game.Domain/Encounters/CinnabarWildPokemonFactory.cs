using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.Encounters;

/// <summary>Source-owned115 generation and exact new-map held slots. Candidate production stays separate.</summary>
public static class CinnabarWildPokemonFactory
{
    public static ImmutableArray<SpeciesId> WildSpecies { get; } = [
        new("pret:SPECIES_DITTO"),
        new("pret:SPECIES_GRIMER"),
        new("pret:SPECIES_GROWLITHE"),
        new("pret:SPECIES_GYARADOS"),
        new("pret:SPECIES_HORSEA"),
        new("pret:SPECIES_KOFFING"),
        new("pret:SPECIES_KRABBY"),
        new("pret:SPECIES_MAGIKARP"),
        new("pret:SPECIES_PSYDUCK"),
        new("pret:SPECIES_RATICATE"),
        new("pret:SPECIES_RATTATA"),
        new("pret:SPECIES_SEADRA"),
        new("pret:SPECIES_SHELLDER"),
        new("pret:SPECIES_TANGELA"),
        new("pret:SPECIES_TENTACOOL"),
        new("pret:SPECIES_WEEZING")
    ];
    public static EarlyLandWildPokemonResult Generate(GamePokemonCatalog owned, EarlyLandEncounterTable table,
        EarlyLandEncounterState encounter, RngState rng, uint playerTrainerId)
    {
        if (owned is null || encounter.RulesVersion != EarlyLandEncounterRules.CinnabarRulesVersion) throw new ArgumentException("cinnabar_wild_profile");
        EarlyLandEncounterRules.ValidateState(encounter, table);
        var pending = encounter.Pending ?? throw new InvalidOperationException("cinnabar_wild_pending");
        if (!owned.Catalog.Species.ContainsKey(pending.Candidate.Species)) throw new NotSupportedException("cinnabar_wild_species");
        var result = Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, pending.Candidate.Species, pending.Candidate.Level, rng, playerTrainerId);
        return new(result.Pokemon, result.GlobalRng, result.RandomTrace);
    }
    public static EarlyLandWildHeldItemResult AssignHeldItem(SpeciesId species, RngState rng)
    {
        if (!WildSpecies.Contains(species)) throw new NotSupportedException("cinnabar_wild_held_species");
        var draw = Gen3Rng.Next(rng, "wild-battle.held-item", 100);
        return new(HeldItem(species,draw.Trace.Result), draw.State, [draw.Trace]);
    }
    public static string HeldItem(SpeciesId species, int roll)
    {
        if (!WildSpecies.Contains(species) || roll is < 0 or >= 100) throw new ArgumentException("cinnabar_wild_held_slot");
        return species.Value switch
        {
            "pret:SPECIES_DITTO" when roll >= 95 => "ITEM_METAL_POWDER",
            "pret:SPECIES_RATICATE" when roll >= 95 => "ITEM_SITRUS_BERRY",
            "pret:SPECIES_RATICATE" when roll >= 45 => "ITEM_ORAN_BERRY",
            "pret:SPECIES_GROWLITHE" when roll is >= 45 and < 95 => "ITEM_RAWST_BERRY",
            "pret:SPECIES_SHELLDER" when roll >= 95 => "ITEM_BIG_PEARL",
            "pret:SPECIES_SHELLDER" when roll >= 45 => "ITEM_PEARL",
            _ => "ITEM_NONE"
        };
    }
}
