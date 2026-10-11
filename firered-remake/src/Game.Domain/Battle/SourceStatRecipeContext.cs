using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Shared stat command storage contract, also used by source entry scripts.</summary>
internal abstract class SourceStatRecipeContext
{
    internal abstract int Turn { get; }
    internal abstract WildBattleCombatant Get(BattlerSlot slot);
    internal abstract void Set(BattlerSlot slot, WildBattleCombatant body);
    internal abstract void Add(Func<long, SourceBattleEvent> create);
    internal abstract void RecordAbility(BattlerSlot slot, string ability);
    internal abstract bool SourceCalledMist(BattlerSlot slot);
    internal abstract bool SourceCalledSecondaryBlocked(BattlerSlot slot);
    internal abstract void SourceCalledProtectionFailure(BattlerSlot actor, BattlerSlot target, MoveId move, string reason);
        internal static int SourceStage(WildBattleCombatant mon, WildBattleStat stat) => stat switch
        {
            WildBattleStat.Attack => mon.AttackStage, WildBattleStat.Defense => mon.DefenseStage,
            WildBattleStat.Speed => mon.SpeedStage, WildBattleStat.SpecialAttack => 6 + mon.SpecialAttackStageDelta,
            WildBattleStat.SpecialDefense => 6 + mon.SpecialDefenseStageDelta,
            WildBattleStat.Accuracy => mon.AccuracyStage, _ => mon.EvasionStage
        };

        internal void SourceAssignStat(BattlerSlot side, BattlerSlot targetSide, WildBattleStat stat, int after)
        {
            var mon = Get(targetSide); int before = SourceStage(mon, stat);
            if (before == after) return;
            Set(targetSide, stat switch {
                WildBattleStat.Attack => mon with { AttackStage = after }, WildBattleStat.Defense => mon with { DefenseStage = after },
                WildBattleStat.Speed => mon with { SpeedStage = after }, WildBattleStat.SpecialAttack => mon with { SpecialAttackStageDelta = after - 6 },
                WildBattleStat.SpecialDefense => mon with { SpecialDefenseStageDelta = after - 6 },
                WildBattleStat.Accuracy => mon with { AccuracyStage = after }, _ => mon with { EvasionStage = after }
            });
            Add(n => new SourceStatChangedEvent(n, Turn, side, targetSide, stat, before, after));
        }

        internal void SourceChangeStat(BattlerSlot side, BattlerSlot target, WildBattleStat stat, int amount, MoveId move,
            bool secondary = false, bool certain = false)
        { bool shown = false; SourceChangeStat(side, target, stat, amount, move, ref shown, secondary, certain); }

        internal void SourceChangeStat(BattlerSlot side, BattlerSlot target, WildBattleStat stat, int amount, MoveId move,
            ref bool preventionShown, bool secondary = false, bool certain = false)
        {
            var mon = Get(target);
            if (secondary && (mon.Pokemon.CurrentHp == 0 || side != target && SourceCalledSecondaryBlocked(target))) return;
            if (amount < 0 && !certain && move.Value != "pret:MOVE_CURSE" && SourceCalledMist(target))
            {
                if (!secondary && !preventionShown)
                {
                    SourceCalledProtectionFailure(side, target, move, "mist");
                    preventionShown = true;
                }
                return;
            }
            string ability = WildBattleEffective.Ability(mon);
            if (amount < 0 && !certain && (ability is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE" ||
                ability == "ABILITY_KEEN_EYE" && stat == WildBattleStat.Accuracy ||
                ability == "ABILITY_HYPER_CUTTER" && stat == WildBattleStat.Attack))
            {
                if (!secondary && !preventionShown)
                {
                    RecordAbility(target, ability);
                    Add(n => new SourceAbilityPreventedEvent(n, Turn, side, target, move, ability));
                    // Clear Body and White Smoke report once for a multiple-stat script.
                    if (ability is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") preventionShown = true;
                }
                return;
            }
            if (amount < 0 && secondary && ability == "ABILITY_SHIELD_DUST") return;
            int before = SourceStage(mon, stat), after = Math.Clamp(before + amount, 0, 12);
            if (before == after)
            {
                if (!secondary)
                {
                    if (amount > 0) Add(n => new SourceStatAtMaximumEvent(n, Turn, side, target, stat));
                    else Add(n => new SourceStatAtMinimumEvent(n, Turn, side, target, stat));
                }
                return;
            }
            SourceAssignStat(side, target, stat, after);
        }
}
