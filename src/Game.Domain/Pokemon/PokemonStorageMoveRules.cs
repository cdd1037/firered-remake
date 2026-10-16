using System.Collections.Immutable;

namespace Game.Domain.Pokemon;

/// <summary>Zero-based source coordinates. Box 14 denotes the six-slot party panel.</summary>
public sealed record PokemonStorageMoveLocation(int BoxIndex, int SlotIndex)
{
    public const int PartyBox = PokemonStorageState.BoxCount;
    public bool IsParty => BoxIndex == PartyBox;
    public static PokemonStorageMoveLocation Party(int slot) => new(PartyBox, slot);
    public static PokemonStorageMoveLocation Box(int box, int slot) => new(box, slot);
}

/// <summary>A transient owned creature removed from its source. Origin tracks the latest grab/shift,
/// not an undo destination. It must participate in ownership/mail validation and cannot be saved.</summary>
public sealed record PokemonStorageMoveCursor(PartyPokemon Pokemon, PokemonStorageMoveLocation Origin);

public enum PokemonStorageMoveOutcome { Rejected, Grabbed, Placed, Shifted, BoxSelected, PartyMayHide, ExitAllowed }

/// <summary>Rejections retain the exact input party, storage and cursor. No RNG or allocator changes.</summary>
public sealed record PokemonStorageMoveResult(PokemonStorageMoveOutcome Outcome,
    ImmutableArray<PartyPokemon> Party, PokemonStorageState Storage, PokemonStorageMoveCursor? Cursor,
    PokemonStorageDiagnostic? Diagnostic = null)
{
    public bool Succeeded => Outcome != PokemonStorageMoveOutcome.Rejected;
}

/// <summary>Single-creature MOVE POKEMON: grab, place and shift, derived from pret 037335f
/// pokemon_storage_system_data.c and tasks.c. Presentation and command revision guards belong to callers.</summary>
public static class PokemonStorageMoveRules
{
    public static PokemonStorageDiagnostic? Validate(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState? storage,
        PokemonStorageMoveCursor? cursor, long? nextInstanceId = null)
    {
        if (PokemonStorageValidator.Validate(content, party, storage, nextInstanceId) is { } invalid) return invalid;
        if (cursor is null) return null;
        if (LocationError(cursor.Origin) is { } originError) return originError;
        if (PokemonStateValidator.ValidateParty(content, cursor.Pokemon) is { } error)
            return new("storage_move_cursor_pokemon", PokemonError: error);
        long id = cursor.Pokemon.InstanceId;
        if (party.Any(p => p.InstanceId == id) || storage!.Boxes.Any(b => b.Slots.Any(p => p?.InstanceId == id)))
            return new("storage_duplicate_instance_id");
        if (nextInstanceId.HasValue && id >= nextInstanceId.Value) return new("storage_next_instance_id");
        // Mail cannot have come from a box: source prevents leaving the party panel while holding it.
        if (!cursor.Origin.IsParty && HasMail(cursor.Pokemon)) return new("storage_boxed_mail");
        return null;
    }

    public static PokemonStorageMoveResult Grab(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor,
        PokemonStorageMoveLocation source, long expectedInstanceId)
    {
        if (Validate(content, party, storage, cursor) is { } error) return Reject(party, storage, cursor, error);
        if (cursor is not null) return Reject(party, storage, cursor, new("storage_move_already_holding"));
        if (TargetError(storage, source) is { } targetError) return Reject(party, storage, cursor, targetError);
        var pokemon = At(party, storage, source);
        if (pokemon is null) return Reject(party, storage, cursor, new("storage_slot_empty"));
        if (pokemon.InstanceId != expectedInstanceId) return Reject(party, storage, cursor, new("storage_move_target_identity"));
        if (source.IsParty && !OtherAble(party, source.SlotIndex))
            return Reject(party, storage, cursor, new("storage_last_usable", PartyIndex: source.SlotIndex));
        return new(PokemonStorageMoveOutcome.Grabbed,
            source.IsParty ? party.RemoveAt(source.SlotIndex) : party,
            source.IsParty ? storage : PokemonStorageRules.Put(storage, Address(source), null), new(pokemon, source));
    }

    public static PokemonStorageMoveResult Place(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor,
        PokemonStorageMoveLocation destination, long expectedHeldInstanceId)
    {
        if (MoveError(content, party, storage, cursor, destination, expectedHeldInstanceId) is { } error)
            return Reject(party, storage, cursor, error);
        if (At(party, storage, destination) is not null)
            return Reject(party, storage, cursor, new("storage_move_slot_occupied"));
        // All six visible party slots are selectable; an empty later slot compacts to the end.
        var pokemon = cursor!.Pokemon;
        return new(PokemonStorageMoveOutcome.Placed, destination.IsParty ? party.Add(pokemon) : party,
            destination.IsParty ? storage : PokemonStorageRules.Put(storage, Address(destination),
                PokemonStorageRules.ToBoxProjection(content!.Catalog, pokemon)), null);
    }

