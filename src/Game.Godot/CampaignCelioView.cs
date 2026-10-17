using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.World;
using Godot;

/// <summary>Original beast art and machine pixels. Authoritative commands own handoff and roaming state.</summary>
public partial class CampaignView
{
    private CelioPresentationData? _celioArt;
    private readonly Dictionary<int,PokedexMenuEntry> _celioDex = [];
    private readonly Dictionary<(int Tile,int Layer),Texture2D> _celioMachineLayers = [];
    private JsonElement CelioManifest => _celioArt?.Manifest ?? throw new InvalidDataException("Celio presentation missing.");

    private void LoadCelioPresentation()
    {
        var pokemon = _catalog.PokemonContent?.Catalog ?? throw new InvalidDataException("Celio owned Pokemon catalog missing.");
        _celioArt = CelioPresentationAssets.Load(_root);
        Texture2D Pixels(JsonElement row, bool replicatePalette = false) => PresentationTexture(_celioArt.Files, row, replicatePalette);
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

    }

    private CaptureDexEntry? CelioCaptureDexEntry(int species) => !_celioDex.TryGetValue(species,out var e) ? null :
        new(e.SourceSpeciesId,e.NationalNumber,e.Name,e.Category,e.DisplayCategory,e.HeightDecimeters,e.WeightHectograms,e.HeightDisplay,e.WeightDisplay,e.Description,
            e.Footprint!.TexturePath,e.Portrait!.TexturePath,e.HabitatTitle);
    private byte[]? ReadCelioDexTexture(PokedexMenuTexture texture) => _celioDex.Values.Any(e => e.Portrait == texture || e.Footprint == texture)
        ? _celioArt!.Files[texture.TexturePath] : null;


    private void VerifyCelioMoveGeometry()
    {
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
