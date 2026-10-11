using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>One slot-typed recipe implementation. Storage and action ownership are supplied by the
/// canonical kernel or the explicitly singles-only historical boundary bridge.</summary>
internal abstract partial class SourceRecipeContext : SourceStatRecipeContext
{
    internal virtual bool SourceDoubleBattle => false;
    internal virtual int SourceDefenderCount(BattlerSlot target) => 1;
    internal virtual bool SourcePlusMinusBoost(WildBattleCombatant actor)
    {
        string partnerAbility = WildBattleEffective.Ability(actor) switch {
            "ABILITY_PLUS" => "ABILITY_MINUS", "ABILITY_MINUS" => "ABILITY_PLUS", _ => "" };
        // ABILITY_ON_FIELD2 uses FIELD_SPORT's default all-body scan: no side/HP/absence test.
        return partnerAbility.Length != 0 && ActiveSlots.Any(s => WildBattleEffective.Ability(Get(s)) == partnerAbility);
    }
    internal virtual IEnumerable<BattlerSlot> SourceNumericTargets(BattlerSlot actor, BattlerSlot initialTarget) => [initialTarget];
    internal virtual void BeginScriptTarget(BattlerSlot target) { }
    internal virtual void CompleteScriptTarget() { }
    internal abstract SourceMoveInvocation? Invocation { get; }
    internal abstract IEnumerable<BattlerSlot> ActiveSlots { get; }
    internal abstract ushort Draw(string purpose, int bound = 65536);
    internal abstract int RainTurnsRemaining { get; }
    internal abstract int SandstormTurnsRemaining { get; }
    internal abstract bool Safeguard(BattlerSlot slot);
    internal abstract bool SourceCalledRain { get; }
    internal abstract bool SourceCalledSun { get; }
    internal abstract bool SourceCalledWeatherPresent { get; }
    internal abstract bool SourceCalledWeatherSuppressed { get; }
    internal virtual bool SourceDamageWeatherSuppressed => SourceCalledWeatherSuppressed;
    internal abstract bool RockTunnelReflects(BattlerSlot slot);
    internal abstract bool SourceCalledLightScreen(BattlerSlot slot);
    internal abstract bool SourceCalledHitReachedPokemon(BattlerSlot slot);
    internal abstract bool SourceCalledSafeguardBlocks(BattlerSlot actor, BattlerSlot target, MoveId move);
    internal abstract bool CalledSureHit(BattlerSlot actor, BattlerSlot target);
    internal abstract bool BillUnderground(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move);
    internal abstract void SpendEarlyPartyPp(BattlerSlot actor, BattlerSlot target, int slot, PokemonMoveDefinition move);
    internal abstract int SourceGenericApplyDamage(BattlerSlot actor, BattlerSlot target, MoveId move, int damage, bool critical, int percent);
    internal abstract bool Route11WaterAbsorb(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move);
    internal abstract bool SsAnneFlashFire(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move);
    internal abstract bool Route11Damp(BattlerSlot actor, PokemonMoveDefinition move);
    internal abstract void Route3ContactAbility(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose, int damage);
    internal abstract void ApplyConfusion(BattlerSlot target, string purpose, bool secondary);
    internal abstract void ApplyFlinch(BattlerSlot target);
    internal abstract void ApplyBinding(BattlerSlot target, string purpose, MoveId move);
    internal abstract void ChangeStage(BattlerSlot actor, BattlerSlot target, WildBattleStat stat, int amount, MoveId move, bool secondary = false);
    internal abstract void ChangeStatus(BattlerSlot target, uint status, string reason);
    internal abstract void ForestFaint(BattlerSlot slot);
    internal abstract void ForestOutcome(bool allowReplacement);
    internal abstract void MtMoonRageOnDamage(BattlerSlot actor, BattlerSlot target, MoveId move);
    internal abstract void SourceCalledMoveEndSubstitute();
    internal virtual void SourceMultiHitMoveEnd(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) => SourceCalledMoveEndSubstitute();
    internal virtual void SourceMultiHitFinalMoveEnd() { }
    internal abstract bool Endured(BattlerSlot slot);
    internal abstract bool EarlyStarterPrimaryMove(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move, string purpose);
    internal abstract bool EarlyPartyScript(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move, string purpose);
    internal abstract bool CeruleanScript(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move);
    internal abstract bool TowerScript(PokemonCatalog catalog, BattlerSlot actor, int slot, PokemonMoveDefinition move, string purpose);
}

internal sealed class SourceRecipeUnsupported(string code) : Exception(code);
