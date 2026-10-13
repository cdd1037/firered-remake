using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    // Original SixIsland_RuinValley/map.json object order, at the pinned source.
    // These ordinary boulders share the existing Strength script, push and reset.
    private static bool SixIslandBoulderDefinition(FieldActorDefinition definition)
    {
        (int X, int Y, int Elevation)? expected = definition.SourceLocalId switch
        {
            7 => (17, 10, 5), 8 => (17, 12, 5), 9 => (18, 11, 5),
            10 => (6, 33, 3), 11 => (6, 34, 3),
            12 => (41, 32, 3), 13 => (41, 33, 3), 14 => (42, 33, 3),
            _ => null
        };
        return expected is {} source && (definition.X, definition.Y, definition.Elevation) == source &&
            !definition.IsClone && definition.GraphicsId == FieldHmSourceRules.BoulderGraphics &&
            definition.ScriptId == "EventScript_StrengthBoulder" && definition.HideFlag == "0" &&
            definition.MovementType == "MOVEMENT_TYPE_LOOK_AROUND" &&
            definition.TrainerType == "TRAINER_TYPE_NONE" && definition.TrainerSight == 0;
    }

    private static bool SixIslandBoulderIdentity(CampaignActor actor, FieldActorDefinition definition) =>
        actor.LocalId == definition.LocalId && actor.SourceLocalId == definition.SourceLocalId &&
        actor.GraphicsId == definition.GraphicsId && actor.ScriptId == definition.ScriptId &&
        actor.HideFlag == definition.HideFlag && actor.MovementType == definition.MovementType &&
        (actor.TemplateX, actor.TemplateY, actor.Elevation) == (definition.X, definition.Y, definition.Elevation) &&
        !actor.Invisible;

    private CampaignActor? AdjacentSixIslandBoulder(CampaignState state, Direction direction)
    {
        var player = state.World.Player; var (dx, dy) = Delta(direction);
        var actor = state.Actors.Values.SingleOrDefault(a => a.Active && !a.Invisible &&
            a.X == player.X + dx && a.Y == player.Y + dy && a.GraphicsId == FieldHmSourceRules.BoulderGraphics &&
            (a.Elevation == 0 || player.Elevation == 0 || a.Elevation == player.Elevation));
        if (actor is null) return null;
        var definition = _content.MapDefinition(player.MapId).Actors.SingleOrDefault(a => a.LocalId == actor.LocalId);
        if (definition is null || !SixIslandBoulderDefinition(definition) || !SixIslandBoulderIdentity(actor, definition))
            throw new InvalidDataException("six_island_boulder_definition_changed");
        return actor;
    }

    private string? ValidateSixIslandBoulders(CampaignState state)
    {
        var definitions = _content.MapDefinition(state.World.Player.MapId).Actors
            .Where(a => a.GraphicsId == FieldHmSourceRules.BoulderGraphics).ToArray();
        if (definitions.Length != 8 || definitions.Select(a => a.SourceLocalId).Distinct().Count() != 8 ||
            definitions.Any(a => !SixIslandBoulderDefinition(a))) return "save_six_island_boulder_source";
        bool strength = state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag);
        var map = EditedFieldMaps(state)[state.World.Player.MapId];
        foreach (var definition in definitions)
        {
            if (!state.Actors.TryGetValue(definition.LocalId, out var actor) || !SixIslandBoulderIdentity(actor, definition))
                return "save_six_island_boulder_identity";
            if ((actor.X, actor.Y) != (definition.X, definition.Y) && !strength)
                return "save_six_island_boulder_without_strength";
            var cell = map.CellAt(actor.X, actor.Y);
            // Native elevation-zero transitions preserve the object's current elevation.
            if (cell is null || cell.Collision != 0 || cell.Elevation != 0 && cell.Elevation != actor.Elevation ||
                cell.SourceBehavior is FieldHmSourceRules.FallWarpBehavior or FieldHmSourceRules.NonAnimatedDoorBehavior)
                return "save_six_island_boulder_tile";
            if (!actor.Active) continue;
            bool SameElevation(int elevation) => elevation == 0 || elevation == actor.Elevation;
            if (state.World.Player.X == actor.X && state.World.Player.Y == actor.Y && SameElevation(state.World.Player.Elevation) ||
                state.Actors.Values.Any(a => a.LocalId != actor.LocalId && a.Active && a.X == actor.X && a.Y == actor.Y && SameElevation(a.Elevation)))
                return "save_six_island_boulder_overlap";
        }
        return null;
    }
}
