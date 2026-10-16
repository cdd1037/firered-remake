using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // The ordinary save boundary already excludes running scripts, battles and motions.
    // These retained field payloads still need their own source identity checks.
    private string? ValidateTrainerTowerFieldSave(CampaignState state)
    {
        if (state.TrainerTowerMenu is not null || state.TrainerTowerRecordsVisible)
            return "save_trainer_tower_menu_active";
        if (state.TrainerTower is null && (state.TrainerTowerHistory is not null || state.TrainerTowerElevator is not null))
            return "save_trainer_tower_progress_missing";
        if (state.TrainerTowerHistory is { IsDefault: true }) return "save_trainer_tower_history";

        var mapId = state.World.Player.MapId;
        if (mapId.Value == TrainerTowerLobby)
        {
            // VAR_TEMP_D == 17 schedules ExitElevator. Its open barrier is never
            // a stable save: the script walks through, closes it, then clears D.
            if (state.Script.Variables.GetValueOrDefault("VAR_TEMP_D") == 17)
                return "save_trainer_tower_lobby_transition";
            if (state.MapEdits is { Count: > 0 } edits)
            {
                var tile = _content.Maps[mapId].GetTile(17, 10)!;
                if (!_content.TryConstant("METATILE_TrainerTower_CounterBarrier", out int metatile) ||
                    edits.Count != 1 || !edits.TryGetValue("17:10", out var edit) ||
                    edit != new FieldMetatileEdit(17, 10, metatile, 3, tile.Elevation))
                    return "save_trainer_tower_lobby_edits";
            }
        }
        else if (IsTrainerTowerMap(mapId) && state.MapEdits is { Count: > 0 })
            return "save_trainer_tower_map_edits";

        bool insideElevator = mapId.Value == TrainerTowerElevator;
        if (state.TrainerTowerElevator is not { } elevator)
            return insideElevator ? "save_trainer_tower_elevator_return_missing" : null;
        if (elevator.FloorWindowVisible || elevator.MenuDefault is not null)
            return "save_trainer_tower_elevator_menu_active";
        if (!ValidTrainerTowerElevatorDestination(elevator))
            return "save_trainer_tower_elevator_destination";
        // Selecting the lobby immediately runs the source exit/warp script.
        // Its destination is retained outside the elevator, but not a stable
        // in-elevator state. Returning to the roof or cancelling leaves floor 15.
        if (insideElevator && elevator.DestinationMapId.Value != "map.trainer-tower-roof")
            return "save_trainer_tower_elevator_context";
        return null;
    }

    private bool ValidTrainerTowerElevatorDestination(CampaignDepartmentElevator elevator)
    {
        if (elevator.DestinationMapId.Value is not (TrainerTowerLobby or "map.trainer-tower-roof") ||
            !_content.Maps.TryGetValue(elevator.DestinationMapId, out var map) ||
            map.GetTile(elevator.X, elevator.Y) is not { } tile || tile.Elevation != elevator.Elevation)
            return false;
        if (elevator.DestinationMapId.Value == TrainerTowerLobby)
            return elevator.DestinationWarpId == 255 && elevator.X == 17 && elevator.Y == 8 && elevator.Floor == 3;
        return elevator.Floor == 15 && map.FieldWarps.Any(w => w.Index == elevator.DestinationWarpId &&
            w.X == elevator.X && w.Y == elevator.Y && w.DestinationMapId.Value == TrainerTowerElevator &&
            w.DestinationWarpId == 0 && (w.Elevation == 0 || w.Elevation == tile.Elevation));
    }
}
