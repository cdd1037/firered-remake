using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.World;
using Godot;

/// <summary>Original beast art and machine pixels. Authoritative commands own handoff and roaming state.</summary>
public partial class CampaignView
{
    private bool CelioPresentationProfile => _catalog.CelioEnabled && _catalog.FiveIslandEnabled && (_session.IsCurrentGame || _session.RulesVersion == CampaignSession.CelioRulesVersion);
    private CelioPresentationData? _celioArt;
    private readonly Dictionary<int,PokedexMenuEntry> _celioDex = [];
    private readonly Dictionary<(int Tile,int Layer),Texture2D> _celioMachineLayers = [];
    private JsonElement CelioManifest => _celioArt?.Manifest ?? throw new InvalidDataException("Celio presentation missing.");

    private void LoadCelioPresentation()
    {
        if (!_catalog.CelioEnabled) return;
        var pokemon = _catalog.PokemonContent?.Catalog ?? throw new InvalidDataException("Celio owned Pokemon catalog missing.");
        if (!CelioPresentationProfile)
            throw new InvalidDataException("Celio requires its complete Five Island predecessor and owned beast catalog.");
        _celioArt = CelioPresentationAssets.Load(_root);
        Texture2D Pixels(JsonElement row, bool replicatePalette = false)
        {
            int width = row.GetProperty("width").GetInt32(), height = row.GetProperty("height").GetInt32();
            using var image = NativeImages.LoadPng(_celioArt.Files[row.GetProperty("texturePath").GetString()!], width, height);
            if (replicatePalette)
            {
                var rgba = image.GetData();
                for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                    for (int c = 0; c < 3; c++) { int v = rgba[i + c] >> 3; rgba[i + c] = (byte)((v << 3) | (v >> 2)); }
                image.SetData(width, height, false, Image.Format.Rgba8, rgba);
            }
            return SceneTextures.FromImage(image);
        }
        foreach (var row in CelioManifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            if (pokemon.Species.GetValueOrDefault(new(row.GetProperty("speciesId").GetString()!))?.SourceSpeciesId != species)
                throw new InvalidDataException("Celio sprite is outside its owned catalog.");
            string side = row.GetProperty("side").GetString()!; bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            var texture = Pixels(row);
            _battleArt.Add((species,side,shiny),new(texture,new(row.GetProperty("x").GetInt32(),row.GetProperty("y").GetInt32())));
            if (side == "front" && !shiny)
            {
                _portraits.Add(species,texture);
                _celioDex.Add(species,(_pokedexMenu ?? throw new InvalidDataException("Celio dex metadata missing.")).Entries[species] with { Portrait = SeviiRubyDexTexture(row) });
            }
        }
        foreach (var row in CelioManifest.GetProperty("pokemonIcons").EnumerateArray())
            _storageIcons.Add(new(row.GetProperty("speciesId").GetString()!),Pixels(row,true));
        foreach (var row in CelioManifest.GetProperty("footprints").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32(); _route22Footprints.Add(species,Pixels(row));
            _celioDex[species] = _celioDex[species] with { Footprint = SeviiRubyDexTexture(row) };
        }
        foreach (var row in CelioManifest.GetProperty("layers").EnumerateArray())
            _celioMachineLayers.Add((row.GetProperty("metatileId").GetInt32(),row.GetProperty("layer").GetInt32()),Pixels(row));
        Write("celioPresentation",new {             nativeWindowVerified=false, physicalAudioVerified=false });
    }

    private CaptureDexEntry? CelioCaptureDexEntry(int species) => !_celioDex.TryGetValue(species,out var e) ? null :
        new(e.SourceSpeciesId,e.NationalNumber,e.Name,e.Category,e.DisplayCategory,e.HeightDecimeters,e.WeightHectograms,e.HeightDisplay,e.WeightDisplay,e.Description,
            e.Footprint!.TexturePath,e.Portrait!.TexturePath,e.HabitatTitle);
    private byte[]? ReadCelioDexTexture(PokedexMenuTexture texture) => _celioDex.Values.Any(e => e.Portrait == texture || e.Footprint == texture)
        ? _celioArt!.Files[texture.TexturePath] : null;
    private string? CelioSpeciesName(int species) => _celioDex.GetValueOrDefault(species)?.Name;

    private bool TryCelioMachinePatch(Image canvas, FieldMetatileEdit edit, int layer, MapId map)
    {
        if (!_catalog.CelioEnabled || map.Value != "map.one-island-pokemon-center-1f") return false;
        // The Ruby tile remains owned and authenticated by the predecessor presentation.
        var tile = CelioManifest.GetProperty("tiles").EnumerateArray().SingleOrDefault(t => t.GetProperty("metatileId").GetInt32() == edit.SourceMetatileId);
        if (tile.ValueKind == JsonValueKind.Undefined) return false;
        // The script operand is boolean; committed map edits use the two-bit collision mask.
        if (edit.Collision != (tile.GetProperty("collision").GetInt32() == 0 ? 0 : 3) ||
            !tile.GetProperty("positions").EnumerateArray().Any(p => p.GetProperty("x").GetInt32() == edit.X && p.GetProperty("y").GetInt32() == edit.Y) ||
            !_celioMachineLayers.TryGetValue((edit.SourceMetatileId,layer),out var texture))
            throw new InvalidDataException("Celio machine patch is outside its authenticated source.");
        using var image = texture.GetImage(); canvas.BlitRect(image,new(0,0,16,16),new(edit.X*16,edit.Y*16)); return true;
    }

    private void VerifyCelioMoveGeometry()
    {
        if (!_catalog.CelioEnabled) return;
        foreach (var row in CelioManifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key = (row.GetProperty("species").GetInt32(),row.GetProperty("side").GetString()!);
            if (!_sourceBattlerGeometry.TryGetValue(key,out var actual) || actual.YOffset != row.GetProperty("yOffset").GetInt32() ||
                actual.Elevation != row.GetProperty("elevation").GetInt32() || actual.SourceWidth != row.GetProperty("sourceWidth").GetInt32() ||
                actual.SourceHeight != row.GetProperty("sourceHeight").GetInt32())
                throw new InvalidDataException("Celio source-move battler geometry differs.");
        }
    }

    private void DisposeCelioPresentation()
    {
        _celioArt=null; _celioDex.Clear(); _celioMachineLayers.Clear();
    }
}
