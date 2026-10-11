namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    // Preserve every numeric stored battler, independently of source absence/HP.
    internal override IEnumerable<BattlerSlot> TeeterDanceSlots(BattlerSlot initialTarget) =>
        Enumerable.Range(0, Frame.BattlerCount).Select(i => new BattlerSlot(i));
}
