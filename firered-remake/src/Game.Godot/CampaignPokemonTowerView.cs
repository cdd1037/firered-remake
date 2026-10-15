using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.WildBattle;
using Godot;

public partial class CampaignView
{
    private Texture2D? _towerTerrain, _towerFog, _towerGhost;
    private readonly Dictionary<int,PokedexMenuEntry> _towerDexEntries = [];
    private readonly Dictionary<string,byte[]> _towerDexTextures = new(StringComparer.Ordinal);
    private readonly Dictionary<(bool Ghost,bool Shiny,int Block),Texture2D> _towerMosaic = [];
    private double _towerFogTicks, _towerWhiteTicks, _towerRevealTicks;
    private int _towerWhiteDuration;
    private float _towerWhiteFrom, _towerWhiteTo;
    private long _towerWhiteEffect;
    private string? _towerRevealBattle;
    private bool _towerRevealStartSound, _towerRevealEndSound;
    private bool PokemonTowerInputLocked => _catalog?.PokemonTowerEnabled == true && (_towerWhiteTicks < _towerWhiteDuration || _session?.State.PokeFlute is not null);

    private void LoadPokemonTowerPresentation()
    {
        if (!_catalog.PokemonTowerEnabled) return;
        if (_catalog.PokemonTowerPokemon is null)
            throw new InvalidDataException("Pokemon Tower must extend the 99-species Rocket presentation.");
        var art = PokemonTowerPresentationAssets.Load(_root, art:true);
        ImageTexture Rgba(JsonElement row)
        {
            using var image = NativeImages.LoadPng(art.Files[row.GetProperty("texturePath").GetString()!], row.GetProperty("width").GetInt32(), row.GetProperty("height").GetInt32());
            return ImageTexture.CreateFromImage(image);
        }
        PokedexMenuTexture DexTexture(JsonElement row)
        {
            string file = row.GetProperty("texturePath").GetString()!;
            string path = "content/generated/sprites/pokemon/pokemon-tower/" + file;
            _towerDexTextures.Add(path, art.Files[file]);
            return new(path,row.GetProperty("width").GetInt32(),row.GetProperty("height").GetInt32());
        }
        foreach (var row in art.Manifest.GetProperty("sprites").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32(); string side = row.GetProperty("side").GetString()!;
            bool shiny = row.GetProperty("paletteVariant").GetString() == "shiny"; var p = row.GetProperty("singleBattleTopLeft");
            _battleArt.Add((species,side,shiny),new(Rgba(row),new(p.GetProperty("x").GetInt32(),p.GetProperty("y").GetInt32())));
            if (side == "front" && !shiny) _towerDexEntries.Add(species, _pokedexMenu!.Entries[species] with { Portrait = DexTexture(row) });
        }
        foreach (var row in art.Manifest.GetProperty("footprints").EnumerateArray())
        {
            int species = row.GetProperty("sourceSpeciesId").GetInt32();
            _route22Footprints.Add(species,Rgba(row));
            _towerDexEntries[species] = _towerDexEntries[species] with { Footprint = DexTexture(row) };
        }
        foreach (var row in art.Manifest.GetProperty("icons").EnumerateArray())
        {
            var rgba = NativeImages.ReadPixels(art.Files[row.GetProperty("texturePath").GetString()!]);
            for (int i=0;i<rgba.Length;i+=4) if(rgba[i+3]!=0) for(int c=0;c<3;c++) { int v=rgba[i+c]>>3;rgba[i+c]=(byte)((v<<3)|(v>>2)); }
            using var image=Image.CreateFromData(32,32,false,Image.Format.Rgba8,rgba);
            _storageIcons.Add(new(row.GetProperty("speciesId").GetString()!),ImageTexture.CreateFromImage(image));
        }
        foreach (var row in art.Manifest.GetProperty("portraits").EnumerateArray()) _forestTrainerPortraits.Add("channeler-front",Rgba(row));
        var presentation = PokemonTowerPresentationAssets.Load(_root);
        Texture2D Png(string key)
        {
            using var image=new Image();
            if(image.LoadPngFromBuffer(presentation.Files[key])!=Error.Ok)throw new InvalidDataException("Invalid authenticated Tower PNG.");
            return ImageTexture.CreateFromImage(image);
        }
        _towerTerrain=Png("indoor1-scene.png");_towerFog=Png("fog-horizontal.png");_towerGhost=Png(PokemonTowerPresentationModel.GhostTexture);
        var items=PretPokemonTowerItems.LoadProject(_root);
        foreach(var icon in items.Icons)
        {
            
            _earlyItemIcons.Add(icon.Item,ItemTexture("data/items/pokemon-tower", icon));
        }
        var pocket=_questPocketUi??throw new InvalidDataException("Tower key-item pocket missing.");
        var position=pocket.Assets.Single(a=>a.Id==pocket.Items[0].Icon);
        var flute=items.Items.Single(x=>x.Metadata.Item.SourceItemSymbol=="ITEM_POKE_FLUTE").Metadata;
        var fluteIcon=items.Icons.Single(x=>x.Item==flute.Item.Id);const string fluteId="tower-poke-flute";
        _questPocketTextures.Add(fluteId,ItemTexture("data/items/pokemon-tower", fluteIcon));
        _questPocketUi=pocket with { Items=pocket.Items.Add(new(flute.Item.Id.Value,flute.Item.SourceItemSymbol,flute.Item.Name,flute.Description,fluteId,flute.Item.FieldUseFunction)),
            Assets=pocket.Assets.Add(new(fluteId,fluteIcon.PngPath,fluteIcon.Width,fluteIcon.Height,position.X,position.Y)) };
    }
    private void DisposePokemonTowerPresentation()
    {
        foreach(var texture in _towerMosaic.Values)texture.Dispose();
        _towerMosaic.Clear();
        _towerTerrain?.Dispose(); _towerTerrain=null;
        _towerFog?.Dispose(); _towerFog=null;
        _towerGhost?.Dispose(); _towerGhost=null;
        // Other Tower sprites live in mixed-ownership presentation dictionaries;
        // do not dispose those dictionaries as if every entry belonged to Tower.
    }
    private CaptureDexEntry? TowerCaptureDexEntry(int species)
    {
        if(!_towerDexEntries.TryGetValue(species,out var d))return null;
        return new(d.SourceSpeciesId,d.NationalNumber,d.Name,d.Category,d.DisplayCategory,d.HeightDecimeters,d.WeightHectograms,
            d.HeightDisplay,d.WeightDisplay,d.Description,d.Footprint!.TexturePath,d.Portrait!.TexturePath,d.HabitatTitle);
    }
    private void SynchronizePokemonTowerPresentation(CampaignState? before,CampaignState after)
    {
        if(!_catalog.PokemonTowerEnabled)return;
        if(before is null||before.World.Player.MapId!=after.World.Player.MapId)_towerFogTicks=0;
        if(before is null)
        { _towerWhiteFrom=_towerWhiteTo=after.ScreenIsWhite?1:0;_towerWhiteDuration=0;_towerWhiteTicks=0;_towerWhiteEffect=after.Script.EffectSequence; }
        foreach(var effect in after.PresentationEffects.Where(e=>e.Sequence>_towerWhiteEffect&&e.Opcode=="fadescreen"&&e.Args.Length==1&&e.Args[0] is "FADE_TO_WHITE" or "FADE_FROM_WHITE"))
        {
            _towerWhiteEffect=effect.Sequence;_towerWhiteFrom=TowerWhiteAmount;_towerWhiteTo=after.ScreenIsWhite?1:0;
            _towerWhiteDuration=Math.Max(1,after.PresentationTicks);_towerWhiteTicks=0;
        }
        if(after.WildBattle?.Id!=_towerRevealBattle)
        { _towerRevealBattle=after.WildBattle?.Id;_towerRevealTicks=0;_towerRevealStartSound=false;_towerRevealEndSound=false; }
    }
    private float TowerWhiteAmount => _towerWhiteDuration==0 ? _towerWhiteTo : Mathf.Lerp(_towerWhiteFrom,_towerWhiteTo,(float)Math.Clamp(_towerWhiteTicks/_towerWhiteDuration,0,1));
    private void ProcessPokemonTowerPresentation(double delta)
    {
        if(!_catalog.PokemonTowerEnabled)return;
        ProcessPokemonTowerFlute(delta);
        _towerFogTicks+=delta*60;_towerWhiteTicks=Math.Min(_towerWhiteDuration,_towerWhiteTicks+delta*60);
        if(PresentedWildBattle is {} b&&TowerRevealing(b))
        {
            if(!_towerRevealStartSound){_towerRevealStartSound=true;SourceSound("SE_M_TELEPORT");}
            _towerRevealTicks=Math.Min(94,_towerRevealTicks+delta*60);
            if(!_towerRevealEndSound&&_towerRevealTicks>=48){_towerRevealEndSound=true;SourceSound("SE_M_MINIMIZE");}
        }
    }
    private void DrawPokemonTowerFog(Vector2 camera)
    {
        if(!_catalog.PokemonTowerEnabled||!PokemonTowerPresentationModel.HasFog(_session.State.World.Player.MapId.Value))return;
        long ticks=(long)_towerFogTicks+1;var blend=PokemonTowerPresentationModel.FogEntryBlend(ticks);
        // Source texture, scroll, grid and EVA progression; Godot alpha approximates the
        // two independent GBA blend coefficients. OBJ priority/palette weather is not emulated here.
        foreach(var tile in PokemonTowerPresentationModel.FogSprites(ticks,-(int)camera.X,-(int)camera.Y))
            DrawTexture(_towerFog!,new(tile.X,tile.Y),new Color(1,1,1,blend.Eva/16f));
    }
    private void DrawPokemonTowerWhiteFade()
    {
        if(_catalog.PokemonTowerEnabled&&TowerWhiteAmount>0)DrawRect(new(0,0,240,160),new Color(1,1,1,TowerWhiteAmount));
    }
    private bool TowerRevealing(CampaignWildBattle b)=>b.State.TowerBattleKind==PokemonTowerBattleKind.RevealedMarowak&&
        b.Phase==CampaignBattlePhase.Events&&WildCurrentEvent(b) is WildBattleStartedEvent&&_wildPage==1;
    private bool PokemonTowerRevealInputLocked(CampaignWildBattle b)=>TowerRevealing(b)&&_towerRevealTicks<94||WildCurrentEvent(b) is WildBattlePokeFluteEvent {Phase:PokeFluteBattlePhase.Played or PokeFluteBattlePhase.Fanfare};
    private PokemonTowerPresentationModel.OpponentAppearance? TowerOpponentAppearance()
    {
        if(!_catalog.PokemonTowerEnabled||PresentedWildBattle is not {} b||b.State.TowerBattleKind is not {} kind)return null;
        var phase=PokemonTowerPresentationModel.RevealPhase.NotApplicable;
        if(kind==PokemonTowerBattleKind.RevealedMarowak)
        {
            bool intro=b.Phase==CampaignBattlePhase.Events&&WildCurrentEvent(b) is WildBattleStartedEvent;
            phase=intro&&(_wildPage==0||_wildPage==1&&_towerRevealTicks<47)?PokemonTowerPresentationModel.RevealPhase.BeforeReveal:PokemonTowerPresentationModel.RevealPhase.AfterReveal;
        }
        return PokemonTowerPresentationModel.Opponent(kind,phase,b.DisplayOpponent.SourceSpeciesId,_catalog.PokemonContent!.SpeciesNames[b.DisplayOpponent.SourceSpeciesId]);
    }
    private bool PokemonTowerStartedPages(CampaignWildBattle b,Action<string,(string Key,string Value)[]> source)
    {
        if(!_catalog.PokemonTowerEnabled||b.State.TowerBattleKind is not {} kind)return false;
        source(PokemonTowerPresentationModel.IntroTextSymbol(kind)!,[]);
        if(kind==PokemonTowerBattleKind.RevealedMarowak){source("sText_SilphScopeUnveil",[]);source("sText_TheGhostWas",[]);}
        source("sText_GoPkmn",[]);return true;
    }
    private bool PokemonTowerStartedAudio(CampaignWildBattle b)
    {
        if(!_catalog.PokemonTowerEnabled||b.State.TowerBattleKind is not {} kind)return false;
        if(_wildPage==(kind==PokemonTowerBattleKind.RevealedMarowak?3:2))SourceCry(b.DisplayPlayer);
        return true;
    }
    private bool PokemonTowerHidePlayer(CampaignWildBattle b,WildBattleSide side)=>side==WildBattleSide.Player&&b.State.TowerBattleKind is {} kind&&
        b.Phase==CampaignBattlePhase.Events&&WildCurrentEvent(b) is WildBattleStartedEvent&&_wildPage<(kind==PokemonTowerBattleKind.RevealedMarowak?3:2);
    private bool DrawPokemonTowerOpponent(CampaignWildBattle b)
    {
        var appearance=TowerOpponentAppearance();if(appearance is null)return false;
        var transformed=FuchsiaBattleAppearance(b,WildBattleSide.Opponent,b.DisplayOpponent);
        Texture2D texture=appearance.GhostSprite?_towerGhost!:_battleArt[(transformed.Species,"front",transformed.Shiny)].Texture;
        int block=1;
        if(TowerRevealing(b))block=_towerRevealTicks<47?Math.Clamp(1+(int)_towerRevealTicks/3,1,16):Math.Clamp(16-(int)(_towerRevealTicks-47)/3,1,16);
        if(block>1)
        {
            var key=(appearance.GhostSprite,b.DisplayOpponent.IsShiny,block);
            if(!_towerMosaic.TryGetValue(key,out var mosaic))
            {
                using var image=texture.GetImage();image.Convert(Image.Format.Rgba8);var original=image.GetData();var pixels=original.ToArray();
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)Array.Copy(original,((y/block*block)*64+x/block*block)*4,pixels,(y*64+x)*4,4);
                using var result=Image.CreateFromData(64,64,false,Image.Format.Rgba8,pixels);mosaic=ImageTexture.CreateFromImage(result);_towerMosaic.Add(key,mosaic);
            }
            texture=mosaic;
        }
        // A view-only mosaic approximates AnimTask_TransformMon(255); it never changes the Pokemon.
        DrawTexture(texture,new(appearance.FixedX??144,appearance.FixedY??8));return true;
    }
    private string? _towerFluteFieldKey;
    private double _towerFluteFieldElapsed;
    private ulong _towerFluteTextDrawnFrame=ulong.MaxValue, _towerFluteBattleDrawnFrame=ulong.MaxValue;
    private long _towerFluteBattleDrawnSequence=-1;
    private void NotePokemonTowerFluteMessage()
    {
        if(_session.State.PokeFlute is {Phase:CampaignPokeFlutePhase.PlayedText}&&_towerFluteTextDrawnFrame==ulong.MaxValue)
            _towerFluteTextDrawnFrame=Engine.GetProcessFrames();
    }
    private void ProcessPokemonTowerFlute(double delta)
    {
        var pending=_session.State.PokeFlute;string? key=pending is null?null:pending.WaitId+":"+pending.Phase;
        if(key!=_towerFluteFieldKey){_towerFluteFieldKey=key;_towerFluteFieldElapsed=0;_towerFluteTextDrawnFrame=ulong.MaxValue;}
        if(pending is null)return;
        _towerFluteFieldElapsed+=delta;
        if(_busy)return;
        // This bag renderer prints the full source message in one draw. Its callback
        // must follow that draw; no input press or MIDI end-of-track advances the task.
        if(pending.Phase==CampaignPokeFlutePhase.PlayedText&&_towerFluteTextDrawnFrame!=ulong.MaxValue&&Engine.GetProcessFrames()>_towerFluteTextDrawnFrame)
            Apply(new("poke_flute_text_presented",Text:pending.WaitId));
        else if(pending.Phase==CampaignPokeFlutePhase.Fanfare&&_towerFluteFieldElapsed>=450/SourceAudioFramesPerSecond&&TowerFluteAudioFinished)
            Apply(new("poke_flute_presented",Text:pending.WaitId));
    }
    private bool TowerFluteAudioFinished=>_sourceAudio is null||_audioFaulted||_sourceAudio.Snapshot().FanfareToken is null;
    private bool ProcessPokemonTowerFluteBattle(CampaignWildBattle b)
    {
        if(WildCurrentEvent(b) is not WildBattlePokeFluteEvent e)return false;
        if(e.Phase==PokeFluteBattlePhase.Played)
        {
            if(!_busy&&_towerFluteBattleDrawnSequence==e.Sequence&&Engine.GetProcessFrames()>_towerFluteBattleDrawnFrame)Apply(new("ack"));
            return true;
        }
        if(e.Phase!=PokeFluteBattlePhase.Fanfare)return false;
        if(!_busy&&_wildElapsed>=450/SourceAudioFramesPerSecond&&TowerFluteAudioFinished)Apply(new("ack"));
        return true;
    }
    private void NotePokemonTowerFluteBattle(CampaignWildBattle b)
    {
        if(WildCurrentEvent(b) is WildBattlePokeFluteEvent {Phase:PokeFluteBattlePhase.Played} e)
        { _towerFluteBattleDrawnSequence=e.Sequence;_towerFluteBattleDrawnFrame=Engine.GetProcessFrames(); }
    }
    private bool PokemonTowerSilentEvent(CampaignWildBattle b)=>WildCurrentEvent(b) is {} e&&e is not WildBattlePokeFluteEvent&&PokemonTowerWildMessage(e) is {Symbol.Length:0};
    private ForestMessage? PokemonTowerWildMessage(WildBattleEvent current)=>!_catalog.PokemonTowerEnabled?null:current switch
    {
        WildBattleTowerGhostActionEvent {StringId:"STRINGID_MONTOOSCAREDTOMOVE"} e=>new("sText_TooScaredToMove",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(e.Side))),
        WildBattleTowerGhostActionEvent {StringId:"STRINGID_GHOSTGETOUTGETOUT"}=>new("sText_GetOutGetOut"),
        WildBattleTowerBallDodgedEvent=>new("sText_ItDodgedBall"),
        WildBattlePokeFluteEvent {Phase:PokeFluteBattlePhase.NoEffect}=>new("sText_PlayedFluteCatchyTune",("B_LAST_ITEM","POKé FLUTE")),
        WildBattlePokeFluteEvent {Phase:PokeFluteBattlePhase.Played}=>new("sText_PlayedThe",("B_LAST_ITEM","POKé FLUTE")),
        WildBattlePokeFluteEvent {Phase:PokeFluteBattlePhase.Fanfare}=>new("sText_PlayedThe",("B_LAST_ITEM","POKé FLUTE")),
        WildBattlePokeFluteEvent {Phase:PokeFluteBattlePhase.Awakened}=>new("sText_PkmnHearingFluteAwoke"),
        WildBattleSpiteEvent e=>new("sText_PkmnReducedPP",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(e.Target)),("B_BUFF1",MoveName(e.Move)),("B_BUFF2",(e.PpBefore-e.PpAfter).ToString())),
        WildBattleTowerVolatileEvent {Applied:false}=>new(""),
        WildBattleTowerVolatileEvent {Effect:"curse"}=>new(""),
        WildBattleTowerVolatileEvent {Effect:"mean-look"} e=>new("sText_TargetCantEscapeNow",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(e.Side))),
        WildBattleTowerVolatileEvent {Effect:"nightmare"} e=>new("sText_PkmnFellIntoNightmare",("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(e.Side))),
        WildBattleTowerVolatileEvent {Effect:"destiny-bond"} e=>new("sText_PkmnTryingToTakeFoe",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(e.Side))),
        WildBattleTowerHpEvent {Effect:"curse-cost"} e=>new("sText_PkmnLaidCurse",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(e.Side)),("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(Opposite(e.Side)))),
        WildBattleTowerHpEvent {Effect:"curse"} e=>new("sText_PkmnAfflictedByCurse",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(e.Side))),
        WildBattleTowerHpEvent {Effect:"nightmare"} e=>new("sText_PkmnLockedInNightmare",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(e.Side))),
        WildBattleTowerHpEvent {Effect:"destiny-bond"} e=>new("sText_PkmnTookFoe",("B_ATK_NAME_WITH_PREFIX",WildPrefixedName(e.Side)),("B_DEF_NAME_WITH_PREFIX",WildPrefixedName(Opposite(e.Side)))),
        WildBattleMoveFailedEvent {Reason:"target-awake" or "no-reducible-last-move" or "stat-limits" or "semi-invulnerable" or "already-cursed" or "already-trapped" or "already-nightmare"}=>new("sText_ButItFailed"),
        _=>null
    };
}
