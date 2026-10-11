using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

public sealed record WildBattleTowerVolatileEvent(long Sequence, int Turn, WildBattleSide Side,
    string Effect, bool Applied, string Reason) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleSpiteEvent(long Sequence, int Turn, WildBattleSide Actor, WildBattleSide Target,
    MoveId Move, int Slot, int PpBefore, int PpAfter, int RolledReduction) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleTowerHpEvent(long Sequence, int Turn, WildBattleSide Side,
    string Effect, int HpBefore, int HpAfter, int Damage) : WildBattleEvent(Sequence, Turn);

public static partial class WildBattleRules
{
    private static bool HasTowerVolatiles(WildBattleCombatant? p) => p is not null && !TowerNeutral(p);
    private static bool TowerNeutral(WildBattleCombatant p) => !p.Cursed && !p.Nightmare && !p.DestinyBond && p.MeanLookSource is null;
    private static string? ValidateTowerVolatiles(WildBattleCombatant p, string side, bool tower)
    {
        if ((!tower || p.Pokemon?.CurrentHp == 0) && !TowerNeutral(p)) return $"wild_battle_{side}_tower_volatile_profile";
        if (p.Nightmare && !WildBattleStatus.IsSleeping(p.Status)) return $"wild_battle_{side}_nightmare_awake";
        if (p.MeanLookSource is { } source && (!Enum.IsDefined(source) || side == "player" && source != WildBattleSide.Opponent || side == "opponent" && source != WildBattleSide.Player)) return $"wild_battle_{side}_mean_look_source";
        return null;
    }

    internal sealed partial class Execution
    {
        public bool Tower => State.RulesVersion is TowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion;
        private readonly Dictionary<WildBattleSide, WildBattleSide> towerDestinyKnockouts = [];

