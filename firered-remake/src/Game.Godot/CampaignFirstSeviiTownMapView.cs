using Game.Content;
using Godot;

public partial class CampaignView
{
    private PretFirstSeviiTownMap? _firstSeviiTownMap;
    private int _townMapPage, _townMapPlayerPage, _townMapSwitchSelection;
    private bool _townMapSwitchMenu, _townMapPlayerKnown = true;
    private bool TownMapSwitchAvailable => !FlyMapActive && !WallTownMapActive && _firstSeviiTownMap is not null &&
        _session.State.Script.Flags.GetValueOrDefault(PretFirstSeviiTownMap.UnlockFlag);
    private string TownMapBackground => FlyMapActive ? "fly-current" : WallTownMapActive ? "wall-current" : _townMapPage == 1 ? (TownMapSwitchAvailable ? "sevii123" : "sevii123-locked") : TownMapSwitchAvailable ? "kanto-switch" : "kanto";
    private string TownMapDarkBackground => TownMapBackground == "kanto" ? "dark-name-background" : "dark-" + TownMapBackground;
    private TownMapCell CurrentTownMapCell => FlyMapActive || WallTownMapActive ? _flyMap!.CellAt(_townMapPage, _townMapCursor.X, _townMapCursor.Y, CurrentMapFlags) : _townMapPage == 1 ? _firstSeviiTownMap!.CellAt(_townMapCursor.X, _townMapCursor.Y) :
        _catalog.TownMap!.CellAt(_townMapCursor.X, _townMapCursor.Y);
    private IEnumerable<TownMapIcon> CurrentTownMapIcons => FlyMapActive || WallTownMapActive ? _flyMap!.VisibleDungeonIcons(_townMapPage, CurrentMapFlags) : _townMapPage == 1 ? _firstSeviiTownMap!.Icons : _catalog.TownMap!.Icons;

    private TownMapPoint InitializeFirstSeviiTownMap(string mapId, int x, int y)
    {
        _townMapSwitchMenu = false; _townMapPage = _townMapPlayerPage = 0; _townMapPlayerKnown = true;
        if (_catalog.FirstSeviiEnabled) LoadFirstSeviiTownMap();
        if (_firstSeviiTownMap is not null)
        {
            _townMapPage = _townMapPlayerPage = _firstSeviiTownMap.RegionForMap(mapId) ?? 0;
            if (_firstSeviiTownMap.TryPositionForMap(mapId, x, y, out var point)) return point;
            // A known indoor multi-cell section needs a general source escapeWarp, which
            // this campaign does not retain. Keep the correct page, omit the marker, and
            // park on an actual source button rather than fabricating a player position.
            if (_firstSeviiTownMap.RegionForMap(mapId) is not null)
            { _townMapPlayerKnown = false; return new(21, TownMapSwitchAvailable ? 11 : 13); }
        }
        try { return _catalog.TownMap!.PositionForMap(mapId, x, y); }
        catch (ArgumentException) when (_firstSeviiTownMap is not null)
        { _townMapPlayerKnown = false; return new(21, TownMapSwitchAvailable ? 11 : 13); }
    }

    private void LoadFirstSeviiTownMap()
    {
        if (_firstSeviiTownMap is not null) return;
        var extension = PretFirstSeviiTownMap.Load(_root);
        foreach (var a in extension.Assets)
        {
            using var image = NativeImages.LoadPng(extension.ReadAssetPng(a.Id), a.Width, a.Height);
            _townMapTextures.Add(a.Id, SceneTextures.FromImage(image));
            if (a.Id is not ("kanto-switch" or "sevii123" or "sevii123-locked" or "switch-menu")) continue;
            var pixels = NativeImages.ReadPixels(extension.ReadAssetPng(a.Id));
            for (int i = 0; i < pixels.Length; i += 4)
                for (int c = 0; c < 3; c++)
                {
                    int five = (pixels[i + c] * 31 + 127) / 255;
                    five += (-five * 6) >> 4; pixels[i + c] = (byte)(five * 255 / 31);
                }
            using var dark = Image.CreateFromData(a.Width, a.Height, false, Image.Format.Rgba8, pixels);
            _townMapTextures.Add("dark-" + a.Id, SceneTextures.FromImage(dark));
        }
        _firstSeviiTownMap = extension;
    }

