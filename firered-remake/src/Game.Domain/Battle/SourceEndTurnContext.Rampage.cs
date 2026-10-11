using Game.Domain.WildBattle;
namespace Game.Domain.Battle;
internal abstract partial class SourceEndTurnContext
{
    internal virtual int RampageRemaining(BattlerSlot actor) => Get(actor).CalledTurnState?.Lock is { Kind: WildBattleCalledLockKind.Rampage } locked ? locked.TurnsRemaining : 0;
    internal virtual bool RampageUnable(BattlerSlot actor) => Get(actor).CalledTurnState?.Lock?.UnableToUseMove == true;
    internal abstract void SetRampageRemaining(BattlerSlot actor, int remaining, string reason);
    internal void RampageResidual(BattlerSlot actor)
    {
        int remaining = RampageRemaining(actor);
        if (Get(actor).Pokemon.CurrentHp == 0 || remaining == 0) return;
        if (RampageUnable(actor)) SetRampageRemaining(actor, 0, "rampage-interrupted");
        else if (remaining == 1)
        {
            SetRampageRemaining(actor, 0, "rampage-ended");
            var body = Get(actor);
            // SetMoveEffect(TRUE, 0) ignores Safeguard/Substitute/Shield Dust,
            // but existing confusion and Own Tempo suppress the duration draw.
            if (body.ConfusionTurnsRemaining != 0 || WildBattleEffective.Ability(body) == "ABILITY_OWN_TEMPO") return;
            int turns = (Draw(Purpose(actor, "confusion-duration"), 4) & 3) + 2;
            Set(actor, body with { ConfusionTurnsRemaining = turns });
            Add(n => new SourceConfusionChangedEvent(n, Turn, actor, 0, turns, "move"));
        }
        else SetRampageRemaining(actor, remaining - 1, "countdown");
    }
}
