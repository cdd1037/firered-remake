using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Domain.Pokemon;

/// <summary>Pure original GIVE→write transaction sharing the existing six-slot Mail owner.
/// Draft editing is transient: cancellation keeps the input Bag, held item and Mail state untouched.
/// Source: mail_data.c GiveMailToMon; party_menu.c Task_HandleSwitchItemsYesNoInput and both writing callbacks.</summary>
public static class SourceMailComposerRules
{
    public static SourceMailAuthor CreateAuthor(SourceEasyChatCatalog catalog, string name, uint trainerId)
    {
        var bytes = EncodeName(catalog, name);
        return new(name, bytes, [(byte)trainerId, (byte)(trainerId >> 8), (byte)(trainerId >> 16), (byte)(trainerId >> 24)]);
    }

    private static ImmutableArray<byte> EncodeName(SourceEasyChatCatalog catalog, string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 7) throw new ArgumentException("mail_author_name", nameof(name));
        var bytes = Enumerable.Repeat((byte)255, 8).ToArray();
        for (int index = 0; index < name.Length; index++)
        {
            if (!catalog.NameCharacters.TryGetValue(name[index].ToString(), out byte value))
                throw new ArgumentException("mail_author_character", nameof(name));
            bytes[index] = value;
        }
        // Original GiveMailToMon pads names shorter than six, then writes EOS. Spare byte is canonically EOS.
        for (int index = name.Length; index <= 5; index++) bytes[index] = 0;
        return bytes.ToImmutableArray();
    }

    public static ushort MailSpecies(SourceEasyChatCatalog catalog, int species, uint personality) =>
        species == catalog.UnownSpecies ? (ushort)(30000 + ((((personality & 0x3000000) >> 18) |
            ((personality & 0x30000) >> 12) | ((personality & 0x300) >> 6) | (personality & 3)) % 28)) : checked((ushort)species);

    /// <summary>Persisted/received syntax only: current progression never invalidates an existing letter.</summary>
    public static string? ValidateMessage(SourceEasyChatCatalog catalog, SourceMailMessage message)
    {
        if (SourceMailRules.ValidateMessage(message) is { } invalid) return invalid;
        if (SourceEasyChatRules.ValidateWords(catalog, message.Words) is { } words) return words;
        return message.Species is >= 30000 and < 30028 || catalog.Words.Values.Any(w =>
            (w.Group == catalog.SeenPokemonGroup || w.Group == catalog.NationalPokemonGroup) && w.Index == message.Species)
            ? null : "mail_species";
    }

    public static string? CheckGive(EarlyShopCatalog shop, InventoryState inventory, SourceMailState state,
        ImmutableArray<PartyPokemon> party, int partyIndex, ushort itemId, bool replaceConfirmed = false,
        PewterMachineCatalog? machines = null) =>
        PrepareInventory(shop, inventory, state, party, partyIndex, itemId, replaceConfirmed, machines).Error;

    public static SourceMailInventoryResult Give(EarlyShopCatalog shop, SourceEasyChatCatalog catalog,
        SourceEasyChatSelection selection, InventoryState inventory, SourceMailState state, ImmutableArray<PartyPokemon> party,
        int partyIndex, ushort itemId, ImmutableArray<ushort> words, SourceMailAuthor author, bool replaceConfirmed = false,
        PewterMachineCatalog? machines = null)
    {
        SourceMailInventoryResult No(string error) => new(state, party, inventory, false, error);
        var prepared = PrepareInventory(shop, inventory, state, party, partyIndex, itemId, replaceConfirmed, machines);
        if (!prepared.Succeeded) return No(prepared.Error!);
        if (SourceEasyChatRules.ValidateWords(catalog, words, requireNonempty: true) is { } invalid) return No(invalid);
        if (words.Any(w => w != SourceEasyChatRules.BlankWord && !SourceEasyChatRules.IsSelectable(catalog, selection, w)))
            return No("mail_word_locked");
        if (author is null || author.TrainerIdBytes.IsDefault || author.TrainerIdBytes.Length != 4) return No("mail_trainer_bytes");
        try
        {
            if (author.NameBytes.IsDefault || !author.NameBytes.SequenceEqual(EncodeName(catalog, author.Name))) return No("mail_author_bytes");
        }
        catch (ArgumentException) { return No("mail_author_name"); }
        var pokemon = party[partyIndex];
        if (!catalog.Words.Values.Any(w => (w.Group == catalog.SeenPokemonGroup || w.Group == catalog.NationalPokemonGroup) &&
            w.Index == pokemon.Pokemon.SourceSpeciesId)) return No("mail_species");
        var message = new SourceMailMessage(words, author.Name, author.NameBytes, author.TrainerIdBytes,
            MailSpecies(catalog, pokemon.Pokemon.SourceSpeciesId, pokemon.Pokemon.Personality), itemId);
        var attach = SourceMailRules.AttachReceived(state, party.SetItem(partyIndex, pokemon with { HeldItem = "ITEM_NONE" }), partyIndex, message);
        if (!attach.Succeeded) return No(attach.Error!);
        return new(attach.State, attach.Party, prepared.State, true, null);
    }

    private static InventoryTransactionResult PrepareInventory(EarlyShopCatalog shop, InventoryState inventory,
        SourceMailState state, ImmutableArray<PartyPokemon> party, int partyIndex, ushort itemId, bool replaceConfirmed,
        PewterMachineCatalog? machines)
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
        if (machines is not null && PewterMachineRules.Validate(machines, inventory) is { } machineError) return No(machineError);
        var ordinary = machines is null ? inventory : PewterMachineRules.WithoutMachines(inventory);
        var removed = EarlyInventoryAddRules.Remove(shop, ordinary, new ItemId("pret:" + symbol), 1);
        if (!removed.Succeeded) return No(removed.Error!);
        // Mail always belongs to POCKET_ITEMS. Keep the existing independently owned TM/CASE fields.
        var changed = inventory with { BagItems = removed.State.BagItems };
        if (pokemon.HeldItem == "ITEM_NONE") return new(changed, true, null);
        // Remove the new stationery first: its last stack may free the slot for the old held item.
        var returned = HeldItemTransferRules.Take(shop, changed, pokemon, machines);
        return returned.Succeeded ? new(returned.Inventory, true, null) : No(returned.Error!);
    }
}
