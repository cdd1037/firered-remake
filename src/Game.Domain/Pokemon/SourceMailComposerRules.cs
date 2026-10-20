using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Domain.Pokemon;

/// <summary>Pure original GIVE→write transaction sharing the existing six-slot Mail owner.
/// Draft editing is transient: cancellation keeps the input Bag, held item and Mail state untouched.
/// Source: mail_data.c GiveMailToMon; party_menu.c Task_HandleSwitchItemsYesNoInput and both writing callbacks.</summary>
public enum MailStationeryOrigin { Bag, Pc }

public static class SourceMailComposerRules
{
    public static SourceMailAuthor CreateAuthor(string name, uint trainerId)
    {
        if (!NameTextRules.IsValidTrainerName(name)) throw new ArgumentException("mail_author_name", nameof(name));
        return new(name, trainerId);
    }

    public const int UnownMailSpeciesBase = 30000;
    public static bool IsEncodedUnownSpecies(int species) => species >= UnownMailSpeciesBase && species < UnownMailSpeciesBase + UnownFormRules.FormCount;

    public static ushort MailSpecies(SourceEasyChatCatalog catalog, int species, uint personality) =>
        species == catalog.UnownSpecies ? (ushort)(UnownMailSpeciesBase + UnownFormRules.Form(personality)) : checked((ushort)species);

    /// <summary>Persisted/received syntax only: current progression never invalidates an existing letter.</summary>
    public static string? ValidateMessage(SourceEasyChatCatalog catalog, SourceMailMessage message)
    {
        if (SourceMailRules.ValidateMessage(message) is { } invalid) return invalid;
        if (SourceEasyChatRules.ValidateWords(catalog, message.Words) is { } words) return words;
        return IsEncodedUnownSpecies(message.Species) || catalog.Words.Values.Any(w =>
            (w.Group == catalog.SeenPokemonGroup || w.Group == catalog.NationalPokemonGroup) && w.Index == message.Species)
            ? null : "mail_species";
    }

    public static string? CheckGive(EarlyShopCatalog shop, InventoryState inventory, SourceMailState state,
        ImmutableArray<PartyPokemon> party, int partyIndex, ushort itemId, bool replaceConfirmed = false,
        MachineCatalog? machines = null, MailStationeryOrigin origin = MailStationeryOrigin.Bag) =>
        PrepareInventory(shop, inventory, state, party, partyIndex, itemId, replaceConfirmed, machines, origin).Error;

    public static SourceMailInventoryResult Give(EarlyShopCatalog shop, SourceEasyChatCatalog catalog,
        SourceEasyChatSelection selection, InventoryState inventory, SourceMailState state, ImmutableArray<PartyPokemon> party,
        int partyIndex, ushort itemId, ImmutableArray<ushort> words, SourceMailAuthor author, bool replaceConfirmed = false,
        MachineCatalog? machines = null, MailStationeryOrigin origin = MailStationeryOrigin.Bag)
    {
        SourceMailInventoryResult No(string error) => new(state, party, inventory, false, error);
        var prepared = PrepareInventory(shop, inventory, state, party, partyIndex, itemId, replaceConfirmed, machines, origin);
        if (!prepared.Succeeded) return No(prepared.Error!);
        if (SourceEasyChatRules.ValidateWords(catalog, words, requireNonempty: true) is { } invalid) return No(invalid);
        if (words.Any(w => w != SourceEasyChatRules.BlankWord && !SourceEasyChatRules.IsSelectable(catalog, selection, w)))
            return No("mail_word_locked");
        if (author is null || !NameTextRules.IsValidTrainerName(author.Name)) return No("mail_author_name");
        var pokemon = party[partyIndex];
        if (!catalog.Words.Values.Any(w => (w.Group == catalog.SeenPokemonGroup || w.Group == catalog.NationalPokemonGroup) &&
            w.Index == pokemon.Pokemon.SourceSpeciesId)) return No("mail_species");
        var message = new SourceMailMessage(words, author.Name, author.TrainerId,
            MailSpecies(catalog, pokemon.Pokemon.SourceSpeciesId, pokemon.Pokemon.Personality), itemId, SourceMailOrigin.PlayerAuthored);
        var attach = SourceMailRules.AttachReceived(state, party.SetItem(partyIndex, pokemon with { HeldItem = "ITEM_NONE" }), partyIndex, message);
        if (!attach.Succeeded) return No(attach.Error!);
        return new(attach.State, attach.Party, prepared.State, true, null);
    }

    private static InventoryTransactionResult PrepareInventory(EarlyShopCatalog shop, InventoryState inventory,
        SourceMailState state, ImmutableArray<PartyPokemon> party, int partyIndex, ushort itemId, bool replaceConfirmed,
        MachineCatalog? machines, MailStationeryOrigin origin)
    {
        InventoryTransactionResult No(string error) => new(inventory, false, error);
        if (SourceMailRules.Validate(state, party) is { } invalid) return No(invalid);
        if (partyIndex < 0 || partyIndex >= party.Length) return No("mail_party_index");
        if (SourceMailRules.ItemSymbol(itemId) is not { } symbol) return No("mail_item");
        var pokemon = party[partyIndex];
        if (pokemon.IsEgg) return No("mail_egg");
        if (pokemon.Mail != SourceMailState.NoMail) return No("mail_target_holding_mail");
        if (pokemon.HeldItem != "ITEM_NONE" && !replaceConfirmed) return No("mail_replace_confirmation_required");
        if (state.Attached.All(m => m is not null)) return No("mail_attached_full");
        if (machines is not null && MachineRules.Validate(machines, inventory) is { } machineError) return No(machineError);
        if (HeldItemTransferRules.ValidateInventory(shop, inventory, machines) is {} inventoryError) return No(inventoryError);
        var item = new ItemId("pret:" + symbol);
        InventoryState changed;
        if (origin == MailStationeryOrigin.Pc)
        {
            int count = OpeningInventoryRules.Quantity(inventory.PcItems, item);
            if (count < 1) return No("inventory_insufficient_quantity");
            changed = inventory with { PcItems = OpeningInventoryRules.SetQuantity(inventory.PcItems, item, count - 1) };
        }
        else if (origin == MailStationeryOrigin.Bag)
        {
            var removed = EarlyInventoryAddRules.Remove(shop, inventory, item, 1);
            if (!removed.Succeeded) return No(removed.Error!);
            changed = removed.State;
        }
        else return No("mail_stationery_origin");
        if (pokemon.HeldItem == "ITEM_NONE") return new(changed, true, null);
        // Remove the new stationery first: its last stack may free the slot for the old held item.
        var returned = HeldItemTransferRules.Take(shop, changed, pokemon, machines);
        return returned.Succeeded ? new(returned.Inventory, true, null) : No(returned.Error!);
    }
}
