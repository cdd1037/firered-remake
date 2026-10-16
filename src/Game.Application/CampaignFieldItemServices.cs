using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignFieldItemService(string Id);

public sealed partial class CampaignSession
{
    private CampaignState BeginFieldItemService(CampaignState state, string program)
    {
        var matches = _content.FieldItemServices.Where(s => s.Map == state.World.Player.MapId && s.ScriptId == program).ToArray();
        if (matches.Length == 0) return state;
        if (matches.Length != 1 || state.FieldItemService is not null || state.FieldPickup is not null)
            throw new InvalidDataException("field_item_service_entry_ambiguous");
        var service = matches[0]; var map = _content.MapDefinition(service.Map);
        if (string.IsNullOrEmpty(service.Id) || service.ProgramIds.IsDefaultOrEmpty ||
            !service.ProgramIds.Contains(program) || service.ItemSymbols.IsDefaultOrEmpty ||
            service.ProgramIds.Distinct(StringComparer.Ordinal).Count() != service.ProgramIds.Length ||
            service.ItemSymbols.Distinct(StringComparer.Ordinal).Count() != service.ItemSymbols.Length ||
            service.ProgramIds.Any(p => _content.FindProgram(p) is null))
            throw new InvalidDataException("field_item_service_definition_invalid");
        if (service.SourceLocalId is { } local)
        {
            // Source transitions may relocate an actor template; identity is stable across those moves.
            var source = map.Actors.SingleOrDefault(a => a.SourceLocalId == local);
            if (source is null || source.IsClone || source.ScriptId != program || state.LastTalked is not { } id ||
                !state.Actors.TryGetValue(id, out var actor) || actor.SourceLocalId != local || actor.ScriptId != program ||
                !actor.Active || actor.Invisible)
                throw new InvalidDataException("field_item_service_actor_context");
        }
        else
        {
            var player = state.World.Player; var delta = Delta(player.Facing);
            if (service.X is not { } x || service.Y is not { } y || x != player.X + delta.Item1 || y != player.Y + delta.Item2 ||
                !map.BackgroundTriggers.Any(b => b.X == x && b.Y == y && b.ScriptId == program && b.HiddenItem is null &&
                    (b.Elevation == 0 || b.Elevation == player.Elevation)))
                throw new InvalidDataException("field_item_service_background_context");
        }
        if (ValidateCampaignInventory(state.Inventory) is { } error) throw new InvalidDataException(error);
        return state with { FieldItemService = new(service.Id) };
    }

