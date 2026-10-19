using System.Collections.Immutable;
using System.Globalization;
using Game.Application;
using Game.Domain.Events;
using Godot;

/// <summary>Presentation-only task clock. Source callbacks never wait for this clock.</summary>
internal sealed class BillPresentationTimeline
{
    internal sealed record Cell(string Kind, int Value = 0, int DoorPart = 0);
    private sealed record Pending(double At, long Sequence, int Order, BillSourceDraw Draw);
    private readonly HashSet<long> _seen = [];
    private readonly List<Pending> _pending = [];
    private readonly Dictionary<(int X, int Y), Cell> _metatiles = [];
    private readonly Dictionary<(int X, int Y), Cell> _cells = [];
    private double _clock;
    public IReadOnlyDictionary<(int X, int Y), Cell> Cells => _cells;
    public int PendingCount => _pending.Count;

    public void Reset()
    {
        _seen.Clear(); _pending.Clear(); _metatiles.Clear(); _cells.Clear(); _clock = 0;
    }

    public void Observe(IEnumerable<CampaignPresentation> effects)
    {
        foreach (var effect in effects)
        {
            if (effect.MapId.Value != BillSourcePresentation.Cottage || !effect.Opcode.StartsWith("bill_teleporter_", StringComparison.Ordinal) || _seen.Contains(effect.Sequence)) continue;
            var args = effect.Args;
            int Number(int index) => int.Parse(args[index], NumberStyles.Integer, CultureInfo.InvariantCulture);
            ImmutableArray<BillSourceDraw> frames;
            switch (effect.Opcode)
            {
                case "bill_teleporter_door":
                    if (args.Length != 4 || args[2] is not ("open" or "close") || Number(3) != BillSourcePresentation.DoorAnimationTicks) throw new InvalidDataException("Bill door presentation metadata differs.");
                    frames = BillSourcePresentation.DoorFrames(Number(0), Number(1), args[2] == "open");
                    break;
                case "bill_teleporter_housing":
                    if (args.Length != 5 || Number(0) is not (0 or 1) || Number(3) != 13 || Number(4) != 16) throw new InvalidDataException("Bill housing presentation metadata differs.");
                    int selector = Number(0);
                    // Metadata carries task destination coordinates, not the moving camera position.
                    frames = BillSourcePresentation.HousingFrames(Number(1) - (selector == 0 ? 6 : -1), Number(2) + 5, selector);
                    break;
                case "bill_teleporter_cable":
                    if (args.Length != 4 || Number(2) != 4 || Number(3) != 4) throw new InvalidDataException("Bill cable presentation metadata differs.");
                    frames = BillSourcePresentation.CableFrames(Number(0) - 4, Number(1) + 5);
                    break;
                default: throw new InvalidDataException("Unknown Bill presentation task: " + effect.Opcode);
            }
            _seen.Add(effect.Sequence);
            for (int n = 0; n < frames.Length; n++) _pending.Add(new(_clock + frames[n].Frame, effect.Sequence, n, frames[n]));
        }
        Flush();
    }

    public void Advance(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        _clock += delta * 60;
        Flush();
    }

    private void Flush()
    {
        // Process crossed boundaries in chronological task order, including a large frame delta.
        foreach (var pending in _pending.Where(p => p.At <= _clock + 1e-8).OrderBy(p => p.At).ThenBy(p => p.Sequence).ThenBy(p => p.Order).ToArray())
        {
            var draw = pending.Draw;
            if (draw.Kind == "door")
            {
                for (int part = 0; part < 2; part++)
                {
                    var cell = (draw.X, draw.Y + part);
                    if (draw.Value >= 0) _cells[cell] = new("door", draw.Value, part);
                    else if (_metatiles.TryGetValue(cell, out var restored)) _cells[cell] = restored;
                    else _cells.Remove(cell);
                }
            }
            else _cells[(draw.X, draw.Y)] = _metatiles[(draw.X, draw.Y)] = new(draw.Kind);
            _pending.Remove(pending);
        }
    }
}

