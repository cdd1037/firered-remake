using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.World;
using Godot;
using Direction = Game.Domain.World.Direction;

public partial class CampaignView
{
    private static readonly Game.Domain.Ids.MapId SilphElevatorId = new("map.silph-co-elevator");
    private sealed class SilphPadAnimation
    {
        public required CampaignSilphPad Pad;
        public required CampaignState Origin;
        public string Phase = "step";
        public double Frames;
    }
    private SilphPadAnimation? _silphPad;
    private string? _silphMenuWait;
    private int _silphMenuScroll, _silphMenuRow;
    private double _silphMenuFrames;
    private long _silphElevatorEffect;
    private double _silphElevatorTicks;
    private int _silphElevatorDistance, _silphElevatorDirection, _silphElevatorDuration;
    private bool _silphElevatorDing;
    private static readonly string[] SilphFloorSymbols = ["gText_11F", "gText_10F", "gText_9F", "gText_8F", "gText_7F", "gText_6F", "gText_5F", "gText_4F", "gText_3F", "gText_2F", "gText_1F", "gOtherText_Exit"];
    private bool SilphElevatorMenuActive => _catalog is not null && _session?.State.SilphElevator?.MenuDefault is not null &&
        _session.State.Script.Wait?.Kind == StoryWaitKind.Presentation && _session.State.World.Player.MapId == SilphElevatorId;
    private bool SilphElevatorMenuVisible => SilphElevatorMenuActive && FieldTextComplete &&
        (_session.State.Dialogue is null || _session.State.Dialogue.Page + 1 == _session.State.Dialogue.Pages.Length);
    private bool SilphElevatorAnimating => _silphElevatorDistance > 0 && _silphElevatorTicks + 1e-8 < _silphElevatorDuration &&
        _session?.State.World.Player.MapId == SilphElevatorId;
    private bool SilphInputLocked => _session?.State.SilphPad is not null || _silphPad is not null || SilphElevatorAnimating;
    private int SilphMenuWidth => (((int)SilphFloorSymbols.Max(symbol => _sourceNormal.MeasureText(SourceMenuLabelPresentation.Plain(_battleText, symbol)).X) + 9) / 8 + 1) * 8;
    private Vector2 SilphCameraOffset => !SilphElevatorAnimating ? Vector2.Zero :
        new(0, _silphElevatorTicks < 3 ? 0 : ((int)Math.Floor(_silphElevatorTicks / 3) % 2 == 0 ? 1 : -1));

