using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Domain.Pokemon;

/// <summary>One transient owned item removed from a holder. No origin/undo address and no second Bag.
/// A null cursor represents no active item even though the original animation keeps a stale numeric ID.</summary>
public sealed record PokemonStorageItemCursor(ItemId Item);

public enum PokemonStorageItemOutcome
{
    Rejected, Taken, Given, Switched, SentToBag, GivenFromBag, CursorSentToBag, BoxSelected, ExitAllowed
}

/// <summary>Rejections retain all exact input containers and the cursor; no item or creature is lost.</summary>
public sealed record PokemonStorageItemResult(PokemonStorageItemOutcome Outcome,
    ImmutableArray<PartyPokemon> Party, PokemonStorageState Storage, InventoryState Inventory,
    PokemonStorageItemCursor? Cursor, PokemonStorageDiagnostic? Diagnostic = null)
{
    public bool Succeeded => Outcome != PokemonStorageItemOutcome.Rejected;
}

/// <summary>Original MOVE ITEMS for admitted current items, from pret 037335f storage data/tasks/misc.
/// Creatures never leave their slots. Only their HeldItem field changes; HP, PP, identity and RNG do not.</summary>
public static class PokemonStorageItemRules
{
    public static PokemonStorageDiagnostic? Validate(CampaignPokemonContent? content, EarlyShopCatalog items,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, InventoryState inventory,
        PokemonStorageItemCursor? cursor, MachineCatalog? machines = null, long? nextInstanceId = null)
    {
        if (PokemonStorageValidator.Validate(content, party, storage, nextInstanceId) is {} error) return error;
        if (HeldItemTransferRules.ValidateInventory(items, inventory, machines) is {} invalid) return new(invalid);
        return cursor is not null && !HeldItemTransferRules.CanHold(items, cursor.Item, machines)
            ? new("held_item_not_holdable") : null;
    }

    public static PokemonStorageItemResult Take(CampaignPokemonContent? content, EarlyShopCatalog items,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, InventoryState inventory,
        PokemonStorageItemCursor? cursor, PokemonStorageMoveLocation source, long expectedInstanceId,
        MachineCatalog? machines = null)
    {
        var error = TargetError(content, items, party, storage, inventory, cursor, source, expectedInstanceId, machines);
        if (error is not null) return Reject(party, storage, inventory, cursor, error);
        if (cursor is not null) return Reject(party, storage, inventory, cursor, new("storage_item_already_holding"));
        var pokemon = At(party, storage, source)!;
        if (HeldError(items, pokemon, machines) is {} heldError) return Reject(party, storage, inventory, cursor, heldError);
        return Replace(PokemonStorageItemOutcome.Taken, party, storage, inventory,
            new(new("pret:" + pokemon.HeldItem)), source, pokemon with { HeldItem = "ITEM_NONE" });
    }

    public static PokemonStorageItemResult Give(CampaignPokemonContent? content, EarlyShopCatalog items,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, InventoryState inventory,
        PokemonStorageItemCursor? cursor, PokemonStorageMoveLocation target, long expectedInstanceId,
        ItemId expectedCursorItem, MachineCatalog? machines = null)
        => MoveToHolder(content, items, party, storage, inventory, cursor, target, expectedInstanceId, expectedCursorItem, false, machines);

    public static PokemonStorageItemResult Switch(CampaignPokemonContent? content, EarlyShopCatalog items,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, InventoryState inventory,
        PokemonStorageItemCursor? cursor, PokemonStorageMoveLocation target, long expectedInstanceId,
        ItemId expectedCursorItem, MachineCatalog? machines = null)
        => MoveToHolder(content, items, party, storage, inventory, cursor, target, expectedInstanceId, expectedCursorItem, true, machines);

    private static PokemonStorageItemResult MoveToHolder(CampaignPokemonContent? content, EarlyShopCatalog items,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, InventoryState inventory,
        PokemonStorageItemCursor? cursor, PokemonStorageMoveLocation target, long expectedInstanceId,
        ItemId expectedCursorItem, bool exchange, MachineCatalog? machines)
    {
        var error = TargetError(content, items, party, storage, inventory, cursor, target, expectedInstanceId, machines)
            ?? CursorError(cursor, expectedCursorItem);
        if (error is not null) return Reject(party, storage, inventory, cursor, error);
        var pokemon = At(party, storage, target)!;
        if (exchange)
        {
            if (HeldError(items, pokemon, machines) is {} heldError) return Reject(party, storage, inventory, cursor, heldError);
        }
        else if (pokemon.HeldItem != "ITEM_NONE")
            return Reject(party, storage, inventory, cursor, new("storage_item_slot_occupied"));
        return Replace(exchange ? PokemonStorageItemOutcome.Switched : PokemonStorageItemOutcome.Given,
            party, storage, inventory, exchange ? new(new("pret:" + pokemon.HeldItem)) : null,
            target, pokemon with { HeldItem = cursor!.Item.Value[5..] });
    }

