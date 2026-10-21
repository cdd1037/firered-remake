using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Encounters;
public static class FirstSeviiWildPokemonFactory
{
    public static ImmutableArray<SpeciesId> WildSpecies { get; } = [
        new("pret:SPECIES_DROWZEE"),new("pret:SPECIES_EXEGGCUTE"),new("pret:SPECIES_GLOOM"),new("pret:SPECIES_GOLDEEN"),new("pret:SPECIES_GOLDUCK"),new("pret:SPECIES_GYARADOS"),new("pret:SPECIES_HORSEA"),new("pret:SPECIES_HYPNO"),new("pret:SPECIES_MAGIKARP"),new("pret:SPECIES_MEOWTH"),new("pret:SPECIES_ODDISH"),new("pret:SPECIES_PERSIAN"),new("pret:SPECIES_PIDGEOTTO"),new("pret:SPECIES_PIDGEY"),new("pret:SPECIES_POLIWAG"),new("pret:SPECIES_PSYDUCK"),new("pret:SPECIES_SEADRA"),new("pret:SPECIES_SEAKING"),new("pret:SPECIES_SHELLDER"),new("pret:SPECIES_TENTACOOL"),new("pret:SPECIES_TENTACRUEL"),new("pret:SPECIES_VENOMOTH"),new("pret:SPECIES_VENONAT")];
    public static string HeldItem(SpeciesId species,int roll)
    {
        if(!WildSpecies.Contains(species)||roll is <0 or >=100)throw new ArgumentException("first_sevii_wild_held_slot");
        return species.Value switch {
            "pret:SPECIES_MEOWTH" when roll >= 95 => "ITEM_NUGGET",
            "pret:SPECIES_SHELLDER" when roll >= 95 => "ITEM_BIG_PEARL",
            "pret:SPECIES_SHELLDER" when roll is >= 45 and < 95 => "ITEM_PEARL",
            "pret:SPECIES_VENOMOTH" when roll >= 95 => "ITEM_SILVER_POWDER",
            _=>"ITEM_NONE"
        };
    }
}
