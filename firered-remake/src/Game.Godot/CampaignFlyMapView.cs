using Game.Application;
using Game.Content;
using Game.Domain.Items;
using Godot;

public partial class CampaignView
{
    private PretFlyMap? _flyMap;
    private bool _townMapWasFly;
    private bool FlyMapActive => _session.State.TownMap?.Origin == "fly";
    private HashSet<string> CurrentMapFlags => _session.State.Script.Flags.Where(p => p.Value).Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

    private void LoadFieldFlyIcon()
    {
        if (_catalog.FieldFly is null) return;
        if (!_earlyItemIcons.TryGetValue(PretFieldFly.ReusedHm02Icon, out var icon))
            throw new InvalidDataException("field_fly_shared_icon_missing");
        _earlyItemIcons.Add(FieldFlyMachineCatalog.Hm02Id, icon);
    }

    private TownMapPoint InitializeFlyMap()
    {
        LoadFirstSeviiTownMap();
        _flyMap ??= _catalog.FlyMapPresentation;
        foreach (string id in new[] { "fly-icon-0", "fly-icon-1" })
        {
            if (_townMapTextures.ContainsKey(id)) continue;
            var asset = _flyMap.Asset(id);
            using var image = Image.CreateFromData(asset.Width, asset.Height, false, Image.Format.Rgba8, _flyMap.ReadAssetRgba(id));
            _townMapTextures.Add(id, ImageTexture.CreateFromImage(image));
        }
        var p = _session.State.World.Player;
        var definition = _catalog.MapDefinition(p.MapId);
        var sourceMap = _catalog.Maps[p.MapId];
        _townMapPage = _townMapPlayerPage = _flyMap.RegionForSection(definition.RegionMapSection)
            ?? throw new InvalidDataException("field_fly_map_region_missing");
        _townMapSwitchMenu = false;
        _townMapPlayerKnown = _flyMap.TryPositionForOutdoorMap(definition.RegionMapSection, definition.SourceMapSymbol,
            definition.MapType, sourceMap.Width, sourceMap.Height, p.X, p.Y, out var point);
        if (!_townMapPlayerKnown) throw new InvalidDataException("field_fly_map_position_missing");
        var pixels = _flyMap.ReadBackgroundRgba(_townMapPage, CurrentMapFlags, _catalog.TownMap!, _firstSeviiTownMap!);
        SetBackground("fly-current", pixels);
        for (int i = 0; i < pixels.Length; i += 4)
            for (int c = 0; c < 3; c++)
            {
                int five = (pixels[i + c] * 31 + 127) / 255;
                five += (-five * _catalog.TownMap!.Layout.NameWindowDarkeningCoefficient) >> 4;
                pixels[i + c] = (byte)(five * 255 / 31);
            }
        SetBackground("dark-fly-current", pixels);
        return point;

        void SetBackground(string id, byte[] rgba)
        {
            if (_townMapTextures.Remove(id, out var previous)) previous.Dispose();
            using var image = Image.CreateFromData(240, 160, false, Image.Format.Rgba8, rgba);
            _townMapTextures.Add(id, ImageTexture.CreateFromImage(image));
        }
    }

    private bool TrySelectFlyDestination()
    {
        if (!FlyMapActive) return false;
        string section = CurrentTownMapCell.SectionId;
        if (_flyMap!.IsVisitedDestination(_townMapPage, section, CurrentMapFlags))
        {
            SourceSound("SE_SELECT");
            Apply(new("field_fly_choose", Text: section));
        }
        return true;
    }

    private void DrawFlyDestinations()
    {
        if (!FlyMapActive) return;
        var texture = _townMapTextures[_flyMap!.FlyIconAssetAt((long)(_townMapElapsed * 60))];
        foreach (var icon in _flyMap.VisibleDestinationIcons(_townMapPage, CurrentMapFlags))
            DrawTexture(texture, new(icon.CenterX - 4, icon.CenterY - 4));
    }

    private void DrawFlyMapAction()
    {
        if (!FlyMapActive || !_flyMap!.IsVisitedDestination(_townMapPage, CurrentTownMapCell.SectionId, CurrentMapFlags)) return;
        var layout = _catalog.TownMap!.Layout;
        DrawTexture(_townMapTextures["keypad-a"], new(layout.TopBarRight.X, layout.TopBarRight.Y));
        SourceText(_townMapSmallFont!, new(layout.TopBarRight.X + 8, layout.TopBarRight.Y), "OK");
    }

    private void UseFieldPartyFly()
    {
        long revision = _session.Revision;
        Apply(new("field_fly", PartyIndex: _fieldPartyCursor));
        if (_session.Revision > revision) { ResetFieldMenu(); return; }
        _fieldMenuMode = "party";
        _fieldMenuNotice = _status == "field_fly_badge_required"
            ? "This can't be used until a new\nBADGE is obtained." : "Can't use that here.";
    }
}
