using System.Text.Json;
using Game.Content;
using Game.Domain.Pokemon;
using Godot;

/// <summary>Source-authenticated Seven Island art; Unown's PID selects display keys only.</summary>
public partial class CampaignView
{
    private SevenIslandPresentationData? _sevenIslandArt;
    private readonly Dictionary<int, PokedexMenuEntry> _sevenIslandDex = [];
    private readonly Dictionary<(int Species, bool Shiny), PokedexMenuTexture> _sevenIslandDexPortraits = [];
    private readonly Dictionary<int, Texture2D> _unownIcons = [];
    private readonly Dictionary<int, string> _sevenIslandSpeciesNames = [];
    private readonly Dictionary<string, JsonElement> _sevenIslandTrainers = new(StringComparer.Ordinal);
    private JsonElement SevenIslandManifest => _sevenIslandArt?.Manifest ?? throw new InvalidDataException("Seven Island presentation missing.");

    private static int PresentedRenderSpecies(PokemonInstance mon) => SevenIslandRenderKeys.Species(mon.SourceSpeciesId, mon.Personality);
    private static (int Species, bool Shiny) PresentedAppearance(PokemonInstance mon) =>
        SevenIslandRenderKeys.Appearance(mon.SourceSpeciesId, mon.Personality, mon.OriginalTrainerId);
    private Texture2D PresentedPokemonIcon(PokemonInstance mon) => mon.SourceSpeciesId == UnownFormRules.SourceSpeciesId
        ? _unownIcons[PresentedRenderSpecies(mon)] : _storageIcons[mon.Species];
    private (int Species, bool Shiny) PresentedDexAppearance(int species) =>
        SevenIslandRenderKeys.Pokedex(species, _session.State.UnownPokedexPersonality);

