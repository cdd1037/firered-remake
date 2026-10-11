using Game.Domain.Ids;
using Game.Domain.Pokemon;
namespace Game.Domain.WildBattle;

public sealed record WildBattleYawnEvent(long Sequence, int Turn, WildBattleSide Side,
    int Before, int After, string Reason) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleSleepTalkEvent(long Sequence, int Turn, WildBattleSide Side,
    int CalledSlot, MoveId CalledMove) : WildBattleEvent(Sequence, Turn);

public static partial class WildBattleRules
{
    internal sealed partial class Execution
    {
        internal bool Snorlax => State.RulesVersion is SnorlaxRulesVersion or FuchsiaRulesVersion;
        // Source NO_PPDEDUCT and the persistent attack-canceller tracker belong to this atomic action.
        // They are intentionally neither serialized nor retained into the next selected action.
        private bool snorlaxCalledMove;
        internal void SnorlaxCancelSleepingLocks()
        {
            if (!Snorlax) return;
            foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
                if (WildBattleStatus.IsSleeping(Get(side).Status) && (State.RulesVersion != FuchsiaRulesVersion || Get(side).DigMoveSlot is not null || Get(side).RolloutTurnsRemaining > 0 || Get(side).RampageTurnsRemaining > 0)) CancelRoute3Rollout(side, "sleep");
        }
        internal void SnorlaxEndTurnItems()
        {
            if (Snorlax) VermilionHeldBerry(WildBattleSide.Player);
        }
        private void ChangeYawn(WildBattleSide side, int remaining, string reason)
        {
            int before = Get(side).YawnTurnsRemaining;
            if (before == remaining) return;
            Set(side, Get(side) with { YawnTurnsRemaining = remaining });
            Add(n => new WildBattleYawnEvent(n,State.Turn,side,before,remaining,reason));
        }
        private bool SnorlaxScript(PokemonCatalog catalog, WildBattleSide side, int slot, PokemonMoveDefinition move, string purpose)
        {
            if (move.Effect == "EFFECT_SNORE")
            {
                if (WildBattleStatus.IsSleeping(Get(side).Status)) return false;
                SpendEarlyPartyPp(side,Other(side),slot,move);
                Add(n => new WildBattleMoveFailedEvent(n,State.Turn,side,Other(side),move.Id,"user-awake"));
                return true;
            }
            if (move.Effect == "EFFECT_SLEEP_TALK")
            {
                SpendEarlyPartyPp(side,side,slot,move);
                if (!WildBattleStatus.IsSleeping(Get(side).Status))
                { Add(n => new WildBattleMoveFailedEvent(n,State.Turn,side,side,move.Id,"user-awake")); return true; }
                var mon = Get(side); var foe = Get(Other(side));
                bool Candidate(int i) => SnorlaxSleepTalkCandidate(catalog,mon,foe,i);
                if (!Enumerable.Range(0,4).Any(Candidate))
                { Add(n => new WildBattleMoveFailedEvent(n,State.Turn,side,side,move.Id,"no-callable-move")); return true; }
                int called;
                do { called = Draw($"{purpose}.sleep-talk-slot",4) & 3; } while (!Candidate(called));
                var selected = catalog.Moves[mon.Pokemon.Moves[called].Move];
                if (selected.Target == "MOVE_TARGET_SELECTED")
                    while ((Draw($"{purpose}.sleep-talk-target",2) & 1) == (int)side) { }
                Add(n => new WildBattleSleepTalkEvent(n,State.Turn,side,called,selected.Id));
                snorlaxCalledMove = true;
                try { ForestMove(catalog,side,called); }
                finally { snorlaxCalledMove = false; }
                // MOVEEND_LAST_MOVES retains gChosenMove; the called effect only changes resulting/landed move.
                if (Get(side).Pokemon.CurrentHp > 0) Set(side, Get(side) with { LastUsedMove = selected.Effect == "EFFECT_TRANSFORM" ? new("pret:MOVE_UNAVAILABLE") : move.Id, LastCalledMove = selected.Effect == "EFFECT_TRANSFORM" ? null : selected.Id });
                return true;
            }
            if (move.Effect != "EFFECT_YAWN") return false;
            Recipes.ExecuteSleepResidual(catalog, S(side), S(activeInvocation?.Target ?? Other(side)), slot, move);
            return true;
        }
        private void SnorlaxYawnResidual(WildBattleSide side) => new SinglesEndTurnBridge(this).YawnResidual(S(side));
    }
}
