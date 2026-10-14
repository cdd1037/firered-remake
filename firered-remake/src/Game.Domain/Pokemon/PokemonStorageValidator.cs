using System.Collections.Immutable;

namespace Game.Domain.Pokemon;

public static class PokemonStorageValidator
{
    /// <summary>
    /// Validates fixed storage dimensions, every owned creature, canonical box projections and
    /// globally unique entity IDs. Empty party/storage is valid. Optionally checks the save allocator.
    /// </summary>
    public static PokemonStorageDiagnostic? Validate(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState? storage, long? nextInstanceId = null)
    {
        var contentError = PokemonStateValidator.ValidatePartyCollection(content, [], 1);
        if (contentError is not null) return new("storage_content", PokemonError: contentError);
        if (party.IsDefault || party.Length > 6) return new("storage_party_count");
        if (storage is null) return new("storage_missing");
        if (storage.CurrentBox is < 0 or >= PokemonStorageState.BoxCount)
            return new("storage_current_box", BoxIndex: storage.CurrentBox);
        if (storage.Boxes.IsDefault || storage.Boxes.Length != PokemonStorageState.BoxCount)
            return new("storage_box_count");
        if (nextInstanceId is <= 0) return new("storage_next_instance_id");

        var ids = new HashSet<long>();
        for (int index = 0; index < party.Length; index++)
        {
            var owned = party[index];
            var error = PokemonStateValidator.ValidateParty(content, owned);
            if (error is not null) return new("storage_party_pokemon", PartyIndex: index, PokemonError: error);
            if (!ids.Add(owned.InstanceId)) return new("storage_duplicate_instance_id", PartyIndex: index);
            if (nextInstanceId.HasValue && owned.InstanceId >= nextInstanceId.Value)
                return new("storage_next_instance_id", PartyIndex: index);
        }

        for (int boxIndex = 0; boxIndex < storage.Boxes.Length; boxIndex++)
        {
            var box = storage.Boxes[boxIndex];
            if (box is null) return new("storage_box_missing", BoxIndex: boxIndex);
            if (PokemonStorageCustomizationRules.ValidateBoxMetadata(box) is { } metadataError)
                return new(metadataError, BoxIndex: boxIndex);
            if (box.Slots.IsDefault || box.Slots.Length != PokemonStorageState.SlotsPerBox)
                return new("storage_slot_count", BoxIndex: boxIndex);
            for (int slotIndex = 0; slotIndex < box.Slots.Length; slotIndex++)
            {
                var owned = box.Slots[slotIndex];
                if (owned is null) continue;
                if (SourceMailRules.ValidateBoxedPokemon(owned) is {} mailError)
                    return new("storage_boxed_mail", BoxIndex: boxIndex, SlotIndex: slotIndex, PokemonError: mailError);
                var error = PokemonStateValidator.ValidateParty(content, owned);
                if (error is not null)
                    return new("storage_box_pokemon", BoxIndex: boxIndex, SlotIndex: slotIndex, PokemonError: error);
                if (!ids.Add(owned.InstanceId))
                    return new("storage_duplicate_instance_id", BoxIndex: boxIndex, SlotIndex: slotIndex);
                if (nextInstanceId.HasValue && owned.InstanceId >= nextInstanceId.Value)
                    return new("storage_next_instance_id", BoxIndex: boxIndex, SlotIndex: slotIndex);
                if (PokemonStorageRules.IsMailItem(owned.HeldItem))
                    return new("storage_mail_unsupported", BoxIndex: boxIndex, SlotIndex: slotIndex);
                var pokemon = owned.Pokemon;
                if (owned.Status != 0 || pokemon.CurrentHp != pokemon.Stats.MaxHp)
                    return new("storage_boxed_health", BoxIndex: boxIndex, SlotIndex: slotIndex);
                if (pokemon.StatCalculationEvs is not null)
                    return new("storage_boxed_stat_cache", BoxIndex: boxIndex, SlotIndex: slotIndex);
                if (owned.IsEgg && pokemon.EggStatCalculationIvs != pokemon.Ivs)
                    return new("storage_boxed_egg_stat_cache", BoxIndex: boxIndex, SlotIndex: slotIndex);
                if (pokemon.Moves.Any(move => move.CurrentPp != move.MaximumPp))
                    return new("storage_boxed_pp", BoxIndex: boxIndex, SlotIndex: slotIndex);
            }
        }
        return null;
    }
}
