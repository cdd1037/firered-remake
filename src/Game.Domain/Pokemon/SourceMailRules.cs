using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Domain.Pokemon;

/// <summary>Bounded, pure received-Mail lifecycle. Source: pret/pokefirered 037335f,
/// mail_data.c:41-72,100-165; party_menu.c:3664-3727,5625-5649; player_pc.c:422-450,630-648.
/// Content creation/composition, UI confirmation and authoritative state commits belong to callers.</summary>
public static partial class SourceMailRules
{
    /// <summary>Attach an existing message to an empty-handed party member. This is not a composer.</summary>
    public static SourceMailResult AttachReceived(SourceMailState state, ImmutableArray<PartyPokemon> party,
        int partyIndex, SourceMailMessage message)
    {
        SourceMailResult No(string error) => new(state, party, false, error);
        if (Validate(state, party) is { } invalid) return No(invalid);
        if (!PartyIndex(party, partyIndex)) return No("mail_party_index");
        if (ValidateMessage(message) is { } malformed) return No(malformed);
        var pokemon = party[partyIndex];
        if (pokemon.IsEgg) return No("mail_egg");
        if (pokemon.HeldItem != "ITEM_NONE") return No("mail_target_holding_item");
        int slot = FirstFree(state.Attached);
        if (slot < 0) return No("mail_attached_full");
        return new(state with { Attached = state.Attached.SetItem(slot, message) },
            party.SetItem(partyIndex, Attach(pokemon, message, slot)), true, null, AttachedSlot: slot);
    }

    /// <summary>Receive a Pokemon and optional attached payload. Foreign source slot indexes are
    /// replaced by the local first free index, never interpreted as local references.</summary>
    public static SourceMailResult ReceivePartyPokemon(SourceMailState state, ImmutableArray<PartyPokemon> party,
        PartyPokemon incoming, SourceMailMessage? incomingMail = null)
    {
        SourceMailResult No(string error) => new(state, party, false, error);
        if (Validate(state, party) is { } invalid) return No(invalid);
        if (ValidateIncoming(party, incoming, incomingMail) is { } malformed) return No(malformed);
        if (party.Length >= SourceMailState.AttachedCapacity) return No("mail_party_full");
        int slot = incomingMail is null ? -1 : FirstFree(state.Attached);
        if (incomingMail is not null && slot < 0) return No("mail_attached_full");
        var changed = incomingMail is null ? state : state with { Attached = state.Attached.SetItem(slot, incomingMail) };
        var pokemon = incomingMail is null ? incoming : Attach(incoming, incomingMail, slot);
        return new(changed, party.Add(pokemon), true, null, AttachedSlot: slot < 0 ? null : slot);
    }

    /// <summary>Remove the departing Pokemon and its attached record together. Remaining party
    /// entries compact while their mail references remain unchanged. No message goes to the Bag.</summary>
    public static SourceMailResult RemoveOutgoing(SourceMailState state, ImmutableArray<PartyPokemon> party, int partyIndex)
    {
        SourceMailResult No(string error) => new(state, party, false, error);
        if (Validate(state, party) is { } invalid) return No(invalid);
        if (!PartyIndex(party, partyIndex)) return No("mail_party_index");
        int slot = party[partyIndex].Mail;
        var message = slot == SourceMailState.NoMail ? null : state.Attached[slot];
        var changed = message is null ? state : state with { Attached = state.Attached.SetItem(slot, null) };
        return new(changed, party.RemoveAt(partyIndex), true, null, OutgoingMail: message);
    }

    /// <summary>Atomic trade exchange: first free the outgoing record, then allocate the incoming
    /// record using the remaining local slots. Failed validation/allocation returns all inputs intact.</summary>
    public static SourceMailResult ExchangePartyPokemon(SourceMailState state, ImmutableArray<PartyPokemon> party,
        int partyIndex, PartyPokemon incoming, SourceMailMessage? incomingMail = null)
    {
        SourceMailResult No(string error) => new(state, party, false, error);
        if (Validate(state, party) is { } invalid) return No(invalid);
        if (!PartyIndex(party, partyIndex)) return No("mail_party_index");
        if (ValidateIncoming(party, incoming, incomingMail) is { } malformed) return No(malformed);
        int outgoingSlot = party[partyIndex].Mail;
        var outgoingMail = outgoingSlot == SourceMailState.NoMail ? null : state.Attached[outgoingSlot];
        var attached = outgoingMail is null ? state.Attached : state.Attached.SetItem(outgoingSlot, null);
        int incomingSlot = incomingMail is null ? -1 : FirstFree(attached);
        if (incomingMail is not null && incomingSlot < 0) return No("mail_attached_full");
        if (incomingMail is not null) attached = attached.SetItem(incomingSlot, incomingMail);
        var pokemon = incomingMail is null ? incoming : Attach(incoming, incomingMail, incomingSlot);
        return new(state with { Attached = attached }, party.SetItem(partyIndex, pokemon), true, null,
            AttachedSlot: incomingSlot < 0 ? null : incomingSlot, OutgoingMail: outgoingMail);
    }

