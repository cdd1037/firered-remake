using System.Text.Json;
using Game.Content;
using Godot;

/// <summary>Source Power Plant art extends the existing battle and owned presentation dictionaries.</summary>
public partial class CampaignView
{
    private PowerPlantPresentationData? _powerPlantArt;
    private readonly Dictionary<int,PokedexMenuEntry> _powerPlantDex=[];
    private JsonElement PowerPlantManifest=>_powerPlantArt?.Manifest??throw new InvalidDataException("Power Plant presentation missing.");

    private Texture2D PowerPlantPixels(JsonElement row,bool replicatePalette=false)
    {
        int width=row.GetProperty("width").GetInt32(),height=row.GetProperty("height").GetInt32();
        var rgba=_powerPlantArt!.Files[row.GetProperty("rgbaPath").GetString()!];
        if(replicatePalette)
        {
            rgba=rgba.ToArray();
            for(int i=0;i<rgba.Length;i+=4)if(rgba[i+3]!=0)
                for(int c=0;c<3;c++){int v=rgba[i+c]>>3;rgba[i+c]=(byte)((v<<3)|(v>>2));}
        }
        using var image=Image.CreateFromData(width,height,false,Image.Format.Rgba8,rgba);
        return SceneTextures.FromImage(image);
    }
    private void LoadPowerPlantPresentation()
    {
        if(!_catalog.PowerPlantEnabled)return;
        if(!_session.IsCurrentGame||!_catalog.EventIslandsEnabled||_battleArt.Count!=840||_storageIcons.Count!=173||_route22Footprints.Count!=126)
            throw new InvalidDataException("Power Plant requires its complete Event Island presentation predecessor.");
        _powerPlantArt=PowerPlantPresentationAssets.Load(_root);
        foreach(var row in PowerPlantManifest.GetProperty("sprites").EnumerateArray())
        {
            int species=row.GetProperty("sourceSpeciesId").GetInt32();string id=row.GetProperty("speciesId").GetString()!,side=row.GetProperty("side").GetString()!;
            if(_catalog.PokemonContent?.Catalog.Species.GetValueOrDefault(new(id))?.SourceSpeciesId!=species)
                throw new InvalidDataException("Power Plant sprite species is not admitted.");
            bool shiny=row.GetProperty("paletteVariant").GetString()=="shiny";var texture=PowerPlantPixels(row);
            _battleArt.Add((species,side,shiny),new(texture,new(row.GetProperty("x").GetInt32(),row.GetProperty("y").GetInt32())));
            if(side=="front"&&!shiny)
            {
                _portraits.Add(species,texture);
                _powerPlantDex.Add(species,(_pokedexMenu??throw new InvalidDataException("Power Plant dex metadata missing.")).Entries[species]
                    with{Portrait=SeviiRubyDexTexture(row)});
            }
        }
        foreach(var row in PowerPlantManifest.GetProperty("pokemonIcons").EnumerateArray())
            _storageIcons.Add(new(row.GetProperty("speciesId").GetString()!),PowerPlantPixels(row,true));
        foreach(var row in PowerPlantManifest.GetProperty("footprints").EnumerateArray())
        {
            int species=row.GetProperty("sourceSpeciesId").GetInt32();_route22Footprints.Add(species,PowerPlantPixels(row));
            _powerPlantDex[species]=_powerPlantDex[species] with{Footprint=SeviiRubyDexTexture(row)};
        }
        foreach(var row in PowerPlantManifest.GetProperty("itemIcons").EnumerateArray())
        {
            if(row.GetProperty("inherited").GetBoolean())continue;
            byte[] pixels=Convert.FromBase64String(row.GetProperty("rgbaBase64").GetString()!);
            if(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(pixels))!=row.GetProperty("rgbaSha256").GetString())
                throw new InvalidDataException("Power Plant item pixels differ.");
            using var image=Image.CreateFromData(row.GetProperty("width").GetInt32(),row.GetProperty("height").GetInt32(),false,Image.Format.Rgba8,pixels);
            _earlyItemIcons.Add(new(row.GetProperty("item").GetProperty("value").GetString()!),(ImageTexture)SceneTextures.FromImage(image));
        }
        if(_battleArt.Count!=852||_storageIcons.Count!=176||_route22Footprints.Count!=129||_powerPlantDex.Count!=3)
            throw new InvalidDataException("Power Plant presentation inventory differs.");
        Write("powerPlantPresentation",new{PowerPlantPresentationAssets.ManifestSha256,battleSprites=852,canonicalIcons=176,footprints=129,
            newMoveAnimations=0,nativeWindowVerified=false,physicalAudioVerified=false});
    }
    private CaptureDexEntry? PowerPlantCaptureDexEntry(int species)=>!_powerPlantDex.TryGetValue(species,out var e)?null:
        new(e.SourceSpeciesId,e.NationalNumber,e.Name,e.Category,e.DisplayCategory,e.HeightDecimeters,e.WeightHectograms,
            e.HeightDisplay,e.WeightDisplay,e.Description,e.Footprint!.TexturePath,e.Portrait!.TexturePath,e.HabitatTitle,e.Source,e.DescriptionSource);
    private PokedexMenuPage? PowerPlantPokedexPage(int species,PokedexMenuPage page)=>_powerPlantDex.TryGetValue(species,out var e)
        ?page with{Portrait=e.Portrait,Footprint=page.Caught?e.Footprint:null}:null;
    private byte[]? ReadPowerPlantDexTexture(PokedexMenuTexture texture)=>_powerPlantArt is not null&&
        _powerPlantDex.Values.Any(e=>e.Portrait==texture||e.Footprint==texture)?_powerPlantArt.Files[texture.TexturePath]:null;
    private string? PowerPlantSpeciesName(int species)=>_powerPlantDex.GetValueOrDefault(species)?.Name;
    private void VerifyPowerPlantMoveGeometry()
    {
        if(!_catalog.PowerPlantEnabled)return;
        foreach(var row in PowerPlantManifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key=(row.GetProperty("species").GetInt32(),row.GetProperty("side").GetString()!);
            if(!_sourceBattlerGeometry.TryGetValue(key,out var actual)||actual.YOffset!=row.GetProperty("yOffset").GetInt32()||
                actual.Elevation!=row.GetProperty("elevation").GetInt32()||actual.SourceWidth!=row.GetProperty("sourceWidth").GetInt32()||
                actual.SourceHeight!=row.GetProperty("sourceHeight").GetInt32())throw new InvalidDataException("Power Plant move geometry differs.");
        }
    }
    private void DisposePowerPlantPresentation()
    {
        _powerPlantArt=null;_powerPlantDex.Clear();
    }
}
