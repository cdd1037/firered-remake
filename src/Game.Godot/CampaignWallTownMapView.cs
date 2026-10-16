using Game.Content;
using Godot;

public partial class CampaignView
{
    private bool WallTownMapActive => _session.State.TownMap?.Origin == "wall";

    private TownMapPoint InitializeWallTownMap()
    {
        LoadFirstSeviiTownMap();
        _flyMap ??= _catalog.FlyMapPresentation;
        var p = _session.State.World.Player;
        string section = _catalog.MapDefinition(p.MapId).RegionMapSection;
        _townMapPage = _townMapPlayerPage = _flyMap.RegionForSection(section)
            ?? throw new InvalidDataException("wall_town_map_region_missing");
        _townMapSwitchMenu = false;
        // Indoor one-cell sections have an exact source position without an escape
        // warp. Do not invent a marker for multi-cell indoor sections.
        var cells = _flyMap.Region(_townMapPage).Cells.Where(c => c.SectionId == section).ToArray();
        _townMapPlayerKnown = cells.Length == 1;
        var point = _townMapPlayerKnown ? new TownMapPoint(cells[0].X, cells[0].Y) :
            new(_catalog.TownMap!.Layout.Cancel.X, _catalog.TownMap.Layout.Cancel.Y);
        // WALL and FLY share the source border/background, but only FLY draws
        // destination icons and accepts a destination selection.
        SetMapBackground("wall-current", false);
        SetMapBackground("dark-wall-current", true);
        return point;
    }
}
