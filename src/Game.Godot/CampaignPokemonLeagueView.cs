using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Application;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.World;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Godot;

public partial class CampaignView
{
    private SourceGameFont _leagueCreditHeader=null!;
    private string? _leagueCreditPreviousText,_leagueCreditText;
    private HallConfettiPresentation? _hallConfetti;
    private int[] _hallSine=[];
    private readonly Dictionary<ulong,Texture2D> _hallMutedTextures=[];
    private readonly Dictionary<(ulong,int),Texture2D> _hallBorderTextures=[];
    private int _creditsRunnerScene,_creditsRunnerTick;
    private PokemonLeaguePresentationData? _pokemonLeagueArt;
    private readonly Dictionary<string,Texture2D> _leagueTextures=new(StringComparer.Ordinal);
    private readonly Dictionary<string,JsonElement> _leagueTrainers=new(StringComparer.Ordinal);
    private string? _leagueRequestId,_leagueRecordWaitId;
    private CampaignPokemonLeagueCompletionStage? _leagueStage;
    private double _leagueElapsed,_leagueRecordElapsed;
    private int _leagueRecordTick=-1,_leagueDisplayCue=-1,_leagueCreditIndex=-1,_leagueCreditTick;
    private double _leagueCreditFrames,_leagueCreditMapFrames,_leagueCreditsExitFrames=-1;
    private int _leagueCreditMap=-1;
    private bool _leagueSaveAttempted,_leagueOverwritePrompt,_leagueOverwriteYes;
    private long _leagueSlowFadeSequence;
    private double _leagueSlowFadeElapsed;
    private float PokemonLeagueFieldFadeAlpha => _leagueSlowFadeSequence==0?1:Math.Min(16,((int)(_leagueSlowFadeElapsed*60)/26)*2)/16f;
    private ulong _leagueActionFrame=ulong.MaxValue;
    private JsonElement LeagueManifest=>_pokemonLeagueArt?.Manifest??throw new InvalidDataException("Pokemon League presentation missing.");
    private JsonElement LeagueCredits=>LeagueManifest.GetProperty("credits");
    private CampaignPokemonLeagueCompletion? LeagueCompletion=>_session?.State.PokemonLeagueCompletion;
    private bool LeagueOwnsInput=>LeagueCompletion is {Stage:not CampaignPokemonLeagueCompletionStage.ExteriorCredits} c&&
        !(c.Stage==CampaignPokemonLeagueCompletionStage.RecordingFieldEffect&&c.FieldEffectCompleted);

