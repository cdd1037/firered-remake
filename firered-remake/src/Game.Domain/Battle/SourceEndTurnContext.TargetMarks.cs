namespace Game.Domain.Battle;

internal abstract partial class SourceEndTurnContext
{
    internal abstract int SureHitTurnsRemaining(BattlerSlot slot);
    internal abstract void SetSureHitTurnsRemaining(BattlerSlot slot, int remaining);
    internal void LockOnResidual(BattlerSlot slot)
    {
        int before = SureHitTurnsRemaining(slot);
        if (Get(slot).Pokemon.CurrentHp > 0 && before > 0) SetSureHitTurnsRemaining(slot, before - 1);
    }
}
