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
        byte[] pixels = _flyMap.ReadBackgroundRgba(_townMapPage, CurrentMapFlags, _catalog.TownMap!, _firstSeviiTownMap!);
        SetBackground("wall-current", pixels);
        for (int i = 0; i < pixels.Length; i += 4)
            for (int c = 0; c < 3; c++)
            {
                int five = (pixels[i + c] * 31 + 127) / 255;
                five += (-five * _catalog.TownMap!.Layout.NameWindowDarkeningCoefficient) >> 4;
                pixels[i + c] = (byte)(five * 255 / 31);
            }
        SetBackground("dark-wall-current", pixels);
        return point;

        void SetBackground(string id, byte[] rgba)
        {
            if (_townMapTextures.Remove(id, out var previous)) previous.Dispose();
            using var image = Image.CreateFromData(240, 160, false, Image.Format.Rgba8, rgba);
            _townMapTextures.Add(id, ImageTexture.CreateFromImage(image));
        }
    }
}
