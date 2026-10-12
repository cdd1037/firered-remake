using Game.Content;
using Game.Domain.Items;
using Godot;

/// <summary>Bounded Kanto and unlocked first-Sevii atlas browsing. It never moves the field player or grants Fly.</summary>
public partial class CampaignView
{
    private readonly Dictionary<string, ImageTexture> _townMapTextures = new(StringComparer.Ordinal);
    private readonly Dictionary<int, SourceGameFont> _townMapFonts = [];
    private SourceGameFont? _townMapSmallFont;
    private bool _townMapWasActive, _townMapMoving;
    private Vector2I _townMapCursor, _townMapPlayer, _townMapMoveFrom, _townMapMoveTo;
    private Vector2I? _townMapHeld;
    private double _townMapElapsed, _townMapMoveElapsed;
    private int _townMapSnap;
    private string? _townMapNotice;
    private ulong _townMapClosedFrame = ulong.MaxValue;
    private bool TownMapActive => _session?.State.TownMap is not null;

    private void ExtendTownMapQuestPocket(ref QuestItemUiDocument document)
    {
        if (_catalog.TownMap is not { } map) return;
        var asset = map.Asset("item-town-map");
        string firstIcon = document.Items[0].Icon;
        var placement = document.Assets.Single(a => a.Id == firstIcon);
        using var image = Image.CreateFromData(asset.Width, asset.Height, false, Image.Format.Rgba8, map.ReadAssetRgba(asset.Id));
        _questPocketTextures.Add(asset.Id, ImageTexture.CreateFromImage(image));
        document = document with
        {
            Assets = document.Assets.Add(new(asset.Id, asset.TexturePath, asset.Width, asset.Height,
                placement.X, placement.Y, asset.Sha256, asset.RgbaSha256)),
            Items = document.Items.Add(new(map.Item.Id.Value, map.Item.SourceItemSymbol, map.Item.Name,
                map.ItemDescription, asset.Id, map.Item.FieldUseFunction))
        };
    }

    private void LoadTownMapPresentation()
    {
        if (_townMapTextures.Count > 0 || _catalog.TownMap is not { } map) return;
        foreach (var asset in map.Assets.Where(a => a.Id != "item-town-map"))
        {
            byte[] pixels = map.ReadAssetRgba(asset.Id);
            using var image = Image.CreateFromData(asset.Width, asset.Height, false, Image.Format.Rgba8, pixels);
            _townMapTextures.Add(asset.Id, ImageTexture.CreateFromImage(image));
        }
        // Original GPU darkening is local to nonempty name windows. Precompute
        // those pixels without changing the authenticated base texture.
        byte[] dark = map.ReadAssetRgba("kanto");
        for (int i = 0; i < dark.Length; i += 4)
            for (int channel = 0; channel < 3; channel++)
            {
                int five = (dark[i + channel] * 31 + 127) / 255;
                five += (-five * map.Layout.NameWindowDarkeningCoefficient) >> 4;
                dark[i + channel] = (byte)(five * 255 / 31);
            }
        using (var image = Image.CreateFromData(240, 160, false, Image.Format.Rgba8, dark))
            _townMapTextures.Add("dark-name-background", ImageTexture.CreateFromImage(image));
        Color Palette(int index)
        {
            var rgb = map.Layout.TextPaletteRgb[index];
            return Color.Color8((byte)rgb[0], (byte)rgb[1], (byte)rgb[2]);
        }
        string fonts = System.IO.Path.Combine(_root, "content/generated/source-font");
        foreach (int index in new[] { map.Layout.MapTextColor, map.Layout.DungeonVisitedTextColor, map.Layout.DungeonUnvisitedTextColor }.Distinct())
            _townMapFonts.Add(index, SourceGameFont.Load(fonts, foreground: Palette(index), shadow: Palette(map.Layout.TextShadowColor)));
        _townMapSmallFont = SourceGameFont.Load(fonts, "small", Palette(map.Layout.MapTextColor), Palette(map.Layout.TextShadowColor));
    }

    private void DisposeTownMapPresentation()
    {
        foreach (var texture in _townMapTextures.Values) texture.Dispose();
        foreach (var font in _townMapFonts.Values) font.Dispose();
        _townMapTextures.Clear(); _townMapFonts.Clear(); _firstSeviiTownMap = null; _townMapSmallFont?.Dispose(); _townMapSmallFont = null;
    }

