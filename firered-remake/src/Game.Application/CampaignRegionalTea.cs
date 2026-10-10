using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private static readonly ItemId TeaId = new("pret:ITEM_TEA");
    private const string TeaSource = "data/maps/CeladonCity_Condominiums_1F/scripts.inc";
    private const string TeaMap = "map.celadon-city-condominiums-1f";
    private const string SharedTeaGateScene = "VAR_MAP_SCENE_ROUTE5_ROUTE6_ROUTE7_ROUTE8_GATES";
    private static readonly ImmutableDictionary<string, string> TeaGateSources = new Dictionary<string, string>
    {
        ["map.route-5-south-entrance"] = "Route5_SouthEntrance",
        ["map.route-6-north-entrance"] = "Route6_NorthEntrance",
        ["map.route-7-east-entrance"] = "Route7_EastEntrance",
        ["map.route-8-west-entrance"] = "Route8_WestEntrance"
    }.ToImmutableDictionary(StringComparer.Ordinal);

    private InventoryState WithoutRegionalTea(InventoryState inventory) => !_content.RegionalServicesEnabled ? inventory :
        inventory with { KeyItems = inventory.KeyItems is { } keys ? keys.Where(s => s.Item != TeaId).ToImmutableArray() : null };

    private string? ValidateRegionalTeaInventory(InventoryState inventory)
    {
        var tea = (inventory.KeyItems ?? []).Where(s => s is not null && s.Item == TeaId).ToArray();
        return tea.Length > 0 && (!_content.RegionalServicesEnabled || tea.Length != 1 || tea[0].Quantity != 1)
            ? "inventory_regional_tea_invalid" : null;
    }

    private string? ValidateRegionalTeaProgress(CampaignState state)
    {
        bool received = state.Script.Flags.GetValueOrDefault("FLAG_GOT_TEA");
        int scene = state.Script.Variables.GetValueOrDefault(SharedTeaGateScene);
        int quantity = OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], TeaId);
        if (!_content.RegionalServicesEnabled)
            return received || scene != 0 || quantity != 0 ? "save_regional_tea_profile_missing" : null;
        // Source sets the gift flag before giveitem; full bag legitimately leaves zero Tea.
        // All four guards test that flag, ignore failed removeitem, and share scene 1.
        return scene is < 0 or > 1 || quantity is < 0 or > 1 || quantity > 0 && !received ||
            scene == 1 && (!received || quantity != 0) ? "save_regional_tea_progress" : null;
    }

    private bool TryApplyRegionalTeaEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!_content.RegionalServicesEnabled || effect.Opcode is not ("checkitemspace" or "additem" or "removeitem" or "checkitemtype" or "bufferitemname" or "bufferitemnameplural")) return false;
        var a = effect.Args;
        int Value(string token) => int.TryParse(token, out int n) ? n : state.Script.Variables.TryGetValue(token, out int v) ? v :
            _content.TryConstant(token, out int c) ? c : throw new InvalidDataException("regional_tea_operand_unknown: " + token);
        bool buffer = effect.Opcode.StartsWith("bufferitem", StringComparison.Ordinal);
        if (a.Length < (buffer ? 2 : 1)) throw new InvalidDataException("regional_tea_item_arity");
        if (Value(a[buffer ? 1 : 0]) != 369) return false;
        bool woman = state.World.Player.MapId.Value == TeaMap && state.LastTalked is { } actor &&
            state.Actors.TryGetValue(actor, out var npc) && npc.ScriptId == "CeladonCity_Condominiums_1F_EventScript_TeaWoman";
        bool gate = TeaGateSources.TryGetValue(state.World.Player.MapId.Value, out string? gateSource);
        if (effect.Opcode == "removeitem")
        {
            if (!gate || effect.Source.Path != $"data/maps/{gateSource}/scripts.inc" || effect.Source.Symbol != gateSource + "_EventScript_GiveTea" ||
                state.Script.Variables.GetValueOrDefault(SharedTeaGateScene) != 0 || !state.Script.Flags.GetValueOrDefault("FLAG_GOT_TEA"))
                throw new InvalidDataException("regional_tea_gate_context");
        }
        else if (!woman || !state.Script.Flags.GetValueOrDefault("FLAG_GOT_TEA") ||
            effect.Source.Path is not (TeaSource or "data/scripts/obtain_item.inc" or "data/event_scripts.s"))
            throw new InvalidDataException("regional_tea_gift_context");
        CampaignState Result(int value) => state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_RESULT", value) } };
        if (buffer)
        {
            if (a[0] != "STR_VAR_2" || a.Length != (effect.Opcode == "bufferitemnameplural" ? 3 : 2) || a.Length == 3 && Value(a[2]) != 1)
                throw new InvalidDataException("regional_tea_buffer_context");
            next = state with { TextBuffers = state.TextBuffers.SetItem(a[0], "TEA") }; return true;
        }
        if (effect.Opcode == "checkitemtype")
        {
            if (a.Length != 1) throw new InvalidDataException("regional_tea_item_arity");
            next = Result(Value("POCKET_KEY_ITEMS")); return true;
        }
        if (a.Length > 2 || a.Length == 2 && Value(a[1]) != 1) throw new InvalidDataException("regional_tea_quantity");
        var inventory = state.Inventory ?? throw new InvalidDataException("inventory_missing");
        string? error = ValidateCampaignInventory(inventory); if (error is not null) throw new InvalidDataException(error);
        var keys = inventory.KeyItems ?? [];
        int index = -1; for (int i = 0; i < keys.Length; i++) if (keys[i].Item == TeaId) { index = i; break; }
        bool remove = effect.Opcode == "removeitem";
        bool success = remove ? index >= 0 : index < 0 && keys.Length < 30;
        next = Result(success ? 1 : 0);
        if (success && effect.Opcode != "checkitemspace")
            next = next with { Inventory = inventory with { KeyItems = remove ? keys.RemoveAt(index) : keys.Add(new(TeaId, 1)) } };
        return true;
    }
}
