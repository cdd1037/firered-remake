using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace Game.Domain.Battle;

// Source status3 flags are derived from the one authoritative charging lock.
public static class SourceChargeRules
{
    public static WildBattleSemiInvulnerable SemiInvulnerable(MoveId? chargedMove) => chargedMove?.Value switch {
        "pret:MOVE_FLY" or "pret:MOVE_BOUNCE" => WildBattleSemiInvulnerable.Air,
        "pret:MOVE_DIG" => WildBattleSemiInvulnerable.Underground,
        "pret:MOVE_DIVE" => WildBattleSemiInvulnerable.Underwater, _ => WildBattleSemiInvulnerable.None };
    public static bool Misses(WildBattleSemiInvulnerable state, PokemonMoveDefinition move) => state switch {
        WildBattleSemiInvulnerable.Air => move.Id.Value is not ("pret:MOVE_GUST" or "pret:MOVE_TWISTER" or "pret:MOVE_THUNDER" or "pret:MOVE_SKY_UPPERCUT"),
        WildBattleSemiInvulnerable.Underground => move.Effect is not ("EFFECT_EARTHQUAKE" or "EFFECT_MAGNITUDE"),
        WildBattleSemiInvulnerable.Underwater => move.Id.Value is not ("pret:MOVE_SURF" or "pret:MOVE_WHIRLPOOL"), _ => false };
    public static int DamageMultiplier(WildBattleSemiInvulnerable state, PokemonMoveDefinition move) => state switch {
        WildBattleSemiInvulnerable.Air when move.Id.Value is "pret:MOVE_GUST" or "pret:MOVE_TWISTER" => 2,
        WildBattleSemiInvulnerable.Underground when move.Effect is "EFFECT_EARTHQUAKE" or "EFFECT_MAGNITUDE" => 2,
        WildBattleSemiInvulnerable.Underwater when move.Id.Value is "pret:MOVE_SURF" or "pret:MOVE_WHIRLPOOL" => 2, _ => 1 };
}