    private void SnapTownMapPage()
    {
        var layout = _catalog.TownMap!.Layout;
        if (TownMapSwitchAvailable)
        {
            _townMapSnap = (_townMapSnap + 1) % 3;
            if (_townMapSnap == 0 && (_townMapPage != _townMapPlayerPage || !_townMapPlayerKnown)) _townMapSnap = 1;
            _townMapCursor = _townMapSnap switch
            { 0 => _townMapPlayer, 1 => new(layout.SwitchButton.X, layout.SwitchButton.Y), _ => new(layout.Cancel.X, layout.Cancel.Y) };
        }
        else
        {
            _townMapSnap = (_townMapSnap + 1) % 2;
            _townMapCursor = _townMapSnap == 0 && _townMapPlayerKnown ? _townMapPlayer : new(layout.Cancel.X, layout.Cancel.Y);
        }
    }

    private bool TownMapCursorOnSwitch => TownMapSwitchAvailable &&
        _townMapCursor == new Vector2I(_catalog.TownMap!.Layout.SwitchButton.X, _catalog.TownMap.Layout.SwitchButton.Y);

    private void OpenTownMapSwitchMenu()
    {
        SourceSound("SE_M_HYPER_BEAM2"); _townMapSwitchSelection = _townMapPage;
        _townMapSwitchMenu = true; _townMapHeld = null; _townMapMoving = false;
    }

    private bool TryTownMapSwitchInput(InputEventKey key)
    {
        if (!_townMapSwitchMenu) return false;
        _townMapHeld = null;
        if (!key.Pressed || key.Echo) return true;
        int previous = _townMapSwitchSelection;
        if (key.Keycode == Key.Up) _townMapSwitchSelection = Math.Max(0, previous - 1);
        if (key.Keycode == Key.Down) _townMapSwitchSelection = Math.Min(_firstSeviiTownMap!.SwitchMenu.MaxSelection, previous + 1);
        if (previous != _townMapSwitchSelection) SourceSound("SE_BAG_CURSOR");
        _townMapPage = _townMapSwitchSelection;
        if (key.Keycode == Key.Enter)
        { SourceSound("SE_M_SWIFT"); _townMapSwitchMenu = false; }
        else if (key.Keycode == Key.Backspace)
        {
            // InitSwitchMapMenu initializes chosenRegion to the player's region.
            _townMapPage = _townMapSwitchSelection = _townMapPlayerPage; _townMapSwitchMenu = false;
        }
        return true;
    }

    private Rect2 TownMapSwitchHighlight(int selection)
    {
        var s = _firstSeviiTownMap!.SwitchMenu;
        return new(s.HighlightLeft, 8 * (s.YOffset + 4 * selection), s.HighlightRight - s.HighlightLeft, s.HighlightHeight);
    }

    private bool MouseTownMapSwitch(Vector2 point)
    {
        for (int row = 0; row <= _firstSeviiTownMap!.SwitchMenu.MaxSelection; row++)
            if (TownMapSwitchHighlight(row).HasPoint(point))
            {
                if (row != _townMapSwitchSelection) SourceSound("SE_BAG_CURSOR");
                _townMapPage = _townMapSwitchSelection = row; return MouseKey();
            }
        return false;
    }

    private void DrawTownMapSwitchMenu()
    {
        var s = _firstSeviiTownMap!.SwitchMenu; var selected = TownMapSwitchHighlight(_townMapSwitchSelection);
        // Original pixel art and RGB555 menu-row darkening. The original GPU blend
        // also darkens selected background layers/OBJ outside this window; that
        // compositing and the fade transitions remain a separate fidelity gate.
        DrawTexture(_townMapTextures["dark-switch-menu"], Vector2.Zero);
        DrawTextureRectRegion(_townMapTextures["switch-menu"], selected, selected);
        int frame = (int)(_townMapElapsed * 60 / s.ArrowFrameTicks) % 2;
        DrawTexture(_townMapTextures["switch-left-" + frame], new(s.LeftArrowX - 16, selected.Position.Y));
        DrawTexture(_townMapTextures["switch-right-" + frame], new(s.RightArrowX - 16, selected.Position.Y));
        var l = _catalog.TownMap!.Layout;
        DrawTexture(_townMapTextures["keypad-updown"], new(l.TopBarLeft.X, l.TopBarLeft.Y));
        SourceText(_townMapSmallFont!, new(l.TopBarLeft.X + 8, l.TopBarLeft.Y), "PICK");
        DrawTexture(_townMapTextures["keypad-a"], new(l.TopBarRight.X, l.TopBarRight.Y));
        SourceText(_townMapSmallFont!, new(l.TopBarRight.X + 8, l.TopBarRight.Y), "OK");
    }
}
