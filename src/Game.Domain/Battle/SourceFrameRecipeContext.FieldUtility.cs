using Game.Domain.WildBattle;
namespace Game.Domain.Battle;
internal sealed partial class FrameRecipeContext
{
    internal override int PayDayMoney { get => Frame.Field.PayDayMoney; set => Frame = Frame with { Field = Frame.Field with { PayDayMoney = value } }; }
    internal override bool UproarLocked(BattlerSlot actor) => Frame.Get(actor).Registers.Turn?.Lock?.Kind == WildBattleCalledLockKind.Uproar;
    internal override void CancellerUproarWake(BattlerSlot actor) => SharedUproarWake(actor);
    internal override void ClearRapidSpinLinks(BattlerSlot actor)
    {
        var cell = Frame.Get(actor);
        Frame = Frame with { Slots = Frame.Slots.SetItem(actor.Value, cell with { Registers = cell.Registers with { WrappedBy = null, LeechSeedSource = null } }) };
    }
    internal override void TeleportEscape() => EndWildMoveEscape(new(invocation.Actor, invocation.CurrentMove));
    private void EndWildMoveEscape(SourceMoveEscapeReceipt? moveEscape)
    {
        // The four-action battle owner admits trainer battles only. Do not invent
        // wild-double termination, rewards or application callbacks here.
        if (Frame.Layout != SourceBattleLayout.Singles) throw new SourceRecipeUnsupported("source_four_action_battle_kind");
        Frame = SourceBattleOutcomeRules.SetOutcome(Frame, WildBattleOutcome.Escaped, SourceWildBattleOutcome.PlayerTeleported);
        Add(n => new SourceEndedEvent(n, Turn, WildBattleOutcome.Escaped) { MoveEscape = moveEscape });
    }
}
