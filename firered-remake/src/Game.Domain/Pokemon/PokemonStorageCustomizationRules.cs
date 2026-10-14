using System.Collections.Immutable;
using System.Globalization;

namespace Game.Domain.Pokemon;

public enum PokemonStorageCustomizationOutcome { Rejected, BoxNamed, WallpaperChanged, MarkingsChanged }

/// <summary>Every rejection retains the exact input owners, slots, current box and moving cursor.</summary>
public sealed record PokemonStorageCustomizationResult(PokemonStorageCustomizationOutcome Outcome,
    ImmutableArray<PartyPokemon> Party, PokemonStorageState Storage, PokemonStorageMoveCursor? Cursor,
    PokemonStorageDiagnostic? Diagnostic = null)
{
    public bool Succeeded => Outcome != PokemonStorageCustomizationOutcome.Rejected;
}

/// <summary>Pinned original box naming, wallpaper selection and four-bit markings. These are committed
/// values only: keyboard/menu drafts and Cancel/B belong to the caller and never mutate these owners.</summary>
public static class PokemonStorageCustomizationRules
{
    public const int BoxNameLength = 8;
    public const int WallpaperCount = 16;
    public const int DefaultWallpaperCount = 4;
    public const int Circle = 1, Square = 2, Triangle = 4, Heart = 8;
    public const string BoxNameAlphabet = PokemonStateValidator.NicknameAlphabet;

    public static string DefaultBoxName(int boxIndex)
    {
        CheckBoxIndex(boxIndex);
        return "BOX" + (boxIndex + 1).ToString(CultureInfo.InvariantCulture);
    }

    public static int DefaultWallpaperId(int boxIndex)
    {
        CheckBoxIndex(boxIndex);
        return boxIndex % DefaultWallpaperCount;
    }

    public static string BoxName(PokemonStorageState storage, int boxIndex)
        => storage.Boxes[boxIndex].Name ?? DefaultBoxName(boxIndex);
    public static int WallpaperId(PokemonStorageState storage, int boxIndex)
        => storage.Boxes[boxIndex].WallpaperId ?? DefaultWallpaperId(boxIndex);

    public static string? ValidateNameInput(string? name)
        => name is null || name.Length > BoxNameLength ? "storage_box_name_length"
            : name.Any(c => !BoxNameAlphabet.Contains(c, StringComparison.Ordinal)) ? "storage_box_name_character" : null;

    internal static string? ValidateBoxMetadata(PokemonStorageBox box)
    {
        if (box.Name is { } name)
        {
            if (ValidateNameInput(name) is { } error) return error;
            if (name.All(c => c == ' ')) return "storage_box_name_blank";
        }
        return box.WallpaperId is < 0 or >= WallpaperCount ? "storage_wallpaper_id" : null;
    }

    /// <summary>The source starts an empty input buffer. Empty/all-space submission keeps the old
    /// name; leading/trailing spaces on a nonblank submission are preserved, never trimmed.</summary>
    public static PokemonStorageCustomizationResult RenameBox(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor,
        int boxIndex, string? name)
    {
        if (BoxError(content, party, storage, cursor, boxIndex) is { } error) return Reject(party, storage, cursor, error);
        if (ValidateNameInput(name) is { } invalid) return Reject(party, storage, cursor, new(invalid));
        if (name!.All(c => c == ' ')) return new(PokemonStorageCustomizationOutcome.BoxNamed, party, storage, cursor);
        string? value = name == DefaultBoxName(boxIndex) ? null : name;
        var box = storage.Boxes[boxIndex];
        return new(PokemonStorageCustomizationOutcome.BoxNamed, party,
            box.Name == value ? storage : storage with { Boxes = storage.Boxes.SetItem(boxIndex, box with { Name = value }) }, cursor);
    }

    public static PokemonStorageCustomizationResult ChangeWallpaper(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor,
        int boxIndex, int wallpaperId)
    {
        if (BoxError(content, party, storage, cursor, boxIndex) is { } error) return Reject(party, storage, cursor, error);
        if (wallpaperId is < 0 or >= WallpaperCount) return Reject(party, storage, cursor, new("storage_wallpaper_id"));
        int? value = wallpaperId == DefaultWallpaperId(boxIndex) ? null : wallpaperId;
        var box = storage.Boxes[boxIndex];
        return new(PokemonStorageCustomizationOutcome.WallpaperChanged, party,
            box.WallpaperId == value ? storage : storage with { Boxes = storage.Boxes.SetItem(boxIndex, box with { WallpaperId = value }) }, cursor);
    }

    /// <summary>Original SetMonMarkings addresses the moving creature first. When holding, the
    /// selected slot is ignored; expectedInstanceId must identify the held creature. Eggs may be marked.</summary>
    public static PokemonStorageCustomizationResult SetMarkings(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor,
        PokemonStorageMoveLocation? target, long expectedInstanceId, int markings)
    {
        if (PokemonStorageMoveRules.Validate(content, party, storage, cursor) is { } error) return Reject(party, storage, cursor, error);
        if (markings is < 0 or > 15) return Reject(party, storage, cursor, new("party_markings"));
        if (cursor is not null)
        {
            if (cursor.Pokemon.InstanceId != expectedInstanceId) return Reject(party, storage, cursor, new("storage_markings_target_identity"));
            return new(PokemonStorageCustomizationOutcome.MarkingsChanged, party, storage,
                cursor.Pokemon.Markings == markings ? cursor : cursor with { Pokemon = cursor.Pokemon with { Markings = markings } });
        }
        if (target is null) return Reject(party, storage, cursor, new("storage_move_location_missing"));
        if (PokemonStorageMoveRules.TargetError(storage, target) is { } targetError) return Reject(party, storage, cursor, targetError);
        var pokemon = PokemonStorageMoveRules.At(party, storage, target);
        if (pokemon is null) return Reject(party, storage, cursor, new("storage_slot_empty"));
        if (pokemon.InstanceId != expectedInstanceId) return Reject(party, storage, cursor, new("storage_markings_target_identity"));
        if (pokemon.Markings == markings) return new(PokemonStorageCustomizationOutcome.MarkingsChanged, party, storage, cursor);
        var marked = pokemon with { Markings = markings };
        return new(PokemonStorageCustomizationOutcome.MarkingsChanged,
            target.IsParty ? party.SetItem(target.SlotIndex, marked) : party,
            target.IsParty ? storage : PokemonStorageRules.Put(storage, PokemonStorageMoveRules.Address(target), marked), cursor);
    }

    private static PokemonStorageDiagnostic? BoxError(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PokemonStorageMoveCursor? cursor, int boxIndex)
        => PokemonStorageMoveRules.Validate(content, party, storage, cursor)
            ?? (boxIndex is < 0 or >= PokemonStorageState.BoxCount ? new("storage_box_index", BoxIndex: boxIndex)
                : PokemonStorageMoveRules.TargetError(storage, PokemonStorageMoveLocation.Box(boxIndex, 0)));
    private static void CheckBoxIndex(int boxIndex)
    {
        if (boxIndex is < 0 or >= PokemonStorageState.BoxCount) throw new ArgumentOutOfRangeException(nameof(boxIndex));
    }
    private static PokemonStorageCustomizationResult Reject(ImmutableArray<PartyPokemon> party,
        PokemonStorageState storage, PokemonStorageMoveCursor? cursor, PokemonStorageDiagnostic error)
        => new(PokemonStorageCustomizationOutcome.Rejected, party, storage, cursor, error);
}
