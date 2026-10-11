using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

public sealed record WildBattleObedienceEvent(long Sequence, int Turn, WildBattleSide Actor, MoveId AttemptedMove,
    SourceObedienceDecision Decision) : WildBattleEvent(Sequence, Turn);

public static partial class WildBattleRules
{
    internal sealed partial class Execution
    {
        private bool SourceObedienceRedirectsAction(PokemonCatalog catalog, string purpose)
        {
            if (activeInvocation is null) return false;
            var decision = Recipes.CheckObedience(catalog, purpose);
            if (decision.Outcome == SourceObedienceOutcome.Obey) return false;
            if (decision.Outcome != SourceObedienceOutcome.AlternateMove)
            {
                invocationCommandNoEffect = decision.SourceReturn == 1;
                return true;
            }
            var current = CalledDefinition(catalog, catalog.Moves[activeInvocation.CurrentMove].SourceMoveId);
            var side = activeInvocation.Actor;
            var target = WildBattleSourceCommands.ResolveSinglesTarget(side, current.Target,
                () => Draw(purpose + ".obedience-target", 2));
            activeInvocation = activeInvocation with { Target = target };
            Add(n => new WildBattleCalledMoveTransitionEvent(n, State.Turn, activeInvocation));
            ForestMove(catalog, side, activeInvocation.PpSlot, current);
            // The supplied-caller diagnostic entry point must not append its old called move.
            invocationCancellerPassed = false;
            return true;
        }
        private sealed partial class SinglesRecipeBridge
        {
            internal override SourceObedienceContext? ObedienceContext => owner.State.Obedience;
            internal override PartyMemberKey ObedienceMember(BattlerSlot slot) => new(slot.Side,
                WildBattleSourcePartyEffects.ActiveIndex(owner.State.CalledParty!, L(slot)));
        }
    }
}