    // The command has already committed the endpoint. Hold the origin visuals until source fade-out.
    private bool BeginSilphPadPresentation(CampaignState? before, CampaignState after)
    {
        if (after.SilphPad is not { } pad) { _silphPad = null; return false; }
        if (_silphPad?.Pad.PresentationId == pad.PresentationId) return true;
        if (before is null) throw new InvalidDataException("A transient Silph pad cannot be restored as a player save.");
        if (before.World.Player != pad.From || after.World.Player != pad.To)
            throw new InvalidDataException("Silph pad presentation endpoints differ.");
        _silphPad = new() { Pad = pad, Origin = before };
        _held = null; _tracks.Clear(); _fade = 0;
        return true;
    }
    private bool ProcessSilphPresentation(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (_silphPad is { } animation)
        {
            _held = null; animation.Frames += delta * 60;
            int frame = (int)Math.Floor(animation.Frames + 1e-7);
            if (animation.Phase == "step" && frame >= 16)
            { animation.Frames = animation.Frames - 16 + 1; animation.Phase = "out"; SourceSound("SE_WARP_IN"); }
            else if (animation.Phase == "out" && SilphSourcePresentation.TeleportOut(animation.Pad.Pad.Facing, frame).Complete)
            { animation.Frames = 0; animation.Phase = "fade-out"; }
            else if (animation.Phase == "fade-out" && frame >= 16)
            {
                animation.Frames = 1; animation.Phase = "in";
                LoadMap(animation.Pad.To.MapId); ApplyMapEdits(_session.State); _fade = 0;
                SynchronizeSourceAudio(animation.Origin, _session.State); SourceSound("SE_WARP_OUT");
            }
            else if (animation.Phase == "in" && SilphSourcePresentation.TeleportIn(animation.Pad.To.Facing, frame).Complete)
            {
                string id = animation.Pad.PresentationId;
                _silphPad = null; Apply(new("silph_pad_ack", Text: id));
                if (_session.State.SilphPad is not null) throw new InvalidDataException("Silph pad acknowledgement was rejected.");
            }
            QueueRedraw(); return true;
        }
        if (SilphElevatorMenuActive) { _held = null; _silphMenuFrames += delta * 60; QueueRedraw(); return true; }
        if (_session.State.World.Player.MapId == SilphElevatorId && _silphElevatorDistance > 0)
        {
            _silphElevatorTicks = Math.Min(_silphElevatorDuration, _silphElevatorTicks + delta * 60);
            if (!_silphElevatorDing && _silphElevatorTicks + 1e-8 >= _silphElevatorDuration)
            { _silphElevatorDing = true; SourceSound("SE_DING_DONG"); }
        }
        return false;
    }
    private void SynchronizeSilphPresentation(CampaignState? before, CampaignState after)
    {
        if (before is null || before.World.Player.MapId != after.World.Player.MapId)
        {
            _silphElevatorDistance = 0; _silphElevatorDuration = 0; _silphElevatorTicks = 0; _silphElevatorDing = false;
            _silphElevatorEffect = before is null ? after.Script.EffectSequence : 0;
        }
        string? menu = SilphElevatorMenuActive ? after.Script.Wait!.CorrelationId : null;
        if (menu != _silphMenuWait)
        {
            _silphMenuWait = menu; _silphMenuFrames = 0;
            if (menu is not null) (_silphMenuScroll, _silphMenuRow) = SilphSourcePresentation.MenuDefault(after.SilphElevator!.MenuDefault!.Value);
        }
        foreach (var effect in after.PresentationEffects.Where(e => e.Sequence > _silphElevatorEffect &&
            e.MapId == SilphElevatorId && e.Opcode == "special" && e.Args.SequenceEqual(new[] { "AnimateElevator" })))
        {
            _silphElevatorEffect = effect.Sequence;
            int from = after.Script.Variables.GetValueOrDefault("VAR_0x8005"), to = after.Script.Variables.GetValueOrDefault("VAR_0x8006");
            _silphElevatorDistance = Math.Abs(from - to); _silphElevatorDirection = from > to ? 1 : 0;
            _silphElevatorTicks = 0; _silphElevatorDing = false;
            _silphElevatorDuration = SilphSourcePresentation.ElevatorShakeFrames(from, to);
            if (from is < 4 or > 14 || to is < 4 or > 14 || _silphElevatorDistance == 0 || after.PresentationTicks != _silphElevatorDuration)
                throw new InvalidDataException("Silph elevator source duration differs.");
            _held = null; SourceSound("SE_ELEVATOR");
        }
    }
    private bool TrySilphInput(InputEventKey key)
    {
        if (!SilphElevatorMenuActive || key.Keycode is Key.Escape or Key.F12) return false;
        _held = null; if (!key.Pressed || key.Echo || !SilphElevatorMenuVisible) return true;
        if (key.Keycode is Key.Up or Key.Down)
        {
            var next = SilphSourcePresentation.MoveMenu(_silphMenuScroll, _silphMenuRow, key.Keycode == Key.Down);
            if (next != (_silphMenuScroll, _silphMenuRow)) { (_silphMenuScroll, _silphMenuRow) = next; SourceSound("SE_SELECT"); }
        }
        else if (key.Keycode is Key.Enter or Key.Backspace)
        { SourceSound("SE_SELECT"); Apply(new("silph_elevator_choice", Choice: key.Keycode == Key.Enter ? _silphMenuScroll + _silphMenuRow : 127)); }
        return true;
    }
    private bool NavigateSilphPointer(Vector2 point, int direction) => SilphElevatorMenuVisible &&
        PointerRows(point, direction, ref _silphMenuRow, 7, 8, 8, SilphMenuWidth, 14);
    private bool MouseSilphElevator(Vector2 point) => SilphElevatorMenuVisible &&
        MouseSelect(point, ref _silphMenuRow, 7, 8, 8, SilphMenuWidth, 14);
    private void DrawSilphElevatorMenu()
    {
        if (_session.State.World.Player.MapId != SilphElevatorId) return;
        if (_session.State.SilphElevator?.FloorWindowVisible == true)
        {
            _battleSkin.DrawUserFrame(this, new(168, 0, 72, 48)); SourceText(_sourceNormal, new(176, 10), SourceMenuLabelPresentation.Plain(_battleText, "gText_NowOn"));
            int floor = _session.State.Script.Variables.GetValueOrDefault("VAR_0x8005");
            if (floor is < 4 or > 14) throw new InvalidDataException("Silph current floor differs.");
            string label = SourceMenuLabelPresentation.Plain(_battleText, SilphFloorSymbols[14 - floor]);
            SourceText(_sourceNormal, new(232 - _sourceNormal.MeasureText(label).X, 24), label);
        }
        if (!SilphElevatorMenuVisible) return;
        _battleSkin.DrawUserFrame(this, new(0, 0, SilphMenuWidth + 16, 112));
        for (int row = 0; row < 7; row++) SourceText(_sourceNormal, new(16, 8 + row * 14), SourceMenuLabelPresentation.Plain(_battleText, SilphFloorSymbols[_silphMenuScroll + row]), 1);
        SourceText(_sourceNormal, new(8, 8 + _silphMenuRow * 14), "▶");
        // The source shows scroll indicators only when there are hidden entries in that direction.
        int tick = (int)Math.Floor(_silphMenuFrames + 1e-7);
        if (_silphMenuScroll > 0) DrawTexture(_silphScrollUp!, new(SilphMenuWidth / 2, _silphInterface!.BounceOffset(false, tick)));
        if (_silphMenuScroll < 5) DrawTexture(_silphScrollDown!, new(SilphMenuWidth / 2, 98 + _silphInterface!.BounceOffset(true, tick)));
    }
    private bool DrawSilphElevatorLayer(Texture2D original, Vector2 camera, int layer, Rid? target = null)
    {
        if (_session.State.World.Player.MapId != SilphElevatorId || _silphElevatorDistance == 0) return false;
        int count = Math.Min(SilphSourcePresentation.ElevatorWindowCycles(_silphElevatorDistance), Math.Max(0, ((int)Math.Floor(_silphElevatorTicks) - 1) / 6));
        int phase = count % 3; if (_silphElevatorDirection != 0) phase = (3 - phase) % 3;
        for (int y = 0; y < _art.Source.Height; y++) for (int x = 0; x < _art.Source.Width; x++)
        {
            var destination = new Rect2(new Vector2(x * 16, y * 16) - camera, new(16, 16));
            if (x is >= 1 and <= 3 && y <= 2)
            {
                string symbol = "METATILE_SilphCo_ElevatorWindow_" + (y == 0 ? "Top" : y == 1 ? "Mid" : "Bottom") + phase;
                var tile = _rocketTiles[("silph_co", _rocketTileSymbols[symbol])];
                DrawMapTextureRegion(_rocketTileLayers[tile.Tileset][layer], destination, new(tile.Region.Position, tile.Region.Size), target);
            }
            else DrawMapTextureRegion(original, destination, new(x * 16, y * 16, 16, 16), target);
        }
        return true;
    }
    private bool DrawSilphPad()
    {
        if (_silphPad is not { } animation) return false;
        var pad = animation.Pad; bool incoming = animation.Phase == "in";
        var state = incoming ? _session.State : animation.Origin;
        var position = incoming ? new Vector2(pad.To.X * 16, pad.To.Y * 16) : new Vector2(pad.Pad.X * 16, pad.Pad.Y * 16);
        var pose = new Pose { Position = position, Facing = incoming ? pad.To.Facing : pad.Pad.Facing, Visible = true };
        int frame = (int)Math.Floor(animation.Frames + 1e-7);
        if (animation.Phase == "step")
        { pose.Position = new Vector2(pad.From.X * 16, pad.From.Y * 16).Lerp(position, (float)Math.Clamp(animation.Frames / 16, 0, 1)); pose.Frame = frame / 8 % 4 + 1; }
        else
        {
            var sourcePose = incoming ? SilphSourcePresentation.TeleportIn(pad.To.Facing, frame) : SilphSourcePresentation.TeleportOut(pad.Pad.Facing, animation.Phase == "fade-out" ? 36 : frame);
            pose.Facing = sourcePose.Facing; pose.DrawYOffset = sourcePose.OffsetY;
        }
        var camera = (position + new Vector2(8, 8) - new Vector2(120, 80)).Floor();
        if (animation.Phase == "step") camera = (pose.Position + new Vector2(8, 8) - new Vector2(120, 80)).Floor();
        PrepareVisibleMapResources(camera);
        DrawRect(new(0, 0, 240, 160), Colors.Black);
        if (_art.Border is { } border) DrawMapBorder(border.Bottom, camera); DrawTexture(_art.Bottom, -camera);
        if (_art.Border is { } middle) DrawMapBorder(middle.Middle, camera); DrawTexture(_art.Middle, -camera);
        foreach (var actor in state.Actors.Values.Where(a => a.Visible && a.MovementType != "MOVEMENT_TYPE_INVISIBLE").OrderBy(a => a.Y))
        {
            if (!_art.Npcs.TryGetValue(ResolveRegionGraphics(actor.GraphicsId), out var asset)) throw new InvalidDataException("Silph NPC pixels missing: " + actor.GraphicsId);
            DrawActor(asset, new Pose { Position = new(actor.X * 16, actor.Y * 16), Facing = actor.Facing, Visible = true }, camera);
        }
        if (_art.Border is { } top) DrawMapBorder(top.Top, camera); DrawTexture(_art.Top, -camera);
        DrawActor(CurrentPlayerSprite, pose, camera);
        float alpha = animation.Phase == "fade-out" ? Math.Clamp((float)animation.Frames / 16, 0, 1) : incoming ? Math.Clamp(1 - (float)animation.Frames / 16, 0, 1) : 0;
        if (alpha > 0) DrawRect(new(0, 0, 240, 160), new Color(0, 0, 0, alpha));
        return true;
    }
}
