using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal WildBattleCalledWish? DelayedWish(BattlerSlot slot) => Frame.Get(slot).Position.Wish;
    internal SourceFutureAttack? DelayedFuture(BattlerSlot slot) => Frame.Get(slot).Position.FutureAttack;
    internal int DelayedPartyIndex(BattlerSlot slot) => Frame.Get(slot).Member!.Value.Index;
    internal void SetDelayedWish(BattlerSlot slot, WildBattleCalledWish? wish)
    {
        var cell = Frame.Get(slot);
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Position = cell.Position with { Wish = wish } }) };
    }
    internal void SetDelayedFuture(BattlerSlot slot, SourceFutureAttack? future)
    {
        var cell = Frame.Get(slot);
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Position = cell.Position with { FutureAttack = future } }) };
    }
}
