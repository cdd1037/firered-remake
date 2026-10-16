using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.WildBattle;
using Godot;

/// <summary>Four Island source pixels and correlated forced-movement presentation. The application remains authoritative.</summary>
public partial class CampaignView
{
    private FourIslandPresentationData? _fourIslandArt;
    private readonly Dictionary<int,PokedexMenuEntry> _fourIslandDex = [];
    private readonly Dictionary<string,JsonElement> _fourIslandTrainers = new(StringComparer.Ordinal);
    private readonly Dictionary<(int Tile,int Layer),Texture2D> _fourIslandIceLayers = [];
    private readonly Dictionary<int,Texture2D> _fourIslandTerrains = [];
    private string? _fourIslandMovementId;
    private double _fourIslandFrames;
    private bool _fourIslandCry;
    private int FourIslandFrame => (int)Math.Floor(_fourIslandFrames + 1e-7);
    private bool FourIslandShowMon => _session.State.FourIslandMovement?.Movement.Phase == FourIslandMovementPhase.ShowPokemon;
    private int FourIslandPlayerFrame => FourIslandFrame < 16 ? FourIslandFrame / 4 : 4;
    private JsonElement FourIslandManifest => _fourIslandArt?.Manifest ?? throw new InvalidDataException("Four Island presentation missing.");

    private void LoadFourIslandPresentation()
    {
        _fourIslandArt = FourIslandPresentationAssets.Load(_root);
        Texture2D Pixels(JsonElement row, bool replicatePalette = false) => PresentationTexture(_fourIslandArt.Files, row, replicatePalette);
        foreach (var row in FourIslandManifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            if (_catalog.PokemonContent?.Catalog.Species.GetValueOrDefault(new(row.GetProperty("speciesId").GetString()!))?.SourceSpeciesId != species)
                throw new InvalidDataException("Four Island sprite is outside its owned catalog.");
            string side = row.GetProperty("side").GetString()!; bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny";
            var texture = Pixels(row); var position = new Vector2(row.GetProperty("x").GetInt32(),row.GetProperty("y").GetInt32());
            if (_battleArt.TryGetValue((species,side,shiny),out var inherited))
            {
                if (species != 87 || inherited.Position != position) throw new InvalidDataException("Unexpected inherited Four Island sprite.");
            }
            else _battleArt.Add((species,side,shiny),new(texture,position));
            if (side == "front" && !shiny)
            {
                if (!_portraits.TryAdd(species,texture) && species != 87) throw new InvalidDataException("Unexpected inherited Four Island portrait.");
                _fourIslandDex.Add(species,(_pokedexMenu ?? throw new InvalidDataException("Four Island dex metadata missing.")).Entries[species] with { Portrait = SeviiRubyDexTexture(row) });
            }
        }
        foreach (var row in FourIslandManifest.GetProperty("pokemonIcons").EnumerateArray())
        {
            var species = new SpeciesId(row.GetProperty("speciesId").GetString()!); var texture = Pixels(row,true);
            if (_storageIcons.TryGetValue(species,out var inherited)) { if (species.Value != "pret:SPECIES_DEWGONG") throw new InvalidDataException("Unexpected inherited icon."); }
            else _storageIcons.Add(species,texture);
        }
        foreach (var row in FourIslandManifest.GetProperty("footprints").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32(); _route22Footprints.Add(species,Pixels(row));
            _fourIslandDex[species] = _fourIslandDex[species] with { Footprint = SeviiRubyDexTexture(row) };
        }
        foreach (var row in FourIslandManifest.GetProperty("portraits").EnumerateArray())
        {
            string id = row.GetProperty("id").GetString()!; var texture = Pixels(row);
            if(!(_forestTrainerPortraits.TryGetValue(id,out var inherited)))_forestTrainerPortraits.Add(id,(ImageTexture)texture);
        }
        foreach (var row in FourIslandManifest.GetProperty("trainers").EnumerateArray())
        {
            string id = row.GetProperty("trainer").GetString()!; var trainer = _catalog.FourIslandTrainers?.Trainers.GetValueOrDefault(new(id)) ?? throw new InvalidDataException("Four Island trainer missing.");
            if (trainer.SourceTrainerId != row.GetProperty("sourceTrainerId").GetInt32() || trainer.SourceTrainerName != row.GetProperty("name").GetString() ||
                trainer.TrainerClass != row.GetProperty("trainerClass").GetString() || trainer.TrainerPic != row.GetProperty("trainerPic").GetString() ||
                trainer.DoubleBattle != row.GetProperty("doubleBattle").GetBoolean() || trainer.EncounterMusicGender != row.GetProperty("encounterMusicGender").GetInt32() || !_forestTrainerPortraits.ContainsKey(row.GetProperty("portrait").GetString()!))
                throw new InvalidDataException("Four Island trainer presentation binding differs.");
            _fourIslandTrainers.Add(id,row);
        }
        foreach (var row in FourIslandManifest.GetProperty("items").EnumerateArray()) _earlyItemIcons.Add(new(row.GetProperty("item").GetString()!),(ImageTexture)Pixels(row));
        foreach (var row in FourIslandManifest.GetProperty("layers").EnumerateArray()) _fourIslandIceLayers.Add((row.GetProperty("metatileId").GetInt32(),row.GetProperty("layer").GetInt32()),Pixels(row));
        foreach (var row in FourIslandManifest.GetProperty("terrains").EnumerateArray()) _fourIslandTerrains.Add(row.GetProperty("terrainId").GetInt32(),Pixels(row));

    }

