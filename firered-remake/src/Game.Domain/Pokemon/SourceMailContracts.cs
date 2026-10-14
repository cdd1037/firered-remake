using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Items;

namespace Game.Domain.Pokemon;

/// <summary>The complete live source Mail payload. Raw fields retain their source order and encoding.
/// AuthorName is a display label, not a replacement for the eight original name bytes.</summary>
public sealed record SourceMailMessage(ImmutableArray<ushort> Words, string AuthorName,
    ImmutableArray<byte> AuthorNameBytes, ImmutableArray<byte> TrainerIdBytes, ushort Species, ushort ItemId)
{
    [JsonIgnore]
    public string ItemSymbol => SourceMailRules.ItemSymbol(ItemId) ?? "ITEM_NONE";
}

/// <summary>Source mail[0..5] is independent of party order; mailbox[0..9] represents source mail[6..15].
/// Null means itemId == ITEM_NONE. Cleared slots do not retain inaccessible stale payload bytes.</summary>
public sealed record SourceMailState(ImmutableArray<SourceMailMessage?> Attached,
    ImmutableArray<SourceMailMessage?> Mailbox)
{
    public const int AttachedCapacity = 6;
    public const int MailboxCapacity = 10;
    public const int NoMail = 255;
    public static SourceMailState Empty { get; } = new(
        Enumerable.Repeat<SourceMailMessage?>(null, AttachedCapacity).ToImmutableArray(),
        Enumerable.Repeat<SourceMailMessage?>(null, MailboxCapacity).ToImmutableArray());
}

/// <summary>Rejected results retain the exact original state and party. Slot numbers are zero-based
/// within their named container. OutgoingMail is the departing Pokemon's original payload.</summary>
public sealed record SourceMailResult(SourceMailState State, ImmutableArray<PartyPokemon> Party,
    bool Succeeded, string? Error, int? AttachedSlot = null, int? MailboxSlot = null,
    SourceMailMessage? OutgoingMail = null);

public sealed record SourceMailInventoryResult(SourceMailState State, ImmutableArray<PartyPokemon> Party,
    InventoryState Inventory, bool Succeeded, string? Error);

public sealed record SourceMailReadResult(SourceMailMessage? Message, bool Succeeded, string? Error);
