using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string TanobyKeyMap = "map.seven-island-sevault-canyon-tanoby-key";
    private const string SevenIslandCanyonMap = "map.seven-island-sevault-canyon";
    private static bool SevenIslandStrengthMap(MapId map) => map.Value is TanobyKeyMap or SevenIslandCanyonMap;
    private bool SevenIslandBoulderDefinition(FieldActorDefinition definition, MapId map)
    {
        (int X, int Y, int Elevation)? expected = map.Value == TanobyKeyMap
            ? _content.SevenIslandField!.TanobyKey.Boulders.SingleOrDefault(b => b.LocalId == definition.SourceLocalId) is {} b
                ? (b.X, b.Y, b.Elevation) : null
            : definition.SourceLocalId switch { 10 => (13, 47, 3), 15 => (17, 47, 3), _ => null };
        return expected is {} source && (definition.X, definition.Y, definition.Elevation) == source &&
            !definition.IsClone && definition.GraphicsId == FieldHmSourceRules.BoulderGraphics &&
            definition.ScriptId == "EventScript_StrengthBoulder" && definition.HideFlag == "0" &&
            definition.MovementType == "MOVEMENT_TYPE_LOOK_AROUND" &&
            definition.TrainerType == "TRAINER_TYPE_NONE" && definition.TrainerSight == 0;
    }
    private (int X, int Y) SevenIslandBoulderTemplate(CampaignState state, FieldActorDefinition definition)
    {
        if (state.World.Player.MapId.Value == TanobyKeyMap && state.TanobyKeyVisit?.SolvedOnEntry == true)
        {
            var boulder = _content.SevenIslandField!.TanobyKey.Boulders.Single(b => b.LocalId == definition.SourceLocalId);
            return (boulder.SolvedX, boulder.SolvedY);
        }
        return (definition.X, definition.Y);
    }
    private bool SevenIslandBoulderIdentity(CampaignState state, CampaignActor actor, FieldActorDefinition definition) =>
        actor.LocalId == definition.LocalId && actor.SourceLocalId == definition.SourceLocalId &&
        actor.GraphicsId == definition.GraphicsId && actor.ScriptId == definition.ScriptId && actor.HideFlag == definition.HideFlag &&
        actor.MovementType == definition.MovementType && actor.Elevation == definition.Elevation &&
        (actor.TemplateX, actor.TemplateY) == SevenIslandBoulderTemplate(state, definition) && !actor.Invisible;
    private CampaignActor? AdjacentSevenIslandBoulder(CampaignState state, Direction direction)
    {
        var player = state.World.Player; var (dx, dy) = Delta(direction);
        var actor = state.Actors.Values.SingleOrDefault(a => a.Active && !a.Invisible &&
            a.X == player.X + dx && a.Y == player.Y + dy && a.GraphicsId == FieldHmSourceRules.BoulderGraphics &&
            (a.Elevation == 0 || player.Elevation == 0 || a.Elevation == player.Elevation));
        if (actor is null) return null;
        var definition = _content.MapDefinition(player.MapId).Actors.SingleOrDefault(a => a.LocalId == actor.LocalId);
        if (definition is null || !SevenIslandBoulderDefinition(definition, player.MapId) || !SevenIslandBoulderIdentity(state, actor, definition))
            throw new InvalidDataException("seven_island_boulder_definition_changed");
        return actor;
    }
    private CampaignState CompleteSevenIslandBoulderPush(CampaignState state, CampaignActor boulder)
    {
        if (state.World.Player.MapId.Value != TanobyKeyMap) return state;
        var puzzle = _content.SevenIslandField!.TanobyKey;
        var map = EditedFieldMaps(state)[puzzle.Map];
        if (map.CellAt(boulder.X, boulder.Y)?.SourceBehavior != puzzle.StrengthBehaviorId) return state;
        var button = puzzle.Switches.SingleOrDefault(s => s.X == boulder.X && s.Y == boulder.Y)
            ?? throw new InvalidDataException("tanoby_switch_not_admitted");
        var trigger = _content.MapDefinition(puzzle.Map).CoordTriggers.SingleOrDefault(t => t.X == button.X && t.Y == button.Y);
        if (trigger is null || trigger.Expected != 99 || trigger.Variable != button.TemporaryVariable ||
            trigger.ScriptId != button.Script || trigger.Elevation != button.Elevation)
            throw new InvalidDataException("tanoby_switch_source");
        var visit = state.TanobyKeyVisit ?? throw new InvalidDataException("tanoby_visit_missing");
        if (state.Script.Variables.GetValueOrDefault(button.TemporaryVariable) == 0)
        {
            if (visit.SwitchBoulderIds[button.Index - 1] != 0) throw new InvalidDataException("tanoby_switch_receipt_duplicate");
            state = state with { TanobyKeyVisit = visit with { SwitchBoulderIds = visit.SwitchBoulderIds.SetItem(button.Index - 1, boulder.SourceLocalId) } };
        }
        // Native HandleBoulderActivateVictoryRoadSwitch dispatches directly by the completed
        // boulder position. The player's VAR==99/elevation gate is deliberately not evaluated.
        return Pump(StartStory(state, trigger.ScriptId));
    }
    private string? ValidateSevenIslandBoulders(CampaignState state)
    {
        var mapId = state.World.Player.MapId;
        var definitions = _content.MapDefinition(mapId).Actors.Where(a => a.GraphicsId == FieldHmSourceRules.BoulderGraphics).ToArray();
        int expectedCount = mapId.Value == TanobyKeyMap ? 7 : 2;
        if (definitions.Length != expectedCount || definitions.Select(a => a.SourceLocalId).Distinct().Count() != expectedCount ||
            definitions.Any(a => !SevenIslandBoulderDefinition(a, mapId))) return "save_seven_island_boulder_source";
        bool strength = state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag);
        var map = EditedFieldMaps(state)[mapId];
        foreach (var definition in definitions)
        {
            if (!state.Actors.TryGetValue(definition.LocalId, out var actor) || !SevenIslandBoulderIdentity(state, actor, definition))
                return "save_seven_island_boulder_identity";
            if ((actor.X, actor.Y) != SevenIslandBoulderTemplate(state, definition) && !strength)
                return "save_seven_island_boulder_without_strength";
            var cell = map.CellAt(actor.X, actor.Y);
            if (cell is null || cell.Collision != 0 || cell.Elevation != 0 && cell.Elevation != actor.Elevation ||
                cell.SourceBehavior is FieldHmSourceRules.FallWarpBehavior or FieldHmSourceRules.NonAnimatedDoorBehavior)
                return "save_seven_island_boulder_tile";
            if (!actor.Active) continue;
            bool SameElevation(int elevation) => elevation == 0 || elevation == actor.Elevation;
            if (state.World.Player.X == actor.X && state.World.Player.Y == actor.Y && SameElevation(state.World.Player.Elevation) ||
                state.Actors.Values.Any(a => a.LocalId != actor.LocalId && a.Active && a.X == actor.X && a.Y == actor.Y && SameElevation(a.Elevation)))
                return "save_seven_island_boulder_overlap";
        }
        return null;
    }
}