    private void SynchronizeTownMapPresentation()
    {
        bool active = TownMapActive;
        if (active && !_townMapWasActive)
        {
            LoadTownMapPresentation(); var p = _session.State.World.Player;
            var projected = InitializeFirstSeviiTownMap(p.MapId.Value, p.X, p.Y);
            _townMapPlayer = _townMapCursor = new(projected.X, projected.Y);
            _townMapElapsed = 0; _townMapSnap = 0; _townMapNotice = null;
            _townMapMoving = false; _townMapHeld = null; _held = null; _repeat = 0;
        }
        if (!active && _townMapWasActive)
        {
            _townMapHeld = null; _townMapMoving = false; _townMapNotice = null; _townMapSwitchMenu = false;
            _questPocket = 1; _questPocketPopup = false;
            _questPocketCursor = Math.Max(0, Enumerable.Range(0, QuestPocketStacks.Length)
                .FirstOrDefault(i => QuestPocketStacks[i].Item == QuestItemRules.TownMapId, 0));
        }
        _townMapWasActive = active;
    }

    private void BeginTownMapMove(Vector2I delta)
    {
        var layout = _catalog.TownMap!.Layout;
        var target = new Vector2I(Math.Clamp(_townMapCursor.X + delta.X, 0, layout.Columns - 1),
            Math.Clamp(_townMapCursor.Y + delta.Y, 0, layout.Rows - 1));
        if (target == _townMapCursor) return;
        _townMapMoveFrom = _townMapCursor; _townMapMoveTo = target;
        _townMapMoveElapsed = 0; _townMapMoving = true; _townMapSnap = 0;
    }

    private bool ProcessTownMapPresentation(double delta)
    {
        if (!TownMapActive) return false;
        _townMapElapsed += delta;
        if (_townMapMoving)
        {
            _townMapMoveElapsed += delta;
            if (_townMapMoveElapsed >= _catalog.TownMap!.Layout.CursorMoveTicks / 60.0)
            { _townMapCursor = _townMapMoveTo; _townMapMoving = false; PlayTownMapCancelSelection(); }
        }
        else if (_townMapHeld is { } direction && _townMapNotice is null && !_townMapSwitchMenu) BeginTownMapMove(direction);
        return true;
    }

    private void CloseTownMap()
    {
        _townMapClosedFrame = Engine.GetProcessFrames(); _townMapHeld = null;
        Apply(new("town_map_close"));
    }

    private bool TryTownMapInput(InputEventKey key)
    {
        if (!TownMapActive) return false;
        if (key.Keycode is Key.Escape or Key.F5 or Key.F9 or Key.F12) return false;
        _held = null; _repeat = 0;
        Vector2I? direction = key.Keycode switch
        { Key.Up => Vector2I.Up, Key.Down => Vector2I.Down, Key.Left => Vector2I.Left, Key.Right => Vector2I.Right, _ => null };
        if (!key.Pressed) { if (direction == _townMapHeld) _townMapHeld = null; return true; }
        if (_townMapNotice is not null)
        { if (key.Keycode is Key.Enter or Key.Backspace) _townMapNotice = null; return true; }
        if (TryTownMapSwitchInput(key)) return true;
        if (direction is { } move) { _townMapHeld = move; if (!_townMapMoving) BeginTownMapMove(move); return true; }
        if (_townMapMoving) return true;
        if (key.Keycode == Key.Backspace) { CloseTownMap(); return true; }
        if (key.Keycode == Key.Tab)
        {
            SnapTownMapPage();
            PlayTownMapCancelSelection();
        }
        if (key.Keycode == Key.Enter)
        {
            var map = _catalog.TownMap!;
            if (TownMapCursorOnSwitch) { OpenTownMapSwitchMenu(); return true; }
            if (_townMapCursor == new Vector2I(map.Layout.Cancel.X, map.Layout.Cancel.Y))
            { SourceSound("SE_M_HYPER_BEAM2"); CloseTownMap(); }
            else if (CurrentTownMapIcons.Any(i => i.SectionId == CurrentTownMapCell.DungeonSectionId &&
                _session.State.Script.Flags.GetValueOrDefault(i.VisitedFlag)))
                _townMapNotice = "Detailed guide pages are not\navailable yet.";
        }
        return true;
    }

    private bool MouseTownMap(Vector2 point)
    {
        if (_townMapNotice is not null) return MouseConfirm(point, MouseMessage);
        if (_townMapSwitchMenu) return MouseTownMapSwitch(point);
        if (_townMapMoving) return false;
        var map = _catalog.TownMap!; var center = map.CursorCenter(map.Layout.Cancel.X, map.Layout.Cancel.Y);
        var switchCenter = map.CursorCenter(map.Layout.SwitchButton.X, map.Layout.SwitchButton.Y);
        if (TownMapSwitchAvailable && new Rect2(switchCenter.X - 8, switchCenter.Y - 8, 16, 16).HasPoint(point))
        { _townMapCursor = new(map.Layout.SwitchButton.X, map.Layout.SwitchButton.Y); return MouseKey(); }
        if (new Rect2(center.X - 8, center.Y - 8, 16, 16).HasPoint(point))
        { _townMapCursor = new(map.Layout.Cancel.X, map.Layout.Cancel.Y); return MouseKey(); }
        if (!new Rect2(32, 32, map.Layout.Columns * 8, map.Layout.Rows * 8).HasPoint(point)) return false;
        _menuMouseActionFrame = Engine.GetProcessFrames(); _townMapHeld = null; _townMapSnap = 0;
        _townMapCursor = new((int)(point.X - 32) / 8, (int)(point.Y - 32) / 8); PlayTownMapCancelSelection(); return true;
    }

