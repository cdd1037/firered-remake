using System.Collections.Immutable;
using Game.Domain.Growth;
using Game.Domain.Rules;

namespace Game.Domain.Pokemon;

/// <summary>Pure first-phase PC storage operations. No creation, trading, randomness or external mutation.</summary>
public static class PokemonStorageRules
{
    /// <summary>
    /// Delivers an already owned capture/gift. A free party slot retains injury/status/PP; otherwise
    /// searches the current box first, then later boxes cyclically, with ascending slot order.
    /// Ownership assignment belongs to the capture/gift caller, never to deposit or withdrawal.
    /// </summary>
    public static PokemonStorageResult Receive(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, PartyPokemon? incoming)
    {
        var error = PokemonStorageValidator.Validate(content, party, storage);
        if (error is not null) return Reject(party, storage, error);
        var pokemonError = PokemonStateValidator.ValidateParty(content, incoming);
        if (pokemonError is not null)
            return Reject(party, storage, new("storage_incoming_pokemon", PokemonError: pokemonError));
        var owned = incoming!;
        if (party.Any(mon => mon.InstanceId == owned.InstanceId) ||
            storage.Boxes.Any(box => box.Slots.Any(mon => mon?.InstanceId == owned.InstanceId)))
            return Reject(party, storage, new("storage_duplicate_instance_id"));
        if (party.Length < 6)
            return new(PokemonStorageOutcome.ReceivedInParty, party.Add(owned), storage, null, null);
        if (IsMailItem(owned.HeldItem)) return Reject(party, storage, new("storage_mail_unsupported"));
        for (int offset = 0; offset < PokemonStorageState.BoxCount; offset++)
        {
            int boxIndex = (storage.CurrentBox + offset) % PokemonStorageState.BoxCount;
            int slotIndex = FirstFreeSlot(storage.Boxes[boxIndex]);
            if (slotIndex < 0) continue;
            var address = new PokemonStorageAddress(boxIndex, slotIndex);
            return new(PokemonStorageOutcome.ReceivedInBox, party,
                Put(storage, address, ToBoxProjection(content!.Catalog, owned)), address, null);
        }
        return Reject(party, storage, new("storage_full"));
    }

    /// <summary>Stores into the chosen box's first empty slot; it never spills into another box.</summary>
    public static PokemonStorageResult Deposit(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, int partyIndex, int boxIndex)
    {
        var error = PokemonStorageValidator.Validate(content, party, storage);
        if (error is not null) return Reject(party, storage, error);
        if (partyIndex < 0 || partyIndex >= party.Length)
            return Reject(party, storage, new("storage_party_index", PartyIndex: partyIndex));
        if (!ValidBoxIndex(boxIndex)) return Reject(party, storage, new("storage_box_index", BoxIndex: boxIndex));
        // Source checks the remaining able party, not merely party count or the selected mon's HP.
        if (!party.Where((_, index) => index != partyIndex).Any(mon => !mon.IsEgg && mon.Pokemon.CurrentHp > 0))
            return Reject(party, storage, new("storage_last_usable", PartyIndex: partyIndex));
        if (IsMailItem(party[partyIndex].HeldItem))
            return Reject(party, storage, new("storage_mail_unsupported", PartyIndex: partyIndex));
        int slotIndex = FirstFreeSlot(storage.Boxes[boxIndex]);
        if (slotIndex < 0) return Reject(party, storage, new("storage_box_full", BoxIndex: boxIndex));
        var address = new PokemonStorageAddress(boxIndex, slotIndex);
        var boxed = ToBoxProjection(content!.Catalog, party[partyIndex]);
        return new(PokemonStorageOutcome.Deposited, party.RemoveAt(partyIndex), Put(storage, address, boxed), address, null);
    }

