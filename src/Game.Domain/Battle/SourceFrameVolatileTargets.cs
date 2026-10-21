using Game.Domain.Ids;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal BattlerSlot? MeanLookSource(BattlerSlot target) => Frame.Get(target).Registers.MeanLookSource;
    internal void SetMeanLookSource(BattlerSlot target, BattlerSlot actor)
    {
        var cell = Frame.Get(target);
        Frame = Frame with { Slots = Frame.Slots.SetItem(target.Value, cell with { Registers = cell.Registers with { MeanLookSource = actor } }) };
        Add(n => new SourceTowerVolatileEvent(n, Turn, target, "mean-look", true, "move"));
    }
    internal void SetBindingSource(BattlerSlot target, BattlerSlot actor)
    {
        var cell = Frame.Get(target);
        Frame = Frame with { Slots = Frame.Slots.SetItem(target.Value, cell with { Registers = cell.Registers with { WrappedBy = actor } }) };
    }
    internal void ApplyBinding(BattlerSlot target, string purpose, MoveId move) => ApplySourceBinding(target, purpose, move);
}
