using System.Collections.Immutable;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private IReadOnlyDictionary<MapId, ConnectedFieldMap> SeviiRubyWalkingMaps(IReadOnlyDictionary<MapId, ConnectedFieldMap> maps)
    {
        if (!_content.SeviiRubyEnabled) return maps;
        var result = maps.ToDictionary(p => p.Key, p => p.Value);
        foreach (var pair in maps.Where(p => p.Key.Value is "map.one-island-kindle-road" or "map.one-island-kindle-road-ember-spa" or "map.mt-ember-exterior"))
        {
            // These authenticated source behaviors are ordinary collision-controlled land.
            // Preserve the source cells for Surf, encounters, terrain and presentation;
            // only the walking/dismount adapter maps them to its ordinary ground token.
            result[pair.Key] = pair.Value with { Cells = pair.Value.Cells.Select(c =>
                c.Collision == 0 && (c.SourceBehavior is 0x0c or 0x17 or 0x28)
                    ? c with { SourceBehavior = 0x00 } : c).ToImmutableArray() };
        }
        return result;
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
            d.MovementType != "MOVEMENT_TYPE_FACE_DOWN" || d.HideFlag != "0" || d.TrainerSight != 0 ||
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

    private bool RubyBrailleWait(CampaignState state) => _content.SeviiRubyEnabled && InSeviiRubyScript(state) &&
        state.Script.Wait?.Kind == StoryWaitKind.ButtonPress && state.Dialogue?.TextSymbol.StartsWith("Braille_Text_", StringComparison.Ordinal) == true;
    private Decision<CampaignState> DecideRubyBrailleButton(CampaignState state, CampaignCommand command)
    {
        if (!RubyBrailleWait(state) || command.Text is not ("A" or "B")) return new(CommandStatus.Rejected, state, "braille_button_required");
        var next = state with { PresentationEffects = [], Motions = [], PresentationTicks = 0 };
        return new(CommandStatus.Accepted, Pump(Advance(next, StoryRules.Continue(next.Script, state.Script.Wait!.CorrelationId, _content))));
    }
    private bool TryApplyRubyBrailleEffect(CampaignState state, StoryEffect effect, out CampaignState next)
    {
        next = state;
        if (!InSeviiRubyScript(state)) return false;
        bool source = effect.Source.Path is "data/maps/MtEmber_RubyPath_B4F/scripts.inc" or "data/maps/MtEmber_RubyPath_B5F/scripts.inc";
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