    // Imported source scripts retain all choices, flags, order and deferred-reward branches.
    // This adapter only supplies shared inventory primitives for their authenticated call graph.
    private bool TryApplyFieldItemServiceEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        bool cut = effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "QuestLog_CutRecording" });
        if (state.FieldItemService is not { } context || !(cut || effect.Opcode is
            "checkplayergender" or "checkitem" or "checkitemspace" or "additem" or "removeitem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        int Value(string token) => token.StartsWith("VAR_", StringComparison.Ordinal) ? state.Script.Variables.GetValueOrDefault(token) :
            int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number :
            token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(token[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out number) ? number :
            _content.TryConstant(token, out number) ? number : throw new InvalidDataException("field_item_service_operand_unknown: " + token);
        var service = _content.FieldItemServices.SingleOrDefault(s => s.Id == context.Id);
        if (service is null || service.Map != state.World.Player.MapId ||
            !service.ProgramIds.Contains(effect.Source.Symbol) || _content.FindProgram(effect.Source.Symbol) is not { } source ||
            !source.Instructions.Any(i => i.Source == effect.Source && i.Opcode == effect.Opcode && i.Args.Length == effect.Args.Length &&
                i.Args.Select((a, index) => a.StartsWith("VAR_", StringComparison.Ordinal)
                    ? Value(a) == Value(effect.Args[index]) : a == effect.Args[index]).All(matches => matches)))
            throw new InvalidDataException("field_item_service_effect_context");
        if (cut)
        {
            if (state.Script.QuestLogState != 0) throw new InvalidDataException("field_item_service_questlog_context");
            return true;
        }
        if (!state.Script.Locked) throw new InvalidDataException("field_item_service_unlocked");
        if (effect.Opcode == "checkplayergender")
        {
            if (!effect.Args.IsEmpty || state.PlayerGender is < 0 or > 1)
                throw new InvalidDataException("field_item_service_gender_context");
            next = state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", state.PlayerGender) } };
            return true;
        }
        var args = effect.Args; bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        if (buffer ? args.Length is < 2 or > 3 : args.Length is < 1 or > 2)
            throw new InvalidDataException("field_item_service_effect_arity");
        int sourceId = Value(args[buffer ? 1 : 0]);
        string? symbol = service.ItemSymbols.SingleOrDefault(s => _content.TryConstant(s, out int value) && value == sourceId);
        if (symbol is null) throw new InvalidDataException("field_item_service_item_not_declared");
        var itemId = new ItemId("pret:" + symbol);
        var machine = _content.Machines is { } machines ? MachineRules.Definition(machines, itemId) : null;
        var ordinary = machine is null ? ShopCatalog.Items.SingleOrDefault(i => i.Id == itemId) : null;
        if (machine is null && ordinary is null) throw new InvalidDataException("field_item_service_item_not_supported");
        int actualSourceId = machine?.Machine.Item.SourceItemId ?? ordinary!.SourceItemId;
        string itemName = machine?.Machine.Item.Name ?? ordinary!.Name;
        string pocket = machine?.Machine.Item.Pocket ?? ordinary!.Pocket;
        if (actualSourceId != sourceId || pocket == "POCKET_KEY_ITEMS")
            throw new InvalidDataException("field_item_service_item_not_supported");
        CampaignState Result(int value) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", value) } };
        if (buffer)
        {
            if (args[0] is not ("STR_VAR_1" or "STR_VAR_2" or "STR_VAR_3") || args.Length == 3 && Value(args[2]) != 1)
                throw new InvalidDataException("field_item_service_buffer_unsupported");
            next = state with { TextBuffers = state.TextBuffers.SetItem(args[0], itemName) }; return true;
        }
        if (effect.Opcode == "checkitemtype") { next = Result(Value(pocket)); return true; }
        if (state.Inventory is not { } inventory) throw new InvalidDataException("inventory_missing");
        if (ValidateCampaignInventory(inventory) is { } invalid) throw new InvalidDataException(invalid);
        int quantity = args.Length == 1 ? 1 : Value(args[1]);
        if (quantity is < 1 or > 999) throw new InvalidDataException("field_item_service_quantity_invalid");
        int count = OpeningInventoryRules.Quantity(machine is not null ? inventory.Machines ?? [] :
            pocket switch { "POCKET_BERRY_POUCH" => inventory.Berries ?? [], "POCKET_POKE_BALLS" => inventory.Balls ?? [], _ => inventory.BagItems }, itemId);
        if (effect.Opcode == "checkitem") { next = Result(count >= quantity ? 1 : 0); return true; }
        bool check = effect.Opcode == "checkitemspace", remove = effect.Opcode == "removeitem";
        InventoryTransactionResult transaction;
        if (machine is not null)
        {
            transaction = check ? new(inventory, MachineRules.CanAddMachine(_content.Machines!, inventory, itemId, quantity), null) :
                remove ? MachineRules.RemoveMachine(_content.Machines!, inventory, itemId, quantity) :
                MachineRules.AddMachine(_content.Machines!, inventory, itemId, quantity);
        }
        else
        {
            var view = inventory;
            transaction = check ? EarlyInventoryAddRules.CheckSpace(ShopCatalog, view, itemId, quantity) :
                remove ? EarlyInventoryAddRules.Remove(ShopCatalog, view, itemId, quantity) : EarlyInventoryAddRules.Add(ShopCatalog, view, itemId, quantity);
            if (transaction.Succeeded && !check) transaction = transaction with { State = transaction.State };
        }
        if (!transaction.Succeeded && transaction.Error is not (null or "inventory_stack_full" or "inventory_destination_full" or "inventory_key_items_full" or "inventory_insufficient_quantity"))
            throw new InvalidDataException(transaction.Error);
        next = CommitInventory(Result(transaction.Succeeded ? 1 : 0), transaction.Succeeded && !check ? transaction.State : inventory);
        return true;
    }
}
