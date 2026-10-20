using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Items;

namespace Game.Domain.Pokemon;

// Acquisition validation must distinguish the fixed NPC letter even if a player has the
// same literal name/ID and composes identical words on identical stationery.
public enum SourceMailOrigin { PlayerAuthored, NpcTrade }

/// <summary>Live Mail payload. The literal author name and full trainer ID are the sole author
/// identity; Easy Chat words and species/item IDs retain their gameplay meanings.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SourceMailMessage(ImmutableArray<ushort> Words, [property: JsonRequired] string AuthorName,
    [property: JsonRequired] uint AuthorTrainerId, ushort Species, ushort ItemId,
    [property: JsonRequired] SourceMailOrigin Origin)
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
