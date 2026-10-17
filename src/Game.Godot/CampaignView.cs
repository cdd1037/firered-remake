using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.World;
using Godot;
using Direction=Game.Domain.World.Direction;

/// <summary>Godot presentation for the verified source-driven opening. Domain commands remain authoritative.</summary>
public partial class CampaignView:Node2D
{
    private sealed record SpriteAsset(Texture2D Texture,int Width,int Height,int Count,Vector2 Anchor,bool Inanimate);
    private sealed record BorderArt(int Width,int Height,Texture2D Bottom,Texture2D Middle,Texture2D Top);
    private sealed record MapArt(PretWorldMap Source,Texture2D Bottom,Texture2D Middle,Texture2D Top,SpriteAsset Player,Dictionary<string,SpriteAsset> Npcs,BorderArt? Border);
    private sealed class Pose{public Vector2 Position;public Direction Facing;public bool Visible=true;public int Frame;public int DrawYOffset;}
    private sealed record Clip(Vector2 From,Vector2 To,Direction Facing,double Duration,bool Visible,string Action);
    private sealed class Track{public required string Actor;public required List<Clip> Clips;public double Time;public double Duration=>Clips.Sum(c=>c.Duration);}
    private PretCampaignCatalog _catalog=null!;
    private PretPresentationCues _cues=null!;
    private CampaignSession _session=null!;
    private readonly CampaignResourceCache _mapResources=new();
    private CampaignResourceCache.MapCache<MapArt>? _maps;
    private CampaignResourceCache.Owner? _sceneTextures;
    private readonly HashSet<string> _drawMapPins=new(StringComparer.Ordinal);
    private CampaignResourceCache.MapCache<MapArt> MapCache=>_maps??=new(_mapResources);
    private CampaignResourceCache.Owner SceneTextures=>_sceneTextures??=_mapResources.CreateOwner("scene");
    private IReadOnlyDictionary<MapId,PretMapPresentationBinding>? _mapPresentation;
    private readonly Dictionary<string,Pose> _poses=new(StringComparer.Ordinal);
    private readonly List<Track> _tracks=[];
    private readonly Dictionary<int,Texture2D> _portraits=[];
    private LineEdit _nicknameEdit=null!;
    private Font _font=null!;
    private SourceGameFont _sourceNormal=null!,_sourceSmall=null!,_sourceWhite=null!,_sourceSmallWhite=null!,_sourceInactiveCursor=null!;
    private OpeningItemUiSkin _itemSkin=null!;
    private SourceUiCorrections _uiCorrections=null!;
    private SourceTextCursor _textCursor=null!;
    private MapArt _art=null!;
    private Direction? _held;
    private double _repeat,_waitElapsed,_waitDuration,_fade,_fanfareRemaining;
    private long _lastFanfareSequence;
    private bool _busy,_choiceYes=true;
    private bool FieldPresentationInputLocked=>SeafoamFieldLocked||SixIslandFieldLocked||_session?.State.FourIslandMovement is not null||FirstSeviiInputLocked||CinnabarInputLocked||SilphInputLocked||PokemonTowerInputLocked||RocketInputLocked||DepartmentElevatorAnimating||SsAnneDepartureInputLocked||_session?.State.CutEffect is not null||_session?.State.RockSmashEffect is not null||_session?.State.FlashEffect is not null||_session?.State.StrengthEffect is not null||_session?.State.SurfEffect is not null||_session?.State.FlyEffect?.Phase is "out" or "in";
    // Cut's MSGBOX_YESNO uses new_menu_helpers.c's default (21,9), 6x4-tile window.
    // CreateYesNoMenu prints at interior y+2 with FONT_NORMAL's 14-pixel row pitch.
    // Keep other field/script choices at their existing positions.
    private bool FieldCutChoice=>_session.State.Script.Wait?.Kind==StoryWaitKind.Choice&&
        (_session.State.Dialogue?.TextSymbol=="Text_CutTreeDown"||
         _session.State.Dialogue?.TextSymbol=="Text_UseStrength"||
         _session.State.Dialogue?.TextSymbol=="Text_WantToSurf"||
         _session.State.Dialogue?.TextSymbol=="Text_UseWaterfall");
    private int FieldChoiceFrameX=>FieldCutChoice?160:176;
    private int FieldChoiceTextY=>FieldCutChoice?74:73;
    private int FieldChoiceRowHeight=>FieldCutChoice?14:16;
    private int FieldChoiceHitY=>FieldCutChoice?74:72;
    private long _sequence;
    private string _root="",_evidence="",_status="方向键移动 · Enter对话 · F5保存 / F9读取";
    private string? _waitId;
    private string? _blockedAutomaticWait;
    private bool _lastCommandFault;
    private bool _quitRequested;
    private bool _startupReady, _startupExited, _loadingDrawn;
    private string _startupCheckpoint="";
    private Task<CatalogLoadResult>? _catalogLoad;
    private sealed record CatalogLoadResult(PretCampaignCatalog? Catalog,Exception? Error);