public partial class CampaignView
{
    private readonly BillPresentationTimeline _billTimeline = new();
    private readonly Dictionary<string, Rect2> _billMetatileRegions = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Texture2D> _billPictures = [];
    private readonly Texture2D?[] _billAnimationLayers = new Texture2D?[3];
    private Texture2D? _billDoor;
    private string? _billMenuWait;
    private int _billPcCursor;
    private string[] BillPcRows = [];
    private const string BillPcProgram = "Route25_SeaCottage_EventScript_BillsMonList";

    private void LoadBillPresentation()
    {
        if (_catalog.BillRegion is not { } source) return;
        BillPcRows = new[] { 133, 136, 135, 134 }.Select(id =>
            _catalog.SourceSpeciesName(id) ?? throw new InvalidDataException("Bill PC species display name missing."))
            .Append(SourceMenuLabelPresentation.Plain(_battleText, "gText_QuitLooking")).ToArray();
        var metadata = source.Animations;
        Texture2D Asset(string path)
        {
            var artifact = metadata.GetProperty("artifacts").EnumerateArray().Single(a => a.GetProperty("path").GetString() == path);
            var texture = SceneTextures.Texture(System.IO.Path.Combine(source.GeneratedDirectory, path));
            if (texture.GetWidth() != artifact.GetProperty("width").GetInt32() || texture.GetHeight() != artifact.GetProperty("height").GetInt32()) throw new InvalidDataException("Bill atlas dimensions differ.");
            return texture;
        }
        string[] layers = ["bottom", "middle", "top"];
        for (int layer = 0; layer < layers.Length; layer++) _billAnimationLayers[layer] = Asset("animation/teleporter-" + layers[layer] + ".png");
        var door = metadata.GetProperty("door");
        if (door.GetProperty("frameWidth").GetInt32() != 16 || door.GetProperty("frameHeight").GetInt32() != 32 || door.GetProperty("frameCount").GetInt32() != 3) throw new InvalidDataException("Bill door frame dimensions differ.");
        _billDoor = Asset(door.GetProperty("texturePath").GetString()!);
        foreach (var tile in metadata.GetProperty("metatiles").EnumerateArray())
        {
            var kind = tile.GetProperty("symbol").GetString()! switch
            {
                "METATILE_SeaCottage_Teleporter_Cable_Top" => "cable_top",
                "METATILE_SeaCottage_Teleporter_Cable_Bottom" => "cable_bottom",
                "METATILE_SeaCottage_Teleporter_Light_Green" => "light_green",
                "METATILE_SeaCottage_Teleporter_Light_Yellow" => "light_yellow",
                "METATILE_SeaCottage_Teleporter_Light_Red" => "light_red",
                "METATILE_SeaCottage_Teleporter_Door" => "door_closed",
                "METATILE_SeaCottage_Teleporter_Door_HalfGlowing" => "door_half_glowing",
                "METATILE_SeaCottage_Teleporter_Door_FullGlowing" => "door_full_glowing",
                "METATILE_SeaCottage_Teleporter_CableBall_Top" => "cable_ball_top",
                "METATILE_SeaCottage_Teleporter_CableBall_Bottom" => "cable_ball_bottom",
                _ => throw new InvalidDataException("Unknown Bill animation metatile.")
            };
            var region = new Rect2(tile.GetProperty("atlasX").GetInt32(), tile.GetProperty("atlasY").GetInt32(), tile.GetProperty("width").GetInt32(), tile.GetProperty("height").GetInt32());
            if (region.Size != new Vector2(16, 16) || !new Rect2(0, 0, 160, 16).Encloses(region)) throw new InvalidDataException("Bill metatile atlas region differs.");
            _billMetatileRegions.Add(kind, region);
        }
        foreach (var picture in (_catalog.BillItems ?? throw new InvalidDataException("Bill picture catalog missing.")).DexViews)
        {
            var texture = SceneTextures.Texture(System.IO.Path.Combine(_root, "content/generated/data/items/bill", picture.PngPath));
            if (texture.GetWidth() != picture.Width || texture.GetHeight() != picture.Height) throw new InvalidDataException("Bill picture dimensions differ.");
            _billPictures.Add(picture.SourceSpeciesId, texture);
        }
    }

