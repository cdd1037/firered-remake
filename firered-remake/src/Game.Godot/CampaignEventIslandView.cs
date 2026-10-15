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

    private Texture2D EventIslandPixels(JsonElement row,bool replicatePalette=false)
    {
        int width=row.GetProperty("width").GetInt32(),height=row.GetProperty("height").GetInt32();
        var rgba=PngRgba8.Decode(_eventIslandArt!.Files[row.GetProperty("texturePath").GetString()!]);
        if(replicatePalette)
        {
            rgba=rgba.ToArray();
            for(int i=0;i<rgba.Length;i+=4)if(rgba[i+3]!=0)
                for(int c=0;c<3;c++){int v=rgba[i+c]>>3;rgba[i+c]=(byte)((v<<3)|(v>>2));}
        }
        using var image=Image.CreateFromData(width,height,false,Image.Format.Rgba8,rgba);
        return SceneTextures.FromImage(image);
    }
    private void LoadEventIslandPresentation()
    {
        if(!_catalog.EventIslandsEnabled)return;
        if(!_session.IsCurrentGame||!_catalog.SevenIslandEnabled)
            throw new InvalidDataException("Event islands require their complete Seven Island presentation predecessor.");
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
        Write("eventIslandPresentation",new{nativeWindowVerified=false,physicalAudioVerified=false});
    }
    private CaptureDexEntry? EventIslandCaptureDexEntry(int species)=>!_eventIslandDex.TryGetValue(species,out var e)?null:
        new(e.SourceSpeciesId,e.NationalNumber,e.Name,e.Category,e.DisplayCategory,e.HeightDecimeters,e.WeightHectograms,
            e.HeightDisplay,e.WeightDisplay,e.Description,e.Footprint!.TexturePath,e.Portrait!.TexturePath,e.HabitatTitle,e.Source,e.DescriptionSource);
    private PokedexMenuPage? EventIslandPokedexPage(int species,PokedexMenuPage page)=>_eventIslandDex.TryGetValue(species,out var e)
        ?page with{Portrait=e.Portrait,Footprint=page.Caught?e.Footprint:null}:null;
    private byte[]? ReadEventIslandDexTexture(PokedexMenuTexture texture)=>_eventIslandArt is not null&&
        _eventIslandDex.Values.Any(e=>e.Portrait==texture||e.Footprint==texture)?_eventIslandArt.Files[texture.TexturePath]:null;
    private string? EventIslandSpeciesName(int species)=>_eventIslandDex.GetValueOrDefault(species)?.Name;
    private void VerifyEventIslandMoveGeometry()
    {
        if(!_catalog.EventIslandsEnabled)return;
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
