using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private CampaignActor? AdjacentSeafoamBoulder(CampaignState state, Direction direction)
    {
        var p = state.World.Player; var (dx, dy) = Delta(direction);
        var actor = state.Actors.Values.SingleOrDefault(a => a.Active && !a.Invisible &&
            a.X == p.X + dx && a.Y == p.Y + dy && a.GraphicsId == FieldHmSourceRules.BoulderGraphics &&
            (a.Elevation == 0 || p.Elevation == 0 || a.Elevation == p.Elevation));
        if (actor is null) return null;
        var source = SeafoamFieldRules.Boulders.SingleOrDefault(b => b.Map == p.MapId && b.LocalId == actor.SourceLocalId);
        if (source is not { Interactable: true }) return null;
        if (actor.HideFlag != source.HideFlag || actor.ScriptId != "EventScript_StrengthBoulder" ||
            (actor.TemplateX, actor.TemplateY, actor.Elevation) != (source.X, source.Y, source.Elevation))
            throw new InvalidDataException("seafoam_boulder_identity");
        return actor;
    }
    private bool SeafoamBoulderFallSupported(CampaignState state, CampaignActor actor, TilePosition target) =>
        _content.SeafoamEnabled && SeafoamFieldRules.Boulders.Any(b => b.Map == target.MapId &&
            b.LocalId == actor.SourceLocalId && b.Interactable) &&
        SeafoamFieldRules.Holes.Any(h => h.Map == target.MapId && h.X == target.X && h.Y == target.Y);

    private CampaignState CompleteSeafoamBoulderPush(CampaignState state, CampaignActor actor)
    {
        if (!_content.SeafoamEnabled || !SeafoamFieldRules.IsCave(state.World.Player.MapId) ||
            EditedFieldMaps(state)[state.World.Player.MapId].CellAt(actor.X, actor.Y)?.SourceBehavior != SeafoamFieldRules.FallBehavior)
            return state;
        var flags = SeafoamFieldRules.CompleteBoulderFall(state.World.Player.MapId, actor.SourceLocalId, actor.X, actor.Y, state.Script.Flags);
        return state with { Script = state.Script with { Flags = flags },
            Actors = state.Actors.SetItem(actor.LocalId, actor with { Active = false }),
            ObjectSlots = state.ObjectSlots?.RemoveRange(state.ObjectSlots.Where(pair => pair.Value == actor.LocalId).Select(pair => pair.Key)) };
    }
    private string? ValidateSeafoamBoulders(CampaignState state)
    {
        var map = state.World.Player.MapId;
        bool active = state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag);
        foreach (var source in SeafoamFieldRules.Boulders.Where(b => b.Map == map))
        {
            var actor = state.Actors.Values.SingleOrDefault(a => a.SourceLocalId == source.LocalId);
            if (actor is null || actor.HideFlag != source.HideFlag || actor.GraphicsId != FieldHmSourceRules.BoulderGraphics ||
                actor.Invisible || (actor.TemplateX, actor.TemplateY, actor.Elevation) != (source.X, source.Y, source.Elevation))
                return "save_seafoam_boulder_identity";
            bool hidden = state.Script.Flags.GetValueOrDefault(source.HideFlag);
            if (hidden && actor.Active) return "save_seafoam_hidden_boulder_active";
            bool fallen = hidden && SeafoamFieldRules.Holes.Any(h => h.Map == map && h.X == actor.X && h.Y == actor.Y);
            if (fallen) continue;
            if ((actor.X, actor.Y) != (source.X, source.Y) && (!source.Interactable || !active))
                return "save_seafoam_boulder_without_strength";
            var cell = EditedFieldMaps(state)[map].CellAt(actor.X, actor.Y);
            if (cell is null || cell.Collision != 0 || cell.Elevation != 0 && cell.Elevation != actor.Elevation ||
                cell.SourceBehavior == SeafoamFieldRules.FallBehavior) return "save_seafoam_boulder_tile";
            if (!actor.Active) continue;
            bool Same(int elevation) => elevation == 0 || elevation == actor.Elevation;
            if (state.World.Player.X == actor.X && state.World.Player.Y == actor.Y && Same(state.World.Player.Elevation) ||
                state.Actors.Values.Any(other => other.LocalId != actor.LocalId && other.Active && other.X == actor.X && other.Y == actor.Y && Same(other.Elevation)))
                return "save_seafoam_boulder_overlap";
        }
        return null;
    }
}