    public static PokemonStorageItemResult ToBag(CampaignPokemonContent? content, EarlyShopCatalog items,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, InventoryState inventory,
        PokemonStorageItemCursor? cursor, PokemonStorageMoveLocation source, long expectedInstanceId,
        MachineCatalog? machines = null)
    {
        var error = TargetError(content, items, party, storage, inventory, cursor, source, expectedInstanceId, machines);
        if (error is not null) return Reject(party, storage, inventory, cursor, error);
        if (cursor is not null) return Reject(party, storage, inventory, cursor, new("storage_item_already_holding"));
        var transferred = HeldItemTransferRules.Take(items, inventory, At(party, storage, source)!, machines);
        return transferred.Succeeded
            ? Replace(PokemonStorageItemOutcome.SentToBag, party, storage, transferred.Inventory, null, source, transferred.Pokemon)
            : Reject(party, storage, inventory, cursor, new(transferred.Error!));
    }

    /// <summary>Bind the source GIVE2 Bag picker to an empty, non-egg, non-mail holder without mutation.</summary>
    public static PokemonStorageDiagnostic? CheckGiveFromBagTarget(CampaignPokemonContent? content, EarlyShopCatalog items,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, InventoryState inventory,
        PokemonStorageItemCursor? cursor, PokemonStorageMoveLocation target, long expectedInstanceId,
        MachineCatalog? machines = null)
    {
        var error = TargetError(content, items, party, storage, inventory, cursor, target, expectedInstanceId, machines);
        if (error is not null) return error;
        if (cursor is not null) return new("storage_item_already_holding");
        return At(party, storage, target)!.HeldItem != "ITEM_NONE" ? new("storage_item_slot_occupied") : null;
    }

    public static PokemonStorageItemResult GiveFromBag(CampaignPokemonContent? content, EarlyShopCatalog items,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, InventoryState inventory,
        PokemonStorageItemCursor? cursor, PokemonStorageMoveLocation target, long expectedInstanceId,
        ItemId item, MachineCatalog? machines = null)
    {
        var error = CheckGiveFromBagTarget(content, items, party, storage, inventory, cursor, target, expectedInstanceId, machines);
        if (error is not null) return Reject(party, storage, inventory, cursor, error);
        var pokemon = At(party, storage, target)!;
        // Source GIVE2 is offered only for a non-egg with no held item. Never overwrite or exchange here.
        var transferred = HeldItemTransferRules.Give(items, inventory, pokemon, item, false, false, machines);
        return transferred.Succeeded
            ? Replace(PokemonStorageItemOutcome.GivenFromBag, party, storage, transferred.Inventory, null, target, transferred.Pokemon)
            : Reject(party, storage, inventory, cursor, new(transferred.Error!));
    }

    /// <summary>Explicit Yes to the held-item exit prompt. Source returns to storage even on success;
    /// a later B/Close interaction is required to leave. No/B must leave the complete state untouched.</summary>
    public static PokemonStorageItemResult CursorToBag(CampaignPokemonContent? content, EarlyShopCatalog items,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, InventoryState inventory,
        PokemonStorageItemCursor? cursor, ItemId expectedCursorItem, MachineCatalog? machines = null)
    {
        var error = Validate(content, items, party, storage, inventory, cursor, machines) ?? CursorError(cursor, expectedCursorItem);
        if (error is not null) return Reject(party, storage, inventory, cursor, error);
        var added = HeldItemTransferRules.ReturnItemToBag(items, inventory, cursor!.Item, machines);
        return added.Succeeded ? new(PokemonStorageItemOutcome.CursorSentToBag, party, storage, added.State, null)
            : Reject(party, storage, inventory, cursor, new(added.Error!));
    }

