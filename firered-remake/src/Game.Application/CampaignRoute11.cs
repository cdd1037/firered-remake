using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

/// <summary>Route 2's source aide transaction in the Route 11 exploration profile.</summary>
public sealed partial class CampaignSession
{
    private static readonly ItemId Hm05 = new("pret:ITEM_HM05");
    private const string FlashAideSource = "data/maps/Route2_EastBuilding/scripts.inc";
    private const string FlashAideEntry = "Route2_EastBuilding_EventScript_Aide";
    private const string FlashAideRequest = "Route2_EastBuilding_EventScript_GetAideRequestInfo";

    private string? ValidateRoute11Progress(CampaignState state)
    {
        bool got = state.Script.Flags.GetValueOrDefault("FLAG_GOT_HM05");
        int quantity = OpeningInventoryRules.Quantity(state.Inventory?.Machines ?? [], Hm05);
        if (!_content.Route11Enabled) return got || quantity != 0 ? "save_route11_profile_missing" : null;
        if (quantity != (got ? 1 : 0)) return "save_route11_hm05_reward_mismatch";
        if (got && (!state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET") ||
                    _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies).Caught < 10))
            return "save_route11_aide_caught_requirement";
        return null;
    }

    private bool TryApplyRoute11Effect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.Route11Enabled) return false;
        var a = effect.Args;
        int Value(string token) => token.StartsWith("VAR_", StringComparison.Ordinal)
            ? state.Script.Variables.GetValueOrDefault(token)
            : int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n
            : _content.TryConstant(token, out n) ? n : throw new InvalidDataException("route11_aide_operand: " + token);
        void Context()
        {
            if (state.World.Player.MapId.Value != "map.route-2-east-building" || state.LastTalked is null ||
                !state.Actors.TryGetValue(state.LastTalked, out var actor) || actor.ScriptId != FlashAideEntry ||
                !state.Script.Locked || state.Script.QuestLogState != 0 || OakLaterProgress(state))
                throw new InvalidDataException("route11_aide_context");
        }
        CampaignState Result(CampaignState basis, int value) => basis with
        { Script = basis.Script with { Variables = basis.Script.Variables.SetItem("VAR_RESULT", value) } };
        if (effect.Source.Path == FlashAideSource && effect.Opcode == "buffernumberstring")
        {
            Context();
            bool request = effect.Source.Symbol == FlashAideRequest && a.Length == 2 && a[0] == "STR_VAR_1" && Value(a[1]) == 10;
            bool count = effect.Source.Symbol == FlashAideEntry && a.Length == 2 && a[0] == "STR_VAR_3" && Value(a[1]) == state.Script.Variables.GetValueOrDefault("VAR_0x8006");
            if (!request && !count) throw new InvalidDataException("route11_aide_buffer_context");
            next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], Value(a[1]).ToString(CultureInfo.InvariantCulture)) };
            return true;
        }
        if (effect.Source.Path == FlashAideSource && effect.Opcode == "specialvar")
        {
            Context();
            if (effect.Source.Symbol != FlashAideEntry || !a.SequenceEqual(new[] { "VAR_RESULT", "GetPokedexCount" }) ||
                state.Script.Variables.GetValueOrDefault("VAR_0x8004") != 0 ||
                !state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"))
                throw new InvalidDataException("route11_aide_count_context");
            var counts = _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies);
            next = Result(state, 0);
            next = next with { Script = next.Script with { Variables = next.Script.Variables
                .SetItem("VAR_0x8005", counts.Seen).SetItem("VAR_0x8006", counts.Caught) } };
            return true;
        }
        if (effect.Opcode is not ("checkitemspace" or "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        if (a.Length < (buffer ? 2 : 1) || Value(a[buffer ? 1 : 0]) != Value("ITEM_HM05")) return false;
        Context();
        if (effect.Source.Path is not (FlashAideSource or "data/scripts/obtain_item.inc" or "data/event_scripts.s"))
            throw new InvalidDataException("route11_aide_item_source");
        if (buffer)
        {
            if (a[0] != "STR_VAR_2" || a.Length != (effect.Opcode == "bufferitemnameplural" ? 3 : 2) || a.Length == 3 && Value(a[2]) != 1)
                throw new InvalidDataException("route11_aide_item_buffer");
            next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], "HM05") }; return true;
        }
        if (effect.Opcode == "checkitemtype") { next = Result(state, Value("POCKET_TM_CASE")); return true; }
        if (effect.Source.Path != FlashAideSource || effect.Source.Symbol != FlashAideEntry ||
            a.Length > 2 || (a.Length == 2 ? Value(a[1]) : 1) != 1 ||
            state.Script.Flags.GetValueOrDefault("FLAG_GOT_HM05") ||
            _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies).Caught < 10)
            throw new InvalidDataException("route11_aide_reward_context");
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        var error = ValidateCampaignInventory(inventory); if (error is not null) throw new InvalidDataException(error);
        if (effect.Opcode == "checkitemspace")
        { next = Result(state, PewterMachineRules.CanAddMachine(_content.PewterMachines!, inventory, Hm05) ? 1 : 0); return true; }
        if (OpeningInventoryRules.Quantity(inventory.Machines ?? [], Hm05) != 0) throw new InvalidDataException("route11_aide_duplicate_hm05");
        var added = PewterMachineRules.AddMachine(_content.PewterMachines!, inventory, Hm05);
        if (!added.Succeeded) throw new InvalidDataException("route11_aide_reward_failed: " + added.Error);
        next = Result(state with { Inventory = added.State }, 1); return true;
    }
}
