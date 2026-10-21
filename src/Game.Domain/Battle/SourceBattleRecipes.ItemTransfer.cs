using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

// Ordinary Gen III battles and the authenticated local Trainer Tower exception.
internal sealed partial class FrameRecipeContext
{
    internal static bool HasItemTransferRecipe(string effect) => effect is "EFFECT_THIEF" or "EFFECT_TRICK" or "EFFECT_KNOCK_OFF" or "EFFECT_RECYCLE";
    private void SetKnockedOffItemCore(BattlerSlot slot, string item) =>
        Set(slot, Get(slot) with { HeldItem = "ITEM_NONE", KnockedOffItem = item, SourceChoiceMove = null });

    internal void ExecuteItemTransfer(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, int slot,
        PokemonMoveDefinition move, string purpose)
    {
        if (!HasItemTransferRecipe(move.Effect) || !SourceMoveDefinitionRules.IsValid(move))
            throw new SourceRecipeUnsupported("source_item_transfer_definition");
        if (move.Effect == "EFFECT_RECYCLE")
        {
            SpendEarlyPartyPp(actor, actor, slot, move);
            var used = UsedHeldItem(actor);
            var user = Get(actor);
            // Remake safeguard for BUG-08: Knock Off leaves Mail permanently attached.
            // A position's consumed-item receipt must not overwrite that Mail attachment.
            if (used is null || user.HeldItem != "ITEM_NONE" ||
                user.KnockedOffItem is { } knocked && PokemonStorageRules.IsMailItem(knocked))
            { ItemTransferFailure(actor, actor, move, "no-recyclable-item"); return; }
            Set(actor, Get(actor) with { HeldItem = used });
            SetUsedHeldItem(actor, null);
            // The source controller writes the permanent item even when Knock Off's bit remains set.
            WriteRecycledPartyItem(actor, used);
            // Successful recycle remains observable even when the party write is already synchronized.
            Add(n => new SourceHeldItemRecycledEvent(n, Turn, actor, move.Id, used));
            return;
        }
        if (move.Effect == "EFFECT_TRICK")
        {
            SpendEarlyPartyPp(actor, target, slot, move);
            if (SourceCalledSecondaryBlocked(target)) { ItemTransferFailure(actor, target, move, "substitute"); return; }
            if (!SourceGenericAccuracy(actor, target, move, purpose)) { ItemTransferFailure(actor, target, move, "missed"); return; }
            var user = Get(actor); var foe = Get(target);
            if (IsTrainerTower || actor.Side == BattleSide.Opponent || user.KnockedOffItem is not null || foe.KnockedOffItem is not null ||
                user.HeldItem == "ITEM_NONE" && foe.HeldItem == "ITEM_NONE" ||
                TransferForbiddenItem(user.HeldItem) || TransferForbiddenItem(foe.HeldItem))
            { ItemTransferFailure(actor, target, move, "item-swap-not-allowed"); return; }
            if (ItemStickyHold(actor, target, move)) return;
            Set(actor, user with { HeldItem = "ITEM_NONE", SourceChoiceMove = null });
            Set(target, foe with { HeldItem = user.HeldItem, SourceChoiceMove = null });
            if (foe.HeldItem != "ITEM_NONE") QueueChangedItem(actor, foe.HeldItem);
            WritePartyItem(actor, foe.HeldItem, "trick-party-write");
            WritePartyItem(target, user.HeldItem, "trick-party-write");
            // tryswapitems also succeeds for equal nonempty items and one-sided giving.
            Add(n => new SourceHeldItemsSwappedEvent(n, Turn, actor, target, move.Id, user.HeldItem, foe.HeldItem));
            return;
        }
        bool hit = SourceGenericAccuracy(actor, target, move, purpose);
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int damage = SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent);
        int adjusted = AdjustHeldDamage(target, move.Id, damage, purpose);
        int lost = SourceGenericApplyDamage(actor, target, move, adjusted, critical, percent);
        SourceGenericEffectiveness(catalog, actor, target, move);
        // Both scripts use the shared source command, including the 100% draw.
        var secondary = SecondaryEffect(actor, move, purpose, damage == 0);
        if (secondary.Apply && !SourceCalledSecondaryBlocked(target))
        {
            if (move.Effect == "EFFECT_THIEF") StealItem(actor, target, move);
            else if (Get(target).Pokemon.CurrentHp > 0 && Get(target).HeldItem != "ITEM_NONE" && !ItemStickyHold(actor, target, move))
            {
                string item = Get(target).HeldItem;
                SetKnockedOffItem(target, item);
                Add(n => new SourceHeldItemKnockedOffEvent(n, Turn, actor, target, move.Id, item));
            }
        }
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0 && SourceCalledHitReachedPokemon(target)) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, adjusted);
        ForestOutcome(true);
    }
    internal void WriteRecycledPartyItem(BattlerSlot actor, string item) => WritePartyItem(actor, item, "recycle-party-write");
    private void ItemTransferFailure(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string reason) =>
        Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, reason));
    private static bool TransferForbiddenItem(string item) => item == "ITEM_ENIGMA_BERRY" || PokemonStorageRules.IsMailItem(item);
    private bool ItemStickyHold(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        if (WildBattleEffective.Ability(Get(target)) != "ABILITY_STICKY_HOLD") return false;
        RecordAbility(target, "ABILITY_STICKY_HOLD");
        Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move.Id, "ABILITY_STICKY_HOLD")); return true;
    }
    private void StealItem(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        if (IsTrainerTower) return; // Cmd_seteffectwithchance: Tower precedes Sticky Hold and all theft mutation.
        var user = Get(actor); var foe = Get(target);
        if (actor.Side == BattleSide.Opponent || user.KnockedOffItem is not null) return;
        if (foe.HeldItem != "ITEM_NONE" && ItemStickyHold(actor, target, move)) return;
        if (user.HeldItem != "ITEM_NONE" || foe.HeldItem == "ITEM_NONE" || TransferForbiddenItem(foe.HeldItem)) return;
        Set(target, foe with { HeldItem = "ITEM_NONE", SourceChoiceMove = null });
        QueueChangedItem(actor, foe.HeldItem);
        WritePartyItem(actor, foe.HeldItem, "steal-party-write");
        WritePartyItem(target, "ITEM_NONE", "steal-party-write");
        Add(n => new SourceHeldItemStolenEvent(n, Turn, actor, target, move.Id, foe.HeldItem));
    }
}
