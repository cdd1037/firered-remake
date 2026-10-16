using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.Items;

/// <summary>TM/HM inventory and learning rules for the current game.</summary>
public static class MachineRules
{
    public static ItemId Hm05Id { get; } = new("pret:ITEM_HM05");
    public static ItemId Tm34Id { get; } = new("pret:ITEM_TM34");
    public static MoveId ShockWaveId { get; } = new("pret:MOVE_SHOCK_WAVE");
    public static ItemId Tm31Id { get; } = new("pret:ITEM_TM31");
    public static ItemId Tm44Id { get; } = new("pret:ITEM_TM44");
    public static ItemId Hm01Id { get; } = new("pret:ITEM_HM01");
    public static ItemId Tm45Id { get; } = new("pret:ITEM_TM45");
    public static ItemId Tm43Id { get; } = new("pret:ITEM_TM43");
    public static ItemId Tm28Id { get; } = new("pret:ITEM_TM28");
    public static ItemId Tm03Id { get; } = new("pret:ITEM_TM03");
    public static MoveId WaterPulseId { get; } = new("pret:MOVE_WATER_PULSE");
    public static ItemId Tm39Id { get; } = new("pret:ITEM_TM39");
    public static ItemId TmCaseId { get; } = new("pret:ITEM_TM_CASE");
    public static MoveId RockTombId { get; } = new("pret:MOVE_ROCK_TOMB");
    public static void ValidateCatalog(MachineCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.Machines.IsDefaultOrEmpty || catalog.MachineSlots <= 0 ||
            catalog.KeyItemSlots <= 0 || catalog.MaximumStack <= 0 || catalog.TmCase.Item.Id != TmCaseId ||
            catalog.TmCase.Item.CanToss || catalog.TmCase.Item.Pocket != "POCKET_KEY_ITEMS")
            throw new ArgumentException("machine_catalog_invalid");
        var ids = new HashSet<ItemId>();
        foreach (var definition in catalog.Machines)
            if (definition is null || !ids.Add(definition.Machine.Item.Id) || definition.MovePp <= 0 || string.IsNullOrWhiteSpace(definition.Machine.Description) ||
                definition.Machine.Item.Pocket != "POCKET_TM_CASE" || definition.Machine.Item.Price < 0 ||
                definition.Compatibility.IsDefault ||
                definition.Compatibility.Select(x => x.Species).Distinct().Count() != definition.Compatibility.Length)
                throw new ArgumentException("machine_definition_invalid");
    }

    /// <summary>Read-only lookup; null means the game does not define the machine.</summary>
    public static MachineDefinition? Definition(MachineCatalog catalog, ItemId machine)
        => catalog.Machines.FirstOrDefault(x => x.Machine.Item.Id == machine);

    /// <summary>Validates Machines and Key Items physical shape/CASE. Other pockets are validated by the inventory service.</summary>
    public static string? Validate(MachineCatalog catalog, InventoryState? state)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (state is null) return "inventory_missing";
        string? error = ValidatePocket(state.Machines, catalog.MachineSlots, true, catalog) ??
            ValidatePocket(state.KeyItems, catalog.KeyItemSlots, false, catalog);
        if (error is not null) return error;
        int cases = OpeningInventoryRules.Quantity(state.KeyItems ?? [], TmCaseId);
        if (cases > 1) return "inventory_tm_case_quantity";
        if ((state.Machines?.Length ?? 0) != 0 && cases != 1) return "inventory_tm_case_missing";
        return null;
    }

    private static string? ValidatePocket(ImmutableArray<InventoryStack>? optional, int capacity, bool machines, MachineCatalog catalog)
    {
        if (optional is null) return null;
        var stacks = optional.Value;
        if (stacks.IsDefault || stacks.Length > capacity)
            return machines ? "inventory_machines_capacity" : "inventory_key_items_capacity";
        var seen = new HashSet<ItemId>();
        foreach (var stack in stacks)
        {
            if (stack is null) return "inventory_stack_missing";
            if (machines && Definition(catalog, stack.Item) is null) return "inventory_machine_unsupported";
            if (stack.Quantity is < 1 or > 999) return "inventory_stack_quantity";
            if (!seen.Add(stack.Item)) return "inventory_duplicate_stack";
        }
        return null;
    }

    /// <summary>Source CheckBagHasSpace checks the TM pocket alone; this is not a promise AddBagItem can insert CASE.</summary>
    public static bool CanAddTm39(MachineCatalog catalog, InventoryState state, int quantity = 1)
        => CanAddMachine(catalog, state, Tm39Id, quantity);

    public static bool CanAddMachine(MachineCatalog catalog, InventoryState state, ItemId machine, int quantity = 1)
    {
        if (Validate(catalog, state) is not null || Definition(catalog, machine) is null || quantity is < 1 or > 999) return false;
        var stacks = state.Machines ?? [];
        int count = OpeningInventoryRules.Quantity(stacks, machine);
        return count != 0 ? count <= catalog.MaximumStack - quantity : stacks.Length < catalog.MachineSlots;
    }

    /// <summary>Remove stock atomically while retaining its reusable TM CASE.</summary>
    public static InventoryTransactionResult RemoveMachine(MachineCatalog catalog, InventoryState state, ItemId machine, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(state);
        InventoryTransactionResult Fail(string error) => new(state, false, error);
        if (Validate(catalog, state) is { } error) return Fail(error);
        if (Definition(catalog, machine) is null) return Fail("inventory_machine_unsupported");
        if (quantity is < 1 or > 999) return Fail("inventory_transfer_quantity");
        int count = OpeningInventoryRules.Quantity(state.Machines ?? [], machine);
        if (count < quantity) return Fail("inventory_insufficient_quantity");
        return new(state with { Machines = OpeningInventoryRules.SetQuantity(state.Machines ?? [], machine, count - quantity) }, true, null);
    }

    public static InventoryTransactionResult AddTm39(MachineCatalog catalog, InventoryState state, int quantity = 1)
        => AddMachine(catalog, state, Tm39Id, quantity);

    public static InventoryTransactionResult AddMachine(MachineCatalog catalog, InventoryState state, ItemId machine, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(state);
        InventoryTransactionResult Fail(string error) => new(state, false, error);
        string? error = Validate(catalog, state);
        if (error is not null) return Fail(error);
        if (Definition(catalog, machine) is null) return Fail("inventory_machine_unsupported");
        if (quantity is < 1 or > 999) return Fail("inventory_transfer_quantity");
        var machines = state.Machines ?? [];
        int count = OpeningInventoryRules.Quantity(machines, machine);
        if (count > catalog.MaximumStack - quantity) return Fail("inventory_stack_full");
        if (count == 0 && machines.Length >= catalog.MachineSlots) return Fail("inventory_destination_full");
        var keys = state.KeyItems ?? [];
        if (count == 0 && OpeningInventoryRules.Quantity(keys, TmCaseId) == 0)
        {
            if (keys.Length >= catalog.KeyItemSlots) return Fail("inventory_key_items_full");
            keys = keys.Add(new(TmCaseId, 1));
        }
        return new(state with { Machines = OpeningInventoryRules.SetQuantity(machines, machine, count + quantity),
            KeyItems = keys }, true, null);
    }

    public static MachineTeachResult TeachTm39(MachineCatalog catalog, GamePokemonCatalog campaign,
        InventoryState inventory, PartyPokemon pokemon, string currentMapSection, int? replacementSlot = null)
        => TeachMachine(catalog, campaign, inventory, pokemon, Tm39Id, currentMapSection, replacementSlot);

    public static MachineTeachResult TeachMachine(MachineCatalog catalog, GamePokemonCatalog campaign,
        InventoryState inventory, PartyPokemon pokemon, ItemId machine, string currentMapSection, int? replacementSlot = null)
    {
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(pokemon);
        ArgumentNullException.ThrowIfNull(campaign);
        var pokemonCatalog = campaign.Catalog;
        MachineTeachResult Fail(string error) => new(inventory, pokemon, false, error);
        string? error = Validate(catalog, inventory);
        if (error is not null) return Fail(error);
        var definition = Definition(catalog, machine);
        if (definition is null) return Fail("inventory_machine_unsupported");
        if (!PokemonMoveRules.IsLearningSupported(pokemonCatalog, definition.Move))
            return Fail("machine_move_execution_unsupported");
        error = PokemonStateValidator.Validate(pokemonCatalog, pokemon.Pokemon);
        if (error is not null) return Fail(error);
        if (!pokemonCatalog.Moves.TryGetValue(definition.Move, out var move) ||
            move.SourceMoveId != definition.SourceMoveId || move.Pp != definition.MovePp)
            return Fail("machine_pokemon_catalog");
        if (string.IsNullOrEmpty(currentMapSection) || !currentMapSection.StartsWith("MAPSEC_", StringComparison.Ordinal))
            return Fail("machine_map_section");
        if (pokemon.IsEgg) return Fail("machine_egg");
        int count = OpeningInventoryRules.Quantity(inventory.Machines ?? [], machine);
        if (count == 0) return Fail("inventory_insufficient_quantity");
        var compatibility = definition.Compatibility.FirstOrDefault(x => x.Species == pokemon.Pokemon.Species);
        if (compatibility is null || !compatibility.CanLearn) return Fail("machine_incompatible");
        var before = pokemon.Pokemon;
        if (before.Moves.Any(x => x.Move == definition.Move)) return Fail("machine_already_known");
        int slot;
        if (before.Moves.Length < 4)
        {
            if (replacementSlot is not null) return Fail("machine_invalid_slot");
            slot = before.Moves.Length;
        }
        else
        {
            if (replacementSlot is null) return Fail("machine_replacement_required");
            if (replacementSlot < 0 || replacementSlot >= 4) return Fail("machine_invalid_slot");
            slot = replacementSlot.Value;
            if (PokemonMoveRules.IsHiddenMachineMove(before.Moves[slot].Move)) return Fail("machine_cannot_forget_hm");
        }
        var learned = new PokemonMoveSlot(definition.Move, definition.MovePp, definition.MovePp);
        var moves = slot == before.Moves.Length ? before.Moves.Add(learned) : before.Moves.SetItem(slot, learned);
        // FRIENDSHIP_EVENT_LEARN_TMHM = {1,1,0}. Soothe Bell floor(1*1.5) remains 1.
        int delta = before.Friendship < 200 ? 1 : 0;
        if (delta > 0)
        {
            if (pokemon.Ball == "ITEM_LUXURY_BALL") delta++;
            if (pokemon.MetLocation == currentMapSection) delta++;
        }
        int friendship = Math.Min(255, before.Friendship + delta);
        var after = pokemon with { Pokemon = before with { Moves = moves, Friendship = friendship } };
        // party_menu.c consumes only source items below ITEM_HM01 (339).
        // The definition is already authenticated; reuse cannot depend on region.
        int consumed = definition.Machine.Item.SourceItemId < 339 ? 1 : 0;
        var afterInventory = inventory with { Machines = OpeningInventoryRules.SetQuantity(inventory.Machines ?? [], machine, count - consumed) };
        return new(afterInventory, after, true, null, slot, consumed, friendship - before.Friendship);
    }
}
