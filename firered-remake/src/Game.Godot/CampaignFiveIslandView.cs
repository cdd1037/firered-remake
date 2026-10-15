using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Five Island authenticated source textures and display catalogs. The application remains authoritative.</summary>
public partial class CampaignView
{
    private bool FiveIslandPresentationProfile => _catalog.FiveIslandEnabled && _catalog.SixIslandEnabled && (_session.RulesVersion == CampaignSession.FiveIslandRulesVersion || CelioPresentationProfile);
    private FiveIslandPresentationData? _fiveIslandArt;
    private readonly Dictionary<int,PokedexMenuEntry> _fiveIslandDex = [];
    private readonly Dictionary<string,JsonElement> _fiveIslandTrainers = new(StringComparer.Ordinal);
    private readonly Dictionary<(int Tile,int Layer),Texture2D> _fiveIslandDoorLayers = [];
    private JsonElement FiveIslandManifest => _fiveIslandArt?.Manifest ?? throw new InvalidDataException("Five Island presentation missing.");

    private void LoadFiveIslandPresentation()
    {
        if (!_catalog.FiveIslandEnabled) return;
        if (!FiveIslandPresentationProfile)
            throw new InvalidDataException("Five Island requires its complete Six Island predecessor.");
        _fiveIslandArt = FiveIslandPresentationAssets.Load(_root);
        Texture2D Pixels(JsonElement row, bool replicatePalette = false)
        {
            int width = row.GetProperty("width").GetInt32(), height = row.GetProperty("height").GetInt32();
            using var image = NativeImages.LoadPng(_fiveIslandArt.Files[row.GetProperty("texturePath").GetString()!], width, height);
            if (replicatePalette)
            {
                var rgba = image.GetData();
                for (int i = 0; i < rgba.Length; i += 4) if (rgba[i + 3] != 0)
                    for (int c = 0; c < 3; c++) { int v = rgba[i + c] >> 3; rgba[i + c] = (byte)((v << 3) | (v >> 2)); }
                image.SetData(width, height, false, Image.Format.Rgba8, rgba);
            }
            return SceneTextures.FromImage(image);
        }
        foreach (var row in FiveIslandManifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            if (_catalog.FiveIslandOpponents?.Catalog.Species.GetValueOrDefault(new(row.GetProperty("speciesId").GetString()!))?.SourceSpeciesId != species)
                throw new InvalidDataException("Five Island sprite is outside its opponent catalog.");
            string side = row.GetProperty("side").GetString()!; bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            var texture = Pixels(row); var position = new Vector2(row.GetProperty("x").GetInt32(),row.GetProperty("y").GetInt32());
            _battleArt.Add((species,side,shiny),new(texture,position));
            if (side == "front" && !shiny)
            {
                _portraits.Add(species,texture);
                if (FiveIslandManifest.GetProperty("newOwnedSpecies").EnumerateArray().Any(s=>s.GetInt32()==species))
                    _fiveIslandDex.Add(species,(_pokedexMenu ?? throw new InvalidDataException("Five Island dex metadata missing.")).Entries[species] with { Portrait = SeviiRubyDexTexture(row) });
            }
        }
        foreach (var row in FiveIslandManifest.GetProperty("pokemonIcons").EnumerateArray())
            _storageIcons.Add(new(row.GetProperty("speciesId").GetString()!),Pixels(row,true));
        foreach (var row in FiveIslandManifest.GetProperty("footprints").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32(); _route22Footprints.Add(species,Pixels(row));
            _fiveIslandDex[species] = _fiveIslandDex[species] with { Footprint = SeviiRubyDexTexture(row) };
        }
        foreach (var row in FiveIslandManifest.GetProperty("portraits").EnumerateArray())
        {
            string id = row.GetProperty("id").GetString()!; var texture = Pixels(row);
            bool inheritedExpected=row.GetProperty("inherited").GetBoolean();
            bool exists=_forestTrainerPortraits.TryGetValue(id,out var inherited);
            if (exists!=inheritedExpected) throw new InvalidDataException("Five Island portrait predecessor scope differs: "+id);
            if(!(exists))_forestTrainerPortraits.Add(id,(ImageTexture)texture);
        }
        foreach (var row in FiveIslandManifest.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("trainer").GetString()!; var trainer = _catalog.FiveIslandTrainers?.Trainers.GetValueOrDefault(new(id)) ?? throw new InvalidDataException("Five Island trainer missing.");
            if (trainer.SourceTrainerId != row.GetProperty("sourceTrainerId").GetInt32() || trainer.SourceTrainerName != row.GetProperty("name").GetString() ||
                trainer.TrainerClass != row.GetProperty("trainerClass").GetString() || trainer.TrainerPic != row.GetProperty("trainerPic").GetString() ||
                trainer.DoubleBattle != row.GetProperty("doubleBattle").GetBoolean() || trainer.EncounterMusicGender != row.GetProperty("encounterMusicGender").GetInt32() || !_forestTrainerPortraits.ContainsKey(row.GetProperty("portrait").GetString()!))
                throw new InvalidDataException("Five Island trainer presentation binding differs.");
            _fiveIslandTrainers.Add(id,row);
        }
        foreach (var row in FiveIslandManifest.GetProperty("items").EnumerateArray())
        {
            string item = row.GetProperty("item").GetString()!, asset = "five-island-" + item[5..].ToLowerInvariant();
            var icon = (ImageTexture)Pixels(row);
            if(!(_earlyItemIcons.TryGetValue(new(item), out var inherited)))_earlyItemIcons.Add(new(item), icon);
            if (row.GetProperty("pocket").GetString() != "POCKET_KEY_ITEMS") continue;
            var document = _questPocketUi ?? throw new InvalidDataException("Five Island key-item pocket missing.");
            if (document.Items.Any(i => i.Id == item)) throw new InvalidDataException("Duplicated Five Island key item.");
            var position = document.Assets.Single(a => a.Id == document.Items[0].Icon);
            using var image = icon.GetImage(); _questPocketTextures.Add(asset, SceneTextures.FromImage(image));
            _questPocketUi = document with
            {
                Items = document.Items.Add(new(item, item[5..], row.GetProperty("name").GetString()!, row.GetProperty("description").GetString()!, asset, row.GetProperty("fieldUseFunction").GetString()!)),
                Assets = document.Assets.Add(new(asset,row.GetProperty("texturePath").GetString()!,24,24,position.X,position.Y))
            };
        }
        foreach (var row in FiveIslandManifest.GetProperty("layers").EnumerateArray()) _fiveIslandDoorLayers.Add((row.GetProperty("metatileId").GetInt32(),row.GetProperty("layer").GetInt32()),Pixels(row));
        Write("fiveIslandPresentation",new { nativeWindowVerified=false, physicalAudioVerified=false });
    }