        internal void ClearTowerDestinyForItem() { FuchsiaCancelPhysicalChain(WildBattleSide.Player, "non-move-action"); SetTowerFlag(WildBattleSide.Player, "destiny-bond", false, "non-move-action"); }
        private void SetTowerFlag(WildBattleSide side, string effect, bool value, string reason)
        {
            var mon = Get(side);
            bool before = effect switch { "curse" => mon.Cursed, "nightmare" => mon.Nightmare, "destiny-bond" => mon.DestinyBond, _ => throw new UnsupportedAction("tower_flag") };
            if (before == value) return;
            Set(side, effect switch { "curse" => mon with { Cursed = value }, "nightmare" => mon with { Nightmare = value }, _ => mon with { DestinyBond = value } });
            Add(n => new WildBattleTowerVolatileEvent(n, State.Turn, side, effect, value, reason));
        }
        private void SetMeanLook(WildBattleSide side, WildBattleSide? source, string reason)
        {
            if (Get(side).MeanLookSource == source) return;
            Set(side, Get(side) with { MeanLookSource = source });
            Add(n => new WildBattleTowerVolatileEvent(n, State.Turn, side, "mean-look", source is not null, reason));
        }
        private void ClearTowerVolatiles(WildBattleSide side, string reason)
        {
            SetTowerFlag(side, "curse", false, reason);
            SetTowerFlag(side, "nightmare", false, reason);
            SetTowerFlag(side, "destiny-bond", false, reason);
            SetMeanLook(side, null, reason);
            if (Get(Other(side)).MeanLookSource == side) SetMeanLook(Other(side), null, "source-left");
        }
        private void TowerHp(WildBattleSide side, string effect, int damage)
        {
            var mon = Get(side); int before = mon.Pokemon.CurrentHp, after = Math.Max(0, before - damage);
            Set(side, mon with { Pokemon = mon.Pokemon with { CurrentHp = after } });
            Add(n => new WildBattleTowerHpEvent(n, State.Turn, side, effect, before, after, damage));
            if (after == 0) ForestFaint(side);
        }
        // Only direct opposing move damage sets this marker. Poison, curse, recoil and confusion do not.
        private void TowerRecordKnockout(WildBattleHpChangedEvent hit)
        {
            if (hit.Actor != hit.Target && hit.HpBefore > 0 && hit.HpAfter == 0 && Get(hit.Target).DestinyBond)
                towerDestinyKnockouts[hit.Target] = hit.Actor;
        }
        private void TowerResolveDestinyBond(WildBattleSide fainted)
        {
            if (!towerDestinyKnockouts.Remove(fainted, out var attacker) || Get(attacker).Pokemon.CurrentHp == 0) return;
            TowerHp(attacker, "destiny-bond", Get(attacker).Pokemon.CurrentHp);
        }
        private bool TowerScript(PokemonCatalog catalog, WildBattleSide side, int slot, PokemonMoveDefinition move, string purpose)
        {
            var targetSide = Other(side);
            if (move.Effect == "EFFECT_DREAM_EATER")
            {
                // Dream Eater checks sleep before accuracy and uses the drain script, without a secondary roll.
                if (!WildBattleStatus.IsSleeping(Get(targetSide).Status))
                {
                    SpendEarlyPartyPp(side, targetSide, slot, move);
                    Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, targetSide, move.Id, "target-awake"));
                }
                else MtMoonDirectHit(catalog, side, slot, move, purpose);
                return true;
            }
            if (move.Effect is not ("EFFECT_SPITE" or "EFFECT_CURSE" or "EFFECT_MEAN_LOOK" or "EFFECT_NIGHTMARE" or "EFFECT_DESTINY_BOND")) return false;
            bool self = move.Effect == "EFFECT_DESTINY_BOND";
            var species = WildBattleEffective.Types(catalog, Get(side));
            bool ghost = species.Type1 == "TYPE_GHOST" || species.Type2 == "TYPE_GHOST";
            if (move.Effect == "EFFECT_CURSE" && !ghost) self = true;
            SpendEarlyPartyPp(side, self ? side : targetSide, slot, move);
            void Fail(string reason) => Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, self ? side : targetSide, move.Id, reason));
            var actor = Get(side); var target = Get(targetSide);
            switch (move.Effect)
            {
                case "EFFECT_SPITE":
                    if (!EarlyPartyAccuracy(side, targetSide, move, purpose, false)) { Fail("accuracy"); return true; }
                    int lastSlot = -1;
                    if (target.LastUsedMove is { } last && last != StruggleMove)
                        for (int i = 0; i < target.Pokemon.Moves.Length; i++) if (target.Pokemon.Moves[i].Move == last) { lastSlot = i; break; }
                    if (lastSlot < 0 || target.Pokemon.Moves[lastSlot].CurrentPp <= 1) { Fail("no-reducible-last-move"); return true; }
                    var pp = target.Pokemon.Moves[lastSlot];
                    int reduction = (Draw($"{purpose}.spite-pp", 4) & 3) + 2, after = Math.Max(0, pp.CurrentPp - reduction);
                    Set(targetSide, target with { Pokemon = target.Pokemon with { Moves = target.Pokemon.Moves.SetItem(lastSlot, pp with { CurrentPp = after }) } });
                    Add(n => new WildBattleSpiteEvent(n, State.Turn, side, targetSide, pp.Move, lastSlot, pp.CurrentPp, after, reduction));
                    if (after == 0) CancelRoute3Rollout(targetSide, "spite-empty-pp");
                    break;
                case "EFFECT_CURSE":
                    if (!ghost)
                    {
                        if (actor.SpeedStage == 0 && actor.AttackStage == 12 && actor.DefenseStage == 12) { Fail("stat-limits"); return true; }
                        ChangeStage(side, side, WildBattleStat.Speed, -1, move.Id);
                        ChangeStage(side, side, WildBattleStat.Attack, 1, move.Id);
                        ChangeStage(side, side, WildBattleStat.Defense, 1, move.Id);
                    }
                    else
                    {
                        // NO_ACC_CALC_CHECK_LOCK_ON still fails against the admitted semi-invulnerable Dig state.
                        if (target.DigMoveSlot is not null) { Fail("semi-invulnerable"); return true; }
                        if (target.Cursed) { Fail("already-cursed"); return true; }
                        SetTowerFlag(targetSide, "curse", true, "move");
                        SourceHitItemsRecord(side, move.Id, Math.Max(1, actor.Pokemon.Stats.MaxHp / 2), Math.Min(actor.Pokemon.CurrentHp, Math.Max(1, actor.Pokemon.Stats.MaxHp / 2)), substitute: false);
                        TowerHp(side, "curse-cost", Math.Max(1, actor.Pokemon.Stats.MaxHp / 2));
                        ForestOutcome(true);
                    }
                    break;
                case "EFFECT_MEAN_LOOK":
                    // NO_ACC_CALC: no accuracy RNG and no Normal/Ghost type immunity in Gen III.
                    if (target.DigMoveSlot is not null) { Fail("semi-invulnerable"); return true; }
                    if (target.MeanLookSource is not null) { Fail("already-trapped"); return true; }
                    SetMeanLook(targetSide, side, "move");
                    break;
                case "EFFECT_NIGHTMARE":
                    if (target.Nightmare || !WildBattleStatus.IsSleeping(target.Status)) { Fail(target.Nightmare ? "already-nightmare" : "target-awake"); return true; }
                    SetTowerFlag(targetSide, "nightmare", true, "move");
                    break;
                case "EFFECT_DESTINY_BOND":
                    SetTowerFlag(side, "destiny-bond", true, "move");
                    break;
            }
            return true;
        }
        private void TowerResidual(WildBattleSide side)
        {
            // Source turn-effects order: poison/burn, nightmare, curse, wrapping.
            if (Get(side).Pokemon.CurrentHp == 0) return;
            if (Get(side).Nightmare)
            {
                if (!WildBattleStatus.IsSleeping(Get(side).Status)) SetTowerFlag(side, "nightmare", false, "awake");
                else { TowerHp(side, "nightmare", Math.Max(1, Get(side).Pokemon.Stats.MaxHp / 4)); ForestOutcome(false); }
            }
            if (State.Outcome == WildBattleOutcome.Ongoing && Get(side).Pokemon.CurrentHp > 0 && Get(side).Cursed)
            { TowerHp(side, "curse", Math.Max(1, Get(side).Pokemon.Stats.MaxHp / 4)); ForestOutcome(false); }
        }
    }
}