    private void SynchronizeBillPresentation(CampaignState? before, CampaignState after)
    {
        if (before is null || before.World.Player.MapId != after.World.Player.MapId) _billTimeline.Reset();
        if (after.World.Player.MapId.Value == BillSourcePresentation.Cottage) _billTimeline.Observe(after.PresentationEffects);
        string? wait = BillPcMenuActive ? after.Script.Wait!.CorrelationId : null;
        if (wait != _billMenuWait) { _billMenuWait = wait; _billPcCursor = 0; }
    }

    private void ProcessBillPresentation(double delta)
    {
        if (_session.State.World.Player.MapId.Value == BillSourcePresentation.Cottage) _billTimeline.Advance(delta);
    }

    private Vector2 BillCameraFocus(Vector2 playerPosition) => _session.State.World.Player.MapId.Value == BillSourcePresentation.Cottage &&
        _session.State.Actors.TryGetValue(BillSourcePresentation.CameraActor, out var actor) && actor.Active && actor.Invisible &&
        _poses.TryGetValue(BillSourcePresentation.CameraActor, out var pose) ? pose.Position : playerPosition;

    private void DrawBillMapLayer(Texture2D original, Vector2 camera, int layer, Rid? target = null)
    {
        original=SixIslandPaletteTexture(original,true);
        if (layer is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(layer));
        if (DrawSilphElevatorLayer(original,camera,layer, target)) return;
        if (DrawDepartmentElevatorLayer(original,camera,layer, target)) return;
        if (DrawRocketElevatorLayer(original,camera,layer, target)) return;
        if (_session.State.World.Player.MapId.Value != BillSourcePresentation.Cottage || _billTimeline.Cells.Count == 0)
        { DrawMapTexture(original, -camera, target); return; }
        // Replace all three cell layers, including transparent pixels. Alpha-overpainting
        // the original map would leave its old light/door pixels above the new frame.
        int left = Math.Max(0, (int)Math.Floor(camera.X / 16)), top = Math.Max(0, (int)Math.Floor(camera.Y / 16));
        int right = Math.Min(original.GetWidth() / 16 - 1, (int)Math.Floor((camera.X + 239) / 16));
        int bottom = Math.Min(original.GetHeight() / 16 - 1, (int)Math.Floor((camera.Y + 159) / 16));
        for (int y = top; y <= bottom; y++) for (int x = left; x <= right; x++)
        {
            var destination = new Rect2(new Vector2(x * 16, y * 16) - camera, new(16, 16));
            if (!_billTimeline.Cells.TryGetValue((x, y), out var cell))
                DrawMapTextureRegion(original, destination, new(x * 16, y * 16, 16, 16), target);
            else if (cell.Kind != "door")
                DrawMapTextureRegion(_billAnimationLayers[layer]!, destination, _billMetatileRegions[cell.Kind], target);
            // Source DrawDoorMetatileAt uses COVERED: door pixels on bottom,
            // transparent middle/top. Door atlas rows are three vertical 16x32 frames.
            else if (layer == 0)
                DrawMapTextureRegion(_billDoor!, destination, new(0, cell.Value * 32 + cell.DoorPart * 16, 16, 16), target);
        }
    }

