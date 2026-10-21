namespace Game.Domain.Battle;

internal sealed partial class FrameEndTurnContext
{
    internal void LockOnResidual(BattlerSlot slot)
    {
        int before = SureHitTurnsRemaining(slot);
        if (Get(slot).Pokemon.CurrentHp > 0 && before > 0) SetSureHitTurnsRemaining(slot, before - 1);
    }
}
