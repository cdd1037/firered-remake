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
            using var image = NativeImages.LoadPng(_flyMap.ReadAssetPng(id), asset.Width, asset.Height);
            _townMapTextures.Add(id, SceneTextures.FromImage(image));
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
        SetMapBackground("fly-current", false);
        SetMapBackground("dark-fly-current", true);
        return point;
    }

    private void SetMapBackground(string id, bool dark)
    {
        var background = _flyMap!.Background(_townMapPage, CurrentMapFlags, dark);
        var r = background.Region;
        _townMapTextures[id] = SceneTextures.NativeTexture(
            System.IO.Path.Combine(_flyMap.GeneratedDirectory, background.TexturePath),
            new Rect2I(r.X, r.Y, r.Width, r.Height));
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
        bool accepted = Apply(new("field_fly", PartyIndex: _fieldPartyCursor));
        if (accepted) { ResetFieldMenu(); return; }
        _fieldMenuMode = "party";
        _fieldMenuNotice = _status == "field_fly_badge_required"
            ? "This can't be used until a new\nBADGE is obtained." : "Can't use that here.";
    }
}