    private CaptureDexEntry? FiveIslandCaptureDexEntry(int species) => !_fiveIslandDex.TryGetValue(species,out var e) ? null :
        new(e.SourceSpeciesId,e.NationalNumber,e.Name,e.Category,e.DisplayCategory,e.HeightDecimeters,e.WeightHectograms,e.HeightDisplay,e.WeightDisplay,e.Description,
            e.Footprint!.TexturePath,e.Portrait!.TexturePath,e.HabitatTitle);
    private PokedexMenuTexture? FiveIslandDexPortrait(int species)
    {
        if (_fiveIslandArt is null) return null;
        var row=FiveIslandManifest.GetProperty("sprites").EnumerateArray().SingleOrDefault(s=>s.GetProperty("sourceSpeciesId").GetInt32()==species && s.GetProperty("side").GetString()=="front" && s.GetProperty("paletteVariant").GetString()=="normal");
        return row.ValueKind==JsonValueKind.Undefined ? null : SeviiRubyDexTexture(row);
    }
    private byte[]? ReadFiveIslandDexTexture(PokedexMenuTexture texture) =>
        _fiveIslandDex.Values.Any(e => e.Portrait == texture || e.Footprint == texture) ||
        _fiveIslandArt is not null && FiveIslandManifest.GetProperty("newOpponentOnlySpecies").EnumerateArray().Any(s=>FiveIslandDexPortrait(s.GetInt32())==texture)
            ? _fiveIslandArt!.Files[texture.TexturePath] : null;
    private string? VisibleFiveIslandTrainerText(string key) => _session.State.ForestTrainerBattle is {} battle && _fiveIslandTrainers.TryGetValue(battle.Trainer.Value,out var row) ? row.GetProperty(key).GetString() : null;
    private bool IsFiveIslandPatchedMap(MapId map) => _catalog.FiveIslandEnabled && map.Value is "map.five-island-meadow" or "map.five-island-rocket-warehouse";
    private bool TryFiveIslandPatch(Image canvas, FieldMetatileEdit edit, int layer, MapId map)
    {
        if (!IsFiveIslandPatchedMap(map)) return false;
        bool permitted = FiveIslandManifest.GetProperty("tiles").EnumerateArray().Any(tile =>
            tile.GetProperty("map").GetString() == map.Value && tile.GetProperty("metatileId").GetInt32() == edit.SourceMetatileId &&
            (tile.GetProperty("collision").GetInt32() == 0 ? 0 : 3) == edit.Collision &&
            tile.GetProperty("positions").EnumerateArray().Any(p => p.GetProperty("x").GetInt32() == edit.X && p.GetProperty("y").GetInt32() == edit.Y));
        if (!permitted || !_fiveIslandDoorLayers.TryGetValue((edit.SourceMetatileId, layer), out var texture))
            throw new InvalidDataException("Five Island map patch is outside its authenticated source.");
        using var image = texture.GetImage(); canvas.BlitRect(image, new(0, 0, 16, 16), new(edit.X * 16, edit.Y * 16)); return true;
    }
    private Texture2D? FiveIslandBattleTerrain(MapId map)
    {
        if (_catalog.FiveIslandRegion?.NewMaps.ContainsKey(map) != true) return null;
        if (map.Value == "map.five-island-rocket-warehouse")
            return _rocketIndoorTerrain ?? throw new InvalidDataException("Five Island source Indoor2 terrain missing.");
        return PresentedWildBattle?.State.Terrain switch
        {
            WildBattleTerrain.Water => _cityBattleScenes["water"], WildBattleTerrain.Grass => _wildTerrain,
            WildBattleTerrain.Plain => _cityBattleScenes["plain"], WildBattleTerrain.Building => _cityBattleScenes["building"],
            _ => null
        };
    }

    private string? FiveIslandSpeciesName(int species) => _catalog.FiveIslandOpponents?.Catalog.Species.Values.Any(s=>s.SourceSpeciesId==species)==true
        ? _pokedexMenu?.Entries.GetValueOrDefault(species)?.Name : null;
    private void DisposeFiveIslandPresentation()
    {
        _fiveIslandArt=null; _fiveIslandDex.Clear(); _fiveIslandTrainers.Clear(); _fiveIslandDoorLayers.Clear();
    }
}
