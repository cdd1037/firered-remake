using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Application;

// These are the original native tasks, not a generic close confirmation:
// B asks CONTINUE BOX, CLOSE asks EXIT BOX, and either while holding asks PUT IN BAG.
public enum CampaignStorageItemPrompt { PutInBag, ContinueOperations, Exit }
public sealed record CampaignStorageItemBagTarget(PokemonStorageMoveLocation Location, long InstanceId, string HeldItemId);

public sealed partial class CampaignSession
{
    private Decision<CampaignState> DecideStorageItems(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (state.Pc is not { View: CampaignPcView.MoveItems, MoveCursor: null } pc ||
            state.Storage is not {} storage || state.Inventory is not {} inventory || _content.EarlyShop is not {} items)
            return No("storage_item_cursor_context");
        if (ValidateStorageItemOwnership(state) is {} invalid) return No(invalid);
        var content = _content.PokemonContent;
        var machines = _content.Machines;
        Decision<CampaignState> ClearPrompt() => new(CommandStatus.Accepted, state with { Pc = pc with { ItemPrompt = null } });
        Decision<CampaignState> Exit()
        {
            var exit = PokemonStorageItemRules.CheckExit(content, items, state.Party, storage, inventory, pc.ItemCursor, machines);
            return exit.Succeeded
                ? new(CommandStatus.Accepted, state with { Pc = pc with { View = CampaignPcView.StorageMenu, ItemPrompt = null } })
                : No(exit.Diagnostic!.Code);
        }
        Decision<CampaignState> Commit(PokemonStorageItemResult result)
        {
            if (!result.Succeeded) return No(result.Diagnostic!.Code);
            var next = CommitInventory(state, result.Inventory) with { Party = result.Party, Storage = result.Storage,
                Pc = pc with { ItemCursor = result.Cursor, ItemPrompt = null, ItemBagTarget = null } };
            return ValidateStorageItemOwnership(next) is {} error ? No(error) : new(CommandStatus.Accepted, next);
        }
        if (pc.ItemBagTarget is {} bagTarget)
        {
            if (command.Kind is "pc_back" or "storage_item_bag_cancel")
                return new(CommandStatus.Accepted, state with { Pc = pc with { ItemBagTarget = null } });
            if (command.Kind != "storage_item_from_bag") return No("storage_item_bag_active");
            if (command.StorageLocation != bagTarget.Location || command.TargetInstanceId != bagTarget.InstanceId ||
                command.TargetHeldItemId != bagTarget.HeldItemId) return No("storage_item_bag_target_identity");
        }
        else if (command.Kind == "storage_item_from_bag") return No("storage_item_bag_not_open");
        if (pc.ItemPrompt is {} prompt)
        {
            int? choice = command.Kind == "pc_back" ? 127 : command.Kind == "storage_item_prompt" ? command.Choice : null;
            if (choice is null) return No("storage_item_prompt_active");
            if (choice is not (0 or 1 or 127)) return No("storage_item_prompt_choice");
            if (prompt == CampaignStorageItemPrompt.PutInBag)
            {
                if (choice != 0) return ClearPrompt();
                if (string.IsNullOrEmpty(command.HeldItemId)) return No("storage_item_cursor_identity");
                // Task_CloseBoxWhileHoldingItem returns to BOX after putting the item away.
                // A full Bag leaves the cursor and prompt intact, including its cancel path.
                return Commit(PokemonStorageItemRules.CursorToBag(content, items, state.Party, storage, inventory,
                    pc.ItemCursor, new(command.HeldItemId), machines));
            }
            bool exit = prompt == CampaignStorageItemPrompt.Exit ? choice == 0 : choice != 0;
            return exit ? Exit() : ClearPrompt();
        }
        if (command.Kind is "pc_back" or "storage_item_close")
        {
            var nextPrompt = pc.ItemCursor is not null ? CampaignStorageItemPrompt.PutInBag :
                command.Kind == "pc_back" ? CampaignStorageItemPrompt.ContinueOperations : CampaignStorageItemPrompt.Exit;
            return new(CommandStatus.Accepted, state with { Pc = pc with { ItemPrompt = nextPrompt } });
        }
        if (command.Kind == "storage_box")
            return command.Choice is {} box
                ? Commit(PokemonStorageItemRules.SelectBox(content, items, state.Party, storage, inventory, pc.ItemCursor, box, machines))
                : No("storage_box_index");
        if (command.Kind is not ("storage_item_take" or "storage_item_give" or "storage_item_switch" or
            "storage_item_to_bag" or "storage_item_from_bag" or "storage_item_bag_open")) return No("storage_item_active");
        if (command.Quantity != 1) return No("storage_item_quantity_invalid");
        if (command.StorageLocation is not {} location) return No("storage_item_location_missing");
        if (command.TargetInstanceId is not {} target) return No("storage_item_target_identity");
        if (string.IsNullOrEmpty(command.TargetHeldItemId) ||
            OwnedPokemon(state).FirstOrDefault(p => p.InstanceId == target) is {} selected &&
            command.TargetHeldItemId != "pret:" + selected.HeldItem) return No("storage_item_target_item_identity");
        if (command.Kind == "storage_item_bag_open")
        {
            var targetError = PokemonStorageItemRules.CheckGiveFromBagTarget(content, items, state.Party, storage,
                inventory, pc.ItemCursor, location, target, machines);
            return targetError is not null ? No(targetError.Code) :
                new(CommandStatus.Accepted, state with { Pc = pc with { ItemBagTarget = new(location, target, command.TargetHeldItemId) } });
        }
        if (command.Kind is "storage_item_give" or "storage_item_switch" && string.IsNullOrEmpty(command.HeldItemId))
            return No("storage_item_cursor_identity");
        if (command.Kind == "storage_item_from_bag" && string.IsNullOrEmpty(command.Text)) return No("held_item_not_holdable");
        return Commit(command.Kind switch
        {
            "storage_item_take" => PokemonStorageItemRules.Take(content, items, state.Party, storage, inventory, pc.ItemCursor, location, target, machines),
            "storage_item_give" => PokemonStorageItemRules.Give(content, items, state.Party, storage, inventory, pc.ItemCursor, location, target, new(command.HeldItemId!), machines),
            "storage_item_switch" => PokemonStorageItemRules.Switch(content, items, state.Party, storage, inventory, pc.ItemCursor, location, target, new(command.HeldItemId!), machines),
            "storage_item_to_bag" => PokemonStorageItemRules.ToBag(content, items, state.Party, storage, inventory, pc.ItemCursor, location, target, machines),
            "storage_item_from_bag" => PokemonStorageItemRules.GiveFromBag(content, items, state.Party, storage, inventory, pc.ItemCursor, location, target, new(command.Text!), machines),
            _ => throw new InvalidOperationException("storage_item_command_dispatch")
        });
    }

