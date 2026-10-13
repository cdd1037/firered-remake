using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Application;

/// <summary>Bounded Six Island successor ending with the original Warehouse Sapphire recovery.</summary>
public sealed partial class CampaignSession
{
    public const string FiveIslandRulesVersion = "firered-five-island-campaign-v1";
    public bool FiveIslandEnabled => _content.FiveIslandEnabled;
    private static readonly ItemId SapphireItem = new("pret:ITEM_SAPPHIRE");

    private static void ValidateFiveIslandCatalog(ICampaignCatalog content)
    {
        if (!content.FiveIslandEnabled)
        {
            if (content.FiveIslandOwnership is not null || content.FiveIslandOpponents is not null || content.FiveIslandTrainers is not null)
                throw new InvalidDataException("five_island_profile_missing");
            return;
        }
        if (!content.SixIslandEnabled || content.FiveIslandOwnership is not {} owned ||
            content.FiveIslandOpponents is not {} opponents || content.FiveIslandTrainers is not {} trainers ||
            !ReferenceEquals(owned.Predecessor, content.SixIslandOwnership) || !ReferenceEquals(content.CelioOwnership?.Predecessor.Catalog ?? content.PokemonContent?.Catalog, owned.Catalog) ||
            !ReferenceEquals(opponents.Owned, owned) || !ReferenceEquals(opponents.Predecessor, content.SixIslandOpponents) ||
            CelioFiveMachines(content) is not {} machines || !ReferenceEquals(machines.Pokemon, owned) ||
            FiveIslandShopCatalog.For(content.EarlyShop) is null)
            throw new InvalidDataException("five_island_source_profile_missing");
        FiveIslandTrainerPartyFactory.ValidateCatalog(trainers);
    }

    private string? ValidateFiveIslandProgress(CampaignState state)
    {
        bool Flag(string key) => state.Script.Flags.GetValueOrDefault(key);
        bool door = Flag(FiveIslandDoorFlag), defeated = Flag(FiveIslandRocketsFlag), recovered = Flag(FiveIslandSapphireFlag);
        int scene = state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_ROCKET_WAREHOUSE");
        int sapphire = OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], SapphireItem);
        if (!_content.FiveIslandEnabled)
            return door || defeated || recovered || scene != 0 || sapphire != 0 || FiveIslandMap(state.World.Player.MapId)
                ? "save_five_island_profile_missing" : null;
        if (scene is < 0 or > 1) return "save_five_island_scene";
        if ((door || defeated || recovered || scene != 0 || FiveIslandMap(state.World.Player.MapId)) &&
            (!NationalDexEnabledIn(state) || !RainbowQuestComplete(state) ||
             OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], RainbowPassItem) != 1)) return "save_five_island_rainbow_gate";
        if ((door || defeated || recovered || scene != 0 || state.World.Player.MapId.Value == FiveIslandWarehouseMap) &&
            !Flag(SixIslandPasswordFlag)) return "save_five_island_password_required";
        if ((defeated || recovered || scene != 0 || state.World.Player.MapId.Value == FiveIslandWarehouseMap) && !door)
            return "save_five_island_warehouse_door_required";
        if (defeated != Flag("FLAG_HIDE_FIVE_ISLAND_ROCKETS") ||
            defeated && (!state.BattledTrainers.Contains("pret:TRAINER_TEAM_ROCKET_ADMIN_2") || scene != 1))
            return "save_five_island_rocket_progress";
        if (recovered && !state.BattledTrainers.Contains("pret:TRAINER_SCIENTIST_GIDEON") || sapphire > (recovered ? 1 : 0))
            return "save_five_island_sapphire_progress";
        // Gideon's original source sets RECOVERED before AddBagItem; a full Key pocket
        // can leave the flag without the item. This is retained, not silently repaired.
        if (Flag("FLAG_SYS_CAN_LINK_WITH_RS") && !CelioCompleted(state)) return "save_celio_activation_not_admitted";
        if (FiveIslandEditedMap(state.World.Player.MapId) &&
            !(state.MapEdits ?? ImmutableSortedDictionary<string, FieldMetatileEdit>.Empty).SequenceEqual(FiveIslandExpectedEdits(state)))
            return "save_five_island_metatiles";
        return null;
    }
}
