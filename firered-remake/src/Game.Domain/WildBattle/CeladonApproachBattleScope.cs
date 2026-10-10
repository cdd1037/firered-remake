using Game.Domain.Pokemon;
using Game.Domain.Ids;
namespace Game.Domain.WildBattle;
/// <summary>The 94-species Celadon approach reuses authenticated Rock Tunnel singles effects and the source Sludge definition.
/// Species admission is independent of learnset completeness: unsupported moves still fault atomically.</summary>
internal static class CeladonApproachBattleScope
{
    internal static bool Supports(MoveId move) => RockTunnelBattleScope.Supports(move) || move.Value == "pret:MOVE_SLUDGE";
    internal static bool SupportsForCatalog(PokemonCatalog? catalog, MoveId move) => catalog?.Species.Count == 94 ? Supports(move) : RockTunnelBattleScope.Supports(move);
    internal static bool ValidDefinitionForCatalog(PokemonCatalog? catalog, PokemonMoveDefinition move) => catalog?.Species.Count == 94 ? ValidDefinition(move) : RockTunnelBattleScope.ValidDefinition(move);
    internal static bool ValidDefinition(PokemonMoveDefinition move) => RockTunnelBattleScope.ValidDefinition(move) ||
        move.Id.Value == "pret:MOVE_SLUDGE" && (move.SourceMoveId,move.Pp,move.Power,move.Accuracy,move.Type,move.Effect,move.Target,move.SecondaryEffectChance,move.Priority) ==
        (124,20,65,100,"TYPE_POISON","EFFECT_POISON_HIT","MOVE_TARGET_SELECTED",30,0) &&
        !move.Flags.IsDefault && move.Flags.Length == 2 && move.Flags.Contains("FLAG_PROTECT_AFFECTED") && move.Flags.Contains("FLAG_MIRROR_MOVE_AFFECTED");
    internal static bool SupportsSpecies(SpeciesId species) => PokemonFactory.CeladonApproachSupportedSpecies.Contains(species);
    internal static bool ValidSpecies(PokemonSpeciesDefinition s) => RockTunnelBattleScope.ValidSpecies(s) || s.SourceSpeciesId == 89 && s.Id.Value == "pret:SPECIES_MUK" && (s.Type1,s.Type2,s.Ability1,s.Ability2) == ("TYPE_POISON","TYPE_POISON","ABILITY_STENCH","ABILITY_STICKY_HOLD");
    internal static int SharedCatalogCount(PokemonCatalog? catalog) => catalog?.Species.Count == 94 ? 94 : 93;
}