    private void PlayTownMapCancelSelection()
    {
        var cancel=_catalog.TownMap!.Layout.Cancel;
        if(TownMapCursorOnSwitch || _townMapCursor==new Vector2I(cancel.X,cancel.Y))SourceSound("SE_M_SPIT_UP");
    }

    private bool DrawTownMap()
    {
        if (!TownMapActive) return false;
        var map = _catalog.TownMap!; var layout = map.Layout;
        DrawTexture(_townMapTextures[TownMapBackground], Vector2.Zero);
        var flags = _session.State.Script.Flags.Where(f => f.Value).Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var icon in CurrentTownMapIcons.Where(i => i.VisibleFlag is null || flags.Contains(i.VisibleFlag)))
            DrawTexture(_townMapTextures[flags.Contains(icon.VisitedFlag) ? "dungeon-visited" : "dungeon-unvisited"], new(icon.CenterX - 4, icon.CenterY - 4));
        var player = map.CursorCenter(_townMapPlayer.X, _townMapPlayer.Y);
        if (_townMapPlayerKnown && _townMapPage == _townMapPlayerPage)
            DrawTexture(_townMapTextures[_session.State.PlayerGender == 1 ? "player-leaf" : "player-red"], new(player.X - 8, player.Y - 8));
        if (_townMapSwitchMenu) { DrawTownMapSwitchMenu(); return true; }
        var cursor = map.CursorCenter(_townMapCursor.X, _townMapCursor.Y); var cursorPosition = new Vector2(cursor.X, cursor.Y);
        if (_townMapMoving)
        {
            var from = map.CursorCenter(_townMapMoveFrom.X, _townMapMoveFrom.Y); var to = map.CursorCenter(_townMapMoveTo.X, _townMapMoveTo.Y);
            cursorPosition = new Vector2(from.X, from.Y).Lerp(new(to.X, to.Y), (float)Math.Clamp(_townMapMoveElapsed * 60 / layout.CursorMoveTicks, 0, 1)).Floor();
        }
        DrawTexture(_townMapTextures[(int)(_townMapElapsed * 60 / layout.CursorFrameTicks) % 2 == 0 ? "cursor-0" : "cursor-1"], cursorPosition - new Vector2(8, 8));
        var cell = CurrentTownMapCell;
        void NameWindow(TownMapRect rectangle, TownMapPoint position, string text, int color)
        {
            if (text.Length == 0) return;
            var rect = new Rect2(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
            DrawTextureRectRegion(_townMapTextures[TownMapDarkBackground], rect, rect);
            SourceText(_townMapFonts[color], new(position.X, position.Y), text);
        }
        NameWindow(layout.MapNameWindow, layout.MapName, cell.Name, layout.MapTextColor);
        bool visited = CurrentTownMapIcons.Any(i => i.SectionId == cell.DungeonSectionId && flags.Contains(i.VisitedFlag));
        NameWindow(layout.DungeonNameWindow, layout.DungeonName, cell.DungeonName,
            visited ? layout.DungeonVisitedTextColor : layout.DungeonUnvisitedTextColor);
        DrawTexture(_townMapTextures["keypad-dpad-any"], new(layout.TopBarLeft.X, layout.TopBarLeft.Y));
        SourceText(_townMapSmallFont!, new(layout.TopBarLeft.X + 8, layout.TopBarLeft.Y), "MOVE");
        if (_townMapCursor == new Vector2I(layout.Cancel.X, layout.Cancel.Y))
        {
            DrawTexture(_townMapTextures["keypad-a"], new(layout.TopBarRight.X, layout.TopBarRight.Y));
            SourceText(_townMapSmallFont!, new(layout.TopBarRight.X + 8, layout.TopBarRight.Y), "CANCEL");
        }
        if (TownMapCursorOnSwitch)
        {
            DrawTexture(_townMapTextures["keypad-a"], new(layout.TopBarRight.X, layout.TopBarRight.Y));
            SourceText(_townMapSmallFont!, new(layout.TopBarRight.X + 8, layout.TopBarRight.Y), "SWITCH");
        }
        if (_townMapNotice is not null) ItemMessageBox(_townMapNotice);
        return true;
    }
}
