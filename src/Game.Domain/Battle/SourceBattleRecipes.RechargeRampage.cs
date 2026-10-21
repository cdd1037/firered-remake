using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal static bool HasRechargeRampageRecipe(string effect) => effect is "EFFECT_RECHARGE" or "EFFECT_RAMPAGE";
    internal int RechargeDuration => 2;

    // BattleScript_EffectRampage and EffectRecharge share HitFromCritCalc, but have
    // different PP and SetMoveEffect paths. A missed continuation still runs ppreduce.
    internal void ExecuteRechargeRampage(PokemonCatalog catalog, BattlerSlot actor, PokemonMoveDefinition move, string purpose)
    {
        var target = Invocation!.Target;
        bool repeated = RampageLocked(actor) || UproarLocked(actor);
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
        var secondary = SecondaryEffect(actor, move, purpose, !effective, certain: move.Effect == "EFFECT_RECHARGE");
        if (secondary.Apply)
        {
            if (move.Effect == "EFFECT_RECHARGE")
                StartRechargeRampageLock(actor, move, WildBattleCalledLockKind.Recharge, RechargeDuration);
            else if (move.Effect == "EFFECT_UPROAR")
            {
                if (!UproarLocked(actor) && Get(actor).Pokemon.CurrentHp > 0)
                    StartRechargeRampageLock(actor, move, WildBattleCalledLockKind.Uproar, (Draw($"{purpose}.uproar-duration", 4) & 3) + 2);
            }
            else if (!RampageLocked(actor))
                StartRechargeRampageLock(actor, move, WildBattleCalledLockKind.Rampage, (Draw($"{purpose}.rampage-duration", 2) & 1) + 2);
        }
        ChargeFinish(catalog, actor, target, move, purpose, lost);
    }
}
