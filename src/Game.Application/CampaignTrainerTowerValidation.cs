using System.Collections.Immutable;
using System.Globalization;
using Game.Domain.Battle;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string TrainerTowerBattlePrefix = "trainer-tower-battle:";

    private static string TrainerTowerBattleId(long serial) =>
        TrainerTowerBattlePrefix + serial.ToString(CultureInfo.InvariantCulture);

    private static bool TryTrainerTowerBattleSerial(string? id, out long serial)
    {
        serial = 0;
        return id is not null && id.StartsWith(TrainerTowerBattlePrefix, StringComparison.Ordinal) &&
            long.TryParse(id.AsSpan(TrainerTowerBattlePrefix.Length), NumberStyles.None,
                CultureInfo.InvariantCulture, out serial) && serial > 0 && id == TrainerTowerBattleId(serial);
    }

    private string NextTrainerTowerBattleId(ImmutableArray<CampaignTrainerTowerHistory>? history)
    {
        if (ValidateTrainerTowerHistory(history) is {} error) throw new InvalidDataException(error);
        long last = 0;
        if (history is { Length: 1 }) _ = TryTrainerTowerBattleSerial(history.Value[0].Id, out last);
        try { return TrainerTowerBattleId(checked(last + 1)); }
        catch (OverflowException ex) { throw new InvalidDataException("trainer_tower_battle_serial_exhausted", ex); }
    }

    private string? ValidateTrainerTowerHistory(ImmutableArray<CampaignTrainerTowerHistory>? history)
    {
        if (history is null) return null;
        if (history.Value.IsDefault || history.Value.Length > 1) return "save_trainer_tower_history";
        return history.Value.IsEmpty ? null : ValidateTrainerTowerReceipt(history.Value[0]);
    }

    private string? ValidateTrainerTowerReceipt(CampaignTrainerTowerHistory? receipt)
    {
        if (receipt is not {} h || !TryTrainerTowerBattleSerial(h.Id, out _) || !Enum.IsDefined(h.Course) ||
            h.FloorIndex is < 0 or > 7 || h.Level is < 1 or > 100 ||
            h.Outcome is not (WildBattleOutcome.Won or WildBattleOutcome.Lost or WildBattleOutcome.Draw) ||
            h.OpponentsSeen.IsDefault)
            return "save_trainer_tower_history";
        try
        {
            var descriptor = _content.TrainerTowerCatalog!.DescribeBattle(h.Course, h.FloorIndex, h.TrainerIndex, h.Level);
            if (h.OpponentsSeen.IsEmpty || h.OpponentsSeen.Distinct().Count() != h.OpponentsSeen.Length ||
                h.OpponentsSeen.Any(n => n < 0 || n >= descriptor.Members.Length) ||
                h.Outcome == WildBattleOutcome.Won && h.OpponentsSeen.Length != descriptor.Members.Length)
                return "save_trainer_tower_seen";
        }
        catch (ArgumentException) { return "save_trainer_tower_history_descriptor"; }
        return null;
    }

    /// <summary>Explicit offline v3 validation only. Checks every old receipt before the full stable-save boundary.</summary>
    public string? ValidateLegacyTrainerTowerCheckpoint(CampaignState state)
    {
        if (state is null) return "save_structure_invalid";
        if (state.TrainerTowerHistory is {} history)
        {
            if (history.IsDefault) return "save_trainer_tower_history";
            for (int i = 0; i < history.Length; i++)
            {
                if (ValidateTrainerTowerReceipt(history[i]) is {} error) return error;
                if (history[i].Id != TrainerTowerBattleId((long)i + 1)) return "save_trainer_tower_history";
            }
        }
        return ValidatePlayerSave(RetainLastTrainerTowerReceipt(state));
    }

    /// <summary>Explicit offline conversion only. Continue never trims or repairs a legacy save.</summary>
    public CampaignState ConvertLegacyTrainerTowerCheckpoint(CampaignState state)
    {
        if (ValidateLegacyTrainerTowerCheckpoint(state) is {} error)
            throw new InvalidDataException("checkpoint_conversion_invalid: " + error);
        return RetainLastTrainerTowerReceipt(state);
    }

    private static CampaignState RetainLastTrainerTowerReceipt(CampaignState state) =>
        state.TrainerTowerHistory is { Length: > 1 } history ? state with { TrainerTowerHistory = [history[^1]] } : state;

    private CampaignTrainerTowerHistory ActiveTrainerTowerReceipt(CampaignState state, SourceBattleFrame frame,
        string id, bool finished)
    {
        if (state.TrainerTower is not {} tower || tower.FloorChain is not {} chain ||
            state.Script.Wait?.Kind != StoryWaitKind.Battle || state.WildBattle is not null || state.Battle is not null ||
            frame.TrainerTowerEncounter?.Roster is not {} roster ||
            roster.Course != tower.SelectedCourse.ToString() || roster.FloorIndex + 1 != chain.FloorNumber ||
            roster.TrainerIndex != chain.TrainerIndex || TrainerTowerFloorNumber(state.World.Player.MapId) != chain.FloorNumber ||
            state.Script.Variables.GetValueOrDefault("VAR_TEMP_1") != chain.TrainerIndex)
            throw new InvalidDataException("trainer_tower_battle_identity");
        TrainerTowerProgressRules.Validate(tower, _content.TrainerTowerCatalog!);
        ImmutableArray<int> seen;
        if (frame.Layout == SourceBattleLayout.Singles && chain.Format != TrainerTowerBattleFormat.Double &&
            state.DoubleBattle is null && state.ForestTrainerBattle is {} single && single.Id == id &&
            single.Trainer == roster.Trainer.Id && ReferenceEquals(single.Frame, frame) &&
            single.Stage == (finished ? CampaignForestTrainerStage.Finished : CampaignForestTrainerStage.Combat))
            seen = single.OpponentsSent;
        else if (frame.Layout == SourceBattleLayout.PresetDoubles && chain.Format == TrainerTowerBattleFormat.Double &&
            state.ForestTrainerBattle is null && state.DoubleBattle is {} doubles && doubles.Id == id &&
            doubles.Trainer == roster.Trainer.Id && ReferenceEquals(doubles.Frame, frame) &&
            doubles.Stage == (finished ? CampaignDoubleBattleStage.Finished : CampaignDoubleBattleStage.Combat))
            seen = [0, 1];
        else throw new InvalidDataException("trainer_tower_battle_identity");
        var receipt = new CampaignTrainerTowerHistory(id, tower.SelectedCourse, roster.FloorIndex,
            roster.TrainerIndex, frame.Party(BattleSide.Opponent).Members[0].Pokemon.Level, frame.Outcome, seen);
        if (ValidateTrainerTowerReceipt(receipt) is {} error) throw new InvalidDataException(error);
        return receipt;
    }

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
