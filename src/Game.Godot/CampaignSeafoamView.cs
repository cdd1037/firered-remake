using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.WildBattle;
using Game.Domain.World;
using Godot;

/// <summary>Seafoam source pixels extend the existing battle, storage and dex consumers.</summary>
public partial class CampaignView
{
    private SeafoamPresentationData? _seafoamArt;
    private void LoadSeafoamPresentation()
    {
        var art = PretSeafoamPresentation.Load(_root);
        _earlyItemIcons.Add(Game.Domain.Items.ItemIds.WaterStoneId, ItemTexture("data/items", ItemIconAssets.LoadProject(_root, "content/generated/data/items/seafoam.json", Game.Domain.Items.ItemIds.WaterStoneId)[0]));
        Texture2D Pixels(JsonElement row) => PresentationTexture(art.Files, row);
        foreach (var row in art.Manifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            string id = row.GetProperty("speciesId").GetString()!, side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            if (_catalog.PokemonContent?.Catalog.Species.GetValueOrDefault(new(id))?.SourceSpeciesId != species)
                throw new InvalidDataException("Seafoam art species is not admitted by the owned catalog.");
            var texture = Pixels(row);
            _battleArt.Add((species, side, shiny), new(texture, new(row.GetProperty("x").GetInt32(), row.GetProperty("y").GetInt32())));
            if (side == "front" && !shiny) _portraits.Add(species, texture);
        }
        foreach (var row in art.Manifest.GetProperty("pokemonIcons").EnumerateArray())
            _storageIcons.Add(new(row.GetProperty("speciesId").GetString()!), Pixels(row));
        foreach (var row in art.Manifest.GetProperty("footprints").EnumerateArray())
            _route22Footprints.Add(row.GetProperty("sourceSpeciesId").GetInt32(), Pixels(row));
        foreach (var row in art.Manifest.GetProperty("trainerPortraits").EnumerateArray())
        {
            if (row.GetProperty("id").GetString() != "bird-keeper-front" || row.GetProperty("width").GetInt32() != 64 ||
                row.GetProperty("height").GetInt32() != 64 || row.GetProperty("x").GetInt32() != 144 || row.GetProperty("y").GetInt32() != 8)
                throw new InvalidDataException("Seafoam Bird Keeper portrait binding differs.");
            _forestTrainerPortraits.Add("bird-keeper-front", (ImageTexture)Pixels(row));
        }
        foreach (var entry in art.DexEntries.Values)
            if (_pokedexMenu is null || !_pokedexMenu.HasSameEntryRules(entry))
                throw new InvalidDataException("Seafoam dex metadata differs from the authenticated source menu.");
        _seafoamArt = art;
        _supplementalDex.AddEntries(art.DexEntries.Values, art.Files);

    }
    private void VerifySeafoamMoveGeometry()
    {
        if (_seafoamArt is not { } art) return;
        foreach (var row in art.Manifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key = (row.GetProperty("species").GetInt32(), row.GetProperty("side").GetString()!);
            if (!_sourceBattlerGeometry.TryGetValue(key, out var actual) || actual.YOffset != row.GetProperty("yOffset").GetInt32() ||
                actual.Elevation != row.GetProperty("elevation").GetInt32() || actual.SourceWidth != row.GetProperty("sourceWidth").GetInt32() ||
                actual.SourceHeight != row.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException("Seafoam inherited move geometry differs.");
        }
    }
    private void DisposeSeafoamPresentation()
    {
        _seafoamArt = null; _seafoamMovementId = null; _seafoamMovementFrames = 0;
    }

    private bool IsSeafoamPresentationMap(MapId map) => _catalog.SeafoamRegion.NewMaps.ContainsKey(map);
    private string SeafoamPresentationMapKey(MapId map) => map == _session.State.World.Player.MapId && _session.State.MapLayout is {} layout
        ? map.Value + ":" + layout : map.Value;
    private PretWorldMap SeafoamPresentationSourceMap(MapId map) => map == _session.State.World.Player.MapId && _session.State.MapLayout is {} layout
        ? (PretWorldMap)(_catalog.MapLayout(map, layout) ?? throw new InvalidDataException("Map layout art missing.")) : _catalog.SourceMaps[map];
    private Texture2D? SeafoamBattleTerrain(MapId map)
    {
        if (!IsSeafoamPresentationMap(map)) return null;
        return PresentedWildBattle?.State.Terrain switch
        {
            WildBattleTerrain.Cave => _mtMoonTerrain ?? throw new InvalidDataException("Seafoam cave terrain missing."),
            WildBattleTerrain.Pond => _cityBattleScenes["pond"], WildBattleTerrain.Water => _cityBattleScenes["water"],
            WildBattleTerrain.Grass => _wildTerrain, WildBattleTerrain.Plain => _cityBattleScenes["plain"],
            WildBattleTerrain.Sand => _cityBattleScenes["sand"], WildBattleTerrain.Building => _cityBattleScenes["building"],
            null when SeafoamFieldRules.IsCave(map) => _mtMoonTerrain ?? throw new InvalidDataException("Seafoam cave terrain missing."),
            _ => null
        };
    }
}
