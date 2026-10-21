using Game.Domain.WildBattle;
namespace Game.Domain.Battle;
internal sealed partial class FrameEndTurnContext
{
    internal void UproarResidual(BattlerSlot actor)
    {
        int remaining = UproarRemaining(actor);
        if (Get(actor).Pokemon.CurrentHp == 0 || remaining == 0) return;
        // DoBattlerEndTurnEffects wakes numeric battler IDs before ending this lock.
        for (int i = 0; i < BattlerCount; i++)
        {
            var slot = new BattlerSlot(i); var body = Get(slot);
            if (!WildBattleStatus.IsSleeping(body.Status) || WildBattleEffective.Ability(body) == "ABILITY_SOUNDPROOF") continue;
            ChangeStatus(slot, WildBattleStatus.None, "uproar-wake");
            if (Get(slot).Nightmare) Set(slot, Get(slot) with { Nightmare = false });
        }
        if (UproarUnable(actor) || remaining == 1) SetUproarRemaining(actor, 0, "uproar-ended");
        else SetUproarRemaining(actor, remaining - 1, "countdown");
    }
}