    private void LoadSevenIslandPresentation()
    {
        if (!_catalog.SevenIslandEnabled) return;
        if (!_session.IsCurrentGame || !_catalog.LeagueRematchEnabled || _battleArt.Count != 672 ||
            _storageIcons.Count != 163 || _route22Footprints.Count != 116 ||
            _catalog.PokemonContent?.Catalog.Species.Count != (_catalog.EventIslandsEnabled?149:146) || _catalog.SevenIslandOpponents?.Catalog.Species.Count != 180)
            throw new InvalidDataException("Seven Island requires its complete current League-rematch predecessor.");
        _sevenIslandArt = SevenIslandPresentationAssets.Load(_root);
        Texture2D Pixels(JsonElement row, bool replicatePalette = false)
        {
            int width=row.GetProperty("width").GetInt32(), height=row.GetProperty("height").GetInt32();
            var rgba=_sevenIslandArt.Files[row.GetProperty("rgbaPath").GetString()!];
            if (replicatePalette)
            {
                rgba=rgba.ToArray();
                for(int i=0;i<rgba.Length;i+=4) if(rgba[i+3]!=0)
                    for(int c=0;c<3;c++){int v=rgba[i+c]>>3;rgba[i+c]=(byte)((v<<3)|(v>>2));}
            }
            using var image=Image.CreateFromData(width,height,false,Image.Format.Rgba8,rgba);
            return SceneTextures.FromImage(image);
        }
        foreach(var row in SevenIslandManifest.GetProperty("sprites").EnumerateArray().Concat(SevenIslandManifest.GetProperty("inheritedSprites").EnumerateArray()))
        {
            int species=row.GetProperty("sourceSpeciesId").GetInt32(),render=row.GetProperty("renderSpeciesId").GetInt32();
            string id=row.GetProperty("speciesId").GetString()!,side=row.GetProperty("side").GetString()!;
            bool shiny=row.GetProperty("paletteVariant").GetString()=="shiny";
            if(_catalog.SevenIslandOpponents!.Catalog.Species.GetValueOrDefault(new(id))?.SourceSpeciesId!=species)
                throw new InvalidDataException("Seven Island sprite canonical species is not admitted.");
            var position=new Vector2(row.GetProperty("x").GetInt32(),row.GetProperty("y").GetInt32());
            if(row.TryGetProperty("inherited",out var inherited) && inherited.GetBoolean())
            {
                if(!_battleArt.TryGetValue((render,side,shiny),out var old) || old.Position!=position)
                    throw new InvalidDataException("Inherited Tyranitar sprite missing.");
                using var oldImage=old.Texture.GetImage(); oldImage.Convert(Image.Format.Rgba8);
                if(!oldImage.GetData().SequenceEqual(_sevenIslandArt.Files[row.GetProperty("rgbaPath").GetString()!]))
                    throw new InvalidDataException("Inherited Tyranitar pixels differ.");
            }
            else
            {
                var texture=Pixels(row);_battleArt.Add((render,side,shiny),new(texture,position));
                if(side=="front"&&!shiny&&render==species)_portraits.Add(species,texture);
            }
            if(side=="front")
            {
                _sevenIslandDexPortraits.Add((render,shiny),SeviiRubyDexTexture(row));
                if(!shiny&&render==species&&SevenIslandManifest.GetProperty("newOwnedSpecies").EnumerateArray().Any(s=>s.GetInt32()==species))
                    _sevenIslandDex.Add(species,(_pokedexMenu??throw new InvalidDataException("Seven Island dex metadata missing.")).Entries[species]
                        with{Portrait=SeviiRubyDexTexture(row)});
            }
        }
        foreach(var row in SevenIslandManifest.GetProperty("pokemonIcons").EnumerateArray())
        {
            int species=row.GetProperty("sourceSpeciesId").GetInt32(),render=row.GetProperty("renderSpeciesId").GetInt32();
            var texture=Pixels(row,true);
            if(species==201)_unownIcons.Add(render,texture);
            if(species==render)_storageIcons.Add(new(row.GetProperty("speciesId").GetString()!),texture);
        }
        foreach(var row in SevenIslandManifest.GetProperty("footprints").EnumerateArray())
        {
            int species=row.GetProperty("sourceSpeciesId").GetInt32();_route22Footprints.Add(species,Pixels(row));
            _sevenIslandDex[species]=_sevenIslandDex[species] with{Footprint=SeviiRubyDexTexture(row)};
        }
        foreach(var row in SevenIslandManifest.GetProperty("speciesNames").EnumerateArray())
            _sevenIslandSpeciesNames.Add(row.GetProperty("sourceSpeciesId").GetInt32(),row.GetProperty("name").GetString()!);
        foreach(var row in SevenIslandManifest.GetProperty("portraits").EnumerateArray())
        {
            string id=row.GetProperty("id").GetString()!;bool exists=_forestTrainerPortraits.TryGetValue(id,out var old);
            if(row.GetProperty("inherited").GetBoolean()&&!exists)throw new InvalidDataException("Seven Island portrait predecessor missing: "+id);
            // An earlier region may already register the same source portrait. Reuse it
            // only after the byte comparison below, irrespective of this pack's cache route.
            if(exists)
            {
                using var image=old!.GetImage();image.Convert(Image.Format.Rgba8);
                if(!image.GetData().SequenceEqual(_sevenIslandArt.Files[row.GetProperty("rgbaPath").GetString()!]))
                    throw new InvalidDataException("Seven Island inherited portrait pixels differ.");
            }
            else _forestTrainerPortraits.Add(id,(ImageTexture)Pixels(row));
        }
        foreach(var row in SevenIslandManifest.GetProperty("trainers").EnumerateArray())
        {
            string id=row.GetProperty("trainer").GetString()!;
            var trainer=_catalog.SevenIslandTrainerParties!.Trainers[new(id)];
            if(trainer.SourceTrainerId!=row.GetProperty("sourceTrainerId").GetInt32()||trainer.TrainerClass!=row.GetProperty("trainerClass").GetString()||
                trainer.SourceTrainerName!=row.GetProperty("name").GetString()||trainer.TrainerPic!=row.GetProperty("trainerPic").GetString()||
                trainer.DoubleBattle!=row.GetProperty("doubleBattle").GetBoolean()||trainer.EncounterMusicGender!=row.GetProperty("encounterMusicGender").GetInt32())
                throw new InvalidDataException("Seven Island trainer presentation binding differs.");
            _sevenIslandTrainers.Add(id,row);
        }
        if(_battleArt.Count!=828||_storageIcons.Count!=170||_route22Footprints.Count!=123||_unownIcons.Count!=28||_sevenIslandDex.Count!=7)
            throw new InvalidDataException("Seven Island presentation inventory differs.");
        Write("sevenIslandPresentation",new{SevenIslandPresentationAssets.ManifestSha256,battleSprites=828,canonicalIcons=170,unownFormIcons=28,
            footprints=123,ownedSpecies=146,opponentSpecies=180,trainerBindings=18,newMoveAnimations=0,nativeWindowVerified=false,physicalAudioVerified=false});
    }

