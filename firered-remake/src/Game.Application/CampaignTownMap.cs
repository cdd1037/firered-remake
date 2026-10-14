using Game.Domain;
using Game.Domain.Items;

namespace Game.Application;

/// <summary>Shared Bag/Fly map callback; cursor and original artwork belong to the adapter.</summary>
public sealed record CampaignTownMap(string Origin = "bag");

public sealed partial class CampaignSession
{
    public const string TownMapSceneVariable = "VAR_MAP_SCENE_PALLET_TOWN_RIVALS_HOUSE";
    public const string TownMapHideFlag = "FLAG_HIDE_TOWN_MAP";

    private string? ValidateTownMapProgress(CampaignState state)
    {
        int scene = state.Script.Variables.GetValueOrDefault(TownMapSceneVariable);
        bool hidden = state.Script.Flags.GetValueOrDefault(TownMapHideFlag);
        int quantity = OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], QuestItemRules.TownMapId);
        if (!_content.TownMapEnabled)
            return scene >= 2 || hidden || quantity != 0 ? "save_town_map_profile_missing" : null;
        bool dex = state.Script.Flags.GetValueOrDefault("FLAG_SYS_POKEDEX_GET");
        if (scene is < 0 or > 2 || (scene != 0) != dex || hidden != (scene == 2) || quantity != (scene == 2 ? 1 : 0))
            return "save_town_map_reward_mismatch";
        if (state.World.Player.MapId.Value != "map.rivals-house") return null;
        var definition = _content.MapDefinition(state.World.Player.MapId).Actors.Single(a => a.HideFlag == TownMapHideFlag);
        if (!state.Actors.TryGetValue(definition.LocalId, out var actor)) return "save_town_map_actor_missing";
        var player = state.World.Player;
        bool inView = actor.X + 7 >= player.X - 2 && actor.X + 7 <= player.X + 17 && actor.Y + 7 >= player.Y && actor.Y + 7 <= player.Y + 16;
        if (actor.Active != (!hidden && inView) || actor.Invisible || actor.X != definition.X || actor.Y != definition.Y ||
            actor.TemplateX != definition.X || actor.TemplateY != definition.Y)
            return "save_town_map_actor_inconsistent";
        int receivedOnEntry = state.Script.Variables.GetValueOrDefault("VAR_TEMP_1");
        return receivedOnEntry is < 0 or > 1 || scene < 2 && receivedOnEntry != 0 ? "save_town_map_entry_state_invalid" : null;
    }

    private Decision<CampaignState> OpenTownMap(CampaignState state, CampaignCommand command)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (!_content.TownMapEnabled) return No("town_map_not_enabled");
        if (state.ItemMenu is not { Origin: "bag", View: "bag" } || command.Text != QuestItemRules.TownMapId.Value)
            return No("town_map_context_invalid");
        var error = ValidateCampaignInventory(state.Inventory) ?? ValidateTownMapProgress(state);
        if (error is not null) return No(error);
        if (OpeningInventoryRules.Quantity(state.Inventory!.KeyItems ?? [], QuestItemRules.TownMapId) != 1)
            return No("town_map_not_owned");
        return new(CommandStatus.Accepted, state with { TownMap = new() });
    }

    private Decision<CampaignState> DecideTownMap(CampaignState state, CampaignCommand command)
    {
        if (state.TownMap?.Origin == "fly") return DecideFieldFly(state, command);
        if (command.Kind != "town_map_close") return new(CommandStatus.Rejected, state, "town_map_active");
        if (!_content.TownMapEnabled || state.TownMap?.Origin != "bag" || state.ItemMenu is not { Origin: "bag", View: "bag" })
            return new(CommandStatus.Fault, state, "town_map_callback_context_invalid");
        return new(CommandStatus.Accepted, state with { TownMap = null });
    }
}
