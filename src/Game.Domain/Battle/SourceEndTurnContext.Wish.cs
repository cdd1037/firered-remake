using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameEndTurnContext
{
    internal void HealWish(BattlerSlot slot, PokemonMoveDefinition move, int amount)
    {
        var body = Get(slot); int before = body.Pokemon.CurrentHp, after = Math.Min(body.Pokemon.Stats.MaxHp, before + amount);
        if (before == after)
        { Add(n => new SourceMoveFailedEvent(n, Turn, slot, slot, move.Id, "already-full-hp")); return; }
        Set(slot, body with { Pokemon = body.Pokemon with { CurrentHp = after } });
        Add(n => new SourceRecoveredEvent(n, Turn, slot, move.Id, before, after, after - before));
    }
    internal void WishResidual(PokemonCatalog catalog, BattlerSlot slot)
    {
        if (GetWish(slot) is not { } wish) return;
        // Unlike Future Sight, DoFieldEndTurnEffects does not skip source-absent slots.
        int after = wish.TurnsRemaining - 1;
        SetWish(slot, after == 0 ? null : wish with { TurnsRemaining = after });
        var move = catalog.Moves[new("pret:MOVE_WISH")];
        Add(n => new SourceCalledDelayedEvent(n, Turn, slot, move.Id, wish.TurnsRemaining, after,
            after == 0 ? "resolved" : "countdown", wish.SourcePartyIndex));
        // Gen III uses the replacement's maximum HP; only the displayed attribution
        // belongs to the member that originally used Wish.
        if (after == 0 && Get(slot).Pokemon.CurrentHp > 0)
            HealWish(slot, move, Math.Max(1, Get(slot).Pokemon.Stats.MaxHp / 2));
    }
}

internal sealed partial class FrameEndTurnContext
{
    internal WildBattleCalledWish? GetWish(BattlerSlot slot) => Frame.Get(slot).Position.Wish;
    internal void SetWish(BattlerSlot slot, WildBattleCalledWish? wish)
    {
        var cell = Frame.Get(slot);
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Position = cell.Position with { Wish = wish } }) };
    }
}