    public static SourceMailResult SendToMailbox(SourceMailState state, ImmutableArray<PartyPokemon> party, int partyIndex)
    {
        SourceMailResult No(string error) => new(state, party, false, error);
        if (Validate(state, party) is { } invalid) return No(invalid);
        if (!PartyIndex(party, partyIndex)) return No("mail_party_index");
        var pokemon = party[partyIndex];
        if (pokemon.Mail == SourceMailState.NoMail) return No("mail_attached_missing");
        int slot = FirstFree(state.Mailbox);
        if (slot < 0) return No("mail_mailbox_full");
        var message = state.Attached[pokemon.Mail]!;
        return new(state with { Attached = state.Attached.SetItem(pokemon.Mail, null), Mailbox = state.Mailbox.SetItem(slot, message) },
            party.SetItem(partyIndex, Detach(pokemon)), true, null, MailboxSlot: slot);
    }

    public static SourceMailResult GiveFromMailbox(SourceMailState state, ImmutableArray<PartyPokemon> party,
        int mailboxSlot, int partyIndex)
    {
        SourceMailResult No(string error) => new(state, party, false, error);
        var read = TryReadMailbox(state, party, mailboxSlot);
        if (!read.Succeeded) return No(read.Error!);
        var attached = AttachReceived(state, party, partyIndex, read.Message!);
        if (!attached.Succeeded) return No(attached.Error!);
        return attached with { State = attached.State with { Mailbox = Compact(state.Mailbox.SetItem(mailboxSlot, null)) }, MailboxSlot = mailboxSlot };
    }

    /// <summary>Only an explicit erase choice returns blank stationery to the Bag. The AddBagItem
    /// capacity transaction completes before any message/ref/item is cleared (party_menu.c:3703-3719).</summary>
    public static SourceMailInventoryResult EraseAttachedToBag(EarlyShopCatalog catalog, InventoryState inventory,
        SourceMailState state, ImmutableArray<PartyPokemon> party, int partyIndex, bool eraseConfirmed)
    {
        SourceMailInventoryResult No(string error) => new(state, party, inventory, false, error);
        if (Validate(state, party) is { } invalid) return No(invalid);
        if (!PartyIndex(party, partyIndex)) return No("mail_party_index");
        var pokemon = party[partyIndex];
        if (pokemon.Mail == SourceMailState.NoMail) return No("mail_attached_missing");
        if (!eraseConfirmed) return No("mail_erase_confirmation_required");
        var message = state.Attached[pokemon.Mail]!;
        var added = EarlyInventoryAddRules.Add(catalog, inventory, new ItemId("pret:" + message.ItemSymbol), 1);
        if (!added.Succeeded) return No(added.Error!);
        return new(state with { Attached = state.Attached.SetItem(pokemon.Mail, null) },
            party.SetItem(partyIndex, Detach(pokemon)), added.State, true, null);
    }

    /// <summary>Mailbox erase preserves the entire message if Bag capacity fails; successful removal
    /// retains the original relative mailbox order (player_pc.c:435-450,630-648).</summary>
    public static SourceMailInventoryResult EraseMailboxToBag(EarlyShopCatalog catalog, InventoryState inventory,
        SourceMailState state, ImmutableArray<PartyPokemon> party, int mailboxSlot, bool eraseConfirmed)
    {
        SourceMailInventoryResult No(string error) => new(state, party, inventory, false, error);
        var read = TryReadMailbox(state, party, mailboxSlot);
        if (!read.Succeeded) return No(read.Error!);
        if (!eraseConfirmed) return No("mail_erase_confirmation_required");
        var added = EarlyInventoryAddRules.Add(catalog, inventory, new ItemId("pret:" + read.Message!.ItemSymbol), 1);
        if (!added.Succeeded) return No(added.Error!);
        return new(state with { Mailbox = Compact(state.Mailbox.SetItem(mailboxSlot, null)) }, party, added.State, true, null);
    }

    private static string? ValidateIncoming(ImmutableArray<PartyPokemon> party, PartyPokemon? incoming, SourceMailMessage? message)
    {
        if (incoming is null) return "mail_incoming_missing";
        if (incoming.InstanceId <= 0 || party.Any(p => p.InstanceId == incoming.InstanceId)) return "mail_party_identity";
        if (message is null) return ValidateAttachmentReference(incoming) ??
            (incoming.Mail == SourceMailState.NoMail ? null : "mail_incoming_payload_missing");
        if (ValidateMessage(message) is { } invalid) return invalid;
        if (incoming.IsEgg) return "mail_egg";
        if (incoming.Mail != SourceMailState.NoMail && incoming.Mail is not (>= 0 and < SourceMailState.AttachedCapacity))
            return "mail_attached_index";
        return incoming.HeldItem == message.ItemSymbol ? null : "mail_held_item_agreement";
    }

    private static PartyPokemon Attach(PartyPokemon pokemon, SourceMailMessage message, int slot) =>
        pokemon with { Mail = slot, HeldItem = message.ItemSymbol };
    private static PartyPokemon Detach(PartyPokemon pokemon) => pokemon with { Mail = SourceMailState.NoMail, HeldItem = "ITEM_NONE" };
    private static bool PartyIndex(ImmutableArray<PartyPokemon> party, int index) => index >= 0 && index < party.Length;
    private static int FirstFree(ImmutableArray<SourceMailMessage?> messages)
    {
        for (int slot = 0; slot < messages.Length; slot++) if (messages[slot] is null) return slot;
        return -1;
    }
    private static ImmutableArray<SourceMailMessage?> Compact(ImmutableArray<SourceMailMessage?> mailbox)
    {
        var occupied = mailbox.Where(message => message is not null).ToImmutableArray();
        return occupied.AddRange(Enumerable.Repeat<SourceMailMessage?>(null, SourceMailState.MailboxCapacity - occupied.Length));
    }
}
