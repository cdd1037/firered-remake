using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal override WildBattleCalledWish? DelayedWish(BattlerSlot slot) => Frame.Get(slot).Position.Wish;
    internal override SourceFutureAttack? DelayedFuture(BattlerSlot slot) => Frame.Get(slot).Position.FutureAttack;
    internal override int DelayedPartyIndex(BattlerSlot slot) => Frame.Get(slot).Member!.Value.Index;
    internal override void SetDelayedWish(BattlerSlot slot, WildBattleCalledWish? wish)
    {
        var cell = Frame.Get(slot);
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Position = cell.Position with { Wish = wish } }) };
    }
    internal override void SetDelayedFuture(BattlerSlot slot, SourceFutureAttack? future)
    {
        var cell = Frame.Get(slot);
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Position = cell.Position with { FutureAttack = future } }) };
    }
}