    public static PokemonStorageItemResult SelectBox(CampaignPokemonContent? content, EarlyShopCatalog items,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, InventoryState inventory,
        PokemonStorageItemCursor? cursor, int boxIndex, MachineCatalog? machines = null)
    {
        if (Validate(content, items, party, storage, inventory, cursor, machines) is {} error)
            return Reject(party, storage, inventory, cursor, error);
        var selected = PokemonStorageRules.SelectBox(content, party, storage, boxIndex);
        return selected.Succeeded ? new(PokemonStorageItemOutcome.BoxSelected, party, selected.Storage, inventory, cursor)
            : Reject(party, storage, inventory, cursor, selected.Diagnostic!);
    }

    /// <summary>Leaving/disposal is forbidden with an active item. There is no implicit Bag fallback,
    /// return-to-origin, rollback, cursor discard or serialization. Resolve the item explicitly first.</summary>
    public static PokemonStorageItemResult CheckExit(CampaignPokemonContent? content, EarlyShopCatalog items,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, InventoryState inventory,
        PokemonStorageItemCursor? cursor, MachineCatalog? machines = null)
    {
        if (Validate(content, items, party, storage, inventory, cursor, machines) is {} error)
            return Reject(party, storage, inventory, cursor, error);
        return cursor is not null ? Reject(party, storage, inventory, cursor, new("storage_item_holding"))
            : new(PokemonStorageItemOutcome.ExitAllowed, party, storage, inventory, null);
    }

    private static PokemonStorageDiagnostic? TargetError(CampaignPokemonContent? content, EarlyShopCatalog items,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, InventoryState inventory,
        PokemonStorageItemCursor? cursor, PokemonStorageMoveLocation? target, long expectedInstanceId,
        MachineCatalog? machines)
    {
        if (Validate(content, items, party, storage, inventory, cursor, machines) is {} invalid) return invalid;
        if (target is null) return new("storage_item_location_missing");
        if (target.BoxIndex is < 0 or > PokemonStorageMoveLocation.PartyBox)
            return new("storage_box_index", BoxIndex: target.BoxIndex);
        if (target.SlotIndex < 0 || target.SlotIndex >= (target.IsParty ? 6 : PokemonStorageState.SlotsPerBox))
            return target.IsParty ? new("storage_party_index", PartyIndex: target.SlotIndex)
                : new("storage_slot_index", BoxIndex: target.BoxIndex, SlotIndex: target.SlotIndex);
        if (!target.IsParty && target.BoxIndex != storage.CurrentBox)
            return new("storage_item_box_not_selected", BoxIndex: target.BoxIndex);
        var pokemon = At(party, storage, target);
        if (pokemon is null) return new("storage_slot_empty");
        if (pokemon.InstanceId != expectedInstanceId) return new("storage_item_target_identity");
        return pokemon.IsEgg || pokemon.Mail != SourceMailState.NoMail || PokemonStorageRules.IsMailItem(pokemon.HeldItem)
            ? new("held_item_mail_or_egg_unsupported") : null;
    }

    private static PokemonStorageDiagnostic? HeldError(EarlyShopCatalog items, PartyPokemon pokemon, MachineCatalog? machines)
        => pokemon.HeldItem == "ITEM_NONE" ? new("held_item_none")
            : !HeldItemTransferRules.CanHold(items, new("pret:" + pokemon.HeldItem), machines) ? new("held_item_not_holdable") : null;
    private static PokemonStorageDiagnostic? CursorError(PokemonStorageItemCursor? cursor, ItemId expected)
        => cursor is null ? new("storage_item_not_holding")
            : cursor.Item != expected ? new("storage_item_cursor_identity") : null;
    private static PartyPokemon? At(ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveLocation location)
        => location.IsParty ? location.SlotIndex < party.Length ? party[location.SlotIndex] : null
            : storage.Boxes[location.BoxIndex].Slots[location.SlotIndex];
    private static PokemonStorageItemResult Replace(PokemonStorageItemOutcome outcome, ImmutableArray<PartyPokemon> party,
        PokemonStorageState storage, InventoryState inventory, PokemonStorageItemCursor? cursor,
        PokemonStorageMoveLocation target, PartyPokemon pokemon)
        => new(outcome, target.IsParty ? party.SetItem(target.SlotIndex, pokemon) : party,
            target.IsParty ? storage : PokemonStorageRules.Put(storage, new(target.BoxIndex, target.SlotIndex), pokemon), inventory, cursor);
    private static PokemonStorageItemResult Reject(ImmutableArray<PartyPokemon> party, PokemonStorageState storage,
        InventoryState inventory, PokemonStorageItemCursor? cursor, PokemonStorageDiagnostic error)
        => new(PokemonStorageItemOutcome.Rejected, party, storage, inventory, cursor, error);
}
