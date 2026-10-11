using System.Collections.Immutable;

namespace Game.Domain.Battle;

// SpecialStatusesClear belongs to a completed action, not turn preparation or a
// per-hit callback. Delayed damage can legitimately retain these fields overnight.
internal static class SourceFrameSpecialStatuses
{
    internal static SourceBattleFrame Clear(SourceBattleFrame frame) => frame with {
        Slots = frame.Slots.Select(cell => cell.Position.FocusBanded || cell.Position.HitItemReceipt is not null
            ? cell with { Position = cell.Position with { FocusBanded = false, HitItemReceipt = null } }
            : cell).ToImmutableArray() };
}

internal sealed partial class FrameRecipeContext
{
    internal override bool FocusBanded(BattlerSlot slot) => Frame.Get(slot).Position.FocusBanded;
    internal override void SetFocusBanded(BattlerSlot slot)
    {
        var cell = Frame.Get(slot);
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value,
            cell with { Position = cell.Position with { FocusBanded = true } }) };
    }
}