    // This worker captures only a path. It never owns a node, session or Godot resource,
    // and returns failures as data so an abandoned load cannot fault unobserved.
    private static Task<CatalogLoadResult> StartCatalogLoad(string root)=>Task.Run(()=>
    {
        try{return new CatalogLoadResult(PretCampaignCatalog.LoadCurrent(root),null);}
        catch(Exception error){return new CatalogLoadResult(null,error);}
    });
    private CampaignDiagnostics _diagnostics = new(null, null);
    private StoryWaitKind? _waitKind;
    public GameStartupSelection? Startup { private get; set; }
    public override void _Ready()
    {
        try
        {
            GetTree().AutoAcceptQuit=false;
            Input.JoyConnectionChanged+=OnCampaignJoyConnectionChanged;
            if(Engine.PhysicsTicksPerSecond!=60)throw new InvalidOperationException("play_time_requires_60_physics_ticks");
            var startup=Startup??throw new InvalidOperationException("current_startup_selection_missing");
            if(startup.Mode!=GameStartupMode.Current)throw new InvalidOperationException("current_startup_mode_invalid");
            _root=startup.ProjectRoot;_evidence=OS.GetEnvironment("FIRERED_EVIDENCE");
            _diagnostics=new(_evidence,OS.GetEnvironment("FIRERED_VERBOSE"));
            _font=GD.Load<FontFile>("res://Assets/ProbeFont.ttc");
            if(_font is null)throw new InvalidDataException("startup_font_missing");
            _startupCheckpoint=startup.Checkpoint;
            _catalogLoad=StartCatalogLoad(_root);
            QueueRedraw();
        }
        catch(Exception error){FailStartup(error);}
    }
    private void FailStartup(Exception error)
    {
        _quitRequested=true;_catalogLoad=null;
        SetProcess(false);SetPhysicsProcess(false);SetProcessUnhandledInput(false);
        GD.PushError(error.ToString());GetTree().Quit(21);
    }
    private void ProcessStartup()
    {
        QueueRedraw();
        // Yield a submitted loading frame even when an already-warm catalog finishes quickly.
        if(!_loadingDrawn||_catalogLoad is not {IsCompleted:true} pending)return;
        _catalogLoad=null;
        var result=pending.GetAwaiter().GetResult();
        if(result.Error is {} loadError){FailStartup(loadError);return;}
        _catalog=result.Catalog!;
        try
        {
            // Godot resource creation and all session/presentation work stay on this thread.
            var fontDirectory=System.IO.Path.Combine(_root,"content/generated/ui/font");
            _sourceNormal=SourceGameFont.Load(fontDirectory,colorProfile:SourceGameFont.ColorProfile.MgbaBitReplicated);_sourceSmall=SourceGameFont.Load(fontDirectory,"small",colorProfile:SourceGameFont.ColorProfile.MgbaBitReplicated);
            _sourceWhite=SourceGameFont.Load(fontDirectory,foreground:Colors.White,shadow:new Color("636363"));_sourceSmallWhite=SourceGameFont.Load(fontDirectory,"small",Colors.White,new Color("636363"));
            _sourceInactiveCursor=SourceGameFont.Load(fontDirectory,foreground:OpeningItemUiSkin.MenuTextShadow,shadow:OpeningItemUiSkin.MenuTextColor);
            _uiCorrections=SourceUiCorrections.Load(System.IO.Path.Combine(_root,"content/generated/ui/ui-corrections"));
            _itemSkin=OpeningItemUiSkin.Load(System.IO.Path.Combine(_root,"content/generated/ui/item-ui"));_textCursor=SourceTextCursor.Load(System.IO.Path.Combine(_root,"content/generated/ui/text-cursor"));
            _session=CampaignSession.CreateCurrent(_catalog,new(0x11223344));
            LoadHelpPresentation();
            LoadShopPresentation();LoadCenterHealingPresentation();LoadOldManPresentation();LoadQuestPocketPresentation();LoadPortraits();LoadMuseumPicturesPresentation();LoadBattlePresentation();LoadWildBattlePresentation();LoadForestTrainerPresentation();LoadStoragePresentation();LoadPokedexMenuPresentation();LoadTrainerCardPresentation();LoadOptionsPresentation();LoadMtMoonPresentation();LoadCeruleanPresentation();LoadBillCreaturePresentation();LoadBillPresentation();LoadVermilionPresentation();LoadSsAnnePresentation();LoadVermilionGymPresentation();LoadCityServicesPresentation();LoadRoute11Presentation();LoadRockTunnelPresentation();LoadCeladonApproachPresentation();LoadCeladonGymPresentation();LoadRocketHideoutPresentation();LoadPokemonTowerPresentation();LoadSnorlaxPresentation();LoadRegionalServicesPresentation();LoadFuchsiaRoutePresentation();LoadFuchsiaGymPresentation();LoadSafariPresentation();LoadFieldStrengthPresentation();LoadSurfPresentation();LoadSilphPresentation();LoadSaffronGymPresentation();LoadCinnabarPresentation();LoadFirstSeviiPresentation();LoadViridianGymPresentation();LoadVictoryRoadPresentation();LoadPokemonLeaguePresentation();LoadSeviiRubyPresentation();LoadFourIslandPresentation();LoadSixIslandPresentation();LoadFiveIslandPresentation();LoadCelioPresentation();LoadMewtwoPresentation();LoadLeagueRematchPresentation();LoadSevenIslandPresentation();LoadEventIslandPresentation();LoadPowerPlantPresentation();LoadTrainerTowerPresentation();LoadFightingDojoPresentation();LoadFossilLabPresentation();LoadSeafoamPresentation();LoadQwilfishPresentation();LoadStoneEvolutionPresentation();LoadGameCornerPresentation();LoadBreedingPresentation();LoadOrdinaryEvolutionPresentation();LoadMemorialPillarPresentation();LoadGiftEggPresentation();LoadPokemonCollectionPresentation();LoadSharedTrainerPresentation();LoadDepartmentMachineIcon();LoadRooftopIcons();LoadGameCornerPrizeIcons();LoadFieldFlyIcon();LoadLostCaveItemIcons();if(_catalog.FieldFly is not null)LoadFieldFlyTravelPresentation();_cues=PretPresentationCues.Load(_root);
            _mapTileArt=CreateMapTileArt();
            _nicknameEdit=new LineEdit{Position=new(20,62),Size=new(200,22),MaxLength=10,Visible=false,PlaceholderText="",CaretBlink=true,KeepEditingOnTextSubmit=true};
            _nicknameEdit.AddThemeFontOverride("font",_font);_nicknameEdit.AddThemeFontSizeOverride("font_size",11);
            _nicknameEdit.TextSubmitted+=SubmitCampaignNickname;_nicknameEdit.GuiInput+=NameRaterNicknameGuiInput;_nicknameEdit.GuiInput+=GiftNicknameGuiInput;_nicknameEdit.GuiInput+=EggNicknameGuiInput;_nicknameEdit.GuiInput+=StorageBoxNameGuiInput;AddChild(_nicknameEdit);
            var checkpoint=_startupCheckpoint;
            if(checkpoint.Length>0)
            {
                var saved=new SaveService(new LocalSaveStorage()).Load<CampaignState>(checkpoint,_session.RulesVersion,_catalog.ContentVersion,_session.ValidatePlayerSave);
                _session=CampaignSession.RestoreCurrent(_catalog,saved.State,saved.Generation);_sequence=saved.Generation;
                Write("checkpointLoaded",_diagnostics.Payload(new{saved.Source,saved.Generation},()=>new{saved.Source,saved.Generation,hash=CanonicalJson.Hash(saved.State)}));
            }
            LoadSourceAudio();LoadMap(_session.State.World.Player.MapId);if(!InitializePlayerEntry(checkpoint))Synchronize(null,_session.State);
            _startupReady=true;ResetPlayTimeHost();
            if (_diagnostics.Verbose) Write("presentationResources", new { nativePngDecodes = NativeImages.DecodedPngs, cache = _mapResources.Snapshot });
            Write("ready",_diagnostics.Payload(new{_catalog.ContentVersion,_session.Revision},()=>new{scope="source-driven opening and starter gift/nickname; first lab battle semantic profile; original menu art, animation, audio and exact timing incomplete",_catalog.ContentVersion,state=_session.State}));
        }
        catch(Exception error){FailStartup(error);}
    }
    private Texture2D Texture(string path,bool darkened=false)=>SceneTextures.Texture(path,darkened);
    // These resources are constructed directly, not borrowed from SceneTextures.
    private void DisposeCorePresentation()
    {
        DisposeInventoryPresentation();
        DisposeTrainerCardPresentation();
        _leagueCreditHeader?.Dispose(); _leagueCreditHeader=null!;
        DisposePokedexVisualPresentation();
        DisposeEvolutionScenePresentation();
        DisposeNpcTradePresentation();
        ResetSummaryPresentation();
        _sourceNormal?.Dispose(); _sourceSmall?.Dispose(); _sourceWhite?.Dispose();
        _sourceSmallWhite?.Dispose(); _sourceInactiveCursor?.Dispose();
        _sourceNormal=_sourceSmall=_sourceWhite=_sourceSmallWhite=_sourceInactiveCursor=null!;
        _itemSkin?.Dispose(); _itemSkin=null!;
        _uiCorrections?.Dispose(); _uiCorrections=null!;
        _textCursor?.Dispose(); _textCursor=null!;
    }
    private void DisposeMapResources()
    {
        _cutGrassParticle?.Dispose();_cutGrassParticle=null;_mapTileArt=null;ReleaseMapEdits();_drawMapEditOwner?.Dispose();_drawMapEditOwner=null;
        _maps?.Dispose();_mapResources.Dispose();_drawMapPins.Clear();_art=null!;
    }
    private void LoadPortraits()
    {
        var directory=System.IO.Path.Combine(_root,"content/generated/sprites/trainers/starter");var bytes=System.IO.File.ReadAllBytes(System.IO.Path.Combine(directory,"starter-art.json"));
        using var document=JsonDocument.Parse(bytes);
        foreach(var entry in document.RootElement.GetProperty("sprites").EnumerateArray())
        {
            var file=entry.GetProperty("texturePath").GetString()!;if(System.IO.Path.GetFileName(file)!=file)throw new InvalidDataException("Portrait path invalid.");
            var path=System.IO.Path.Combine(directory,file);var texture=Texture(path);if(texture.GetWidth()!=64||texture.GetHeight()!=64)throw new InvalidDataException("Portrait dimensions invalid.");
            _portraits.Add(entry.GetProperty("sourceSpeciesId").GetInt32(),texture);
        }
    }
    private MapArt GetMapArt(MapId id)
    {
        return MapCache.Get(PresentationMapKey(id),owner=>
        {
            var source=PresentationSourceMap(id);
            Texture2D MapTexture(string path)=>owner.MapTexture(path);
            var npcs=new Dictionary<string,SpriteAsset>(StringComparer.Ordinal);
            var binding=(_mapPresentation??=PretCampaignMapPresentation.Create(_catalog))[id];
            npcs=LoadWorldNpcs(source, binding.OverworldCatalogPath, owner);
            BorderArt? borderArt=null;
            if(binding.UsesLocalBorder)borderArt=new(source.BorderWidth,source.BorderHeight,MapTexture(source.BorderBottomTexturePath),MapTexture(source.BorderMiddleTexturePath),MapTexture(source.BorderTopTexturePath));
            return new(source,MapTexture(source.BottomTexturePath),MapTexture(source.MiddleTexturePath),MapTexture(source.TopTexturePath),new(owner.Texture(source.PlayerTexturePath),16,32,9,new(8,24),false),npcs,borderArt);
        });
    }
    private void LoadMap(MapId id)
    {
        EnsurePlayerGenderArt();
        ResetFieldMenu();
        // Keep the last submitted canvas frame alive until _Draw clears/replaces it.
        MapCache.SetPins(_drawMapPins.Append(PresentationMapKey(id)).Concat(_art is null?[]:[PresentationMapKey(_art.Source.Id)]));
        var next=GetMapArt(id);ReleaseMapEdits();_art=next;
        MapCache.SetPins(_drawMapPins.Append(PresentationMapKey(id)));
        _poses.Clear();_tracks.Clear();_held=null;_repeat=0;_fade=.16;
    }
    private void Synchronize(CampaignState? before,CampaignState after)
    {
        if(BeginSilphPadPresentation(before,after))return;
        SynchronizeSourceResultState(before,after);
        SynchronizeSourceMovePresentation();
        bool changed=before is not null&&before.World.Player.MapId!=after.World.Player.MapId;
        if(changed)LoadMap(after.World.Player.MapId);
        ApplyMapEdits(after);
        // Intro page acknowledgements retain the same application motion list. Keep
        // its presentation progress and settled poses instead of starting the walk again.
        bool preserveIntroMotion=!changed&&before?.ForestTrainerBattle is {Stage:CampaignForestTrainerStage.Intro} previousIntro&&
            after.ForestTrainerBattle is {Stage:CampaignForestTrainerStage.Intro} currentIntro&&
            previousIntro.Id==currentIntro.Id&&before.Motions.SequenceEqual(after.Motions);
        bool normalStep=before is not null&&!changed&&(before.World.Player.X!=after.World.Player.X||before.World.Player.Y!=after.World.Player.Y)&&after.Motions.IsEmpty;
        if(!preserveIntroMotion)
        {
            _tracks.Clear();
            foreach(var actor in after.Actors.Values)
            {
                if(!_poses.TryGetValue(actor.LocalId,out var pose))_poses[actor.LocalId]=pose=new();
                pose.Position=new(actor.X*16,actor.Y*16);pose.Facing=actor.Facing;pose.Visible=actor.Visible;pose.Frame=0;pose.DrawYOffset=0;
            }
            if(!_poses.TryGetValue("LOCALID_PLAYER",out var player))_poses["LOCALID_PLAYER"]=player=new();
            player.Position=new(after.World.Player.X*16,after.World.Player.Y*16);player.Facing=after.World.Player.Facing;player.Visible=after.PlayerVisible;player.DrawYOffset=0;
            if(normalStep)
            {
                var from=new Vector2(before!.World.Player.X*16,before.World.Player.Y*16);var to=player.Position;player.Position=from;
                bool ledge=IsFuchsiaLedgeStep(before,after);
                if(after.IsCycling&&!ledge)BeginCityCycleStep();
                _tracks.Add(new(){Actor="LOCALID_PLAYER",Clips=[new(from,to,after.World.Player.Facing,(ledge?CampaignFieldMovementTiming.LedgeFrames:_fieldStepTicks)/60.0,true,ledge?"ledge-jump":after.IsCycling?(_fieldStepTicks==6?"cycle":"cycle-stairs"):_fieldStepAction)]});
            }
            foreach(var motion in after.Motions)
            {
                var clips=new List<Clip>();int index=0;var current=motion.Path[0];bool visible=motion.ActorId=="LOCALID_PLAYER"?(before?.PlayerVisible??true):(before?.Actors.GetValueOrDefault(motion.ActorId)?.Visible??after.Actors.GetValueOrDefault(motion.ActorId)?.Visible??true);
                foreach(var action in motion.Actions)
                {
                    if(action=="step_end")break;
                    if(action is "lock_facing_direction" or "unlock_facing_direction" or "disable_anim" or "restore_anim")
                    {clips.Add(new(new(current.X*16,current.Y*16),new(current.X*16,current.Y*16),current.Facing,1.0/60,visible,action));continue;}
                    if(action is "set_invisible" or "set_visible"){visible=action=="set_visible";clips.Add(new(new(current.X*16,current.Y*16),new(current.X*16,current.Y*16),current.Facing,1.0/60,visible,action));continue;}
                    if(action.StartsWith("delay_",StringComparison.Ordinal)||action.StartsWith("emote_",StringComparison.Ordinal))
                    {double time=action.StartsWith("delay_",StringComparison.Ordinal)&&int.TryParse(action[6..],out var frames)?frames/60.0:.5;clips.Add(new(new(current.X*16,current.Y*16),new(current.X*16,current.Y*16),current.Facing,time,visible,action));continue;}
                    if(action=="face_player"){clips.Add(new(new(current.X*16,current.Y*16),new(current.X*16,current.Y*16),after.Actors.GetValueOrDefault(motion.ActorId)?.Facing??current.Facing,1.0/60,visible,action));continue;}
                    var next=motion.Path[Math.Min(++index,motion.Path.Length-1)];
                    double seconds=SeafoamMotionSeconds(after,motion.ActorId,action)??SixIslandMotionSeconds(after,motion.ActorId,action)??FourIslandMotionSeconds(after,motion.ActorId,action)??(CampaignFieldMovementTiming.SourceDuration(action) is {} sourceFrames?sourceFrames/60.0:action=="jump_2_down"?32.0/60:action.StartsWith("jump_special_",StringComparison.Ordinal)?FieldSurfSourcePresentation.JumpCueFrames/60.0:action=="cut_tree"?FieldCutSourcePresentation.TreeCutCueFrames/60.0:action=="rock_smash_break"?64.0/60:action=="face_original_direction"?1.0/60:action=="nurse_joy_bow"?48.0/60:action.Contains("fastest",StringComparison.Ordinal)?2.0/60:action.Contains("faster",StringComparison.Ordinal)?4.0/60:action.Contains("fast",StringComparison.Ordinal)||action.StartsWith("player_run_",StringComparison.Ordinal)?8.0/60:action.StartsWith("walk_slower_",StringComparison.Ordinal)?32.0/60:16.0/60);
                    clips.Add(new(new(current.X*16,current.Y*16),new(next.X*16,next.Y*16),next.Facing,seconds,visible,action));current=next;
                }
                if(clips.Count>0){_tracks.Add(new(){Actor=motion.ActorId,Clips=clips});if(!_poses.TryGetValue(motion.ActorId,out var pose))_poses[motion.ActorId]=pose=new();pose.Position=clips[0].From;pose.Facing=clips[0].Facing;pose.Visible=clips[0].Visible;}
            }
        }
        foreach(var effect in after.PresentationEffects.Where(e=>e.Opcode=="playfanfare"&&e.Sequence>_lastFanfareSequence))
        {_fanfareRemaining=(_cues.CounterFor(effect.Args[0])+1)/60.0;_lastFanfareSequence=effect.Sequence;}
        bool naming=(after.Nickname is {Submitted:false}&&after.Script.Wait?.Kind==StoryWaitKind.Nickname)||CaptureNamingActive||StorageBoxNaming;
        if(naming&&!_nicknameEdit.Visible){_nicknameEdit.MaxLength=StorageBoxNaming?8:10;_nicknameEdit.Text="";_nicknameEdit.Visible=true;_nicknameEdit.GrabFocus();_nicknameEdit.Edit();}
        else if(!naming&&_nicknameEdit.Visible){_nicknameEdit.Visible=false;_nicknameEdit.ReleaseFocus();if(after.Nickname?.Submitted==true||before?.WildBattle?.Capture?.Stage==CampaignCaptureStage.Naming||before?.SafariBattle?.Capture?.Stage==CampaignCaptureStage.Naming)_fade=16.0/60;}
        SynchronizePokemonTowerPresentation(before,after);SynchronizeBattle();SynchronizeWildBattle();SynchronizeDoubleBattle();SynchronizeSafariBattle();SynchronizeItems();SynchronizeStorage();SynchronizeMachines();SynchronizeShop();SynchronizeCenterHealing(before,after);SynchronizeOldManPresentation();
        SynchronizeNoncombatFieldPresentation(after);SynchronizeEvolutionView(before);SynchronizeItemfinderPresentation();SynchronizeSavedOptions();SynchronizeTrainerTowerSpeech();SynchronizeFieldText();SynchronizeSourceAudio(before,after);SynchronizeRepelPresentation();SynchronizeTownMapPresentation();SynchronizeBillPresentation(before,after);SynchronizeFossilLabPresentation();SynchronizeSourceGridMenu();SynchronizeSourceMenu();SynchronizeSsAnnePresentation(before,after);SynchronizeFieldCutPresentation(before,after);SynchronizeFieldRockSmashPresentation(after);SynchronizeFieldFlashPresentation(after);SynchronizeFieldStrengthPresentation(after);SynchronizeFieldSurfPresentation(before,after);SynchronizeCityServicesPresentation(before,after);SynchronizeRocketPresentation(before,after);SynchronizeDepartmentPresentation(before,after);SynchronizeRooftopMenu();SynchronizeGameCornerCoinMenu();SynchronizeGameCornerPrizeMenu();SynchronizeSilphPresentation(before,after);SynchronizeCinnabarPresentation(before,after);SynchronizeFirstSeviiPresentation(before,after);SynchronizeSeagallopMenu();SynchronizeTrainerTowerMenu();SynchronizeDaisyGroomingPresentation(before,after);SynchronizeSizeRecordPresentation(before,after);SynchronizeNameRaterPresentation(before,after);SynchronizeNpcTradePresentation(before,after);SynchronizeMoveTutorPresentation(before,after);SynchronizePokemonLeaguePresentation(before,after);SynchronizeFourIslandPresentation(after);SynchronizeSixIslandPresentation(after);SynchronizeSeafoamPresentation(after);SynchronizeSevenIslandFieldPresentation(before,after);SynchronizeEventIslandFieldPresentation(before,after);SynchronizeFieldFlyTravelPresentation(after);SynchronizeEggHatchPresentation(before,after);SynchronizeSlotMachine();SynchronizeFameChecker();SynchronizeVsSeekerPresentation(before,after);SynchronizeTeachyTvPresentation();SynchronizeTeachyTvFieldPresentation();SynchronizeTeachyTvBattle();
        SynchronizeTrainerCardPhoto(before,after);
        SynchronizeDiplomaPresentation(before,after);
        var wait=after.Script.Wait;
        if(wait?.CorrelationId!=_waitId||wait?.Kind!=_waitKind||before?.Dialogue?.Page!=after.Dialogue?.Page)
        {
            _waitId=wait?.CorrelationId;_waitKind=wait?.Kind;_waitElapsed=0;_choiceYes=true;
            var movementTime=_tracks.Count==0?0:_tracks.Max(t=>preserveIntroMotion?Math.Max(0,t.Duration-t.Time):t.Duration);
            _waitDuration=normalStep?movementTime+after.PresentationTicks/60.0:Math.Max(movementTime,after.PresentationTicks/60.0);
            if(after.HealingEffect is not null)_waitDuration=Math.Max(_waitDuration,CenterHealingDuration);
            if(wait?.Kind==StoryWaitKind.Presentation&&after.PresentationEffects.Any(e=>e.Opcode=="waitfanfare"))
            {
                // Source counter reaches zero, then its task completes. No active cue means the task is already inactive.
                _waitDuration=Math.Max(_waitDuration,_fanfareRemaining);
            }
            if(wait?.Kind is StoryWaitKind.Presentation or StoryWaitKind.MessagePrinting)_waitDuration=Math.Max(_waitDuration,.08);
        }
    }
    public override void _Process(double delta)
    {
        if(_quitRequested||_startupExited)return;
        if(!_startupReady){ProcessStartup();return;}
        if(TickQuestLogPlayback(delta))return;
        if(!PlayerEntryActive&&!CommitPlayTime(PlayTimeDisplayNeedsFlush)){QueueRedraw();return;}
        if(ProcessHelpPresentation(delta))return;
        AdvanceInventoryPresentation(delta);
        ProcessCampaignButtonRepeats(delta);
        if(PlayerEntryActive){ProcessOpeningPresentation(delta);QueueRedraw();return;}
        if(ProcessSummaryPresentation(delta))return;
        ProcessTrainerCardPresentation(delta);
        ProcessTrainerTowerTime(delta);
        if(_session.State.ItemMenu?.Toss?.BattleOwner is not null){QueueRedraw();return;}
        if(ProcessDiplomaPresentation(delta))return;
        if(ProcessTeachyTvBattle(delta))return;
        if(ProcessTeachyTvFieldPresentation(delta))return;
        if(ProcessTeachyTvPresentation(delta))return;
        if(ProcessVsSeekerPresentation(delta))return;
        if(ProcessSlotMachine(delta))return;
        if(ProcessFameChecker(delta))return;
        if(_session.NationalDexScenePending&&!_lastCommandFault){Apply(new("national_dex_resume"));QueueRedraw();return;}
        if(ProcessPokemonLeaguePresentation(delta)){QueueRedraw();return;}
        if(ProcessFirstSeviiPresentation(delta))return;
        if(ProcessCinnabarPresentation(delta))return;
        if(ProcessSilphPresentation(delta))return;
        if(ProcessCommittedMoveAnimation(delta))return;
        ProcessPokemonTowerPresentation(delta);ProcessSourceBattleAudio();ProcessBillPresentation(delta);ProcessSsAnnePresentation(delta);
        if(ProcessTownMapPresentation(delta)){QueueRedraw();return;}
        if(_session.State.TrainerTowerRecordsVisible){_held=null;QueueRedraw();return;}
        if(_session.State.Itemfinder is not null){_held=null;ProcessItemfinderPresentation(delta);QueueRedraw();return;}
        if(FieldMenuOpen){ProcessFieldHealingPresentation(delta);ProcessFieldSurfPresentation(delta);QueueRedraw();return;}
        ProcessCenterHealing(delta);
        _fanfareRemaining=Math.Max(0,_fanfareRemaining-delta);
        if(_fade>0){_fade=Math.Max(0,_fade-delta);QueueRedraw();return;}
        if(MoveTutorActive||DaisyGroomingChoosing||SizeRecordChoosing||NameRaterChoosing||NpcTradeChoosing||_regionalServiceNotice is not null){QueueRedraw();return;}
        if(ProcessRocketSpin(delta)){QueueRedraw();return;}
        if(ProcessFishingPresentation(delta)){QueueRedraw();return;}
        if(ProcessRegionalSignWalkaway(delta)){QueueRedraw();return;}
        ProcessFieldText(delta);if(ProcessEggHatchPresentation(delta))return;ProcessFieldCutPresentation(delta);ProcessFieldRockSmashPresentation(delta);ProcessRocketPresentation(delta);ProcessDepartmentPresentation(delta);
        if(ProcessFieldFlashPresentation(delta)){QueueRedraw();return;}
        if(ProcessRepelPresentation(delta)){QueueRedraw();return;}
        if(_session.State.OldManTutorial is not null){ProcessOldManPresentation(delta);QueueRedraw();return;}
        if(_session.State.RareCandy is not null){QueueRedraw();return;}
        if(_session.State.Evolution is not null){ProcessEvolutionPresentation(delta);QueueRedraw();return;}
        if(_session.State.SafariBattle is not null){ProcessSafariBattle(delta);QueueRedraw();return;}
        if(DoubleBattleVisible){ProcessDoubleBattle(delta);QueueRedraw();return;}
        if(ForestTrainerCombatVisible||_session.State.WildBattle is not null){ProcessWildBattle(delta);QueueRedraw();return;}
        if(_session.State.Battle is not null){ProcessBattle(delta);QueueRedraw();return;}
        if(_session.State.Pc is not null||_session.State.Shop is not null||_session.State.ItemMenu is not null){QueueRedraw();return;}
        foreach(var track in _tracks)
        {
            track.Time+=delta;double cursor=track.Time;var clip=track.Clips[^1];
            foreach(var candidate in track.Clips){clip=candidate;if(cursor<=candidate.Duration)break;cursor-=candidate.Duration;}
            // Original in-place jumps use the same high arc as ledges, over 16 frames instead of 32.
            var pose=_poses[track.Actor];pose.Position=clip.From.Lerp(clip.To,SeafoamStepFraction(track.Actor,clip.Action,cursor)??SixIslandStepFraction(track.Actor,clip.Action,cursor)??FourIslandStepFraction(track.Actor,clip.Action,cursor)??(clip.Action=="cycle"?CityCycleStepFraction(cursor):(clip.Action.StartsWith("walk_slower_",StringComparison.Ordinal)||clip.Action.StartsWith("jump_special_",StringComparison.Ordinal))?FieldSlowerStepFraction(cursor):(float)CampaignFieldMovementTiming.Fraction(clip.Action,(int)Math.Round(clip.Duration*60),cursor*60)));pose.Facing=clip.Facing;pose.Visible=clip.Visible;pose.DrawYOffset=clip.Action.StartsWith("jump_special_",StringComparison.Ordinal)?FieldSurfJumpYOffset(cursor):clip.Action.StartsWith("jump_in_place_",StringComparison.Ordinal)?FuchsiaLedgeDrawYOffset(cursor*2):clip.Action is "ledge-jump" or "jump_2_down"?FuchsiaLedgeDrawYOffset(cursor):0;
            bool moving=clip.From!=clip.To||clip.Action.StartsWith("walk_in_place",StringComparison.Ordinal);
            pose.Frame=clip.Action=="nurse_joy_bow"?(cursor>=8.0/60&&cursor<40.0/60?-9:0):moving?((int)(track.Time*60/(clip.Action is "cycle" or "cycle-stairs"?4:8))+(clip.Action is "cycle" or "cycle-stairs"?_cityCycleStepPhase:0))%4+1:0;
            if(clip.Action is "disable_anim" or "slide_down" or "restore_anim")pose.Frame=0;
            if(track.Time>=track.Duration||clip.Action is "cycle" or "cycle-stairs"&&track.Time+1e-9>=track.Duration){track.Time=track.Duration;pose.Position=clip.To;pose.Frame=0;pose.DrawYOffset=0;}
        }
        if(ProcessFieldFlyTravelPresentation(delta)){QueueRedraw();return;}
        if(ProcessNoncombatFieldPresentation(delta)){QueueRedraw();return;}
        ProcessSevenIslandFieldPresentation(delta);ProcessEventIslandFieldPresentation(delta);
        if(ProcessSeafoamCurrentPresentation(delta)){QueueRedraw();return;}
        if(ProcessSixIslandPresentation(delta)){QueueRedraw();return;}
        if(ProcessFourIslandPresentation(delta)){QueueRedraw();return;}
        if(ProcessFieldStrengthPresentation(delta)){QueueRedraw();return;}
        if(ProcessFieldSurfPresentation(delta)){QueueRedraw();return;}
        _waitElapsed+=delta;
        if(NpcTradePresenting){ProcessNpcTradeSceneAudio();QueueRedraw();return;}
        if(_session.State.Repel?.Phase==CampaignRepelPhase.WoreOff){QueueRedraw();return;}
        if(!_busy&&_session.State.Script.Wait is {} wait&&wait.CorrelationId!=_blockedAutomaticWait&&(FieldCutVisualComplete||FieldRockSmashVisualComplete||_waitElapsed>=_waitDuration&&_tracks.All(t=>t.Time>=t.Duration)))
        {
            bool rendered=FieldTextComplete&&wait.Kind==StoryWaitKind.MessagePrinting&&(_session.State.Dialogue is null||_session.State.Dialogue.Page+1==_session.State.Dialogue.Pages.Length);
            if((wait.Kind==StoryWaitKind.Presentation||rendered)&&SourceAudioWaitReady()){Apply(new("ack"));QueueRedraw();return;}
        }
        if(ProcessCyclingRoadPull(delta)){QueueRedraw();return;}
        ObserveQuestLogFieldStep();
        if(_held is {} direction&&_session.State.Script.Completed&&_session.State.Script.Wait is null)
        {_repeat+=delta;if(_repeat>=FieldStepTicksForDirection(_session.State,direction)/60.0&&_tracks.All(t=>t.Time>=t.Duration)){_repeat=0;Apply(new("move",direction));}}
        QueueRedraw();
    }
    private async void RequestQuit()
    {
        if(_quitRequested||_startupExited)return;
        if(!_startupReady)
        {
            _quitRequested=true;_catalogLoad=null;
            SetProcess(false);SetPhysicsProcess(false);SetProcessUnhandledInput(false);
            GetTree().Quit();return;
        }
        DisposeHelpPresentation();
        ResetSourceMoveAnimation("quit");
        CommitPlayTime(true);
        _quitRequested=true;_held=null;_repeat=0;ResetPlayTimeHost();
        SetPhysicsProcess(false);
        SetProcess(false);SetProcessUnhandledInput(false);
        var tree=GetTree();
        if(_sourceAudio is not null)
        {
            _sourceAudio.ShutdownPlayback();
            // AudioServer retires stopped playback on its next mix. _ExitTree is too late
            // during engine shutdown, so allow real time and a normal frame before quitting.
            ulong stoppedAt=Time.GetTicksUsec();
            using var timer=tree.CreateTimer(.05,ignoreTimeScale:true);
            await ToSignal(timer,SceneTreeTimer.SignalName.Timeout);
            double elapsed;
            do
            {
                await ToSignal(tree,SceneTree.SignalName.ProcessFrame);
                elapsed=(Time.GetTicksUsec()-stoppedAt)/1_000_000d;
            }
            while(elapsed<.05||(elapsed<.5&&AudioServer.GetTimeSinceLastMix()>=elapsed));
            await ToSignal(tree,SceneTree.SignalName.ProcessFrame);
        }
        tree.Quit();
    }
    public override void _Notification(int what)
    {
        if(what==(int)NotificationWMCloseRequest){RequestQuit();return;}
        if(!_startupReady||_quitRequested||_startupExited)return;
        if(what==(int)NotificationWMWindowFocusOut || what==(int)NotificationApplicationFocusOut)
        {_slotHeld=0;_slotFrameTime=0;_runningShiftHeld=false;_held=null;_repeat=0;_townMapHeld=null;ResetMenuMouseInput();ReleaseFieldTextInput();ResetRegionalSignInput();ResetVsSeekerInput();ReleaseModalHeldInput();ReleaseDiplomaInput();ResetCampaignButtonInput();_storageRectangleAnchor=null;}
    }
    public override void _UnhandledInput(InputEvent input)
    {
        if(_quitRequested||_startupExited)return;
        if(!_startupReady)
        {
            if(input is InputEventKey {Pressed:true,Echo:false,Keycode:Key.Escape})RequestQuit();
            GetViewport().SetInputAsHandled();return;
        }
        if(TrySystemScreenshotInput(input))return;
        var physicalInput=input;
        if(!NormalizeCampaignButtonInput(ref input))return;
        using var normalizedInput=ReferenceEquals(physicalInput,input)?null:input;
        if(TryQuestLogInput(input))return;
        if(TryHelpInput(input))return;
        if(TryDiplomaInput(input))return;
        if(TrySlotMachineInput(input))return;
        if(TryTeachyTvBattleInput(input))return;
        if(TryTeachyTvFieldInput(input))return;
        if(TryTeachyTvInput(input))return;
        if(TryFameCheckerInput(input))return;
        if(EggHatchInput(input))return;
        if(TryVsSeekerInput(input))return;
        if(TryItemfinderInput(input))return;
        if(ConsumeMoveTutorTransitionInput(input)||ConsumeNameRaterTransitionInput(input)||ConsumeDaisyGroomingTransitionInput(input)||ConsumeSizeRecordTransitionInput(input)||ConsumeNpcTradeTransitionInput(input)||ConsumeStorageCustomizationTransitionInput(input)||TryRegionalServiceNoticeInput(input))return;
        if(_quitRequested)return;
        if(TryPokemonLeagueInput(input)){GetViewport().SetInputAsHandled();return;}
        if(TryNoncombatFieldInput(input))return;
        if(FieldPresentationInputLocked&&input is not InputEventKey {Keycode:Key.Escape or Key.F12}){_held=null;GetViewport().SetInputAsHandled();return;}
        if(TryFishingInput(input)){GetViewport().SetInputAsHandled();return;}
        if(HandleRunningShift(input)){GetViewport().SetInputAsHandled();return;}
        if(TryPlayerEntryInput(input))return;
        if(_townMapClosedFrame==Engine.GetProcessFrames()&&input is not InputEventKey {Keycode:Key.Escape or Key.F12 or Key.F5 or Key.F9}){GetViewport().SetInputAsHandled();return;}
        if(ConsumedRepelDismissalFrame(input)){GetViewport().SetInputAsHandled();return;}
        if(TryRegionalSignDirectionInput(input)){GetViewport().SetInputAsHandled();return;}
        if(TryMenuMouseInput(input))return;
        if(SilphElevatorMenuActive&&input is InputEventKey silphKey&&TrySilphInput(silphKey)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(SilphElevatorMenuActive&&input is not InputEventKey {Keycode:Key.Escape or Key.F12}){GetViewport().SetInputAsHandled();return;}
        if(TryFieldMenuToggle(input))return;
        if(_session is null||input is not InputEventKey key||key.Echo)return;
        if(_fade>0&&key.Pressed&&key.Keycode is not (Key.Escape or Key.F12))return;
        if(MoveTutorInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(NpcTradeInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(DaisyGroomingInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(SizeRecordInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(NameRaterInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(GiftNicknameInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryTrainerTowerInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TrySeagallopMenuInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryTownMapInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryRepelInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryFieldMenuInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.OldManTutorial is not null&&TryOldManInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.Shop is not null&&ShopInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.ItemMenu is not null&&BagTossInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.ItemMenu is not null&&MachineInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.ItemMenu is not null&&!_fieldMedicineTargetOpen&&TryQuestPocketInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.ItemMenu is not null&&ItemInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.Pc is not null&&StorageInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.RareCandy is not null&&RareCandyInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.Evolution is not null&&TryEvolutionInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.SafariBattle is not null&&SafariBattleInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.Evolution is null&&DoubleBattleVisible&&DoubleBattleInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.Evolution is null&&(ForestTrainerCombatVisible||_session.State.WildBattle is not null)&&WildBattleInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(_session.State.Battle is not null&&BattleInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(FieldTextInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryTrainerTowerSpeechInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(DoubleIntroInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TrySeviiRubyBrailleInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryFourIslandButtonInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryGameCornerPrizeInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryGameCornerCoinInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryRooftopInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryDepartmentInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryRocketInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TrySourceMenuInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TrySourceGridMenuInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryFossilLabInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryBillPcInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryOakRatingButtonInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(TryForestTrainerIntroInput(key)){QueueRedraw();GetViewport().SetInputAsHandled();return;}
        if(key.Pressed&&FieldTextComplete&&_session.State.Script.Wait?.Kind is StoryWaitKind.Choice or StoryWaitKind.MenuChoice&&_session.State.Dialogue?.Page+1==_session.State.Dialogue?.Pages.Length&&key.Keycode is Key.Up or Key.Down)
        {_choiceYes=_session.State.Script.Wait?.Kind==StoryWaitKind.MenuChoice?key.Keycode==Key.Up:!_choiceYes;_held=null;QueueRedraw();GetViewport().SetInputAsHandled();return;}
        var d=key.Keycode switch{Key.Up=>(Direction?)Direction.North,Key.Down=>Direction.South,Key.Left=>Direction.West,Key.Right=>Direction.East,_=>null};
        if(d is {} direction)
        {
            if(key.Pressed){_held=direction;_repeat=0;if(_session.State.Script.Completed&&_tracks.All(t=>t.Time>=t.Duration))Apply(new(_session.State.World.Player.Facing!=direction?"turn":"move",direction));}
            else if(_held==direction)_held=null;
        }
        else if(key.Pressed)
        {
            if(key.Keycode==Key.B&&_session.State.Script.Completed&&_tracks.All(t=>t.Time>=t.Duration)){_held=null;Apply(new("bag_open"));}
            if(FieldTextProgressEnabled&&key.Keycode==Key.Backspace&&_session.State.Script.Wait?.Kind is StoryWaitKind.Dialog or StoryWaitKind.MessagePrinting)Apply(new("ack"));
            if(key.Keycode==Key.Enter)
            {
                _held=null;
                if(_session.State.Script.Wait is {Kind:StoryWaitKind.Dialog or StoryWaitKind.MessagePrinting})Apply(new("ack"));
                else if(_session.State.Script.Wait?.Kind==StoryWaitKind.Choice)Apply(new("ack",Choice:_choiceYes?1:0));
                else if(_session.State.Script.Wait?.Kind==StoryWaitKind.MenuChoice)Apply(new("ack",Choice:_choiceYes?0:1));
                else if(_session.State.Script.Completed)Apply(new("interact"));
            }
            if(key.Keycode==Key.Backspace&&_session.State.Script.Wait?.Kind is StoryWaitKind.Choice or StoryWaitKind.MenuChoice){_held=null;Apply(new("ack",Choice:_session.State.Script.Wait.Kind==StoryWaitKind.MenuChoice?127:0));}
            if(key.Keycode==Key.F5)ShowPlayerSaveResult(AttemptPlayerSave());
            if(key.Keycode==Key.F9)ShowPlayerSaveResult(AttemptPlayerLoad());
            if(key.Keycode==Key.Escape)RequestQuit();
        }
        QueueRedraw();GetViewport().SetInputAsHandled();
    }
    private bool Apply(CampaignCommand command)
    {
        if(_quitRequested||_busy)return false;
        if(!CommitPlayTime(true))return false;
        CommitTrainerTowerTime(true);_busy=true;
        command=CyclingRoadInput(command);
        if(command.Kind=="move")command=command with{RunHeld=_runningShiftHeld};
        _fieldStepAction="walk";
        var before=_session.State;_fieldStepTicks=command.Kind=="cycle_input"?CyclingRoadInputTicks(before,command):command.Kind=="move"?FieldStepTicksForDirection(before,command.Direction):16;var result=_session.Execute("ui:"+(_sequence++),_session.Revision,command);
        if(result.Status==CommandStatus.Accepted&&command.Kind is "move" or "cycle_input")
        {
            var timing=CampaignFieldMovementTiming.Resolve(before,command,_catalog,result.State);
            _fieldStepTicks=timing.Frames;_fieldStepAction=timing.SourceAction;
        }
        if(command.Kind=="slot_tick")
        {
            // Frame evidence is opt-in and remains a slot summary, not a full state dump.
            if(_diagnostics.Verbose)Write("slotFrame",new{command.SlotInput,status=result.Status.ToString(),result.Error,result.Revision,StateHash=CanonicalJson.Hash(result.State),phase=result.State.SlotMachine?.Phase.ToString(),step=result.State.SlotMachine?.MainStep,coins=result.State.Coins});
        }
        else Write("command",_diagnostics.Payload(new{command,status=result.Status.ToString(),result.Error,result.Revision},()=>new{command,status=result.Status.ToString(),result.Error,result.Revision,StateHash=CanonicalJson.Hash(result.State),state=result.State}));
        if(result.Status==CommandStatus.Accepted){_lastCommandFault=false;_blockedAutomaticWait=null;if(command.Kind=="slot_tick"&&result.State.SlotMachine is not null){SynchronizeSlotMachine();SynchronizeSourceAudio(before,result.State);}else{Synchronize(before,result.State);SynchronizeRegionalSignInput(before,result.State);}_status="方向键移动 · Enter对话 · F5保存 / F9读取";}
        else if(result.Error=="sevii_first_visit_not_yet_available")
        {_regionalServiceNotice="The trip to ONE ISLAND is not\navailable yet.";_held=null;_repeat=0;_lastCommandFault=false;_status="The trip to ONE ISLAND is not available yet.";}
        else if(result.Error=="seagallop_destination_not_yet_available")
        {_regionalServiceNotice="Travel to this island is not\navailable yet.";_held=null;_repeat=0;_lastCommandFault=false;_status=result.Error;}
        else if(result.Error=="npc_trade_access_boundary_not_open")
        {_regionalServiceNotice="This passage is not available yet.";_held=null;_repeat=0;_lastCommandFault=false;_status=result.Error;}
        else if(result.Error?.StartsWith("regional_service_unavailable: ",StringComparison.Ordinal)==true){_regionalServiceNotice=result.Error[30..];_held=null;_repeat=0;_lastCommandFault=false;_status=result.Error;}
        else if(result.Status==CommandStatus.Fault){_lastCommandFault=true;_blockedAutomaticWait=before.Script.Wait?.CorrelationId;_status="未接入或故障："+result.Error;_held=null;/* authoritative state is unchanged; let the player choose a different valid action */}
        else _status=result.Error??"操作被拒绝";
        _busy=false;
        return result.Status==CommandStatus.Accepted;
    }
    // The source trainer approach completes before intro speech opens. One gate
    // covers the message frame, text, cursor and any choice drawn inside it.
    private bool FieldDialogueVisible => _session.VisibleText is not null&&_session.State.Nickname?.Submitted!=true&&
        (_session.State.ForestTrainerBattle?.Stage!=CampaignForestTrainerStage.Intro||_tracks.All(t=>t.Time>=t.Duration));
    public override void _Draw()
    {
        if(_quitRequested||_startupExited)return;
        if(!_startupReady)
        {
            DrawRect(new(0,0,240,160),new Color("101820"));
            if(_font is not null)DrawString(_font,new(58,83),"Loading game...",fontSize:14,modulate:Colors.White);
            _loadingDrawn=true;return;
        }
        BeginMapFrame();
        if(_session is null)return;
        if(DrawHelp())return;
        if(PlayerEntryActive){DrawPlayerEntry();return;}
        if(DrawQuestLogPlayback())return;
        if(_session.State.ItemMenu?.Toss?.BattleOwner is not null && DrawBagToss())return;
        if(DrawDiploma())return;
        if(DrawSlotMachine())return;
        if(DrawTeachyTvBattle())return;
        if(DrawTeachyTvField())return;
        if(DrawTeachyTv())return;
        if(DrawFameChecker())return;
        if(DrawEggHatch())return;
        if(DrawTrainerTowerRecords())return;
        if(DrawPokemonLeagueScreen())return;
        if(DrawFirstSeviiFerry())return;
        if(DrawSilphPad())return;
        if(DrawTownMap())return;
        if(_session.State.OldManTutorial is not null){DrawOldManPresentation();return;}
        if(_session.State.RareCandy is not null){DrawRareCandy();return;}
        if(_session.State.Evolution is not null){DrawEvolution();return;}
        if(_session.State.SafariBattle is not null){DrawSafariBattle();return;}
        if(DoubleBattleVisible){DrawDoubleBattle();return;}
        if(ForestTrainerCombatVisible||_session.State.WildBattle is not null){DrawWildBattle();return;}
        if(_session.State.Battle is not null){DrawBattle();return;}
        if(DrawMoveTutor())return;
        if(DrawNpcTrade())return;
        if(DrawDaisyGroomingParty())return;
        if(DrawSizeRecordParty())return;
        if(DrawNameRaterParty())return;
        DrawRect(new(0,0,240,160),Colors.Black);
        var hero=_poses["LOCALID_PLAYER"];
        // Source camera follows the player even in rooms smaller than the viewport. Border
        // blocks fill outside the layout; centering/clamping the whole map shifts the player.
        var camera=(BillCameraFocus(EventIslandCameraFocus(hero.Position))+new Vector2(8,8)-new Vector2(120,80)).Floor()+RocketCameraOffset+DepartmentCameraOffset+SilphCameraOffset+CinnabarCameraOffset+SixIslandCameraOffset+SevenIslandCameraOffset+EventIslandCameraOffset;
        PrepareVisibleMapResources(camera);
        if(_art.Border is {} borderBottom)DrawMapBorder(borderBottom.Bottom,camera);DrawConnectedNeighbors(camera,0);DrawBillMapLayer(_art.Bottom,camera,0);
        if(_art.Border is {} borderMiddle)DrawMapBorder(borderMiddle.Middle,camera);DrawConnectedNeighbors(camera,1);DrawBillMapLayer(_art.Middle,camera,1);
        // Source invisible script objects (including the Warden journal graphics 0) have no sprite.
        // Visible unknown graphics still fail the normal resource lookup.
        var entries=_session.State.Actors.Values.Where(a=>a.MovementType!="MOVEMENT_TYPE_INVISIBLE"&&RegionalServicesActorHasPixels(a)&&_poses.TryGetValue(a.LocalId,out var pose)&&pose.Visible).Select(a=>(Id:a.LocalId,Asset:_art.Npcs[ResolveRegionGraphics(a.GraphicsId)])).ToList();
        if(hero.Visible&&!(CinnabarLanding&&CinnabarFallPose.FixedPriority)&&!SixIslandLandingFixedPriority)entries.Add(("LOCALID_PLAYER",CurrentPlayerSprite));
        DrawSsAnneWake();DrawFieldStrengthDust(camera);DrawFieldSurfLanding(camera);DrawCinnabarLandingDust(camera);
        foreach(var (id,asset) in entries.OrderBy(e=>_poses[e.Id].Position.Y).ThenBy(e=>e.Id,StringComparer.Ordinal))DrawCityServicesActor(id,asset,_poses[id],camera);
        if(_art.Border is {} borderTop)DrawMapBorder(borderTop.Top,camera);DrawConnectedNeighbors(camera,2);DrawBillMapLayer(_art.Top,camera,2);
        if(hero.Visible&&(CinnabarLanding&&CinnabarFallPose.FixedPriority||SixIslandLandingFixedPriority))DrawCityServicesActor("LOCALID_PLAYER",CurrentPlayerSprite,hero,camera);
        DrawVsSeekerIcons(camera);DrawCutGrassParticles(camera);DrawEventIslandFragments(camera);DrawPokemonTowerFog(camera);DrawSsAnneSmoke();DrawFieldFlashMask();DrawFieldMoveBanner();DrawFieldFlashBanner();DrawFieldStrengthBanner();DrawFourIslandBanner();DrawFishingDots();
        DrawCenterHealing();DrawPokemonLeagueRecording();
        DrawMagikarpMoneyBox();DrawGameCornerCoinsBox();DrawBerryPowderAmount();
        if(_session.State.MonPicture is {} picture&&!DrawBillPcPicture()&&!DrawNamedPokemonPicture(picture))
        {
            var box=new Vector2(picture.X*8,picture.Y*8);DrawRect(new(box,new Vector2(80,80)),new Color("263c48"));DrawRect(new(box+new Vector2(2,2),new Vector2(76,76)),Colors.White);
            if(_portraits.TryGetValue(picture.SourceSpeciesId,out var portrait))DrawTexture(portrait,box+new Vector2(8,8));
        }
        if(!DrawSeviiRubyBraille()&&FieldDialogueVisible&&PresentedFieldText is {} text)
        {
            text=PewterDisplayText(text);
            _battleSkin.DrawDialogue(this);
            var color=_session.State.Script.Variables.GetValueOrDefault("VAR_TEXT_COLOR") switch{0=>new Color("345077"),1=>new Color("a44756"),_=>new Color("283840")};
            var lines=text.Split('\n');var visibleLines=FieldRenderedText(text).Split('\n');for(int i=0;i<visibleLines.Length;i++)SourceText(_sourceNormal,LabBattleUiSkin.DialogueText+new Vector2(0,i*15),visibleLines[i]);
            if(!TrainerTowerMenuActive&&!SeagallopMenuActive&&FieldTextComplete&&_session.State.Script.Wait?.Kind==StoryWaitKind.Choice&&_session.State.Dialogue?.Page+1==_session.State.Dialogue?.Pages.Length)
            {
                int x=FieldChoiceFrameX,y=FieldChoiceTextY,step=FieldChoiceRowHeight;
                _battleSkin.DrawUserFrame(this,new(x,64,64,48));
                SourceText(_sourceNormal,new(x+16,y),"YES");SourceText(_sourceNormal,new(x+16,y+step),"NO");
                SourceText(_sourceNormal,new(x+(FieldCutChoice?8:6),y+(_choiceYes?0:step)),"▶");
            }
            else if(!SourceMenuActive&&!SourceGridMenuActive&&!FossilLabMenuActive&&!TrainerTowerMenuActive&&!SeagallopMenuActive&&!BikeShopMenuActive&&!BillPcMenuActive&&!RocketElevatorMenuActive&&!DepartmentElevatorMenuActive&&!RooftopMenuActive&&!GameCornerCoinMenuActive&&!GameCornerPrizeMenuActive&&!SilphElevatorMenuActive&&FieldTextComplete&&_session.State.Script.Wait?.Kind==StoryWaitKind.MenuChoice&&_session.State.Dialogue?.Page+1==_session.State.Dialogue?.Pages.Length)
            {
                int width=((int)Math.Max(_sourceNormal.Font.GetStringSize("YES",fontSize:_sourceNormal.Size).X,_sourceNormal.Font.GetStringSize("NO",fontSize:_sourceNormal.Size).X)+9)/8+1;
                _battleSkin.DrawUserFrame(this,new(144,56,width*8+16,48));SourceText(_sourceNormal,new(160,66),"YES");SourceText(_sourceNormal,new(160,80),"NO");SourceText(_sourceNormal,new(152,_choiceYes?66:80),"▶");
            }
            else if(FieldTextComplete&&(_session.State.Script.Wait?.Kind is StoryWaitKind.Dialog or StoryWaitKind.MessagePrinting or StoryWaitKind.Choice or StoryWaitKind.ButtonPress||_session.State.ForestTrainerBattle?.Stage==CampaignForestTrainerStage.Intro||_session.State.DoubleBattle?.Stage==CampaignDoubleBattleStage.Intro||_session.State.Repel?.Phase==CampaignRepelPhase.WoreOff))DrawTextCursor(_sourceNormal,LabBattleUiSkin.DialogueText,lines,15,_waitElapsed);
        }
        DrawDayCareResidents();DrawSourceGridMenu();DrawSourceMenu();DrawFossilLabMenu();DrawBillPcMenu();DrawBikeShopMenu();DrawRocketElevatorMenu();DrawDepartmentElevatorMenu();DrawRooftopMenu();DrawGameCornerCoinMenu();DrawGameCornerPrizeMenu();DrawSilphElevatorMenu();DrawSeagallopMenu();DrawTrainerTowerMenu();
        if(_session.State.ForestTrainerBattle?.Stage==CampaignForestTrainerStage.Intro&&_wildNotice is not null)ItemMessageBox(_wildNotice);
        if(_lastCommandFault||_status.StartsWith("starter_",StringComparison.Ordinal))
        {DrawRect(new(2,137,236,21),new Color("741e2b"));DrawString(_font,new(6,147),"事件暂未接入，当前步骤未提交",fontSize:8,modulate:Colors.White);DrawString(_font,new(6,156),"详细错误已写本地日志；Esc退出",fontSize:7,modulate:Colors.White);}
        if(_session.State.ScreenIsBlack)DrawRect(new(0,0,240,160),SourceScreenFadeColor);
        DrawPokemonTowerWhiteFade();
        if(_regionalServiceNotice is not null)ItemMessageBox(_regionalServiceNotice);
        if(!DrawNameRaterNickname()&&!DrawGiftNickname()&&_session.State.Nickname is {Submitted:false} naming)
        {
            DrawRect(new(10,28,220,105),new Color("f4f0d9"));DrawString(_font,new(20,44),"起昵称 · 文字输入适配界面",fontSize:10,modulate:new Color("203645"));
            DrawString(_font,new(20,57),"原名："+naming.OriginalName,fontSize:9,modulate:new Color("203645"));
            DrawString(_font,new(20,101),"Enter确认 / Backspace删除",fontSize:9,modulate:new Color("203645"));
            DrawString(_font,new(20,115),"最多10个原版字符，空名保留原名",fontSize:9,modulate:new Color("203645"));
            if(_status.StartsWith("nickname_",StringComparison.Ordinal))DrawString(_font,new(20,127),_status,fontSize:8,modulate:Colors.Red);
        }
        if(_session.State.Pc is not null)DrawStorage();
        if(_session.State.ItemMenu is not null&&!DrawBagToss()&&!DrawMachines()&&!DrawQuestPocket())DrawItemMenu();
        if(_session.State.Shop is not null){DrawShop();if(_shopNotice is not null)ItemMessageBox(_shopNotice);}
        if(FieldMenuOpen)DrawFieldMenu();
        DrawItemfinder();DrawVsSeekerMessage();
        if(_fade>0)DrawRect(new(0,0,240,160),new Color(0,0,0,(float)(_fade/.16)));
        DrawCinnabarFade();DrawFieldFlyTravelOverlay();DrawNoncombatFieldOverlay();DrawTrainerCardPhotoFlash();DrawDiplomaFieldFade();
    }
    private void SourceText(SourceGameFont font,Vector2 topLeft,string text,int letterSpacing=0)
    {
        if(letterSpacing==0){DrawString(font.Font,topLeft+new Vector2(0,font.Baseline),font.PrepareText(text),fontSize:font.Size,modulate:Colors.White);return;}
        foreach(var rune in text.EnumerateRunes())
        {
            var glyph=font.PrepareText(rune.ToString());DrawString(font.Font,topLeft+new Vector2(0,font.Baseline),glyph,fontSize:font.Size,modulate:Colors.White);
            topLeft.X+=font.Font.GetStringSize(glyph,fontSize:font.Size).X+letterSpacing;
        }
    }
    private void DrawTextCursor(SourceGameFont font,Vector2 origin,string[] lines,int lineStep,double elapsed,bool alternate=false)
    {
        if(lines.Length==0)return;var last=lines[^1];float width=font.Font.GetStringSize(font.PrepareText(last),fontSize:font.Size).X;
        _textCursor.Draw(this,(origin+new Vector2(width,(lines.Length-1)*lineStep)).Floor(),(long)Math.Max(0,elapsed*60),alternate);
    }
    private void DrawMapBorder(Texture2D texture,Vector2 camera)
    {
        texture=SixIslandPaletteTexture(texture,true);
        int left=(int)Math.Floor(camera.X/16),top=(int)Math.Floor(camera.Y/16);
        for(int y=top;y<=Math.Floor((camera.Y+159)/16);y++)for(int x=left;x<=Math.Floor((camera.X+239)/16);x++)
        {
            if((x>=0&&y>=0&&x<_art.Source.Width&&y<_art.Source.Height)||IsConnectedNeighborCell(x,y))continue;
            var border=_art.Border!;
            DrawTextureRectRegion(texture,new(new Vector2(x*16,y*16)-camera,new Vector2(16,16)),new(MapBorderPattern.Coordinate(x,border.Width)*16,MapBorderPattern.Coordinate(y,border.Height)*16,16,16));
        }
    }
    private void DrawActor(SpriteAsset asset,Pose pose,Vector2 camera,Texture2D? textureOverride=null)
    {
        var texture=textureOverride??SixIslandPaletteTexture(asset.Texture,false);
        int idle=asset.Inanimate?0:pose.Facing switch{Direction.South=>0,Direction.North=>1,_=>2};int frame=idle;
        if(pose.Frame>0&&asset.Count>=9){var sequence=pose.Facing switch{Direction.South=>new[]{3,0,4,0},Direction.North=>new[]{5,1,6,1},_=>new[]{7,2,8,2}};frame=sequence[(pose.Frame-1)%4];}
        if(pose.Frame<0)frame=-pose.Frame;
        frame=Math.Clamp(frame,0,asset.Count-1);var position=pose.Position.Floor()+new Vector2(8,8+pose.DrawYOffset)-asset.Anchor-camera;
        if(!asset.Inanimate&&pose.Facing==Direction.East){DrawSetTransform(position+new Vector2(asset.Width,0),0,new(-1,1));DrawTextureRectRegion(texture,new(0,0,asset.Width,asset.Height),new(frame*asset.Width,0,asset.Width,asset.Height));DrawSetTransform(Vector2.Zero);}
        else DrawTextureRectRegion(texture,new(position,new Vector2(asset.Width,asset.Height)),new(frame*asset.Width,0,asset.Width,asset.Height));
    }
    private void Write(string kind,object data)
    {var line=JsonSerializer.Serialize(new{kind,presentationMillis=Time.GetTicksMsec(),data});GD.Print("STORY "+line);if(_evidence.Length>0)System.IO.File.AppendAllText(System.IO.Path.ChangeExtension(_evidence,"jsonl"),line+"\n");}
}