    /// <summary>Appends to the compact party and clears only the selected box slot.</summary>
    public static PokemonStorageResult Withdraw(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, int boxIndex, int slotIndex)
    {
        var error = PokemonStorageValidator.Validate(content, party, storage);
        if (error is not null) return Reject(party, storage, error);
        if (!ValidBoxIndex(boxIndex)) return Reject(party, storage, new("storage_box_index", BoxIndex: boxIndex));
        if (slotIndex is < 0 or >= PokemonStorageState.SlotsPerBox)
            return Reject(party, storage, new("storage_slot_index", BoxIndex: boxIndex, SlotIndex: slotIndex));
        if (party.Length >= 6) return Reject(party, storage, new("storage_party_full"));
        var owned = storage.Boxes[boxIndex].Slots[slotIndex];
        if (owned is null)
            return Reject(party, storage, new("storage_slot_empty", BoxIndex: boxIndex, SlotIndex: slotIndex));
        var address = new PokemonStorageAddress(boxIndex, slotIndex);
        // Stored projections already represent exactly what source BoxMonToMon reconstructs.
        return new(PokemonStorageOutcome.Withdrawn, party.Add(owned), Put(storage, address, null), address, null);
    }

    public static PokemonStorageResult SelectBox(CampaignPokemonContent? content,
        ImmutableArray<PartyPokemon> party, PokemonStorageState storage, int boxIndex)
    {
        var error = PokemonStorageValidator.Validate(content, party, storage);
        if (error is not null) return Reject(party, storage, error);
        if (!ValidBoxIndex(boxIndex)) return Reject(party, storage, new("storage_box_index", BoxIndex: boxIndex));
        return new(PokemonStorageOutcome.BoxSelected, party,
            storage.CurrentBox == boxIndex ? storage : storage with { CurrentBox = boxIndex }, null, null);
    }

    internal static bool IsMailItem(string item) => item is "ITEM_ORANGE_MAIL" or "ITEM_HARBOR_MAIL" or
        "ITEM_GLITTER_MAIL" or "ITEM_MECH_MAIL" or "ITEM_WOOD_MAIL" or "ITEM_WAVE_MAIL" or "ITEM_BEAD_MAIL" or
        "ITEM_SHADOW_MAIL" or "ITEM_TROPIC_MAIL" or "ITEM_DREAM_MAIL" or "ITEM_FAB_MAIL" or "ITEM_RETRO_MAIL";

    private static bool ValidBoxIndex(int boxIndex) => boxIndex is >= 0 and < PokemonStorageState.BoxCount;

    private static int FirstFreeSlot(PokemonStorageBox box)
    {
        for (int index = 0; index < box.Slots.Length; index++) if (box.Slots[index] is null) return index;
        return -1;
    }

    private static PokemonStorageResult Reject(ImmutableArray<PartyPokemon> party,
        PokemonStorageState storage, PokemonStorageDiagnostic error) =>
        new(PokemonStorageOutcome.Rejected, party, storage, null, error);

    private static PokemonStorageState Put(PokemonStorageState storage, PokemonStorageAddress address, PartyPokemon? owned)
    {
        var box = storage.Boxes[address.BoxIndex];
        return storage with { Boxes = storage.Boxes.SetItem(address.BoxIndex,
            box with { Slots = box.Slots.SetItem(address.SlotIndex, owned) }) };
    }

    internal static PartyPokemon ToBoxProjection(PokemonCatalog catalog, PartyPokemon owned)
    {
        var pokemon = owned.Pokemon;
        var species = catalog.Species[pokemon.Species];
        int level = GrowthRules.LevelFromExperience(species.GrowthRate, pokemon.Experience);
        var b = species.BaseStats;
        var iv = pokemon.Ivs;
        var ev = pokemon.Evs;
        int Other(int basis, int value, int effort, Stat stat) =>
            StatRules.CalculateOther(basis, value, effort, level, pokemon.Nature, stat);
        var stats = new BattleStats(StatRules.CalculateHp(b.Hp, iv.Hp, ev.Hp, level),
            Other(b.Attack, iv.Attack, ev.Attack, Stat.Attack),
            Other(b.Defense, iv.Defense, ev.Defense, Stat.Defense),
            Other(b.SpecialAttack, iv.SpecialAttack, ev.SpecialAttack, Stat.SpecialAttack),
            Other(b.SpecialDefense, iv.SpecialDefense, ev.SpecialDefense, Stat.SpecialDefense),
            Other(b.Speed, iv.Speed, ev.Speed, Stat.Speed));
        return owned with
        {
            Status = 0,
            Mail = 255,
            Pokemon = pokemon with
            {
                Level = level,
                Stats = stats,
                StatCalculationEvs = null,
                CurrentHp = stats.MaxHp,
                Moves = pokemon.Moves.Select(move => move with { CurrentPp = move.MaximumPp }).ToImmutableArray()
            }
        };
    }
}
