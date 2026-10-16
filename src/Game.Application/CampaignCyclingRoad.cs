using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string CyclingRoadScene = "VAR_MAP_SCENE_ROUTE16";
    private const string CyclingRoadFlag = "FLAG_SYS_ON_CYCLING_ROAD";

    private string? ValidateCyclingRoadState(CampaignState state)
    {
        int scene = state.Script.Variables.GetValueOrDefault(CyclingRoadScene);
        bool onRoad = state.Script.Flags.GetValueOrDefault(CyclingRoadFlag);
        var p = state.World.Player;
        bool outdoor = p.MapId.Value is "map.route-16" or "map.route-17" or "map.route-18";
        bool gate = _content.CyclingRoadSceneInteriors.Contains(p.MapId);
        if (scene is not (0 or 1) || scene == 1 && (!outdoor && !gate ||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], BicycleId) != 1))
            return "save_cycling_road_scene";
        if (onRoad != (outdoor && scene == 1) || onRoad && !state.IsCycling)
            return "save_cycling_road_position";
        if (state.BicycleSlope && !state.IsCycling)
            return "save_cycling_road_slope";
        return null;
    }

    private bool TryApplyCyclingRoadEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (effect.Opcode != "special" || effect.Args.Length != 1 || effect.Args[0] != "ForcePlayerOntoBike")
            return false;
        string? source = state.World.Player.MapId.Value switch
        {
            "map.route-16" => "Route16",
            "map.route-18" => "Route18",
            _ => null
        };
        if (source is null ||
            effect.Source.Path != "data/maps/" + source + "/scripts.inc" ||
            effect.Source.Symbol != source + "_OnWarpCyclingRoad" ||
            state.Script.Variables.GetValueOrDefault(CyclingRoadScene) != 1 ||
            !state.Script.Flags.GetValueOrDefault(CyclingRoadFlag) || state.IsSurfing ||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], BicycleId) != 1)
            throw new InvalidDataException("cycling_road_force_bicycle_context");
        next = state with { IsCycling = true, BicycleSlope = false, SavedMusic = "MUS_CYCLING", Music = "MUS_CYCLING" };
        return true;
    }

    private Decision<CampaignState> DecideCyclingRoadInput(CampaignState state, CampaignCommand command)
    {
        var p = state.World.Player;
        if (!state.IsCycling ||
            !CyclingRoadRules.IsSlope(SourceBehavior(state, p.X, p.Y)) ||
            command.Choice is not (0 or 1) || command.Text is not (null or "idle"))
            return new(CommandStatus.Rejected, state, "cycling_road_input_context");
        var input = CyclingRoadRules.Resolve(state.BicycleSlope, p.Facing,
            command.Text == "idle" ? null : command.Direction, command.Choice == 1);
        var prepared = state with { BicycleSlope = input.SlopeState };
        if (input.Action == CyclingRoadAction.Idle ||
            input.Action == CyclingRoadAction.Turn && input.Direction == p.Facing)
            return new(CommandStatus.Accepted, prepared);
        if (input.Action == CyclingRoadAction.Turn)
            return DecideConnectedField(prepared, new("turn", input.Direction));
        // The source uphill transition accepts only COLLISION_NONE; downhill also jumps ledges.
        var (dx, dy) = Delta(input.Direction);
        if (input.Ticks == 16 && SourceBehavior(state, p.X + dx, p.Y + dy) is >= 0x38 and <= 0x3b)
            return new(CommandStatus.Accepted, prepared);
        var result = DecideConnectedField(prepared, new("move", input.Direction));
        if (result.Status == CommandStatus.Accepted && result.State.World.Player.MapId == p.MapId &&
            result.State.World.Player.X == p.X && result.State.World.Player.Y == p.Y)
            result = result with { State = result.State with { World = result.State.World with {
                Player = result.State.World.Player with { Facing = p.Facing } } } };
        if (result.Status == CommandStatus.Accepted && result.State.BicycleSlope &&
            !CyclingRoadRules.IsSlope(SourceBehavior(result.State,
                result.State.World.Player.X, result.State.World.Player.Y)))
            result = result with { State = result.State with { BicycleSlope = false } };
        return result;
    }
}
