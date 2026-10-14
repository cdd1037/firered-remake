using System.Collections.Immutable;

namespace Game.Domain.Pokemon;

public enum PokemonStorageReleaseOutcome { Rejected, ConfirmationRequired, Released }

/// <summary>Only Released returns ReleasedPokemon, the exact removed creature including its held
/// item, for the caller's ownership-retirement proof. Rejections preserve every input reference.</summary>
public sealed record PokemonStorageReleaseResult(PokemonStorageReleaseOutcome Outcome,
    ImmutableArray<PartyPokemon> Party, PokemonStorageState Storage, PokemonStorageMoveCursor? Cursor,
    PartyPokemon? ReleasedPokemon = null, PokemonStorageDiagnostic? Diagnostic = null)
{
    public bool Succeeded => Outcome != PokemonStorageReleaseOutcome.Rejected;
}

/// <summary>Original PC RELEASE for selected and held creatures. The caller owns mode admission,
/// default-No confirmation, command revisions, messages/animation and durable ownership history.</summary>
public static class PokemonStorageReleaseRules
{
    /// <summary>Original selection-menu guards, before asking for confirmation. No HM scan or mutation.</summary>
    public static PokemonStorageReleaseResult CheckSelected(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor,
        PokemonStorageMoveLocation target, long expectedInstanceId)
    {
        if (PokemonStorageMoveRules.Validate(content, party, storage, cursor) is { } invalid)
            return Reject(party, storage, cursor, invalid);
        if (cursor is not null) return Reject(party, storage, cursor, new("storage_move_already_holding"));
        if (PokemonStorageMoveRules.TargetError(storage, target) is { } locationError)
            return Reject(party, storage, cursor, locationError);
        var pokemon = PokemonStorageMoveRules.At(party, storage, target);
        if (pokemon is null) return Reject(party, storage, cursor, new("storage_slot_empty"));
        if (pokemon.InstanceId != expectedInstanceId)
            return Reject(party, storage, cursor, new("storage_move_target_identity"));
        // CanMovePartyMon precedes both the Egg and Mail checks, even for a fainted selected mon.
        if (target.IsParty && !PokemonStorageMoveRules.OtherAble(party, target.SlotIndex))
            return Reject(party, storage, cursor, new("storage_last_usable", PartyIndex: target.SlotIndex));
        return CheckCreature(party, storage, cursor, pokemon);
    }

    /// <summary>A held creature is already absent from party/boxes. Its old Origin need not be
    /// selected or empty, and source CanMovePartyMon never applies while holding a creature.</summary>
    public static PokemonStorageReleaseResult CheckHeld(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor,
        long expectedHeldInstanceId)
    {
        if (PokemonStorageMoveRules.Validate(content, party, storage, cursor) is { } invalid)
            return Reject(party, storage, cursor, invalid);
        if (cursor is null) return Reject(party, storage, cursor, new("storage_move_not_holding"));
        if (cursor.Pokemon.InstanceId != expectedHeldInstanceId)
            return Reject(party, storage, cursor, new("storage_move_held_identity"));
        return CheckCreature(party, storage, cursor, cursor.Pokemon);
    }

    /// <summary>After Yes, rechecks the selected target and protects the last Surf/Dive holders.
    /// Removes only that target, compacting party slots but leaving box holes and items untouched.</summary>
    public static PokemonStorageReleaseResult ReleaseSelected(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor,
        PokemonStorageMoveLocation target, long expectedInstanceId)
    {
        var check = CheckSelected(content, party, storage, cursor, target, expectedInstanceId);
        if (!check.Succeeded) return check;
        var pokemon = PokemonStorageMoveRules.At(party, storage, target)!;
        if (LosesRestrictedMove(party, storage, pokemon))
            return Reject(party, storage, cursor, new("storage_release_hm_protected"));
        return new(PokemonStorageReleaseOutcome.Released,
            target.IsParty ? party.RemoveAt(target.SlotIndex) : party,
            target.IsParty ? storage : PokemonStorageRules.Put(storage, PokemonStorageMoveRules.Address(target), null),
            null, pokemon);
    }

    /// <summary>After Yes, clears held ownership only. A released non-Mail held item leaves with
    /// the creature; it is never credited to the Bag or any remaining creature.</summary>
    public static PokemonStorageReleaseResult ReleaseHeld(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor,
        long expectedHeldInstanceId)
    {
        var check = CheckHeld(content, party, storage, cursor, expectedHeldInstanceId);
        if (!check.Succeeded) return check;
        if (LosesRestrictedMove(party, storage, cursor!.Pokemon))
            return Reject(party, storage, cursor, new("storage_release_hm_protected"));
        return new(PokemonStorageReleaseOutcome.Released, party, storage, null, cursor.Pokemon);
    }

    private static PokemonStorageReleaseResult CheckCreature(ImmutableArray<PartyPokemon> party,
        PokemonStorageState storage, PokemonStorageMoveCursor? cursor, PartyPokemon pokemon)
    {
        if (pokemon.IsEgg) return Reject(party, storage, cursor, new("storage_release_egg"));
        if (PokemonStorageMoveRules.HasMail(pokemon))
            return Reject(party, storage, cursor, new("storage_mail_unsupported"));
        return new(PokemonStorageReleaseOutcome.ConfirmationRequired, party, storage, cursor);
    }

    private static bool LosesRestrictedMove(ImmutableArray<PartyPokemon> party,
        PokemonStorageState storage, PartyPokemon released)
    {
        // pret 037335f data.c:741–849 uses exactly Surf/Dive, not all HMs or Waterfall.
        // pokemon.c:3258–3275 MON_DATA_KNOWN_MOVES excludes eggs, but ignores HP and PP.
        bool surf = Knows(released, "pret:MOVE_SURF"), dive = Knows(released, "pret:MOVE_DIVE");
        if (!surf && !dive) return false;
        foreach (var remaining in party.Concat(storage.Boxes.SelectMany(box => box.Slots)))
        {
            if (remaining is null || remaining.InstanceId == released.InstanceId) continue;
            if (surf && Knows(remaining, "pret:MOVE_SURF")) surf = false;
            if (dive && Knows(remaining, "pret:MOVE_DIVE")) dive = false;
            if (!surf && !dive) return false;
        }
        return true;
    }

    private static bool Knows(PartyPokemon pokemon, string move) =>
        !pokemon.IsEgg && pokemon.Pokemon.Moves.Any(slot => slot.Move.Value == move);

    private static PokemonStorageReleaseResult Reject(ImmutableArray<PartyPokemon> party,
        PokemonStorageState storage, PokemonStorageMoveCursor? cursor, PokemonStorageDiagnostic error) =>
        new(PokemonStorageReleaseOutcome.Rejected, party, storage, cursor, Diagnostic: error);
}
