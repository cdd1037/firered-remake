using System.Text.Json;
using Game.Content;
using Godot;

/// <summary>Source event-island art extends the existing battle and owned presentation dictionaries.</summary>
public partial class CampaignView
{
    private EventIslandPresentationData? _eventIslandArt;
    private readonly Dictionary<int,PokedexMenuEntry> _eventIslandDex=[];
    private readonly Dictionary<int,Texture2D> _eventTriangleTextures=[];
    private readonly Dictionary<int,Texture2D> _eventRockFragments=[];
    private JsonElement EventIslandManifest=>_eventIslandArt?.Manifest??throw new InvalidDataException("Event Island presentation missing.");

    private Texture2D EventIslandPixels(JsonElement row,bool replicatePalette=false) => PresentationTexture(_eventIslandArt!.Files, row, replicatePalette);
    private void LoadEventIslandPresentation()
    {
        _eventIslandArt=EventIslandPresentationAssets.Load(_root);
        foreach(var row in EventIslandManifest.GetProperty("sprites").EnumerateArray())
        {
            int species=row.GetProperty("sourceSpeciesId").GetInt32();string id=row.GetProperty("speciesId").GetString()!,side=row.GetProperty("side").GetString()!;
            if(_catalog.PokemonContent?.Catalog.Species.GetValueOrDefault(new(id))?.SourceSpeciesId!=species)
                throw new InvalidDataException("Event Island sprite species is not admitted.");
            bool shiny=row.GetProperty("paletteVariant").GetString()=="shiny";var texture=EventIslandPixels(row);
            _battleArt.Add((species,side,shiny),new(texture,new(row.GetProperty("x").GetInt32(),row.GetProperty("y").GetInt32())));
            if(side=="front"&&!shiny)
            {
                _portraits.Add(species,texture);
                _eventIslandDex.Add(species,(_pokedexMenu??throw new InvalidDataException("Event Island dex metadata missing.")).Entries[species]
                    with{Portrait=SeviiRubyDexTexture(row)});
            }
        }
        foreach(var row in EventIslandManifest.GetProperty("pokemonIcons").EnumerateArray())
            _storageIcons.Add(new(row.GetProperty("speciesId").GetString()!),EventIslandPixels(row,true));
        foreach(var row in EventIslandManifest.GetProperty("footprints").EnumerateArray())
        {
            int species=row.GetProperty("sourceSpeciesId").GetInt32();_route22Footprints.Add(species,EventIslandPixels(row));
            _eventIslandDex[species]=_eventIslandDex[species] with{Footprint=SeviiRubyDexTexture(row)};
        }
        foreach(var row in EventIslandManifest.GetProperty("triangle").EnumerateArray())
            _eventTriangleTextures.Add(row.GetProperty("interactionNumber").GetInt32(),EventIslandPixels(row));
        foreach(var row in EventIslandManifest.GetProperty("fragments").EnumerateArray())
            _eventRockFragments.Add(row.GetProperty("ordinal").GetInt32(),EventIslandPixels(row));

        _supplementalDex.AddEntries(_eventIslandDex.Values, _eventIslandArt.Files);
    }
    private void VerifyEventIslandMoveGeometry()
    {
        foreach(var row in EventIslandManifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key=(row.GetProperty("species").GetInt32(),row.GetProperty("side").GetString()!);
            if(!_sourceBattlerGeometry.TryGetValue(key,out var actual)||actual.YOffset!=row.GetProperty("yOffset").GetInt32()||
                actual.Elevation!=row.GetProperty("elevation").GetInt32()||actual.SourceWidth!=row.GetProperty("sourceWidth").GetInt32()||
                actual.SourceHeight!=row.GetProperty("sourceHeight").GetInt32())throw new InvalidDataException("Event Island move geometry differs.");
        }
    }
    private void DisposeEventIslandPresentation()
    {
        ResetEventIslandFieldPresentation();_eventIslandArt=null;_eventIslandDex.Clear();_eventTriangleTextures.Clear();_eventRockFragments.Clear();
    }
}
