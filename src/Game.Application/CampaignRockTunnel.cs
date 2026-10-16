using System.Globalization;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

/// <summary>Route 10's source Everstone aide transaction in the Rock Tunnel profile.</summary>
public sealed partial class CampaignSession
{
    private static readonly ItemId Everstone = new("pret:ITEM_EVERSTONE");
    private const string EverstoneAideSource = "data/maps/Route10_PokemonCenter_1F/scripts.inc";
    private const string EverstoneAideEntry = "Route10_PokemonCenter_1F_EventScript_Aide";
    private const string EverstoneAideRequest = "Route10_PokemonCenter_1F_EventScript_GetAideRequestInfo";

    private string? ValidateRockTunnelProgress(CampaignState state)
    {
        bool got = state.Script.Flags.GetValueOrDefault("FLAG_GOT_EVERSTONE_FROM_OAKS_AIDE");
        // The original flag remains after the gift is held, sold or discarded.
        if (got && (!state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET") ||
                    _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies).Caught < 20))
            return "save_rocktunnel_aide_caught_requirement";
        return null;
    }

    private bool TryApplyRockTunnelEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        var a = effect.Args;
        int Value(string token) => token.StartsWith("VAR_", StringComparison.Ordinal)
            ? state.Script.Variables.GetValueOrDefault(token)
            : int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n
            : _content.TryConstant(token, out n) ? n : throw new InvalidDataException("rocktunnel_aide_operand: " + token);
        void Context()
        {
            if (state.World.Player.MapId.Value != "map.route-10-pokemon-center-1f" || state.LastTalked is null ||
                !state.Actors.TryGetValue(state.LastTalked, out var actor) || actor.ScriptId != EverstoneAideEntry ||
                !state.Script.Locked || state.Script.QuestLogState != 0 || OakLaterProgress(state) && false)
                throw new InvalidDataException("rocktunnel_aide_context");
        }
        CampaignState Result(CampaignState basis, int value) => basis with
        { Script = basis.Script with { Variables = basis.Script.Variables.SetItem("VAR_RESULT", value) } };
        if (effect.Source.Path == EverstoneAideSource && effect.Opcode == "buffernumberstring")
        {
            Context();
            bool request = effect.Source.Symbol == EverstoneAideRequest && a.Length == 2 && a[0] == "STR_VAR_1" && Value(a[1]) == 20;
            bool count = effect.Source.Symbol == EverstoneAideEntry && a.Length == 2 && a[0] == "STR_VAR_3" && Value(a[1]) == state.Script.Variables.GetValueOrDefault("VAR_0x8006");
            if (!request && !count) throw new InvalidDataException("rocktunnel_aide_buffer_context");
            next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], Value(a[1]).ToString(CultureInfo.InvariantCulture)) };
            return true;
        }
        if (effect.Source.Path == EverstoneAideSource && effect.Opcode == "specialvar")
        {
            Context();
            if (effect.Source.Symbol != EverstoneAideEntry || !a.SequenceEqual(new[] { "VAR_RESULT", "GetPokedexCount" }) ||
                state.Script.Variables.GetValueOrDefault("VAR_0x8004") != 0 ||
                !state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET"))
                throw new InvalidDataException("rocktunnel_aide_count_context");
            var counts = _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies);
            next = Result(state, 0);
            next = next with { Script = next.Script with { Variables = next.Script.Variables
                .SetItem("VAR_0x8005", counts.Seen).SetItem("VAR_0x8006", counts.Caught) } };
            return true;
        }
        if (effect.Opcode is not ("checkitemspace" or "additem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        if (a.Length < (buffer ? 2 : 1) || Value(a[buffer ? 1 : 0]) != Value("ITEM_EVERSTONE")) return false;
        Context();
        if (effect.Source.Path is not (EverstoneAideSource or "data/scripts/obtain_item.inc" or "data/event_scripts.s"))
            throw new InvalidDataException("rocktunnel_aide_item_source");
        if (buffer)
        {
            if (a[0] != "STR_VAR_2" || a.Length != (effect.Opcode == "bufferitemnameplural" ? 3 : 2) || a.Length == 3 && Value(a[2]) != 1)
                throw new InvalidDataException("rocktunnel_aide_item_buffer");
            next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], "EVERSTONE") }; return true;
        }
        if (effect.Opcode == "checkitemtype") { next = Result(state, Value("POCKET_ITEMS")); return true; }
        if (effect.Source.Path != EverstoneAideSource || effect.Source.Symbol != EverstoneAideEntry ||
            a.Length > 2 || (a.Length == 2 ? Value(a[1]) : 1) != 1 ||
            state.Script.Flags.GetValueOrDefault("FLAG_GOT_EVERSTONE_FROM_OAKS_AIDE") ||
            _content.EvaluateOakRating(state.SeenSpecies, state.CaughtSpecies).Caught < 20)
            throw new InvalidDataException("rocktunnel_aide_reward_context");
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        var error = ValidateCampaignInventory(inventory); if (error is not null) throw new InvalidDataException(error);
        var view = inventory;
        var result = effect.Opcode == "additem"
            ? EarlyInventoryAddRules.Add(ShopCatalog, view, Everstone, 1)
            : EarlyInventoryAddRules.CheckSpace(ShopCatalog, view, Everstone, 1);
        if (!result.Succeeded && effect.Opcode == "additem")
            throw new InvalidDataException("rocktunnel_aide_reward_failed: " + result.Error);
        next = Result(state with { Inventory = effect.Opcode == "additem" ? result.State : inventory }, result.Succeeded ? 1 : 0);
        return true;
    }
}
