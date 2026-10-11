using Game.Domain.Ids;

namespace Game.Domain.Safari;

/// <summary>
/// Exact flee data for the 22 species present in the pinned FireRed Safari land/water/fishing
/// encounter tables. This list admits the battle calculation, not encounters or field travel.
/// Catch rates still come from the authenticated 386-species SourceMechanicsCatalog.
/// Source: src/data/pokemon/species_info.h and src/data/wild_encounters.json.
/// </summary>
public static class SafariSpeciesRules
{
    public static int? FleeRate(SpeciesId species) => species.Value switch
    {
        "pret:SPECIES_CHANSEY" or "pret:SPECIES_DRAGONAIR" or "pret:SPECIES_KANGASKHAN"
            or "pret:SPECIES_SCYTHER" or "pret:SPECIES_TAUROS" => 125,
        "pret:SPECIES_DRATINI" => 100,
        "pret:SPECIES_EXEGGCUTE" or "pret:SPECIES_NIDORINA" or "pret:SPECIES_NIDORINO"
            or "pret:SPECIES_PARASECT" or "pret:SPECIES_RHYHORN" or "pret:SPECIES_SEAKING"
            or "pret:SPECIES_VENOMOTH" => 75,
        "pret:SPECIES_DODUO" or "pret:SPECIES_GOLDEEN" or "pret:SPECIES_NIDORAN_F"
            or "pret:SPECIES_NIDORAN_M" or "pret:SPECIES_PARAS" or "pret:SPECIES_POLIWAG"
            or "pret:SPECIES_PSYDUCK" or "pret:SPECIES_VENONAT" => 50,
        "pret:SPECIES_MAGIKARP" => 25,
        _ => null
    };
}