    private CaptureDexEntry? SevenIslandCaptureDexEntry(int species)=>!_sevenIslandDex.TryGetValue(species,out var e)?null:
        new(e.SourceSpeciesId,e.NationalNumber,e.Name,e.Category,e.DisplayCategory,e.HeightDecimeters,e.WeightHectograms,
            e.HeightDisplay,e.WeightDisplay,e.Description,e.Footprint!.TexturePath,e.Portrait!.TexturePath,e.HabitatTitle,e.Source,e.DescriptionSource);
    private PokedexMenuPage? SevenIslandPokedexPage(int species,PokedexMenuPage page)
    {
        if(_sevenIslandArt is null||!_sevenIslandDexPortraits.TryGetValue(PresentedDexAppearance(species),out var portrait))return null;
        return page with{Portrait=portrait,Footprint=page.Caught&&_sevenIslandDex.TryGetValue(species,out var e)?e.Footprint:null};
    }
    private byte[]? ReadSevenIslandDexTexture(PokedexMenuTexture texture)=>_sevenIslandArt is not null&&
        (_sevenIslandDexPortraits.Values.Contains(texture)||_sevenIslandDex.Values.Any(e=>e.Footprint==texture))
        ?_sevenIslandArt.Files[texture.TexturePath]:null;
    private string? SevenIslandSpeciesName(int species)=>_sevenIslandSpeciesNames.GetValueOrDefault(species);
    private string? VisibleSevenIslandTrainerText(string key)=>_session.State.ForestTrainerBattle is {} battle&&_sevenIslandTrainers.TryGetValue(battle.Trainer.Value,out var row)?row.GetProperty(key).GetString():null;
    private string? SevenIslandDoubleText(string key)=>_session.State.DoubleBattle is {} battle&&_sevenIslandTrainers.TryGetValue(battle.Trainer.Value,out var row)?row.GetProperty(key).GetString():null;
    private Texture2D? SevenIslandDoublePortrait=>SevenIslandDoubleText("portrait") is {} id?_forestTrainerPortraits[id]:null;
    private void VerifySevenIslandMoveGeometry()
    {
        if(!_catalog.SevenIslandEnabled)return;
        foreach(var row in SevenIslandManifest.GetProperty("moveAnimationGeometry").GetProperty("rows").EnumerateArray())
        {
            var key=(row.GetProperty("species").GetInt32(),row.GetProperty("side").GetString()!);
            if(!_sourceBattlerGeometry.TryGetValue(key,out var actual)||actual.YOffset!=row.GetProperty("yOffset").GetInt32()||
                actual.Elevation!=row.GetProperty("elevation").GetInt32()||actual.SourceWidth!=row.GetProperty("sourceWidth").GetInt32()||
                actual.SourceHeight!=row.GetProperty("sourceHeight").GetInt32())throw new InvalidDataException("Seven Island form-specific move geometry differs.");
        }
    }
    private void DisposeSevenIslandPresentation()
    {
        ResetSevenIslandFieldPresentation();_sevenIslandArt=null;_sevenIslandDex.Clear();_sevenIslandDexPortraits.Clear();_unownIcons.Clear();_sevenIslandSpeciesNames.Clear();_sevenIslandTrainers.Clear();
    }
}