    private CaptureDexEntry? FourIslandCaptureDexEntry(int species) => !_fourIslandDex.TryGetValue(species,out var e) ? null :
        new(e.SourceSpeciesId,e.NationalNumber,e.Name,e.Category,e.DisplayCategory,e.HeightDecimeters,e.WeightHectograms,e.HeightDisplay,e.WeightDisplay,e.Description,
            e.Footprint!.TexturePath,e.Portrait!.TexturePath,e.HabitatTitle);
    private byte[]? ReadFourIslandDexTexture(PokedexMenuTexture texture) => _fourIslandDex.Values.Any(e => e.Portrait == texture || e.Footprint == texture) ? _fourIslandArt!.Files[texture.TexturePath] : null;
    private bool IsFourIslandPatchedMap(MapId map) => map == FourIslandFieldSource.FirstFloor;
    private bool TryFourIslandPatch(Image canvas,FieldMetatileEdit edit,int layer,MapId map)
    {
        if (!IsFourIslandPatchedMap(map)) return false;
        if (!_fourIslandIceLayers.TryGetValue((edit.SourceMetatileId,layer),out var texture) || !_catalog.FourIslandRegion!.IceFallPairs.EnumerateArray().Any(p =>
            p.GetProperty("source").GetProperty("x").GetInt32() == edit.X && p.GetProperty("source").GetProperty("y").GetInt32() == edit.Y))
            throw new InvalidDataException("Four Island ice patch is outside its authenticated source.");
        using var image = texture.GetImage(); canvas.BlitRect(image,new(0,0,16,16),new(edit.X*16,edit.Y*16)); return true;
    }
    private Texture2D? FourIslandBattleTerrain(MapId map)
    {
        if (_catalog.FourIslandRegion?.NewMaps.ContainsKey(map) != true) return null;
        return PresentedWildBattle?.State.Terrain switch
        {
            WildBattleTerrain.Cave => _fourIslandTerrains[7], WildBattleTerrain.Pond => _fourIslandTerrains[5],
            WildBattleTerrain.Water => _cityBattleScenes["water"], WildBattleTerrain.Grass => _wildTerrain,
            WildBattleTerrain.Plain => _cityBattleScenes["plain"], WildBattleTerrain.Building => _cityBattleScenes["building"],
            null when map.Value.StartsWith("map.four-island-icefall-cave-",StringComparison.Ordinal) => _fourIslandTerrains[7],
            _ => null
        };
    }