    private string? ValidateStorageItemOwnership(CampaignState state)
    {
        if (state.Pc is not { View: CampaignPcView.MoveItems, MoveCursor: null } pc || state.Storage is null ||
            state.Inventory is null || _content.EarlyShop is null) return "storage_item_cursor_context";
        if (pc.ItemPrompt is {} prompt && (!Enum.IsDefined(prompt) ||
            (prompt == CampaignStorageItemPrompt.PutInBag) != (pc.ItemCursor is not null))) return "storage_item_prompt_context";
        if (pc.ItemBagTarget is {} target)
        {
            if (pc.ItemPrompt is not null || pc.ItemCursor is not null || target.HeldItemId != "pret:ITEM_NONE") return "storage_item_bag_context";
            var error = PokemonStorageItemRules.CheckGiveFromBagTarget(_content.PokemonContent, _content.EarlyShop,
                state.Party, state.Storage, state.Inventory, pc.ItemCursor,
                target.Location, target.InstanceId, _content.Machines);
            if (error is not null) return error.Code;
        }
        return ValidateCampaignInventory(state.Inventory) ?? ValidateStorageMoveOwnership(state) ??
            PokemonStorageItemRules.Validate(_content.PokemonContent, _content.EarlyShop, state.Party, state.Storage,
                state.Inventory, pc.ItemCursor, _content.Machines, state.NextPokemonId)?.Code;
    }
}
