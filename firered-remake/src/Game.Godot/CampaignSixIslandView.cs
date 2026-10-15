using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Six Island authenticated source textures and display catalogs. The application remains authoritative.</summary>
public partial class CampaignView
{
    private bool SixIslandPresentationProfile => _catalog.SixIslandEnabled && _catalog.FourIslandEnabled && (_session.RulesVersion == CampaignSession.SixIslandRulesVersion || FiveIslandPresentationProfile);
    private SixIslandPresentationData? _sixIslandArt;
    private readonly Dictionary<int,PokedexMenuEntry> _sixIslandDex = [];
    private readonly Dictionary<string,JsonElement> _sixIslandTrainers = new(StringComparer.Ordinal);
    private readonly Dictionary<(int Tile,int Layer),Texture2D> _sixIslandDoorLayers = [];
    private readonly Dictionary<int,Texture2D> _sixIslandTerrains = [];
    private JsonElement SixIslandManifest => _sixIslandArt?.Manifest ?? throw new InvalidDataException("Six Island presentation missing.");

    private void LoadSixIslandPresentation()
    {
        if (!_catalog.SixIslandEnabled) return;
        if (!SixIslandPresentationProfile)
            throw new InvalidDataException("Six Island requires its complete Four Island predecessor.");
        _sixIslandArt = SixIslandPresentationAssets.Load(_root);
        Texture2D Pixels(JsonElement row, bool replicatePalette = false)
        {
            int width = row.GetProperty("width").GetInt32(), height = row.GetProperty("height").GetInt32();
            using var image = NativeImages.LoadPng(_sixIslandArt.Files[row.GetProperty("texturePath").GetString()!], width, height);
            if (replicatePalette)
            {
                var rgba = image.GetData();
                for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                    for (int c = 0; c < 3; c++) { int v = rgba[i + c] >> 3; rgba[i + c] = (byte)((v << 3) | (v >> 2)); }
                image.SetData(width, height, false, Image.Format.Rgba8, rgba);
            }
            return SceneTextures.FromImage(image);
        }
        foreach (var row in SixIslandManifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            if (_catalog.SixIslandOpponents?.Catalog.Species.GetValueOrDefault(new(row.GetProperty("speciesId").GetString()!))?.SourceSpeciesId != species)
                throw new InvalidDataException("Six Island sprite is outside its opponent catalog.");
            string side = row.GetProperty("side").GetString()!; bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            var texture = Pixels(row); var position = new Vector2(row.GetProperty("x").GetInt32(),row.GetProperty("y").GetInt32());
            _battleArt.Add((species,side,shiny),new(texture,position));
            if (side == "front" && !shiny)
            {
                _portraits.Add(species,texture);
                if (SixIslandManifest.GetProperty("newOwnedSpecies").EnumerateArray().Any(s=>s.GetInt32()==species))
                    _sixIslandDex.Add(species,(_pokedexMenu ?? throw new InvalidDataException("Six Island dex metadata missing.")).Entries[species] with { Portrait = SeviiRubyDexTexture(row) });
            }
        }
        foreach (var row in SixIslandManifest.GetProperty("pokemonIcons").EnumerateArray())
            _storageIcons.Add(new(row.GetProperty("speciesId").GetString()!),Pixels(row,true));
        foreach (var row in SixIslandManifest.GetProperty("footprints").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32(); _route22Footprints.Add(species,Pixels(row));
            _sixIslandDex[species] = _sixIslandDex[species] with { Footprint = SeviiRubyDexTexture(row) };
        }
        foreach (var row in SixIslandManifest.GetProperty("portraits").EnumerateArray())
        {
            string id = row.GetProperty("id").GetString()!; var texture = Pixels(row);
            bool inheritedExpected=row.GetProperty("inherited").GetBoolean();
            bool exists=_forestTrainerPortraits.TryGetValue(id,out var inherited);
            if (exists!=inheritedExpected) throw new InvalidDataException("Six Island portrait predecessor scope differs: "+id);
            if(!(exists))_forestTrainerPortraits.Add(id,(ImageTexture)texture);
        }
        foreach (var row in SixIslandManifest.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("trainer").GetString()!; var trainer = _catalog.SixIslandTrainers?.Trainers.GetValueOrDefault(new(id)) ?? throw new InvalidDataException("Six Island trainer missing.");
            if (trainer.SourceTrainerId != row.GetProperty("sourceTrainerId").GetInt32() || trainer.SourceTrainerName != row.GetProperty("name").GetString() ||
                trainer.TrainerClass != row.GetProperty("trainerClass").GetString() || trainer.TrainerPic != row.GetProperty("trainerPic").GetString() ||
                trainer.DoubleBattle != row.GetProperty("doubleBattle").GetBoolean() || trainer.EncounterMusicGender != row.GetProperty("encounterMusicGender").GetInt32() || !_forestTrainerPortraits.ContainsKey(row.GetProperty("portrait").GetString()!))
                throw new InvalidDataException("Six Island trainer presentation binding differs.");
            _sixIslandTrainers.Add(id,row);
        }
        foreach (var row in SixIslandManifest.GetProperty("items").EnumerateArray()) _earlyItemIcons.Add(new(row.GetProperty("item").GetString()!),(ImageTexture)Pixels(row));
        foreach (var row in SixIslandManifest.GetProperty("layers").EnumerateArray()) _sixIslandDoorLayers.Add((row.GetProperty("metatileId").GetInt32(),row.GetProperty("layer").GetInt32()),Pixels(row));
        foreach (var row in SixIslandManifest.GetProperty("terrains").EnumerateArray()) _sixIslandTerrains.Add(row.GetProperty("terrainId").GetInt32(),Pixels(row));
        Write("sixIslandPresentation",new { nativeWindowVerified=false, physicalAudioVerified=false });
    }

