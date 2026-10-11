using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasRechargeRampageRecipe(string effect) => effect is "EFFECT_RECHARGE" or "EFFECT_RAMPAGE";
    internal virtual bool RampageLocked(BattlerSlot actor) => Get(actor).CalledTurnState?.Lock?.Kind == WildBattleCalledLockKind.Rampage;
    internal virtual int RechargeDuration => 2;
    internal abstract void StartRechargeRampageLock(BattlerSlot actor, PokemonMoveDefinition move, WildBattleCalledLockKind kind, int remaining);

    // BattleScript_EffectRampage and EffectRecharge share HitFromCritCalc, but have
    // different PP and SetMoveEffect paths. A missed continuation still runs ppreduce.
    internal void ExecuteRechargeRampage(PokemonCatalog catalog, BattlerSlot actor, PokemonMoveDefinition move, string purpose)
    {
        var target = Invocation!.Target;
        bool repeated = RampageLocked(actor);
        bool hit = ChargeAccuracy(actor, target, move, purpose);
        if (!repeated || !hit) ChargePp(actor, target, move, true);
        else RolloutAttackString(actor);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        if (Route11WaterAbsorb(actor, target, move) || SsAnneFlashFire(actor, target, move)) return;
        bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int damage = ChargeDamage(catalog, actor, target, move, critical, percent, purpose);
        int lost = SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
        ChargeReportType(catalog, actor, target, move);
        bool effective = !ChargeImmune(catalog, target, move);
        if (move.Effect == "EFFECT_RECHARGE" && effective)
            StartRechargeRampageLock(actor, move, WildBattleCalledLockKind.Recharge, RechargeDuration);
        else
        {
            // CERTAIN recharge skips this draw. Rampage still draws on continuation,
            // but only its first effective hit draws a new duration, after damage.
            int roll = Draw($"{purpose}.secondary-effect", 100) % 100;
            int chance = move.SecondaryEffectChance * (WildBattleEffective.Ability(Get(actor)) == "ABILITY_SERENE_GRACE" ? 2 : 1);
            if (move.Effect == "EFFECT_RAMPAGE" && effective && roll <= chance && !RampageLocked(actor))
                StartRechargeRampageLock(actor, move, WildBattleCalledLockKind.Rampage, (Draw($"{purpose}.rampage-duration", 2) & 1) + 2);
        }
        ChargeFinish(catalog, actor, target, move, purpose, lost);
    }
}
