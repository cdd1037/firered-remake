using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasBasicStatRecipe(string effect) => effect is
        "EFFECT_ATTACK_DOWN" or "EFFECT_DEFENSE_DOWN" or "EFFECT_SPEED_DOWN" or "EFFECT_ACCURACY_DOWN" or
        "EFFECT_DEFENSE_UP" or "EFFECT_SPECIAL_ATTACK_UP" or "EFFECT_SPEED_UP_2" or "EFFECT_EVASION_UP" or
        "EFFECT_ATTACK_UP_2" or "EFFECT_DEFENSE_DOWN_2" or "EFFECT_EVASION_DOWN" or
        "EFFECT_FOCUS_ENERGY" or "EFFECT_COSMIC_POWER";

    // BattleScript_EffectStatDown checks accuracy before attackstring/ppreduce. The
    // outer owner invokes this once per actual BOTH target and retains its PP markers.
    internal void ExecuteBasicStat(BattlerSlot actor, int slot, PokemonMoveDefinition move, string purpose, ref bool handled)
    {
        if (!HasBasicStatRecipe(move.Effect)) return;
        if (Invocation is null || !MetronomeSourceMoveDefinition.Matches(move))
            throw new SourceRecipeUnsupported("basic_stat_source_definition");
        handled = true;
        var target = move.Target == "MOVE_TARGET_USER" ? actor : Invocation.Target;
        bool hit = target == actor || SourceGenericAccuracy(actor, target, move, purpose);
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        ApplyBasicStat(actor, target, move);
    }

    // Historical regional adapters retain their admission and command positions,
    // then share this result script. Canonical owners use the same stat command.
    internal void ApplyBasicStat(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        if (move.Effect == "EFFECT_FOCUS_ENERGY")
        {
            if (Get(actor).FocusEnergy) SourceStatRecoveryFail(actor, actor, move, "already-focused");
            else { Set(actor, Get(actor) with { FocusEnergy = true }); Add(n => new SourceFocusEnergyEvent(n, Turn, actor)); }
            return;
        }
        if (move.Effect == "EFFECT_COSMIC_POWER")
        {
            var body = Get(actor);
            if (body.DefenseStage == 12 && body.SpecialDefenseStageDelta == 6)
                SourceStatRecoveryFail(actor, actor, move, "stats-at-maximum");
            else
            {
                // Cosmic Power skips each capped message while visiting DEF then SPDEF.
                if (body.DefenseStage < 12) BasicStatChange(actor, actor, WildBattleStat.Defense, 1, move);
                if (body.SpecialDefenseStageDelta < 6) BasicStatChange(actor, actor, WildBattleStat.SpecialDefense, 1, move);
            }
            return;
        }
        var (stat, amount) = move.Effect switch {
            "EFFECT_ATTACK_DOWN" => (WildBattleStat.Attack, -1),
            "EFFECT_DEFENSE_DOWN" => (WildBattleStat.Defense, -1),
            "EFFECT_SPEED_DOWN" => (WildBattleStat.Speed, -1),
            "EFFECT_ACCURACY_DOWN" => (WildBattleStat.Accuracy, -1),
            "EFFECT_EVASION_DOWN" => (WildBattleStat.Evasion, -1),
            "EFFECT_DEFENSE_DOWN_2" => (WildBattleStat.Defense, -2),
            "EFFECT_DEFENSE_UP" => (WildBattleStat.Defense, 1),
            "EFFECT_SPECIAL_ATTACK_UP" => (WildBattleStat.SpecialAttack, 1),
            "EFFECT_SPEED_UP_2" => (WildBattleStat.Speed, 2),
            "EFFECT_EVASION_UP" => (WildBattleStat.Evasion, 1),
            "EFFECT_ATTACK_UP_2" => (WildBattleStat.Attack, 2),
            _ => throw new SourceRecipeUnsupported("basic_stat_result_not_registered")
        };
        BasicStatChange(actor, amount > 0 ? actor : target, stat, amount, move);
    }
    internal virtual void BasicStatChange(BattlerSlot actor, BattlerSlot target, WildBattleStat stat, int amount, PokemonMoveDefinition move) =>
        SourceChangeStat(actor, target, stat, amount, move.Id);
}
