using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal void SubstituteClearsBinding(BattlerSlot actor)
    {
        var cell = Frame.Get(actor);
        // The frame encodes WrappedBy only while wrapping is live. Source's stale
        // unobserved byte is not a live reference after STATUS2_WRAPPED is removed.
        Frame = Frame with { Slots = Frame.Slots.SetItem(actor.Value, cell with {
            Registers = cell.Registers with { WrappedBy = null } }) };
    }
    private void ExecuteFrameSubstitute()
    {
        var actor = invocation.Actor;
        int before = Get(invocation.PpOwner).Pokemon.Moves[invocation.PpSlot].CurrentPp;
        // BattleScript_EffectSubstitute deliberately reverses the normal print/PP order.
        SpendFramePp(actor, actor, move, attackString: false);
        if ((invocation.Flags & (WildBattleInvocationFlags.AttackStringPrinted | WildBattleInvocationFlags.NoAttackString)) == 0)
        {
            Add(n => new SourceAttackStringEvent(n, Turn, actor, invocation.ChosenMove, move.Id));
            invocation = invocation with { Flags = invocation.Flags | WildBattleInvocationFlags.AttackStringPrinted };
            Add(n => new SourceMoveUsedEvent(n, Turn, actor, actor, invocation.ChosenSlot, move.Id,
                before, Get(invocation.PpOwner).Pokemon.Moves[invocation.PpSlot].CurrentPp));
        }
        ApplySubstitute(actor, move);
    }
}