    private void LoadPokemonLeaguePresentation()
    {
        _pokemonLeagueArt=PokemonLeaguePresentationAssets.Load(_root, _catalog.BattleContent!.Texts);
        var color=LeagueCredits.GetProperty("headerColor").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
        static float Channel(int c){int v=c>>3;return ((v<<3)|(v>>2))/255f;}
        _leagueCreditHeader=SourceGameFont.Load(System.IO.Path.Combine(_root,"content/generated/ui/font"),foreground:new Color(Channel(color[0]),Channel(color[1]),Channel(color[2])),shadow:new Color("636363"));
        _hallSine=LeagueManifest.GetProperty("hallSine").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
        foreach(var a in LeagueManifest.GetProperty("assets").EnumerateArray())
        {
            string id=a.GetProperty("id").GetString()!;int width=a.GetProperty("width").GetInt32(),height=a.GetProperty("height").GetInt32();
            using var image = NativeImages.LoadPng(_pokemonLeagueArt.Files[a.GetProperty("texturePath").GetString()!], width, height);_leagueTextures.Add(id,SceneTextures.FromImage(image));
        }
        var source=_catalog.PokemonLeagueTrainerParties??throw new InvalidDataException("Pokemon League trainers missing.");
        foreach(var t in LeagueManifest.GetProperty("trainers").EnumerateArray())
        {
            string id=t.GetProperty("trainer").GetString()!;
            var trainer=source.Trainers[new(id)];
            if(trainer.SourceTrainerId!=t.GetProperty("sourceTrainerId").GetInt32()||trainer.TrainerClass!=t.GetProperty("trainerClass").GetString()||trainer.SourceTrainerName!=t.GetProperty("name").GetString()||trainer.TrainerPic!=t.GetProperty("trainerPic").GetString())throw new InvalidDataException("Pokemon League trainer binding differs.");
            _leagueTrainers.Add(id,t);
        }
        foreach(var p in LeagueManifest.GetProperty("portraits").EnumerateArray())
        {
            string id=p.GetProperty("id").GetString()!;
            // Prefix avoids colliding with inherited player portrait bindings.
            _forestTrainerPortraits.Add("league-"+id,(ImageTexture)_leagueTextures[id]);
        }
        foreach(var p in LeagueManifest.GetProperty("sprites").EnumerateArray())
        {
            int species=p.GetProperty("sourceSpeciesId").GetInt32();string side=p.GetProperty("side").GetString()!;bool shiny=p.GetProperty("paletteVariant").GetString()=="shiny";
            if(_catalog.OpponentPokemon?.Catalog.Species.GetValueOrDefault(new(p.GetProperty("speciesId").GetString()!))?.SourceSpeciesId!=species)throw new InvalidDataException("Unadmitted Pokemon League sprite.");
            _battleArt.Add((species,side,shiny),new(_leagueTextures[p.GetProperty("id").GetString()!],new(p.GetProperty("x").GetInt32(),p.GetProperty("y").GetInt32())));
            if(side=="front"&&!shiny)_portraits.Add(species,_leagueTextures[p.GetProperty("id").GetString()!]);
        }
        foreach(var p in LeagueManifest.GetProperty("pokemonIcons").EnumerateArray())_storageIcons.Add(new(p.GetProperty("speciesId").GetString()!),_leagueTextures[p.GetProperty("id").GetString()!]);

    }
    private PokedexMenuTexture? PokemonLeagueDexPortrait(int species)
    {
        if(_pokemonLeagueArt is null)return null;
        var row=LeagueManifest.GetProperty("sprites").EnumerateArray().SingleOrDefault(s=>s.GetProperty("sourceSpeciesId").GetInt32()==species&&s.GetProperty("side").GetString()=="front"&&s.GetProperty("paletteVariant").GetString()=="normal");
        if(row.ValueKind==JsonValueKind.Undefined)return null;
        string png=row.GetProperty("texturePath").GetString()!;
        return new(PokemonLeaguePresentationAssets.Base+png,64,64);
    }
    private byte[]? ReadPokemonLeagueDexTexture(PokedexMenuTexture texture)
    {
        if(_pokemonLeagueArt is null)return null;
        foreach(var row in LeagueManifest.GetProperty("sprites").EnumerateArray())
            if(row.GetProperty("side").GetString()=="front"&&row.GetProperty("paletteVariant").GetString()=="normal"&&texture==PokemonLeagueDexPortrait(row.GetProperty("sourceSpeciesId").GetInt32()))return _pokemonLeagueArt.Files[row.GetProperty("texturePath").GetString()!];
        return null;
    }
    private string? PokemonLeagueMusic(CampaignState state)
    {
        if(state.PokemonLeagueCompletion is {} c)
        {
            if(c.Stage is CampaignPokemonLeagueCompletionStage.AwaitingSave or CampaignPokemonLeagueCompletionStage.SaveFailed or CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame)return "MUS_HALL_OF_FAME";
            if(c.Stage==CampaignPokemonLeagueCompletionStage.Credits)return "MUS_CREDITS";
        }
        if(state.ForestTrainerBattle is not {Stage:not CampaignForestTrainerStage.Intro} battle||!_leagueTrainers.TryGetValue(battle.Trainer.Value,out var trainer))return null;
        bool champion=trainer.GetProperty("trainerClass").GetString()=="TRAINER_CLASS_CHAMPION";
        bool won=battle.Combat.Phase==ForestTrainerCombatPhase.Ended&&battle.Combat.Battle.Outcome==WildBattleOutcome.Won;
        // battle_main.c: Elite Four victory uses trainer music; Champion uses leader victory.
        return won?(champion?"MUS_VICTORY_GYM_LEADER":"MUS_VICTORY_TRAINER"):(champion?"MUS_VS_CHAMPION":"MUS_VS_GYM_LEADER");
    }
    private void SynchronizePokemonLeaguePresentation(CampaignState? before,CampaignState after)
    {
        if(after.PokemonLeagueCompletion is not {} c){_leagueStage=null;_leagueRequestId=null;_leagueSlowFadeSequence=0;return;}
        var fade=after.PresentationEffects.LastOrDefault(e=>e.Opcode=="fadescreenspeed"&&e.Args.SequenceEqual(new[]{"FADE_TO_BLACK","24"}));
        if(fade is not null&&fade.Sequence!=_leagueSlowFadeSequence){_leagueSlowFadeSequence=fade.Sequence;_leagueSlowFadeElapsed=0;}
        if(_leagueRequestId!=c.RequestId)
        {
            _leagueRequestId=c.RequestId;_leagueRecordWaitId=after.PresentationEffects.Any(e=>e.Opcode=="waitfieldeffect")?after.Script.Wait?.CorrelationId:null;_leagueRecordElapsed=0;_leagueRecordTick=-1;
        }
        if(_leagueStage==c.Stage)return;
        _leagueStage=c.Stage;_leagueElapsed=0;_leagueActionFrame=Engine.GetProcessFrames();_held=null;_repeat=0;
        if(c.Stage==CampaignPokemonLeagueCompletionStage.AwaitingSave)
        {_leagueSaveAttempted=false;_leagueOverwritePrompt=false;_leagueOverwriteYes=false;}
        if(c.Stage==CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame){_leagueDisplayCue=-1;_hallConfetti=new(unchecked((uint)c.ChallengeNumber));SourceSound("SE_SAVE");}
        if(c.Stage==CampaignPokemonLeagueCompletionStage.Credits)
        {_leagueCreditIndex=-1;_leagueCreditsExitFrames=-1;_leagueCreditTick=0;_leagueCreditFrames=0;_leagueCreditMapFrames=0;_leagueCreditMap=-1;_creditsRunnerScene=0;_creditsRunnerTick=0;_leagueCreditPreviousText=null;_leagueCreditText=null;}
        Write("pokemonLeagueStage",new{c.Stage,c.RequestId,c.SaveSucceeded,c.FieldEffectCompleted});
    }
    private bool ProcessPokemonLeaguePresentation(double delta)
    {
        if(LeagueCompletion is not {} c)return false;
        _leagueElapsed+=delta;if(_leagueSlowFadeSequence!=0)_leagueSlowFadeElapsed+=delta;
        if(c.Stage==CampaignPokemonLeagueCompletionStage.RecordingFieldEffect)
        {
            _leagueRecordElapsed+=delta;int tick=(int)Math.Floor(_leagueRecordElapsed*60+1e-9);
            for(int i=_leagueRecordTick+1;i<=tick;i++)if(i%25==0&&i/25<c.PartyCount)SourceSound("SE_BALL");
            _leagueRecordTick=tick;
            if(!c.FieldEffectCompleted&&_session.State.PresentationEffects.Any(e=>e.Opcode=="waitfieldeffect"&&e.Args.Contains("FLDEFF_HALL_OF_FAME_RECORD"))&&tick>=PokemonLeagueRecordingTimeline.RemovalTick(c.PartyCount)&&!_busy)
            {
                string correlation=_session.State.Script.Wait?.CorrelationId??throw new InvalidDataException("Hall recording wait missing.");
                if(_leagueRecordWaitId is not null&&correlation!=_leagueRecordWaitId)throw new InvalidDataException("Hall recording wait changed.");
                Write("pokemonLeagueRecordingComplete",new{sourceGlowState=7,sourceTick=tick,playHealSe=false,taskRemoved=true,correlation});
                Apply(new("league_record_complete",Text:correlation,Quantity:7));
            }
            return !c.FieldEffectCompleted;
        }
        if(c.Stage==CampaignPokemonLeagueCompletionStage.AwaitingSave)
        {
            // Keep a rendered saving frame visible before blocking filesystem work.
            if(_leagueElapsed>=1.0/60&&!_leagueSaveAttempted&&!_leagueOverwritePrompt)SavePokemonLeagueHallOfFame(false);
            return true;
        }
        if(c.Stage==CampaignPokemonLeagueCompletionStage.SaveFailed)return true;
        if(c.Stage==CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame)
        {
            var team=CurrentHallOfFameTeam();var display=HallDisplayPosition(team.Members.Length,(int)(_leagueElapsed*60));
            if(display.ApplauseTick>=0)_hallConfetti?.AdvanceTo(display.ApplauseTick,_hallSine);
            else if(display.BorderFade>0)_hallConfetti?.AdvanceTo(display.PlayerTick>=0?410+display.PlayerTick:400+display.BorderFade-8,_hallSine);
            if(display.Current!=_leagueDisplayCue)
            {
                _leagueDisplayCue=display.Current;
                // hall_of_fame.c Task_Hof_PlayMonCryAndPrintInfo never cries for an Egg.
                if(display.Current>=0&&display.Current<team.Members.Length&&team.Members[display.Current].SpeciesOrEgg!=412)SourceCrySymbol(team.Members[display.Current].SpeciesOrEgg.ToString());
                else if(display.Current==team.Members.Length)SourceSound("SE_APPLAUSE");
            }
            return true;
        }
        if(c.Stage==CampaignPokemonLeagueCompletionStage.Credits)
        {
            if(_leagueCreditsExitFrames>=0)
            {
                _leagueCreditsExitFrames+=delta*60;
                if(_leagueCreditsExitFrames>=17)Apply(new("league_credits_complete",Text:c.RequestId));
                return true;
            }
            _leagueCreditFrames+=delta*60;int next=(int)_leagueCreditFrames;
            while(_leagueCreditTick<next&&LeagueCompletion?.Stage==CampaignPokemonLeagueCompletionStage.Credits)
            {
                _leagueCreditTick++;_leagueCreditMapFrames++;_creditsRunnerTick++;
                if(_leagueCreditIndex>=0&&LeagueCreditCommand.GetProperty("op").GetString()=="MON"&&_leagueCreditTick==PokemonLeagueScenePresentation.MonCryFrame)
                    SourceCrySymbol(CreditSpecies(LeagueCreditCommand.GetProperty("param").GetString()!).ToString());
                int duration=LeagueCreditDuration;
                if(_leagueCreditTick<duration)continue;
                _leagueCreditFrames-=duration;_leagueCreditTick-=duration;next-=duration;
                _leagueCreditIndex++;
                if(_leagueCreditIndex>=LeagueCredits.GetProperty("commands").GetArrayLength())
                {_leagueCreditIndex--;_leagueCreditsExitFrames=0;break;}
                var command=LeagueCreditCommand;
                if(command.GetProperty("op").GetString()=="PRINT")
                {_leagueCreditPreviousText=_leagueCreditText;_leagueCreditText=command.GetProperty("param").GetString();}
                if(command.GetProperty("op").GetString() is "MAP" or "MAPNEXT")
                {_leagueCreditMap=LeagueCreditMapIndex(command.GetProperty("param").GetString()!);_leagueCreditMapFrames=0;_leagueCreditText=null;_leagueCreditPreviousText=null;}
                if(command.GetProperty("op").GetString()=="MAP")
                {_creditsRunnerScene=_leagueCreditMap switch {6=>2,9=>3,12=>4,_=>1};_creditsRunnerTick=0;}
            }
            return true;
        }
        if(c.Stage==CampaignPokemonLeagueCompletionStage.ResetRequested)
        {
            ResetPlayTimeHost();ResetSourceAudio();_held=null;_repeat=0;_tracks.Clear();CloseNamingScreen();
            if(_entrySource is null)InitializePlayerEntry("",force:true);else ReadEntrySave();
            _entryActionFrame=Engine.GetProcessFrames();
            Write("pokemonLeagueReturnToEntry",new{verifiedContinue=_entrySave is not null,originalTitleMovieImplemented=false});return true;
        }
        return false;
    }
    private void SavePokemonLeagueHallOfFame(bool approvedOverwrite)
    {
        if(!CommitPlayTime(true))return;
        // One coherent snapshot/generation/hash per write attempt, including retries.
        var request=_session.PokemonLeagueHallOfFameSaveRequest;
        if(request is null)return;
        _leagueSaveAttempted=true;
        try
        {
            string fingerprint=EntrySlotFingerprint();
            if(_entrySource is not null&&!approvedOverwrite&&(_entryOverwritePending||fingerprint!=_entryObservedSlot))
            {_leagueSaveAttempted=false;_leagueOverwritePrompt=true;_entryPromptSlot=fingerprint;return;}
            if(approvedOverwrite&&fingerprint!=_entryPromptSlot)throw new IOException("save_file_changed_before_commit");
            _entrySaveAttemptSlot=fingerprint;
            PokemonLeagueDurableSave.Write(EntrySaveStorage(),PlayerSavePath,request.ContinueState,request.Generation,request.PayloadHash,_session.RulesVersion,_catalog.ContentVersion,_session.ValidatePlayerSave);
            EntrySaveSucceeded();
            Write("pokemonLeagueDurableSave",new{request.RequestId,request.Generation,request.PayloadHash,primaryReadbackVerified=true,continueMap=request.ContinueState.World.Player.MapId.Value,request.ContinueState.World.Player.X,request.ContinueState.World.Player.Y});
            Apply(new("league_save_result",Text:request.RequestId,Choice:1));
        }
        catch(Exception error) when(error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or InvalidOperationException)
        {
            Write("pokemonLeagueSaveFailed",new{request.RequestId,error=error.Message,successAcknowledged=false});
            Apply(new("league_save_result",Text:request.RequestId,Choice:0));
        }
    }
    private bool TryPokemonLeagueInput(InputEvent input)
    {
        if(!LeagueOwnsInput||PlayerEntryActive||input is InputEventKey {Keycode:Key.Escape or Key.F12})return false;
        if(input is InputEventKey {Pressed:true,Echo:false} key)
        {
            if(_leagueActionFrame==Engine.GetProcessFrames())return true;
            if(_leagueOverwritePrompt&&key.Keycode is Key.Up or Key.Down){_leagueOverwriteYes=!_leagueOverwriteYes;QueueRedraw();return true;}
            if(key.Keycode==Key.Enter)
            {
                _leagueActionFrame=Engine.GetProcessFrames();var c=LeagueCompletion!;
                if(_leagueOverwritePrompt)
                {
                    _leagueOverwritePrompt=false;
                    if(_leagueOverwriteYes)SavePokemonLeagueHallOfFame(true);
                    else {_leagueSaveAttempted=true;Apply(new("league_save_result",Text:c.RequestId,Choice:0));}
                }
                else if(c.Stage==CampaignPokemonLeagueCompletionStage.SaveFailed)Apply(new("league_save_retry",Text:c.RequestId));
                else if(c.Stage==CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame&&HallDisplayPosition(CurrentHallOfFameTeam().Members.Length,(int)(_leagueElapsed*60)).Ready)Apply(new("league_hall_of_fame_exit",Text:c.RequestId));
                else if(c.Stage==CampaignPokemonLeagueCompletionStage.Credits&&_leagueCreditIndex>=0&&LeagueCreditCommand.GetProperty("op").GetString()=="WAITBUTTON"&&_leagueCreditsExitFrames<0)_leagueCreditsExitFrames=0;
            }
        }
        else if(input is InputEventMouseButton {Pressed:true,DoubleClick:false,ButtonIndex:MouseButton.Left} mouse&&!_menuMousePressed)
        {
            _menuMousePressed=true;var point=GetGlobalTransformWithCanvas().AffineInverse()*mouse.Position;
            if(OpeningItemUiSkin.LogicalRect.HasPoint(point))
            {
                if(_leagueOverwritePrompt)
                {if(!new Rect2(144,72,80,32).HasPoint(point))return true;_leagueOverwriteYes=point.Y<88;}
                using var simulatedKey=new InputEventKey{Pressed=true,Keycode=Key.Enter};TryPokemonLeagueInput(simulatedKey);
            }
        }
        QueueRedraw();return true;
    }
    private void DrawPokemonLeagueRecording()
    {
        if(LeagueCompletion is not {Stage:CampaignPokemonLeagueCompletionStage.RecordingFieldEffect} c||_centerHealingArt is null)return;
        int tick=Math.Max(0,_leagueRecordTick),palette=PokemonLeagueRecordingTimeline.Palette(c.PartyCount,tick);
        for(int i=0;i<PokemonLeagueRecordingTimeline.BallCount(c.PartyCount,tick);i++)
            DrawTextureRectRegion(_centerHealingTextures["pokeball-glow"],new(113+i%2*6,56+i/2*4,8,8),new(0,palette*8,8,8));
        int monitor=PokemonLeagueRecordingTimeline.MonitorFrame(c.PartyCount,tick);
        if(monitor>=0)DrawTextureRectRegion(_leagueTextures["hof-monitor"],new(112,17,16,16),new(0,monitor*16,16,16));
    }
    private CampaignHallOfFameTeam CurrentHallOfFameTeam()=>_session.State.HallOfFame?.Teams.LastOrDefault(t=>t.ChallengeNumber==LeagueCompletion?.ChallengeNumber)??throw new InvalidDataException("Recorded Hall of Fame team missing.");
    private Texture2D HallPokemonTexture(CampaignHallOfFamePokemon mon)
    {
        if(mon.SpeciesOrEgg==412)return BreedingEggShell();
        bool shiny=((mon.OriginalTrainerId&0xffff)^(mon.OriginalTrainerId>>16)^(mon.Personality&0xffff)^(mon.Personality>>16))<8;
        return _battleArt.TryGetValue((SevenIslandRenderKeys.Species(mon.SpeciesOrEgg,mon.Personality),"front",shiny),out var sprite)
            ? sprite.Texture : throw new InvalidDataException("Recorded Pokemon art missing.");
    }
    private Texture2D HallMutedTexture(Texture2D source)
    {
        ulong key=source.GetInstanceId();
        if(_hallMutedTextures.TryGetValue(key,out var cached))return cached;
        using var image=source.GetImage();
        for(int y=0;y<image.GetHeight();y++)for(int x=0;x<image.GetWidth();x++)
        {
            var c=image.GetPixel(x,y);
            static float Blend(float value,int target){int v=(int)Math.Round(value*255)>>3;v+=((target-v)*12)>>4;return ((v<<3)|(v>>2))/255f;}
            image.SetPixel(x,y,new(Blend(c.R,22),Blend(c.G,24),Blend(c.B,29),c.A));
        }
        var texture=SceneTextures.FromImage(image);_hallMutedTextures.Add(key,texture);return texture;
    }
    private void DrawHallPokemon(Texture2D texture,Vector2 position,bool behindBorder,int fade)
    {
        if(!behindBorder){DrawTexture(texture,position);return;}
        var key=(texture.GetInstanceId(),fade);
        if(!_hallBorderTextures.TryGetValue(key,out var border))
        {
            using var image=texture.GetImage();using var tiles=_leagueTextures["hof-tiles"].GetImage();var foreground=tiles.GetPixel(8,0);
            for(int y=0;y<image.GetHeight();y++)for(int x=0;x<image.GetWidth();x++)
            {
                var c=image.GetPixel(x,y);
                float Add(float a,float b){int v=Math.Min(31,(fade==0?((int)Math.Round(a*255)>>3):0)+(((int)Math.Round(b*255)>>3)*(fade==0?7:fade)/16));return ((v<<3)|(v>>2))/255f;}
                image.SetPixel(x,y,new(Add(foreground.R,c.R),Add(foreground.G,c.G),Add(foreground.B,c.B),c.A));
            }
            border=SceneTextures.FromImage(image);_hallBorderTextures.Add(key,border);
        }
        void Slice(int low,int high,Texture2D source)
        {
            float first=Math.Max(position.Y,low),last=Math.Min(position.Y+texture.GetHeight(),high);
            if(last>first)DrawTextureRectRegion(source,new(position.X,first,texture.GetWidth(),last-first),new(0,first-position.Y,texture.GetWidth(),last-first));
        }
        Slice(0,16,border);Slice(16,112,texture);Slice(112,160,border);
    }
    private static readonly (int X,int Y,int EndX,int EndY)[] HallFull=[(120,210,120,40),(326,220,56,40),(-86,220,184,40),(120,-62,120,88),(-70,-92,200,88),(310,-92,40,88)];
    private static readonly (int X,int Y,int EndX,int EndY)[] HallHalf=[(120,234,120,64),(326,244,56,64),(-86,244,184,64)];
    private static (int Current,int Moving,int MotionTick,int ApplauseTick,int PlayerTick,bool Ready,int BorderFade) HallDisplayPosition(int count,int tick)
    {
        tick-=34;if(tick<0)return(-1,-1,0,-1,-1,false,0);
        for(int i=0;i<count;i++)
        {
            var p=(count>3?HallFull:HallHalf)[i];int motion=Math.Max(Math.Abs(p.X-p.EndX)/15,Math.Abs(p.Y-p.EndY)/10)+1;
            if(tick<motion)return(i-1,i,tick,-1,-1,false,0);
            tick-=motion;if(tick<122)return(i,-1,0,-1,-1,false,0);tick-=122;
        }
        if(tick<400)return(count,-1,0,tick,-1,false,0);
        if(tick<410)return(count,-1,0,-1,-1,false,Math.Min(16,8+tick-400));tick-=410;
        return(count+1,-1,0,-1,Math.Max(0,tick),tick>=194,16);
    }
    private bool DrawPokemonLeagueScreen()
    {
        if(LeagueCompletion is not {} c)return false;
        if(c.Stage==CampaignPokemonLeagueCompletionStage.Credits){DrawPokemonLeagueCredits();return true;}
        if(c.Stage is not (CampaignPokemonLeagueCompletionStage.AwaitingSave or CampaignPokemonLeagueCompletionStage.SaveFailed or CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame))return false;
        DrawHallBackground();
        if(c.Stage!=CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame)
        {
            ItemMessageBox(c.Stage==CampaignPokemonLeagueCompletionStage.SaveFailed?_saveText.Get("league.error-save"):LeagueManifest.GetProperty("texts").GetProperty("gText_SavingDontTurnOffThePower2").GetString()!);
            if(_leagueOverwritePrompt)
            {
                ItemMessageBox(_saveText.Get("league.overwrite"));_battleSkin.DrawStandardFrame(this,new(136,64,96,48));
                SourceText(_sourceNormal,new(160,72),InventorySourceText("gText_Yes"));SourceText(_sourceNormal,new(160,88),InventorySourceText("gText_No"));SourceText(_sourceNormal,new(144,_leagueOverwriteYes?72:88),"▶");
            }
            return true;
        }
        var team=CurrentHallOfFameTeam();var phase=HallDisplayPosition(team.Members.Length,(int)(_leagueElapsed*60));
        int shown=phase.Moving>=0?phase.Moving:Math.Min(team.Members.Length-1,phase.Current);
        for(int i=0;i<=shown;i++)
        {
            var mon=team.Members[i];var texture=HallPokemonTexture(mon);
            var p=(team.Members.Length>3?HallFull:HallHalf)[i];Vector2 position=new(p.EndX,p.EndY);
            if(i==phase.Moving)position=new(Mathf.MoveToward(p.X,p.EndX,15*phase.MotionTick),Mathf.MoveToward(p.Y,p.EndY,10*phase.MotionTick));
            DrawHallPokemon(phase.BorderFade>0||phase.PlayerTick>=0||i<phase.Current&&phase.Current<team.Members.Length?HallMutedTexture(texture):texture,position-new Vector2(32,32),phase.PlayerTick<0&&phase.ApplauseTick<0,phase.BorderFade);
        }
            if(_hallConfetti is {} confetti)foreach(var piece in confetti.Pieces)
                DrawTextureRectRegion(_leagueTextures["confetti"],new(piece.X+piece.OffsetX-4,piece.Y+piece.OffsetY-4,8,8),new(0,piece.Frame*8,8,8));
        if(phase.Current>=0&&phase.Current<team.Members.Length&&phase.Moving<0)
        {
            var mon=team.Members[phase.Current];
            var details=HallPcDetails(new(mon,new(0,0),true));
            string nickname=details.Nickname;
            float width=HallPcTextWidth(_sourceWhite,nickname);
            SourceText(_sourceWhite,new(144-(mon.SpeciesOrEgg==412?width/2:width),121),nickname);
            if(mon.SpeciesOrEgg!=412)
            {
                SourceText(_sourceWhite,new(32,121),details.Dex);SourceText(_sourceWhite,new(144,121),details.Species);
                SourceText(_sourceWhite,new(48,137),details.Level);SourceText(_sourceWhite,new(112,137),details.TrainerId);
            }
        }
        else if(phase.ApplauseTick>=0)
        {
            string welcome=LeagueManifest.GetProperty("texts").GetProperty("gText_WelcomeToHOF").GetString()!;
            SourceText(_sourceWhite,new(16+(208-HallPcTextWidth(_sourceWhite,welcome))/2,121),welcome);

        }
        else if(phase.PlayerTick>=0)
        {
            DrawTexture(_leagueTextures[_session.State.PlayerGender==0?"red-front":"leaf-front"],new(Math.Min(192,120+Math.Max(0,phase.PlayerTick-120))-32,40));
            if(phase.Ready)
            {
                _battleSkin.DrawStandardFrame(this,new(8,8,144,64));SourceText(_sourceNormal,new(20,17),_battleText["gText_Name"]+"  "+_session.State.PlayerName);
                SourceText(_sourceNormal,new(20,33),_battleText["gText_IDNumber"]+" "+((ushort)_session.State.TrainerId).ToString("D5"));SourceText(_sourceNormal,new(20,49),_battleText["gText_Time"]+"  "+FormatPlayTime(_session.State.PlayTime));
                if(_session.State.PlayTime.SinceCheckpoint)SourceText(_sourceSmall,new(8,83),CheckpointTimeNote);
                ItemMessageBox(LeagueManifest.GetProperty("texts").GetProperty("gText_LeagueChamp").GetString()!);
            }
        }
        return true;
    }
    private void DrawHallBackground()
    {
        var tex=_leagueTextures["hof-tiles"];
        int fade=LeagueCompletion?.Stage==CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame?HallDisplayPosition(CurrentHallOfFameTeam().Members.Length,(int)(_leagueElapsed*60)).BorderFade:0;
        bool showBorder=fade<16;
        for(int y=0;y<20;y++)for(int x=0;x<30;x++)
        {
            bool border=(y<2||y>=14)&&showBorder;
            DrawTextureRectRegion(border?_leagueTextures["hof-border"]:tex,new(x*8,y*8,8,8),new(border?(fade==0?0:(fade-7)*8):16,0,8,8));
        }
    }
    private JsonElement LeagueCreditCommand=>LeagueCredits.GetProperty("commands")[_leagueCreditIndex];
    private int LeagueCreditDuration=>_leagueCreditIndex<0?504:LeagueCreditCommand.GetProperty("op").GetString()=="MON"?PokemonLeagueScenePresentation.MonDuration:Math.Max(1,LeagueCreditCommand.GetProperty("ticks").GetInt32()+(LeagueCreditCommand.GetProperty("op").GetString()=="PRINT"?20:LeagueCreditCommand.GetProperty("op").GetString()=="THEENDGFX"?100:0));
    private static int CreditSpecies(string symbol)=>symbol switch{"CHARIZARD"=>6,"VENUSAUR"=>3,"BLASTOISE"=>9,"PIKACHU"=>25,_=>throw new InvalidDataException("Unknown credits Pokemon.")};
    private static int LeagueCreditMapIndex(string name)=>Array.IndexOf(new[]{"ROUTE23","VIRIDIAN_CITY","PEWTER_CITY","CERULEAN_CITY","ROUTE25","VERMILION_CITY","ROUTE10","CELADON_CITY","SAFFRON_CITY","ROUTE17","FUCHSIA_CITY","CINNABAR_ISLAND","ROUTE21_NORTH"},name);
    private static readonly string[] CreditMapIds=["map.route-23","map.viridian-city","map.pewter-city","map.cerulean-city","map.route-25","map.vermilion-city","map.route-10","map.celadon-city","map.saffron-city","map.route-17","map.fuchsia-city","map.cinnabar-island","map.route-21-north"];
    private void DrawPokemonLeagueCredits()
    {
        string op=_leagueCreditIndex<0?"TITLE":LeagueCreditCommand.GetProperty("op").GetString()!;
        string param=_leagueCreditIndex<0?"":LeagueCreditCommand.GetProperty("param").GetString()!;
        DrawRect(new(0,0,240,160),Colors.Black);
        if(op=="WAITBUTTON"){DrawTexture(_leagueTextures["credits-the_end"],Vector2.Zero);if(_leagueCreditsExitFrames>=0)DrawRect(new(0,0,240,160),new Color(1,1,1,(float)Math.Min(1,_leagueCreditsExitFrames/16)));return;}
        bool exitWalk=op=="THEENDGFX"&&param=="ALLRIGHTSRESERVED"&&_leagueCreditTick<80;
        if(op=="THEENDGFX"&&!exitWalk)
        {
            bool previous=_leagueCreditTick<80;
            DrawTexture(_leagueTextures[previous||param=="ALLRIGHTSRESERVED"?"credits-copyright":"credits-the_end"],Vector2.Zero);
            float fade=previous?_leagueCreditTick/80f:param=="ALLRIGHTSRESERVED"?Math.Max(0,1-(_leagueCreditTick-83)/16f):0;
            DrawRect(new(0,0,240,160),new Color(0,0,0,Math.Clamp(fade,0,1)));return;
        }
        if(op=="MON")
        {
            int tick=_leagueCreditTick;
            if(tick>=PokemonLeagueScenePresentation.MonCryFrame)DrawTexture(_leagueTextures["credits-ball-"+param.ToLowerInvariant()],Vector2.Zero);
            else
            {
                int affine=PokemonLeagueScenePresentation.CircleAffine(tick);
                if(affine>0){float size=256*256f/affine;DrawTextureRect(_leagueTextures["credits-circle"],new(120-size/2,80-size/2,size,size),false);}
                else DrawRect(new(0,0,240,160),Colors.Black);
            }
            int col=Array.IndexOf(new[]{"CHARIZARD","VENUSAUR","BLASTOISE","PIKACHU"},param);
            DrawTextureRectRegion(_leagueTextures["credits-mon-zoom"],new(64,16,120,112),new(col*120,PokemonLeagueScenePresentation.ZoomPicture(tick)*112,120,112));
            DrawRect(new(0,0,240,160),new Color(0,0,0,PokemonLeagueScenePresentation.MonFade(tick)));return;
        }
        MapId map=_leagueCreditMap<0?_session.State.World.Player.MapId:new(CreditMapIds[_leagueCreditMap]);
        MapCache.SetPins([map.Value]);var art=GetMapArt(map);Vector2 camera;
        if(_leagueCreditMap<0)camera=new(11*16-120,6*16-80);
        else
        {
            var scene=LeagueCredits.GetProperty("maps")[_leagueCreditMap];float scroll=PokemonLeagueScenePresentation.CameraPixels((int)_leagueCreditMapFrames,scene.GetProperty("duration").GetInt32());
            camera=new(scene.GetProperty("x").GetInt32()*16-120+scene.GetProperty("dx").GetInt32()*scroll,scene.GetProperty("y").GetInt32()*16-80+scene.GetProperty("dy").GetInt32()*scroll);
        }
        camera=camera.Floor();DrawTexture(art.Bottom,-camera);DrawTexture(art.Middle,-camera);DrawTexture(art.Top,-camera);DrawRect(new(0,0,240,160),new(0,0,0,10f/16));
        // WIN0 reveals the central 88 scanlines; BG darken excludes sprites/text.
        int top=op=="TITLE"?Math.Max(36,79-(int)_leagueCreditFrames):36;
        DrawRect(new(0,0,240,top),Colors.Black);DrawRect(new(0,160-top,240,top),Colors.Black);
        var runner=PokemonLeagueScenePresentation.Runner(_creditsRunnerScene,Math.Max(0,_creditsRunnerTick-(_creditsRunnerScene==0?44:0)));
        if(exitWalk)runner.X-=_leagueCreditTick;
        int frame=_creditsRunnerTick/8;string player=runner.Rival?"credits-rival":_session.State.PlayerGender==0?"credits-player_male":"credits-player_female";
        string ground=runner.Ground==2?"dirt":runner.Ground==3?"city":"grass";
        if(op!="TITLE"||_leagueCreditTick>=44)
        {
            DrawLeagueCreditSprite(_leagueTextures["credits-ground_"+ground],runner.X-32,runner.Y+22,(runner.StaticGround?0:frame%8)*32,32,top);
            DrawLeagueCreditSprite(_leagueTextures[player],runner.X-32,runner.Y-32,frame%6*64,64,top);
        }
        if(op=="TITLE"){if(_leagueCreditTick<144)return;DrawLeagueCreditText(LeagueCredits.GetProperty("title").GetString()!,8,73,_leagueCreditHeader);return;}
        if(exitWalk){DrawRect(new(0,0,240,160),new Color(0,0,0,_leagueCreditTick/80f));return;}
        if(op!="PRINT")return;
        string? visible=_leagueCreditTick<20?_leagueCreditPreviousText:_leagueCreditText;
        if(visible is null)return;
        float brightness=_leagueCreditTick<20?Math.Max(0,1-_leagueCreditTick/16f):Math.Min(1,(_leagueCreditTick-20)/16f);
        var text=LeagueCredits.GetProperty("texts").GetProperty(visible);
        DrawLeagueCreditText(text.GetProperty("title").GetString()!,2,38,_leagueCreditHeader,brightness);
        DrawLeagueCreditText(text.GetProperty("names").GetString()!,8,38,_sourceWhite,brightness);
    }
    private void DrawLeagueCreditSprite(Texture2D texture,int x,int y,int sourceY,int height,int top)
    {
        int first=Math.Max(y,top),last=Math.Min(y+height,160-top);
        if(last>first)DrawTextureRectRegion(texture,new(x,first,64,last-first),new(0,sourceY+first-y,64,last-first));
    }
    private void DrawLeagueCreditText(string text,int x,int y,SourceGameFont font,float brightness=1)
    {
        foreach(string line in text.Split('\n'))
        {
            var clear=Regex.Match(line,@"^\{CLEAR_TO 0x([0-9A-Fa-f]+)\}");int left=x;
            string visible=line;if(clear.Success){left+=Convert.ToInt32(clear.Groups[1].Value,16);visible=line[clear.Length..];}
            DrawString(font.Font,new(left,y+font.Baseline),font.PrepareText(visible),fontSize:font.Size,modulate:new Color(brightness,brightness,brightness));y+=14;
        }
    }
}