    private CaptureDexEntry? SixIslandCaptureDexEntry(int species) => !_sixIslandDex.TryGetValue(species,out var e) ? null :
        new(e.SourceSpeciesId,e.NationalNumber,e.Name,e.Category,e.DisplayCategory,e.HeightDecimeters,e.WeightHectograms,e.HeightDisplay,e.WeightDisplay,e.Description,
            e.Footprint!.TexturePath,e.Portrait!.TexturePath,e.HabitatTitle);
    private PokedexMenuTexture? SixIslandDexPortrait(int species)
    {
        if (_sixIslandArt is null) return null;
        var row=SixIslandManifest.GetProperty("sprites").EnumerateArray().SingleOrDefault(s=>s.GetProperty("sourceSpeciesId").GetInt32()==species && s.GetProperty("side").GetString()=="front" && s.GetProperty("paletteVariant").GetString()=="normal");
        return row.ValueKind==JsonValueKind.Undefined ? null : SeviiRubyDexTexture(row);
    }
    private byte[]? ReadSixIslandDexTexture(PokedexMenuTexture texture) =>
        _sixIslandDex.Values.Any(e => e.Portrait == texture || e.Footprint == texture) ||
        _sixIslandArt is not null && SixIslandManifest.GetProperty("newOpponentOnlySpecies").EnumerateArray().Any(s=>SixIslandDexPortrait(s.GetInt32())==texture)
            ? _sixIslandArt!.Files[texture.TexturePath] : null;
    private string? SixIslandDoubleText(string key) => _session.State.DoubleBattle is {} battle && _sixIslandTrainers.TryGetValue(battle.Trainer.Value,out var row) ? row.GetProperty(key).GetString() : null;
    private Texture2D? SixIslandDoublePortrait => SixIslandDoubleText("portrait") is {} id ? _forestTrainerPortraits[id] : null;
    private string? VisibleSixIslandTrainerText(string key) => _session.State.ForestTrainerBattle is {} battle && _sixIslandTrainers.TryGetValue(battle.Trainer.Value,out var row) ? row.GetProperty(key).GetString() : null;
    private bool IsSixIslandPatchedMap(MapId map) => _catalog.SixIslandEnabled && map.Value == "map.six-island-ruin-valley";
    private bool TrySixIslandPatch(Image canvas,FieldMetatileEdit edit,int layer,MapId map)
    {
        if (!IsSixIslandPatchedMap(map)) return false;
        if (!_sixIslandDoorLayers.TryGetValue((edit.SourceMetatileId,layer),out var texture) || edit.X != 24 || edit.Y != 24)
            throw new InvalidDataException("Six Island door patch is outside its authenticated source.");
        using var image = texture.GetImage(); canvas.BlitRect(image,new(0,0,16,16),new(edit.X*16,edit.Y*16)); return true;
    }
    private Texture2D? SixIslandBattleTerrain(MapId map)
    {
        if (_catalog.SixIslandRegion?.NewMaps.ContainsKey(map) != true) return null;
        return PresentedWildBattle?.State.Terrain switch
        {
            WildBattleTerrain.Cave => _sixIslandTerrains[7], WildBattleTerrain.Pond => _sixIslandTerrains[5], WildBattleTerrain.Mountain => _sixIslandTerrains[6],
            WildBattleTerrain.Water => _cityBattleScenes["water"], WildBattleTerrain.Grass => _wildTerrain,
            WildBattleTerrain.Plain => _cityBattleScenes["plain"], WildBattleTerrain.Building => _cityBattleScenes["building"],
            null when map.Value.StartsWith("map.six-island-dotted-hole-",StringComparison.Ordinal) => _sixIslandTerrains[7],
            _ => null
        };
    }

    private string? SixIslandSpeciesName(int species) => _catalog.SixIslandOpponents?.Catalog.Species.Values.Any(s=>s.SourceSpeciesId==species)==true
        ? _pokedexMenu?.Entries.GetValueOrDefault(species)?.Name : null;
    private JsonElement? SixIslandBrailleMessage(string symbol)
    {
        if (_sixIslandArt is null) return null;
        var row = SixIslandManifest.GetProperty("braille").GetProperty("messages").EnumerateArray().SingleOrDefault(m=>m.GetProperty("label").GetString()==symbol);
        return row.ValueKind == JsonValueKind.Undefined ? null : row;
    }
    private void DisposeSixIslandPresentation()
    {
        ResetSixIslandFieldPresentation(); _sixIslandArt=null; _sixIslandDex.Clear(); _sixIslandTrainers.Clear(); _sixIslandDoorLayers.Clear(); _sixIslandTerrains.Clear();
    }
}