    private bool BillPcMenuActive => _session.State.World.Player.MapId.Value == BillSourcePresentation.Cottage &&
        _session.State.Script.Wait?.Kind == StoryWaitKind.MenuChoice && !_session.State.Script.Stack.IsEmpty &&
        _session.State.Script.Stack[^1].ProgramId == BillPcProgram;
    private bool BillPcMenuVisible => BillPcMenuActive && FieldTextComplete &&
        _session.State.Dialogue?.Page + 1 == _session.State.Dialogue?.Pages.Length;
    private bool BillPcPictureActive => _session.State.World.Player.MapId.Value == BillSourcePresentation.Cottage &&
        _session.State.Script.Wait?.Kind == StoryWaitKind.ButtonPress && _session.State.MonPicture is { } picture &&
        _billPictures.ContainsKey(picture.SourceSpeciesId);
    private int BillPcWidth => ((int)BillPcRows.Max(row => _sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText(row), fontSize: _sourceNormal.Size).X) + 9) / 8 * 8 + 8;
    private Rect2 BillPcRowsRect => new(8, 8, BillPcWidth, 5 * 14);

    private bool TryBillPcInput(InputEventKey key)
    {
        if (!BillPcMenuActive && !BillPcPictureActive) return false;
        if (key.Keycode is not (Key.Up or Key.Down or Key.Left or Key.Right or Key.Enter or Key.Backspace)) return false;
        _held = null;
        if (!key.Pressed || !FieldTextComplete || _tracks.Any(t => t.Time < t.Duration) || _waitElapsed < _waitDuration || _fanfareRemaining > 0) return true;
        if (BillPcPictureActive)
        {
            if (key.Keycode is Key.Enter or Key.Backspace) Apply(new("button_press", Text: key.Keycode == Key.Enter ? "A" : "B"));
            return true;
        }
        if (!BillPcMenuVisible) return true;
        if (key.Keycode == Key.Up) _billPcCursor = (_billPcCursor + 4) % 5;
        else if (key.Keycode == Key.Down) _billPcCursor = (_billPcCursor + 1) % 5;
        else if (key.Keycode == Key.Enter) Apply(new("ack", Choice: _billPcCursor));
        else if (key.Keycode == Key.Backspace) Apply(new("ack", Choice: 127));
        return true;
    }

    private bool NavigateBillPcPointer(Vector2 point, int direction)
    {
        if (!BillPcMenuVisible || !BillPcRowsRect.HasPoint(point)) return false;
        return PointerRows(point, direction, ref _billPcCursor, 5, 8, 8, BillPcWidth, 14);
    }

    private bool MouseBillPc(Vector2 point)
    {
        if (BillPcMenuVisible) return MouseSelect(point, ref _billPcCursor, 5, 8, 8, BillPcWidth, 14);
        if (BillPcPictureActive && _session.State.MonPicture is { } picture &&
            (new Rect2(picture.X * 8, picture.Y * 8, 80, 80).HasPoint(point) || MouseMessage.HasPoint(point))) return MouseKey();
        return false;
    }

    private void DrawBillPcMenu()
    {
        if (!BillPcMenuVisible) return;
        // ScriptMenu_Multichoice(0,0): interior begins at (8,8), five rows
        // use 14px spacing, and GetMCWindowHeight(5) is nine source tiles.
        _battleSkin.DrawUserFrame(this, new(0, 0, BillPcWidth + 16, 88));
        for (int row = 0; row < BillPcRows.Length; row++) SourceText(_sourceNormal, new(16, 10 + row * 14), BillPcRows[row]);
        SourceText(_sourceNormal, new(8, 10 + _billPcCursor * 14), "▶");
    }

    private bool DrawBillPcPicture()
    {
        if (_session.State.World.Player.MapId.Value != BillSourcePresentation.Cottage ||
            _session.State.MonPicture is not { } picture || !_billPictures.TryGetValue(picture.SourceSpeciesId, out var texture)) return false;
        var position = new Vector2(picture.X * 8, picture.Y * 8);
        _battleSkin.DrawUserFrame(this, new(position, new(80, 80)));
        DrawTexture(texture, position + new Vector2(8, 8));
        return true;
    }
}