    public static PokemonStorageMoveResult Shift(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor,
        PokemonStorageMoveLocation destination, long expectedHeldInstanceId, long expectedTargetInstanceId)
    {
        if (MoveError(content, party, storage, cursor, destination, expectedHeldInstanceId) is { } error)
            return Reject(party, storage, cursor, error);
        var displaced = At(party, storage, destination);
        if (displaced is null) return Reject(party, storage, cursor, new("storage_slot_empty"));
        if (displaced.InstanceId != expectedTargetInstanceId)
            return Reject(party, storage, cursor, new("storage_move_target_identity"));
        var pokemon = cursor!.Pokemon;
        if (destination.IsParty && !OtherAble(party, destination.SlotIndex) && !Able(pokemon))
            return Reject(party, storage, cursor, new("storage_last_usable", PartyIndex: destination.SlotIndex));
        return new(PokemonStorageMoveOutcome.Shifted,
            destination.IsParty ? party.SetItem(destination.SlotIndex, pokemon) : party,
            destination.IsParty ? storage : PokemonStorageRules.Put(storage, Address(destination),
                PokemonStorageRules.ToBoxProjection(content!.Catalog, pokemon)), new(displaced, destination));
    }

    public static PokemonStorageMoveResult SelectBox(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor, int boxIndex)
    {
        if (Validate(content, party, storage, cursor) is { } error) return Reject(party, storage, cursor, error);
        if (cursor is not null && HasMail(cursor.Pokemon))
            return Reject(party, storage, cursor, new("storage_mail_unsupported"));
        var selected = PokemonStorageRules.SelectBox(content, party, storage, boxIndex);
        return selected.Succeeded ? new(PokemonStorageMoveOutcome.BoxSelected, party, selected.Storage, cursor)
            : Reject(party, storage, cursor, selected.Diagnostic!);
    }

    public static PokemonStorageMoveResult CheckHideParty(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor)
    {
        if (Validate(content, party, storage, cursor) is { } error) return Reject(party, storage, cursor, error);
        return cursor is not null && HasMail(cursor.Pokemon)
            ? Reject(party, storage, cursor, new("storage_mail_unsupported"))
            : new(PokemonStorageMoveOutcome.PartyMayHide, party, storage, cursor);
    }

    /// <summary>B/Close cannot discard or return a held Pokemon. Menu Cancel only dismisses its menu.</summary>
    public static PokemonStorageMoveResult CheckExit(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor)
    {
        if (Validate(content, party, storage, cursor) is { } error) return Reject(party, storage, cursor, error);
        return cursor is not null ? Reject(party, storage, cursor, new("storage_move_holding"))
            : new(PokemonStorageMoveOutcome.ExitAllowed, party, storage, cursor);
    }

    private static PokemonStorageDiagnostic? MoveError(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor,
        PokemonStorageMoveLocation destination, long expectedHeldInstanceId)
    {
        if (Validate(content, party, storage, cursor) is { } invalid) return invalid;
        if (cursor is null) return new("storage_move_not_holding");
        if (cursor.Pokemon.InstanceId != expectedHeldInstanceId) return new("storage_move_held_identity");
        if (TargetError(storage, destination) is { } error) return error;
        return !destination.IsParty && HasMail(cursor.Pokemon) ? new("storage_mail_unsupported") : null;
    }

    internal static bool HasMail(PartyPokemon pokemon) => pokemon.Mail != SourceMailState.NoMail || PokemonStorageRules.IsMailItem(pokemon.HeldItem);
    private static bool Able(PartyPokemon pokemon) => !pokemon.IsEgg && pokemon.Pokemon.CurrentHp > 0;
    internal static bool OtherAble(ImmutableArray<PartyPokemon> party, int except) => party.Where((_, i) => i != except).Any(Able);
    internal static PokemonStorageAddress Address(PokemonStorageMoveLocation location) => new(location.BoxIndex, location.SlotIndex);
    internal static PartyPokemon? At(ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveLocation location) =>
        location.IsParty ? location.SlotIndex < party.Length ? party[location.SlotIndex] : null : storage.Boxes[location.BoxIndex].Slots[location.SlotIndex];

    private static PokemonStorageDiagnostic? LocationError(PokemonStorageMoveLocation? location)
    {
        if (location is null) return new("storage_move_location_missing");
        if (location.BoxIndex is < 0 or > PokemonStorageMoveLocation.PartyBox)
            return new("storage_box_index", BoxIndex: location.BoxIndex);
        if (location.SlotIndex < 0 || location.SlotIndex >= (location.IsParty ? 6 : PokemonStorageState.SlotsPerBox))
            return location.IsParty ? new("storage_party_index", PartyIndex: location.SlotIndex)
                : new("storage_slot_index", BoxIndex: location.BoxIndex, SlotIndex: location.SlotIndex);
        return null;
    }

    internal static PokemonStorageDiagnostic? TargetError(PokemonStorageState storage, PokemonStorageMoveLocation location) =>
        LocationError(location) ?? (!location.IsParty && location.BoxIndex != storage.CurrentBox
            ? new("storage_move_box_not_selected", BoxIndex: location.BoxIndex) : null);

    private static PokemonStorageMoveResult Reject(ImmutableArray<PartyPokemon> party, PokemonStorageState storage,
        PokemonStorageMoveCursor? cursor, PokemonStorageDiagnostic error) => new(PokemonStorageMoveOutcome.Rejected, party, storage, cursor, error);
}
