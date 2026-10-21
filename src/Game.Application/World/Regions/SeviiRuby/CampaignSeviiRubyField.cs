using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private IReadOnlyDictionary<MapId, IFieldMap> SeviiRubyMovementMaps(IReadOnlyDictionary<MapId, IFieldMap> maps, CampaignState state)
    {
        return new CampaignMapView<IFieldMap>(maps,(id,map) =>
        {
            if(!SeviiRubyMap(id)&&id.Value!=RubyCenterMap)return map;
            var edits = id == state.World.Player.MapId ? state.MapEdits : SeviiRubyExpectedEdits(
                state with { World = state.World with { Player = state.World.Player with { MapId = id } } });
            return new SeviiRubyMovementMap(map, edits, _content);
        });
    }

    private sealed class SeviiRubyMovementMap(IFieldMap original, ImmutableSortedDictionary<string, FieldMetatileEdit>? edits,
        ICampaignCatalog content) : IFieldMap
    {
        public MapId Id => original.Id;
        public int Width => original.Width;
        public int Height => original.Height;
        public ImmutableArray<FieldWarp> FieldWarps => original.FieldWarps;
        private int? Behavior(int x, int y) => edits?.TryGetValue(x + ":" + y, out var edit) == true
            ? content.MetatileDefinition(Id, edit.SourceMetatileId)!.Behavior : original.SourceBehaviorAt(x, y);
        public int? SourceEncounterTypeAt(int x, int y) => edits?.TryGetValue(x + ":" + y, out var edit) == true
            ? content.MetatileDefinition(Id, edit.SourceMetatileId)!.EncounterType : original.SourceEncounterTypeAt(x, y);
        public int? SourceBehaviorAt(int x, int y) => Behavior(x, y) is 0x0c or 0x17 or 0x28 && GetTile(x, y)?.Blocked == false ? 0 : Behavior(x, y);
        public TileCell? GetTile(int x, int y)
        {
            var tile = original.GetTile(x, y);
            if (edits?.TryGetValue(x + ":" + y, out var edit) == true)
                tile = new(edit.Collision != 0, edit.Elevation, tile?.Behavior ?? TileBehavior.Unsupported);
            return tile is { Blocked: false } && Behavior(x, y) is 0x00 or 0x0c or 0x17 or 0x28
                ? tile with { Behavior = TileBehavior.Floor } : tile;
        }
    }

    private IReadOnlyDictionary<MapId, ConnectedFieldMap> SeviiRubyWalkingMaps(IReadOnlyDictionary<MapId, ConnectedFieldMap> maps)
    {
        // Preserve source cells for Surf, encounters, terrain and presentation;
        // only the walking/dismount view maps this land to its ordinary ground token.
        return new CampaignMapView<ConnectedFieldMap>(maps,(id,map)=>
            id.Value is "map.one-island-kindle-road" or "map.one-island-kindle-road-ember-spa" or "map.mt-ember-exterior"
                ? map with { Cells = map.Cells.Select(c => c.Collision == 0 && (c.SourceBehavior is 0x0c or 0x17 or 0x28)
                    ? c with { SourceBehavior = 0x00 } : c).ToImmutableArray() } : map);
    }

    private CampaignActor? AdjacentSeviiRubyBoulder(CampaignState state, Direction direction)
    {
        var p = state.World.Player; var (dx, dy) = Delta(direction);
        var actor = state.Actors.Values.SingleOrDefault(a => a.Active && !a.Invisible &&
            a.X == p.X + dx && a.Y == p.Y + dy && a.GraphicsId == FieldHmSourceRules.BoulderGraphics &&
            (a.Elevation == 0 || p.Elevation == 0 || a.Elevation == p.Elevation));
        if (actor is null) return null;
        var d = _content.MapDefinition(p.MapId).Actors.SingleOrDefault(a => a.LocalId == actor.LocalId);
        if (d is null || d.IsClone || d.GraphicsId != FieldHmSourceRules.BoulderGraphics || d.ScriptId != "EventScript_StrengthBoulder" ||
            d.MovementType != "MOVEMENT_TYPE_LOOK_AROUND" || d.HideFlag != "0" || d.TrainerSight != 0 ||
            actor.SourceLocalId != d.SourceLocalId || actor.ScriptId != d.ScriptId || actor.HideFlag != d.HideFlag || actor.MovementType != d.MovementType)
            throw new InvalidDataException("sevii_ruby_boulder_source");
        return actor;
    }

    private string? ValidateSeviiRubyBoulders(CampaignState state)
    {
        var map = state.World.Player.MapId; bool active = state.Script.Flags.GetValueOrDefault(FieldHmSourceRules.StrengthActiveFlag);
        foreach (var d in _content.MapDefinition(map).Actors.Where(a => a.GraphicsId == FieldHmSourceRules.BoulderGraphics))
        {
            if (!state.Actors.TryGetValue(d.LocalId, out var a) || a.Invisible || d.ScriptId != "EventScript_StrengthBoulder" ||
                a.ScriptId != d.ScriptId || a.MovementType != d.MovementType || a.SourceLocalId != d.SourceLocalId ||
                a.HideFlag != d.HideFlag || (a.TemplateX, a.TemplateY) != (d.X, d.Y) || a.Elevation != d.Elevation)
                return "save_sevii_ruby_boulder_identity";
            if ((a.X, a.Y) != (d.X, d.Y) && !active) return "save_sevii_ruby_boulder_without_strength";
            var cell = EditedFieldMaps(state)[map].CellAt(a.X, a.Y);
            if (cell is null || cell.Collision != 0 || cell.Elevation != d.Elevation ||
                cell.SourceBehavior is FieldHmSourceRules.FallWarpBehavior or FieldHmSourceRules.NonAnimatedDoorBehavior)
                return "save_sevii_ruby_boulder_tile";
            if (!a.Active) continue;
            bool Same(int e) => e == 0 || a.Elevation == 0 || e == a.Elevation;
            if (state.World.Player.X == a.X && state.World.Player.Y == a.Y && Same(state.World.Player.Elevation) ||
                state.Actors.Values.Any(o => o.LocalId != a.LocalId && o.Active && o.X == a.X && o.Y == a.Y && Same(o.Elevation)))
                return "save_sevii_ruby_boulder_overlap";
        }
        return null;
    }

    private bool RubyBrailleWait(CampaignState state) => InSeviiRubyScript(state) && state.Script.Locked &&
        state.World.Player.MapId.Value is "map.mt-ember-ruby-path-b4f" or "map.mt-ember-ruby-path-b5f" &&
        state.Script.Wait?.Kind == StoryWaitKind.ButtonPress && state.Dialogue?.TextSymbol.StartsWith("Braille_Text_", StringComparison.Ordinal) == true;
    private bool RubyPasswordWait(CampaignState state) => state.Script.Locked &&
        state.World.Player.MapId.Value == RubyExteriorMap && state.Script.Wait?.Kind == StoryWaitKind.ButtonPress &&
        state.Script.Stack.LastOrDefault()?.ProgramId == "MtEmber_Exterior_EventScript_RocketPasswordScene" &&
        state.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_MT_EMBER_EXTERIOR") == 1 &&
        state.Dialogue?.TextSymbol == "MtEmber_Exterior_Text_WhatsPasswordAgain";
    private bool RubyButtonWait(CampaignState state) => RubyBrailleWait(state) || RubyPasswordWait(state);
    private Decision<CampaignState> DecideRubyButton(CampaignState state, CampaignCommand command)
    {
        if (!RubyButtonWait(state) || command.Text is not ("A" or "B")) return new(CommandStatus.Rejected, state, "sevii_ruby_button_required");
        var next = state with { PresentationEffects = [], Motions = [], PresentationTicks = 0 };
        return new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, state.Script.Wait!.CorrelationId, _content))));
    }
    private bool TryApplyRubyBrailleEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!InSeviiRubyScript(state)) return false;
        bool source = effect.Source.Path is "data/maps/MtEmber_RubyPath_B4F/scripts.inc" or "data/maps/MtEmber_RubyPath_B5F/scripts.inc";
        if (effect.Opcode == "waitbuttonpress" && (source ||
            effect.Source.Path == "data/event_scripts.s" && effect.Source.Symbol == "EventScript_BrailleCursorWaitButton" ||
            effect.Source.Path == RubyExteriorSource && effect.Source.Symbol == "MtEmber_Exterior_EventScript_RocketPasswordScene"))
        {
            if (effect.Args.Length != 0 || !RubyButtonWait(state) || state.Script.Stack.LastOrDefault()?.ProgramId != effect.Source.Symbol)
                throw new InvalidDataException("sevii_ruby_button_source_context");
            return true;
        }
        if (source && effect.Opcode is "braillemessage" or "getbraillestringwidth")
        {
            if (!state.Script.Locked || effect.Args.Length != 1 || !effect.Args[0].StartsWith("Braille_Text_", StringComparison.Ordinal))
                throw new InvalidDataException("sevii_braille_source_context");
            var pages = _content.TextPages(effect.Args[0]);
            if (pages.Length != 1 || pages[0].Any(c => !(c is >= 'A' and <= 'Z' or ' ' or ',' or '.')))
                throw new InvalidDataException("sevii_braille_characters");
            // Original FONT_BRAILLE: width16, spacing0, bounded plain-letter bank.
            next = effect.Opcode == "braillemessage" ? state with { Dialogue = new(effect.Args[0], pages, 0, false, false) } :
                state with { Script = state.Script with { Variables = state.Script.Variables.SetItem("VAR_0x8004", checked(pages[0].Length * 16)) } };
            return true;
        }
        if (effect.Opcode == "special" && effect.Args.SequenceEqual(new[] { "BrailleCursorToggle" }) &&
            effect.Source.Path == "data/event_scripts.s" && effect.Source.Symbol == "EventScript_BrailleCursorWaitButton")
        {
            if (state.Dialogue?.TextSymbol.StartsWith("Braille_Text_", StringComparison.Ordinal) != true ||
                state.Script.Variables.GetValueOrDefault("VAR_0x8006") is not (0 or 1) ||
                state.Script.Variables.GetValueOrDefault("VAR_0x8005") != 130) throw new InvalidDataException("sevii_braille_cursor_context");
            return true; // Presentation consumes this retained source effect and variables.
        }
        return false;
    }
}
