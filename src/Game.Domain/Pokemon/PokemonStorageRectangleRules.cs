using System.Collections.Immutable;

namespace Game.Domain.Pokemon;

/// <summary>Inclusive box corners, normalized only after both have been bounds checked.</summary>
public sealed record PokemonStorageRectangle(int BoxIndex, int FirstSlot, int LastSlot)
{
    public int Left => Math.Min(FirstSlot % 6, LastSlot % 6);
    public int Top => Math.Min(FirstSlot / 6, LastSlot / 6);
    public int Width => Math.Abs(FirstSlot % 6 - LastSlot % 6) + 1;
    public int Height => Math.Abs(FirstSlot / 6 - LastSlot / 6) + 1;
    public bool IsValid => BoxIndex is >= 0 and < PokemonStorageState.BoxCount &&
        FirstSlot is >= 0 and < 30 && LastSlot is >= 0 and < 30;
    public int Slot(int offset) => (Top + offset / Width) * 6 + Left + offset % Width;
}

/// <summary>Owns every removed creature and the holes in the original row-major rectangle.</summary>
public sealed record PokemonStorageGroupCursor(PokemonStorageRectangle Origin, ImmutableArray<PartyPokemon?> Cells);
public sealed record PokemonStorageGroupResult(PokemonStorageState Storage, PokemonStorageGroupCursor? Cursor,
    PokemonStorageDiagnostic? Diagnostic = null)
{
    public bool Succeeded => Diagnostic is null;
}

/// <summary>pret 037335f4 misc.c MultiMove_GetMonsFromSelection/RemoveMonsFromBox/SetPlacedMonData.
/// Each operation checks the entire payload before publishing any mutation. B never returns held mons.</summary>
public static class PokemonStorageRectangleRules
{
    public static PokemonStorageDiagnostic? Validate(CampaignPokemonContent? content, ImmutableArray<PartyPokemon> party,
        PokemonStorageState? storage, PokemonStorageGroupCursor? cursor, long? nextInstanceId = null)
    {
        if (PokemonStorageValidator.Validate(content, party, storage, nextInstanceId) is {} error) return error;
        if (cursor is null) return null;
        if (cursor.Origin is not { IsValid: true } rectangle || cursor.Cells.IsDefault || cursor.Cells.Length != rectangle.Width * rectangle.Height)
            return new("storage_rectangle_payload");
        int anchor = (rectangle.FirstSlot / 6 - rectangle.Top) * rectangle.Width + rectangle.FirstSlot % 6 - rectangle.Left;
        if (cursor.Cells[anchor] is null) return new("storage_rectangle_payload");
        // Validate held boxes with the exact same health, item, identity and allocator invariants as stored boxes.
        var held = PokemonStorageState.Empty.Boxes[0].Slots.ToBuilder();
        for (int i = 0; i < cursor.Cells.Length; i++) held[i] = cursor.Cells[i];
        var holdingStorage = PokemonStorageState.Empty with { Boxes = PokemonStorageState.Empty.Boxes.SetItem(0, new(held.ToImmutable())) };
        if (PokemonStorageValidator.Validate(content, [], holdingStorage, nextInstanceId) is {} heldError) return heldError;
        var ids = party.Select(p => p.InstanceId).Concat(storage!.Boxes.SelectMany(b => b.Slots).OfType<PartyPokemon>().Select(p => p.InstanceId)).ToHashSet();
        foreach (var mon in cursor.Cells.OfType<PartyPokemon>())
            if (!ids.Add(mon.InstanceId)) return new("storage_duplicate_instance_id");
        return null;
    }

    public static PokemonStorageGroupResult Grab(CampaignPokemonContent? content, ImmutableArray<PartyPokemon> party,
        PokemonStorageState storage, PokemonStorageGroupCursor? cursor, PokemonStorageRectangle rectangle, ImmutableArray<long?> identities)
    {
        PokemonStorageGroupResult No(string code) => new(storage, cursor, new(code));
        if (Validate(content, party, storage, cursor) is {} error) return new(storage, cursor, error);
        if (cursor is not null) return No("storage_move_already_holding");
        if (!rectangle.IsValid) return No("storage_rectangle_bounds");
        if (rectangle.BoxIndex != storage.CurrentBox) return No("storage_move_box_not_selected");
        if (storage.Boxes[rectangle.BoxIndex].Slots[rectangle.FirstSlot] is null) return No("storage_slot_empty");
        int count = rectangle.Width * rectangle.Height;
        if (identities.IsDefault || identities.Length != count) return No("storage_move_target_identity");
        var cells = ImmutableArray.CreateBuilder<PartyPokemon?>(count);
        for (int i = 0; i < count; i++)
        {
            var mon = storage.Boxes[rectangle.BoxIndex].Slots[rectangle.Slot(i)];
            if (mon?.InstanceId != identities[i]) return No("storage_move_target_identity");
            cells.Add(mon);
        }
        var slots = storage.Boxes[rectangle.BoxIndex].Slots.ToBuilder();
        for (int i = 0; i < count; i++) slots[rectangle.Slot(i)] = null;
        return new(storage with { Boxes = storage.Boxes.SetItem(rectangle.BoxIndex, storage.Boxes[rectangle.BoxIndex] with { Slots = slots.ToImmutable() }) }, new(rectangle, cells.ToImmutable()));
    }

    public static PokemonStorageGroupResult Place(CampaignPokemonContent? content, ImmutableArray<PartyPokemon> party,
        PokemonStorageState storage, PokemonStorageGroupCursor? cursor, PokemonStorageMoveLocation destination, ImmutableArray<long?> identities)
    {
        PokemonStorageGroupResult No(string code) => new(storage, cursor, new(code));
        if (Validate(content, party, storage, cursor) is {} error) return new(storage, cursor, error);
        if (cursor is null) return No("storage_move_not_holding");
        if (identities.IsDefault || !identities.SequenceEqual(cursor.Cells.Select(p => p?.InstanceId))) return No("storage_move_held_identity");
        if (destination.BoxIndex != storage.CurrentBox) return No("storage_move_box_not_selected");
        int width = cursor.Origin.Width, height = cursor.Origin.Height;
        if (destination.SlotIndex is < 0 or >= 30 || destination.SlotIndex % 6 + width > 6 || destination.SlotIndex / 6 + height > 5)
            return No("storage_rectangle_bounds");
        int Slot(int i) => destination.SlotIndex + i / width * 6 + i % width;
        var box = storage.Boxes[destination.BoxIndex];
        for (int i = 0; i < cursor.Cells.Length; i++)
            if (cursor.Cells[i] is not null && box.Slots[Slot(i)] is not null) return No("storage_move_slot_occupied");
        var slots = box.Slots.ToBuilder();
        for (int i = 0; i < cursor.Cells.Length; i++)
            if (cursor.Cells[i] is {} mon) slots[Slot(i)] = mon;
        return new(storage with { Boxes = storage.Boxes.SetItem(destination.BoxIndex, box with { Slots = slots.ToImmutable() }) }, null);
    }
}
