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
            Recipes.TakeDestinyBond(S(attacker));
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
            if (move.Effect == "EFFECT_MEAN_LOOK")
            { Recipes.ExecuteVolatileTarget(S(side), S(activeInvocation?.Target ?? targetSide), slot, move, purpose); return true; }
            if (move.Effect is "EFFECT_CURSE" or "EFFECT_NIGHTMARE")
            { Recipes.ExecuteSleepResidual(catalog, S(side), S(activeInvocation?.Target ?? targetSide), slot, move); return true; }
            bool self = move.Effect == "EFFECT_DESTINY_BOND";
            SpendEarlyPartyPp(side, self ? side : targetSide, slot, move);
            void Fail(string reason) => Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, self ? side : targetSide, move.Id, reason));
            var target = Get(targetSide);
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
                case "EFFECT_DESTINY_BOND":
                    Recipes.SetDestinyBond(S(side), true, "move");
                    break;
            }
            return true;
        }
        private void TowerResidual(WildBattleSide side)
        {
            // Historical scheduler retains its outcome boundary around the shared recipes.
            if (Get(side).Pokemon.CurrentHp == 0) return;
            var residuals = new SinglesEndTurnBridge(this);
            if (Get(side).Nightmare)
            {
                bool sleeping = WildBattleStatus.IsSleeping(Get(side).Status);
                residuals.NightmareResidual(S(side));
                if (sleeping) ForestOutcome(false);
            }
            if (State.Outcome == WildBattleOutcome.Ongoing && Get(side).Pokemon.CurrentHp > 0 && Get(side).Cursed)
            { residuals.CurseResidual(S(side)); ForestOutcome(false); }
        }
    }
}
