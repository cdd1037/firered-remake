using System.Text.Json;
using Game.Content;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private sealed record MapTilePixels(Texture2D Texture, Rect2I Region);
    private MapMetatileArtResolver<MapTilePixels>? _mapTileArt;

    // Only composition knows which imported packages supply a map's art. Drawing has
    // no region dispatch and never consults the live session to render historical edits.
    private MapMetatileArtResolver<MapTilePixels> CreateMapTileArt()
    {
        var maps = new Dictionary<(string Map, int Tile, int Layer), MapTilePixels>();
        var layouts = new Dictionary<(string Layout, int Tile, int Layer), MapTilePixels>();
        void Bind(string map, int tile, int layer, Texture2D texture, Rect2I? region = null)
            => maps.Add((map, tile, layer), new(texture, region ?? new(0, 0, 16, 16)));
        void BindDefinitions(string map, IEnumerable<PretMartMetatileDefinition> definitions)
        {
            foreach (var tile in definitions)
            {
                string[] paths = [tile.BottomTexturePath, tile.MiddleTexturePath, tile.TopTexturePath];
                for (int layer = 0; layer < 3; layer++) Bind(map, tile.RawMetatileId, layer, SceneTextures.Texture(paths[layer]));
            }
        }
        BindDefinitions(PretMartMetatiles.MapId, _catalog.MartMetatiles!.Definitions.Values);
        BindDefinitions("map.vermilion-city-gym", _catalog.VermilionGymRegion!.BeamDefinitions.Values);
        foreach (var row in CelioManifest.GetProperty("tiles").EnumerateArray())
            for (int layer = 0; layer < 3; layer++)
            {
                int id = row.GetProperty("metatileId").GetInt32();
                Bind(row.GetProperty("map").GetString()!, id, layer, _celioMachineLayers[(id, layer)]);
            }
        foreach (var row in FiveIslandManifest.GetProperty("tiles").EnumerateArray())
            for (int layer = 0; layer < 3; layer++)
            {
                int id = row.GetProperty("metatileId").GetInt32();
                Bind(row.GetProperty("map").GetString()!, id, layer, _fiveIslandDoorLayers[(id, layer)]);
            }
        foreach (var row in SeviiRubyManifest.GetProperty("tiles").EnumerateArray())
            for (int layer = 0; layer < 3; layer++)
            {
                string map = row.GetProperty("map").GetString()!;
                Bind(map, row.GetProperty("metatileId").GetInt32(), layer, _seviiRubyTileLayers[(map, layer)]);
            }
        foreach (var (key, texture) in _fourIslandIceLayers)
            Bind(FourIslandFieldSource.FirstFloor.Value, key.Tile, key.Layer, texture);
        foreach (var (key, texture) in _sixIslandDoorLayers)
            Bind("map.six-island-ruin-valley", key.Tile, key.Layer, texture);
        foreach (var map in _catalog.PokemonLeagueRegion!.NewMaps.Keys)
            foreach (var row in LeagueManifest.GetProperty("tiles").EnumerateArray())
                for (int layer = 0; layer < 3; layer++)
                    Bind(map.Value, row.GetProperty("metatileId").GetInt32(), layer, _leagueTextures["doors-" + layer],
                        new(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32(), 16, 16));
        foreach (var map in _catalog.VictoryRoadRegion!.SourceMaps.Keys.Where(IsVictoryRoadCave))
            foreach (var (id, tile) in _victoryRoadTiles)
                for (int layer = 0; layer < 3; layer++) Bind(map.Value, id, layer, _victoryRoadTileLayers[layer], new(tile.X, tile.Y, 16, 16));
        foreach (var map in _catalog.CinnabarRegion!.NewMaps.Keys)
        {
            string? family = map.Value.StartsWith("map.pokemon-mansion-", StringComparison.Ordinal) ? "mansion" : map.Value == "map.cinnabar-island-gym" ? "gym" : null;
            if (family is null) continue;
            foreach (var (key, tile) in _cinnabarTiles.Where(t => t.Key.Family == family))
                for (int layer = 0; layer < 3; layer++) Bind(map.Value, key.Id, layer, _cinnabarTileLayers[(family, layer)], new(tile.X, tile.Y, 16, 16));
        }
        foreach (var map in _catalog.SilphRegion!.NewMaps.Keys)
            foreach (var (id, tile) in _silphDoorTiles)
                for (int layer = 0; layer < 3; layer++) Bind(map.Value, id, layer, _silphDoorLayers[layer], new(tile.X, tile.Y, tile.Width, tile.Height));
        foreach (var map in _catalog.RocketHideoutRegion!.NewMaps.Keys)
        {
            string tileset = map.Value == "map.celadon-city-game-corner" ? "game_corner" : "silph_co";
            foreach (var (key, tile) in _rocketTiles.Where(t => t.Key.Tileset == tileset))
                for (int layer = 0; layer < 3; layer++) Bind(map.Value, key.Id, layer, _rocketTileLayers[tileset][layer], tile.Region);
        }
        var cut = JsonSerializer.Deserialize<CutArtBindings>(File.ReadAllBytes(Path.Combine(_root, "content/generated/maps/cut-grass-art.json")),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("cut_grass_art_missing");
        var grass = SceneTextures.NativeTexture(Path.Combine(_root, "content/generated/maps/cut-grass.png"));
        foreach (var (layout, group) in cut.Layouts)
            foreach (var (id, row) in cut.Groups[group])
                for (int layer = 0; layer < 3; layer++) layouts.Add((layout, id, layer), new(grass, new(layer * 16, row * 16, 16, 16)));
        return new(maps, layouts);
    }

    private Texture2D PatchMapLayer(CampaignResourceCache.Owner owner, PretWorldMap source,
        Texture2D original, int layer, IEnumerable<FieldMetatileEdit> edits)
    {
        var art = _mapTileArt ?? throw new InvalidOperationException("Map metatile art is not composed.");
        string layout = source.Events.GetProperty("layout").GetString()!;
        using var canvas = (Image)original.GetImage().Duplicate();
        foreach (var edit in edits)
        {
            if (source.GetTile(edit.X, edit.Y) is null) throw new InvalidDataException("map_metatile_edit_outside_layout");
            var pixels = art.Resolve(source.Id.Value, layout, edit.SourceMetatileId, layer, () =>
            {
                var cell = source.Cells.FirstOrDefault(c => c.MetatileId == edit.SourceMetatileId);
                return cell is null ? null : new(original, new(cell.X * 16, cell.Y * 16, 16, 16));
            });
            // BlitRect replaces alpha as well as RGB; clear layers must erase old pixels.
            using var image = pixels.Texture.GetImage();
            canvas.BlitRect(image, pixels.Region, new(edit.X * 16, edit.Y * 16));
        }
        return owner.FromImage(canvas);
    }
}
