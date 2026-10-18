using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal static class SourceItemTransferRules
{
    // Validate the existing party and position owners before any cost, RNG or item mutation.
    internal static string? Validate(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        foreach (var party in frame.Parties)
            foreach (var member in party.Members)
                if (!SourceFrameBerryItems.SupportsHeldItem(member.HeldItem, catalog) ||
                    !SourceFrameBerryItems.SupportsHeldItem(WildBattleSourcePartyEffects.PersistentHeldItem(member), catalog) ||
                    member.KnockedOffItem is { } knocked && (knocked == "ITEM_NONE" || !SourceFrameBerryItems.SupportsHeldItem(knocked, catalog)))
                    return "source_item_party_receipt";
        foreach (var cell in frame.Slots.Take(frame.BattlerCount))
        {
            if (cell.Position.UsedHeldItem is { } used && (used == "ITEM_NONE" || !SourceHeldItemCatalog.Consumable(catalog, used) && used is not ("ITEM_ORAN_BERRY" or "ITEM_CHERI_BERRY" or "ITEM_CHESTO_BERRY" or "ITEM_PECHA_BERRY" or "ITEM_RAWST_BERRY" or "ITEM_ASPEAR_BERRY" or "ITEM_PERSIM_BERRY")))
                return "source_item_used_receipt";
            if (cell.Body?.SourceChoiceMove is { } chosen && chosen.Value is not ("pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE") && !catalog.Moves.ContainsKey(chosen))
                return "source_item_choice_receipt";
        }
        return null;
    }
}

internal sealed partial class FrameRecipeContext
{
    internal override bool IsTrainerTower => Frame.TrainerTowerEncounter?.Roster is not null;
    private readonly Dictionary<BattlerSlot, string> changedItems = [];
    internal override string? UsedHeldItem(BattlerSlot slot) => Frame.Get(slot).Position.UsedHeldItem;
    internal override void SetUsedHeldItem(BattlerSlot slot, string? item)
    {
        var cell = Frame.Get(slot);
        Frame = Frame with { Slots = Frame.Slots.SetItem(slot.Value, cell with { Position = cell.Position with { UsedHeldItem = item } }) };
    }
    internal override void QueueChangedItem(BattlerSlot slot, string item) => changedItems[slot] = item;
    internal override void WritePartyItem(BattlerSlot slot, string item, string reason)
    {
        var key = Frame.Get(slot).Member!.Value; var party = Frame.Party(key.Side); var before = party.Members[key.Index];
        var after = WildBattleSourcePartyEffects.WithPersistentHeldItem(before, item);
        Frame = Frame with { Parties = Frame.Parties.SetItem((int)key.Side, party with { Members = party.Members.SetItem(key.Index, after) }) };
        if (!WildBattleSourcePartyEffects.SameMember(before, after))
            Add(n => new SourceCalledPartyChangedEvent(n, Turn, slot.Side, key.Index, reason, before, after) {
                SourceSlot = reason is "trick-party-write" or "recycle-party-write" or "changed-item-applied" ? slot : null });
    }
    internal override void SetKnockedOffItem(BattlerSlot slot, string item)
    {
        var key = Frame.Get(slot).Member!.Value;
        string persistent = WildBattleSourcePartyEffects.PersistentHeldItem(Frame.Party(key.Side).Members[key.Index]);
        base.SetKnockedOffItem(slot, item);
        WritePartyItem(slot, persistent, "knock-off-battle-only");
    }
    private void FinishChangedItems()
    {
        // Source snapshots the old hold effect before installing changedItems. Receiving
        // Choice Band through Thief/Trick cannot lock the move that just acquired it.
        Set(invocation.Actor, SourceBattleHeldItems.FinishChoice(Get(invocation.Actor), invocation.ChosenMove,
            invocation.Flags.HasFlag(WildBattleInvocationFlags.Obeys),
            Events.Skip(targetEventStart).OfType<SourceMoveFailedEvent>().Any()));
        foreach (var slot in ActiveSlots.OrderBy(s => s.Value))
            if (changedItems.Remove(slot, out var item))
            {
                Set(slot, Get(slot) with { HeldItem = item });
                // Set updates live storage. Clear an obsolete temporary permanent override.
                WritePartyItem(slot, item, "changed-item-applied");
            }
    }
}
