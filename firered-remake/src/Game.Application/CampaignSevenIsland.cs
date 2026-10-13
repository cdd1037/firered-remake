using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    public bool SevenIslandEnabled => _content.SevenIslandEnabled;
    private static bool SevenIslandMap(MapId map) => map.Value == "map.seven-island" || map.Value.StartsWith("map.seven-island-", StringComparison.Ordinal);

    private static void ValidateSevenIslandCatalog(ICampaignCatalog content)
    {
        if (!content.SevenIslandEnabled || content.SevenIslandOwnership is not { } owned ||
            content.SevenIslandOpponents is not { } opponents || content.SevenIslandTrainers is not { } trainers ||
            content.SevenIslandMachines is not { } machines ||
            !ReferenceEquals(SevenIslandOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            !ReferenceEquals(owned, content.EventIslandOwnership?.Predecessor) || owned.Catalog.Species.Count != 146 ||
            !ReferenceEquals(owned.Predecessor, content.CeruleanCaveOwnership) ||
            !ReferenceEquals(SevenIslandOpponentPokemonCatalog.For(owned), opponents) ||
            !ReferenceEquals(opponents.Predecessor, content.LeagueRematchOpponents) || opponents.Catalog.Species.Count != 180 ||
            !ReferenceEquals(SevenIslandMachineCatalog.For(machines.Catalog), machines) ||
            !ReferenceEquals(machines.Pokemon, owned) || !ReferenceEquals(machines.Predecessor, content.CeruleanCaveMachines) ||
            content.SevenIslandShop is null || trainers.Trainers.Count != 18 || trainers.BattlesByScript.Count != 20)
            throw new InvalidDataException("seven_island_source_binding");
        SevenIslandTrainerPartyFactory.ValidateCatalog(trainers);
        if (!content.TryConstant("HEAL_LOCATION_SEVEN_ISLAND", out var healing) || healing != 19)
            throw new InvalidDataException("seven_island_healing_source");
    }

    private string? ValidateSevenIslandProgress(CampaignState state)
    {
        bool used = SevenIslandMap(state.World.Player.MapId) || state.LastHealingLocation == "HEAL_LOCATION_SEVEN_ISLAND" ||
            state.Exploration?.Battles.Any(b => b.SevenIsland is not null) == true ||
            state.BattledTrainers.Any(id => _content.SevenIslandTrainers!.Trainers.ContainsKey(new(id))) ||
            state.Script.Flags.GetValueOrDefault("FLAG_WORLD_MAP_SEVEN_ISLAND") || state.Script.Flags.GetValueOrDefault("FLAG_SYS_UNLOCKED_TANOBY_RUINS");
        if (used && (!NationalDexEnabledIn(state) || !RainbowQuestComplete(state) ||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], RainbowPassItem) != 1)) return "save_seven_island_rainbow_gate";
        return null;
    }

    // Preserve all three source warps. Refuse the unsupported transition before
    // movement, temporary state, RNG or map-entry effects can commit.
    private string? SevenIslandBoundary(MapId map, int x, int y, int oldX, int oldY)
    {
        if (!_content.SevenIslandEnabled || !SevenIslandMap(map)) return null;
        foreach (var warp in _content.Maps[map].FieldWarps)
        {
            if (!((warp.X == x && warp.Y == y) || (warp.X == oldX && warp.Y == oldY))) continue;
            if (!_content.TrainerTowerEnabled && map.Value == "map.seven-island-trainer-tower" && warp.Index == 0) return "seven_island_trainer_tower_challenge_not_admitted";
            if (map.Value == "map.seven-island-pokemon-center-1f" && warp.X == 1 && warp.Y == 6) return "seven_island_multiplayer_not_admitted";
            if (map.Value == "map.seven-island-house-room1" && warp.X == 4 && warp.Y == 1) return "seven_island_ereader_room_not_admitted";
        }
        return null;
    }
}
