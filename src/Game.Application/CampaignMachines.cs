using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Application;

/// <summary>Bounded field-only TM CASE. All learning and consumption is one existing pure transaction.</summary>
public sealed partial class CampaignSession
{
    private Decision<CampaignState> DecideMachine(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        var machines = _content.PewterMachines!;
        if (state.ItemMenu is not { Origin: "bag" } menu || state.Inventory is null)
            return No("machine_context_invalid");
        if (command.Kind == "machine_open")
        {
            if (menu.View != "bag") return No("machine_context_invalid");
            if (OpeningInventoryRules.Quantity(state.Inventory.KeyItems ?? [], MachineRules.TmCaseId) != 1)
                return No("inventory_tm_case_missing");
            return new(CommandStatus.Accepted, state with { ItemMenu = menu with { View = "machines" } });
        }
        if (menu.View != "machines") return No("machine_context_invalid");
        if (command.PartyIndex < 0 || command.PartyIndex >= state.Party.Length) return No("item_party_index_invalid");
        // Existing callers omit Text for TM39. Other values are stable item IDs, never a
        // display-name/slot fallback that could consume a different machine.
        var machine = command.Text is null ? MachineRules.Tm39Id : new ItemId(command.Text);
        var definition = MachineRules.Definition(machines, machine);
        if (definition is null) return No("inventory_machine_unsupported");
        var inventoryError = ValidateCampaignInventory(state.Inventory);
        if (inventoryError is not null) return No(inventoryError);
        // -1 is the existing CampaignCommand convention for an omitted move slot; no other
        // negative value is normalized, so malformed replacement commands remain rejected.
        var result = MachineRules.TeachMachine(machines, _binding.Ownership,
            state.Inventory, state.Party[command.PartyIndex], machine,
            _content.MapDefinition(state.World.Player.MapId).RegionMapSection,
            command.MoveSlot == -1 ? null : command.MoveSlot);
        if (!result.Succeeded) return No(result.Error!);
        return new(CommandStatus.Accepted, state with
        {
            Inventory = result.Inventory,
            Party = state.Party.SetItem(command.PartyIndex, result.Pokemon),
            TextBuffers = state.TextBuffers.SetItem("STR_VAR_1", result.Pokemon.Nickname)
                .SetItem("STR_VAR_2", _content.BattleContent!.MoveNames[definition.Move.Value]),
            ItemMenu = menu with { View = "message", MessageSymbol = "gText_PkmnLearnedMove3", ReturnView = "machines" }
        });
    }
}
