using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private string? ValidatePokedexAideProgress(CampaignState state)
    {
        var received = _content.PokedexAides.ByActor.Values.Where(aide => state.Script.Flags.GetValueOrDefault(aide.RewardFlag)).ToArray();
        if (received.Length == 0) return null;
        if (!state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET")) return "save_pokedex_aide_pokedex_required";
        int caught = _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies).Caught;
        return received.Any(aide => caught < aide.RequiredCaught) ? "save_pokedex_aide_caught_requirement" : null;
        // Ordinary rewards may subsequently be held, sold, stored or discarded. The flag
        // records receipt; it does not require continued possession of the reward.
    }

    private bool TryApplyPokedexAideEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (state.LastTalked is not { } actorId ||
            !_content.PokedexAides.ByActor.TryGetValue((state.World.Player.MapId, actorId), out var aide) ||
            !aide.Effects.TryGetValue(new(effect.Source, effect.Opcode), out var instruction)) return false;
        int Value(string token) => token.StartsWith("VAR_", StringComparison.Ordinal) ? state.Script.Variables.GetValueOrDefault(token) :
            int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value :
            token.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && int.TryParse(token[2..], NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out value) ? value : _content.TryConstant(token, out value) ? value :
            throw new InvalidDataException("pokedex_aide_operand_unknown:" + token);
        if (!state.Actors.TryGetValue(actorId, out var actor) || !actor.Active || actor.Invisible ||
            actor.ScriptId != aide.Actor.ScriptId || actor.SourceLocalId != aide.Actor.SourceLocalId ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId ||
            !state.Script.Locked || state.Script.QuestLogState != 0 ||
            instruction.Args.Length != effect.Args.Length ||
            !instruction.Args.Select((arg, index) => arg.StartsWith("VAR_", StringComparison.Ordinal) && effect.Opcode != "specialvar"
                ? Value(arg) == Value(effect.Args[index]) : arg == effect.Args[index]).All(matches => matches))
            throw new InvalidDataException("pokedex_aide_effect_context");
        var args = effect.Args;
        CampaignState Result(int result) => state with { Script = state.Script with
            { Variables = state.Script.Variables.SetItem("VAR_RESULT", result) } };
        if (effect.Opcode == "specialvar")
        {
            if (!args.SequenceEqual(new[] { "VAR_RESULT", "GetPokedexCount" }) ||
                state.Script.Variables.GetValueOrDefault("VAR_0x8004") != 0 ||
                !state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"))
                throw new InvalidDataException("pokedex_aide_count_context");
            var counts = _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies);
            next = Result(NationalDexEnabledIn(state) ? 1 : 0);
            next = next with { Script = next.Script with { Variables = next.Script.Variables
                .SetItem("VAR_0x8005", counts.Seen).SetItem("VAR_0x8006", counts.Caught) } };
            return true;
        }
        if (effect.Opcode == "buffernumberstring")
        {
            if (args.Length != 2 || !(args[0] == "STR_VAR_1" && Value(args[1]) == aide.RequiredCaught ||
                args[0] == "STR_VAR_3" && instruction.Args[1] == "VAR_0x8006" &&
                    Value(args[1]) == _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies).Caught))
                throw new InvalidDataException("pokedex_aide_number_buffer");
            next = state with { TextBuffers = state.TextBuffers.SetItem(args[0], Value(args[1]).ToString(CultureInfo.InvariantCulture)) };
            return true;
        }
        bool buffer = effect.Opcode is "bufferitemname" or "bufferitemnameplural";
        if (buffer ? args.Length is < 2 or > 3 : args.Length is < 1 or > 2)
            throw new InvalidDataException("pokedex_aide_item_arity");
        if (Value(args[buffer ? 1 : 0]) != aide.Item.SourceItemId)
            throw new InvalidDataException("pokedex_aide_item_mismatch");
        if (buffer)
        {
            if (args[0] != "STR_VAR_2" || args.Length == 3 && Value(args[2]) != aide.Quantity)
                throw new InvalidDataException("pokedex_aide_item_buffer");
            next = state with { TextBuffers = state.TextBuffers.SetItem(args[0], aide.Item.Name) };
            return true;
        }
        if (effect.Opcode == "checkitemtype") { next = Result(Value(aide.Item.Pocket)); return true; }
        if (effect.Opcode is not ("checkitemspace" or "additem")) return false;
        if ((args.Length == 1 ? 1 : Value(args[1])) != aide.Quantity ||
            state.Script.Flags.GetValueOrDefault(aide.RewardFlag) ||
            !state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET") ||
            _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies).Caught < aide.RequiredCaught)
            throw new InvalidDataException("pokedex_aide_reward_context");
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        if (ValidateCampaignInventory(inventory) is { } error) throw new InvalidDataException(error);
        bool check = effect.Opcode == "checkitemspace";
        InventoryTransactionResult transaction;
        if (aide.Item.Pocket == "POCKET_TM_CASE")
        {
            var machines = _content.Machines ?? throw new InvalidDataException("pokedex_aide_machine_catalog_missing");
            transaction = check ? new(inventory, MachineRules.CanAddMachine(machines, inventory, aide.Item.Id, aide.Quantity), null) :
                MachineRules.AddMachine(machines, inventory, aide.Item.Id, aide.Quantity);
        }
        else
        {
            transaction = check ? EarlyInventoryAddRules.CheckSpace(ShopCatalog, inventory, aide.Item.Id, aide.Quantity) :
                EarlyInventoryAddRules.Add(ShopCatalog, inventory, aide.Item.Id, aide.Quantity);
        }
        if (!transaction.Succeeded && (!check || transaction.Error is not (null or "inventory_stack_full" or "inventory_destination_full")))
            throw new InvalidDataException("pokedex_aide_reward_failed:" + transaction.Error);
        next = Result(transaction.Succeeded ? 1 : 0) with { Inventory = !check && transaction.Succeeded ? transaction.State : inventory };
        return true;
    }
}