    private void SynchronizeFourIslandPresentation(CampaignState after)
    {
        string? id = after.FourIslandMovement?.Movement.PendingPresentationId.Value;
        if (id != _fourIslandMovementId) { _fourIslandMovementId=id; _fourIslandFrames=0; _fourIslandCry=false; }
        if (id is not null) { _held=null; _repeat=0; }
    }
    private int FourIslandShowMonPartyIndex()
    {
        var owner = _session.State.FourIslandMovement?.Movement.MoveOwnerInstanceId ?? throw new InvalidDataException("Waterfall move owner missing.");
        int index = Enumerable.Range(0,_session.State.Party.Length).Single(i => _session.State.Party[i].InstanceId == owner);
        if (!WaterfallSourceRules.Knows(_session.State.Party[index])) throw new InvalidDataException("Waterfall presentation owner differs.");
        return index;
    }
    private bool ProcessFourIslandPresentation(double delta)
    {
        if (_session.State.FourIslandMovement is not {} pending) return false;
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        int duration = _session.State.PresentationTicks;
        if (duration <= 0) throw new InvalidDataException("Four Island presentation duration missing.");
        _fourIslandFrames=Math.Min(duration,_fourIslandFrames+delta*60); _held=null; _repeat=0;
        if (FourIslandShowMon && !_fourIslandCry && FourIslandFrame >= FieldFlashSourcePresentation.PlayerFrameDurations.Sum()+FieldFlashSourcePresentation.IndoorBannerOpenFrames+FieldFlashSourcePresentation.MonSlideInFrames)
        { _fourIslandCry=true; SourceCry(_session.State.Party[FourIslandShowMonPartyIndex()].Pokemon); }
        if (!_busy && FourIslandFrame >= duration && _tracks.All(t => t.Time+1e-9 >= t.Duration))
            Apply(new("four_island_field_ack",Text:pending.Movement.PendingPresentationId.Value));
        return true;
    }
    private double? FourIslandMotionSeconds(CampaignState after,string actor,string action) => after.FourIslandMovement?.Movement.Phase == FourIslandMovementPhase.Moving && actor == "LOCALID_PLAYER" &&
        (action.StartsWith("walk_slower_",StringComparison.Ordinal) || action.StartsWith("walk_fast_",StringComparison.Ordinal) || action.StartsWith("ride_water_current_",StringComparison.Ordinal))
        ? after.PresentationTicks/60.0 : null;
    private float? FourIslandStepFraction(string actor,string action,double elapsed)
    {
        if (_session.State.FourIslandMovement?.Movement.Phase != FourIslandMovementPhase.Moving || actor != "LOCALID_PLAYER") return null;
        ImmutableArray<int> pixels = action.StartsWith("walk_slower_",StringComparison.Ordinal) ? FourIslandForcedMovementRules.SlowerPixels :
            action.StartsWith("ride_water_current_",StringComparison.Ordinal) ? FourIslandForcedMovementRules.Fast2Pixels :
            action.StartsWith("walk_fast_",StringComparison.Ordinal) ? FourIslandForcedMovementRules.Fast1Pixels : [];
        if (pixels.IsEmpty) return null;
        int updates=Math.Clamp((int)Math.Floor(elapsed*60+1e-7),0,pixels.Length);
        return pixels.Take(updates).Sum()/16f;
    }
    private bool DrawFourIslandActor(string id,SpriteAsset asset,Pose pose,Vector2 camera)
    {
        if (id != "LOCALID_PLAYER" || !FourIslandShowMon) return false;
        var position=pose.Position.Floor()+new Vector2(8,8)-asset.Anchor-camera;
        DrawTextureRectRegion(_session.State.PlayerGender==0?_fieldCutRed!:_fieldCutGreen!,new(position,new(16,32)),new(FourIslandPlayerFrame*16,0,16,32));
        return true;
    }
    private void DrawFourIslandBanner() { if (FourIslandShowMon) DrawFieldIndoorShowMonBanner(FourIslandFrame,FourIslandShowMonPartyIndex()); }
    private void UseFieldPartyWaterfall()
    {
        bool accepted = Apply(new("field_waterfall",PartyIndex:_fieldPartyCursor));
        if (accepted) { ResetFieldMenu(); return; }
        _fieldMenuMode="party"; _fieldMenuNotice="Can't use that here.";
    }
    private bool TryFourIslandButtonInput(InputEventKey key)
    {
        if (_session.State.Script.Wait?.Kind != StoryWaitKind.ButtonPress ||
            _session.State.Dialogue?.TextSymbol != "FourIsland_IcefallCave_Back_Text_LoreleiWellDeepFreezeYou" || key.Keycode is Key.Escape or Key.F12 or Key.F5 or Key.F9) return false;
        _held = null; _repeat = 0;
        if (key.Pressed && !key.Echo && key.Keycode is Key.Enter or Key.Backspace && FieldTextComplete &&
            _waitElapsed >= _waitDuration && _tracks.All(t => t.Time >= t.Duration))
            Apply(new("button_press", Text: key.Keycode == Key.Enter ? "A" : "B"));
        return true;
    }
    private void DisposeFourIslandPresentation()
    {
        _fourIslandArt=null; _fourIslandDex.Clear(); _fourIslandTrainers.Clear(); _fourIslandIceLayers.Clear(); _fourIslandTerrains.Clear();
        _fourIslandMovementId=null; _fourIslandFrames=0; _fourIslandCry=false;
    }
}
