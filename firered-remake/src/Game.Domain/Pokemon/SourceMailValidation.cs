using System.Collections.Immutable;

namespace Game.Domain.Pokemon;

public static partial class SourceMailRules
{
    // pret 037335f: include/constants/items.h:125-136; src/mail_data.c:167-187.
    public static string? ItemSymbol(ushort itemId) => itemId switch
    {
        121 => "ITEM_ORANGE_MAIL", 122 => "ITEM_HARBOR_MAIL", 123 => "ITEM_GLITTER_MAIL",
        124 => "ITEM_MECH_MAIL", 125 => "ITEM_WOOD_MAIL", 126 => "ITEM_WAVE_MAIL",
        127 => "ITEM_BEAD_MAIL", 128 => "ITEM_SHADOW_MAIL", 129 => "ITEM_TROPIC_MAIL",
        130 => "ITEM_DREAM_MAIL", 131 => "ITEM_FAB_MAIL", 132 => "ITEM_RETRO_MAIL", _ => null
    };

    public static bool IsMailItem(string? item)
    {
        for (ushort id = 121; id <= 132; id++) if (ItemSymbol(id) == item) return true;
        return false;
    }

    /// <summary>Structural source representation only. Content-specific authentication owns known
    /// messages, charmap decoding and Easy Chat vocabulary; raw species/words keep the u16 domain.</summary>
    public static string? ValidateMessage(SourceMailMessage? message)
    {
        if (message is null) return "mail_message_missing";
        if (message.Words.IsDefault || message.Words.Length != 9) return "mail_word_count";
        if (message.AuthorName is null) return "mail_author_name";
        if (message.AuthorNameBytes.IsDefault || message.AuthorNameBytes.Length != 8) return "mail_author_bytes";
        if (message.TrainerIdBytes.IsDefault || message.TrainerIdBytes.Length != 4) return "mail_trainer_bytes";
        if (ItemSymbol(message.ItemId) is null) return "mail_item";
        return null;
    }

    /// <summary>Local reference/item coupling. Complete saves must also call Validate to resolve the
    /// reference and reject duplicate links/orphan records; a standalone Pokemon cannot prove those.</summary>
    public static string? ValidateAttachmentReference(PartyPokemon? pokemon)
    {
        if (pokemon is null) return "mail_pokemon_missing";
        if (pokemon.Mail != SourceMailState.NoMail && pokemon.Mail is not (>= 0 and < SourceMailState.AttachedCapacity))
            return "mail_attached_index";
        if (IsMailItem(pokemon.HeldItem) != (pokemon.Mail != SourceMailState.NoMail)) return "mail_held_item_agreement";
        if (pokemon.IsEgg && pokemon.Mail != SourceMailState.NoMail) return "mail_egg";
        return null;
    }

    public static string? ValidateBoxedPokemon(PartyPokemon? pokemon) => pokemon is null ? null :
        pokemon.Mail != SourceMailState.NoMail || IsMailItem(pokemon.HeldItem) ? "mail_boxed" : null;

    /// <summary>Mail-only invariants; the caller still owns ordinary Pokemon/storage validation.
    /// A transient MOVE POKEMON cursor uses the four-argument overload below.</summary>
    public static string? Validate(SourceMailState? state, ImmutableArray<PartyPokemon> party,
        PokemonStorageState? storage = null) => Validate(state, party, storage, null);

    /// <summary>The cursor retains its existing attached record, even beside a full party.</summary>
    public static string? Validate(SourceMailState? state, ImmutableArray<PartyPokemon> party,
        PokemonStorageState? storage, PartyPokemon? movingPokemon)
    {
        if (state is null) return "mail_state_missing";
        if (state.Attached.IsDefault || state.Attached.Length != SourceMailState.AttachedCapacity) return "mail_attached_count";
        if (state.Mailbox.IsDefault || state.Mailbox.Length != SourceMailState.MailboxCapacity) return "mail_mailbox_count";
        if (party.IsDefault || party.Length > SourceMailState.AttachedCapacity) return "mail_party_count";
        foreach (var message in state.Attached.Concat(state.Mailbox))
            if (message is not null && ValidateMessage(message) is { } error) return error;
        var references = new bool[SourceMailState.AttachedCapacity];
        var identities = new HashSet<long>();
        foreach (var pokemon in movingPokemon is null ? party : party.Add(movingPokemon))
        {
            if (ValidateAttachmentReference(pokemon) is { } error) return error;
            if (pokemon.InstanceId <= 0 || !identities.Add(pokemon.InstanceId)) return "mail_party_identity";
            if (pokemon.Mail == SourceMailState.NoMail) continue;
            if (references[pokemon.Mail]) return "mail_duplicate_reference";
            var message = state.Attached[pokemon.Mail];
            if (message is null) return "mail_attached_missing";
            if (pokemon.HeldItem != message.ItemSymbol) return "mail_held_item_agreement";
            references[pokemon.Mail] = true;
        }
        for (int index = 0; index < references.Length; index++)
            if (state.Attached[index] is not null && !references[index]) return "mail_orphan_attachment";
        if (storage is not null)
        {
            if (storage.Boxes.IsDefault || storage.Boxes.Length != PokemonStorageState.BoxCount) return "mail_storage_shape";
            foreach (var box in storage.Boxes)
            {
                if (box is null || box.Slots.IsDefault || box.Slots.Length != PokemonStorageState.SlotsPerBox) return "mail_storage_shape";
                foreach (var pokemon in box.Slots)
                    if (ValidateBoxedPokemon(pokemon) is { } error) return error;
            }
        }
        return null;
    }

    /// <summary>The argument is an attached record slot, never an implicit party position.</summary>
    public static SourceMailReadResult TryReadAttached(SourceMailState state, ImmutableArray<PartyPokemon> party, int attachedSlot)
    {
        if (Validate(state, party) is { } error) return new(null, false, error);
        if (attachedSlot is < 0 or >= SourceMailState.AttachedCapacity) return new(null, false, "mail_attached_index");
        if (state.Attached[attachedSlot] is not { } message) return new(null, false, "mail_attached_missing");
        return new(message, true, null);
    }

    public static SourceMailReadResult TryReadMailbox(SourceMailState state, ImmutableArray<PartyPokemon> party, int mailboxSlot)
    {
        if (Validate(state, party) is { } error) return new(null, false, error);
        if (mailboxSlot is < 0 or >= SourceMailState.MailboxCapacity) return new(null, false, "mail_mailbox_index");
        if (state.Mailbox[mailboxSlot] is not { } message) return new(null, false, "mail_mailbox_empty");
        return new(message, true, null);
    }
}
