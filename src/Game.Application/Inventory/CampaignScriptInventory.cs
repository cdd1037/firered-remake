using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // Inventory primitives belong to the source interpreter. Reward flags, choices and
    // currency remain in the calling script (or its explicit gameplay callback).
    private bool TryApplyScriptInventoryEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode is not ("checkitem" or "checkitemspace" or "additem" or "removeitem" or
            "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        int Value(string token) => token.StartsWith("VAR_", StringComparison.Ordinal) ? state.Script.Variables.GetValueOrDefault(token) :
            int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number :
            token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(token[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number) ? number :
            _content.TryConstant(token, out number) ? number : throw new InvalidDataException("script_inventory_operand_unknown: " + token);
        var args = effect.Args;
        bool buffer = effect.Opcode is "bufferitemname" or "bufferitemnameplural";
        int arity = effect.Opcode switch { "bufferitemnameplural" => 3, "checkitemtype" => 1, _ => 2 };
        if (args.Length != arity) throw new InvalidDataException("script_inventory_effect_arity");
        // Authenticate the suspended instruction, including substituted variable operands.
        // Merely naming a source map or having it somewhere in the call stack is insufficient.
        var frame = state.Script.Stack.IsEmpty ? null : state.Script.Stack[^1];
        var program = frame is null ? null : _content.FindProgram(frame.ProgramId);
        int index = (frame?.InstructionIndex ?? 0) - 1;
        if (program is null || index < 0 || index >= program.Instructions.Length ||
            state.Script.Wait is not { Kind: StoryWaitKind.NativeEffect } wait || wait.CorrelationId != "event:" + effect.Sequence ||
            program.Instructions[index] is not { } instruction || instruction.Source != effect.Source ||
            instruction.Opcode != effect.Opcode || instruction.Args.Length != args.Length ||
            !instruction.Args.Select((a, i) => a.StartsWith("VAR_", StringComparison.Ordinal) ? Value(a) == Value(args[i]) : a == args[i]).All(equal => equal))
            throw new InvalidDataException("script_inventory_effect_context");
        int sourceId = Value(args[buffer ? 1 : 0]);
        var item = _content.Inventory.Items.Values.SingleOrDefault(i => i.SourceItemId == sourceId)
            ?? throw new InvalidDataException("script_inventory_item_not_supported");
        CampaignState Result(int value) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", value) } };
        if (buffer)
        {
            if (args[0] is not ("STR_VAR_1" or "STR_VAR_2" or "STR_VAR_3"))
                throw new InvalidDataException("script_inventory_buffer_invalid");
            int quantity = args.Length == 3 ? Value(args[2]) : 1;
            if (quantity is < 0 or > ushort.MaxValue) throw new InvalidDataException("script_inventory_quantity_invalid");
            string name = item.Name;
            // Source ScrCmd_bufferitemnameplural only pluralizes Poke Balls and
            // berries before Enigma Berry; ordinary item names remain unchanged.
            if (quantity >= 2 && item.SourceItemSymbol == "ITEM_POKE_BALL") name += "S";
            else if (quantity >= 2 && item.Pocket == "POCKET_BERRY_POUCH" && item.SourceItemSymbol != "ITEM_ENIGMA_BERRY" && name.Length > 0)
                name = name[..^1] + "IES";
            next = state with { TextBuffers = state.TextBuffers.SetItem(args[0], name) };
            return true;
        }
        if (effect.Opcode == "checkitemtype") { next = Result(Value(item.Pocket)); return true; }
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        if (ValidateCampaignInventory(inventory) is { } invalid) throw new InvalidDataException(invalid);
        int countRequested = Value(args[1]);
        if (countRequested < 1 || countRequested > _content.Inventory.Limits.MaximumStack)
            throw new InvalidDataException("script_inventory_quantity_invalid");
        var pocket = item.Pocket switch
        {
            "POCKET_TM_CASE" => inventory.Machines ?? [], "POCKET_KEY_ITEMS" => inventory.KeyItems ?? [],
            "POCKET_BERRY_POUCH" => inventory.Berries ?? [], "POCKET_POKE_BALLS" => inventory.Balls ?? [], _ => inventory.BagItems
        };
        int count = OpeningInventoryRules.Quantity(pocket, item.Id);
        if (effect.Opcode == "checkitem") { next = Result(count >= countRequested ? 1 : 0); return true; }
        bool check = effect.Opcode == "checkitemspace", remove = effect.Opcode == "removeitem";
        InventoryTransactionResult transaction;
        if (!remove && _content.Inventory.Singletons.Contains(item.Id) && count + countRequested > 1)
            transaction = new(inventory, false, "inventory_stack_full");
        else if (item.Pocket == "POCKET_TM_CASE")
        {
            var machines = _content.Machines ?? throw new InvalidDataException("script_inventory_machine_catalog_missing");
            transaction = check ? new(inventory, MachineRules.CanAddMachine(machines, inventory, item.Id, countRequested), null) :
                remove ? MachineRules.RemoveMachine(machines, inventory, item.Id, countRequested) : MachineRules.AddMachine(machines, inventory, item.Id, countRequested);
        }
        else transaction = check ? EarlyInventoryAddRules.CheckSpace(ShopCatalog, inventory, item.Id, countRequested) :
            remove ? EarlyInventoryAddRules.Remove(ShopCatalog, inventory, item.Id, countRequested) : EarlyInventoryAddRules.Add(ShopCatalog, inventory, item.Id, countRequested);
        if (!transaction.Succeeded && transaction.Error is not (null or "inventory_stack_full" or "inventory_destination_full" or "inventory_key_items_full" or "inventory_insufficient_quantity"))
            throw new InvalidDataException(transaction.Error);
        next = transaction.Succeeded && !check ? CommitInventory(Result(1), transaction.State) : Result(transaction.Succeeded ? 1 : 0);
        return true;
    }
}
