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
    private PokemonLeaguePresentationData? _pokemonLeagueArt;
    private readonly Dictionary<string,Texture2D> _leagueTextures=new(StringComparer.Ordinal);
    private readonly Dictionary<string,JsonElement> _leagueTrainers=new(StringComparer.Ordinal);
    private string? _leagueRequestId,_leagueRecordWaitId;
    private CampaignPokemonLeagueCompletionStage? _leagueStage;
    private double _leagueElapsed,_leagueRecordElapsed;
    private int _leagueRecordTick=-1,_leagueDisplayCue=-1,_leagueCreditIndex=-1,_leagueCreditTick;
    private double _leagueCreditFrames,_leagueCreditMapFrames;
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
        if(!_catalog.PokemonLeagueEnabled)return;
        if(!_catalog.VictoryRoadEnabled||_session.RulesVersion!=CampaignSession.PokemonLeagueRulesVersion&&!(_catalog.NationalDexEnabled&&_session.RulesVersion==CampaignSession.NationalDexRulesVersion)&&!SeviiRubyPresentationProfile&&!FourIslandPresentationProfile)throw new InvalidDataException("Pokemon League presentation profile differs.");
        _pokemonLeagueArt=PokemonLeaguePresentationAssets.Load(_root);
        if(_battleArt.Count!=496||_storageIcons.Count!=124||_route22Footprints.Count!=93)throw new InvalidDataException("Pokemon League predecessor art differs.");
        foreach(var a in LeagueManifest.GetProperty("assets").EnumerateArray())
        {
            string id=a.GetProperty("id").GetString()!;int width=a.GetProperty("width").GetInt32(),height=a.GetProperty("height").GetInt32();
            var pixels=_pokemonLeagueArt.Files[a.GetProperty("rgbaPath").GetString()!];
            if(pixels.Length!=width*height*4)throw new InvalidDataException("Pokemon League pixel size differs.");
            using var image=Image.CreateFromData(width,height,false,Image.Format.Rgba8,pixels);_leagueTextures.Add(id,SceneTextures.FromImage(image));
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
            if(_catalog.PokemonLeagueOpponents?.Catalog.Species.GetValueOrDefault(new(p.GetProperty("speciesId").GetString()!))?.SourceSpeciesId!=species)throw new InvalidDataException("Unadmitted Pokemon League sprite.");
            _battleArt.Add((species,side,shiny),new(_leagueTextures[p.GetProperty("id").GetString()!],new(p.GetProperty("x").GetInt32(),p.GetProperty("y").GetInt32())));
            if(side=="front"&&!shiny)_portraits.Add(species,_leagueTextures[p.GetProperty("id").GetString()!]);
        }
        foreach(var p in LeagueManifest.GetProperty("pokemonIcons").EnumerateArray())_storageIcons.Add(new(p.GetProperty("speciesId").GetString()!),_leagueTextures[p.GetProperty("id").GetString()!]);
        if(_battleArt.Count!=532||_storageIcons.Count!=133||_route22Footprints.Count!=93)throw new InvalidDataException("Pokemon League art inventory differs.");
        Write("pokemonLeaguePresentation",new{PokemonLeaguePresentationAssets.SourceCommit,PokemonLeaguePresentationAssets.ManifestSha256,trainerCount=7,battleSprites=532,icons=133,ownedSpecies=115,creditsCommands=66,fullOriginalVisualFidelity=false,physicalAudioVerified=false});
    }
    private string? PokemonLeagueSpeciesName(int species) => _catalog.PokemonLeagueOpponents?.Catalog.Species.Values.Any(s=>s.SourceSpeciesId==species)==true
        ? (_pokedexMenu??throw new InvalidDataException("Pokemon League names missing.")).Entries[species].Name : null;
    private PokedexMenuTexture? PokemonLeagueDexPortrait(int species)
    {
        if(_pokemonLeagueArt is null)return null;
        var row=LeagueManifest.GetProperty("sprites").EnumerateArray().SingleOrDefault(s=>s.GetProperty("sourceSpeciesId").GetInt32()==species&&s.GetProperty("side").GetString()=="front"&&s.GetProperty("paletteVariant").GetString()=="normal");
        if(row.ValueKind==JsonValueKind.Undefined)return null;
        string png=row.GetProperty("texturePath").GetString()!,rgba=row.GetProperty("rgbaPath").GetString()!;
        string Hash(string path)=>LeagueManifest.GetProperty("outputs").EnumerateArray().Single(o=>o.GetProperty("path").GetString()==path).GetProperty("sha256").GetString()!;
        return new(PokemonLeaguePresentationAssets.Base+png,Hash(png),Hash(rgba),64,64);
    }
    private byte[]? ReadPokemonLeagueDexTexture(PokedexMenuTexture texture)
    {
        if(_pokemonLeagueArt is null)return null;
        foreach(var row in LeagueManifest.GetProperty("sprites").EnumerateArray())
            if(row.GetProperty("side").GetString()=="front"&&row.GetProperty("paletteVariant").GetString()=="normal"&&texture==PokemonLeagueDexPortrait(row.GetProperty("sourceSpeciesId").GetInt32()))return _pokemonLeagueArt.Files[row.GetProperty("texturePath").GetString()!];
        return null;
    }
    private string? VisiblePokemonLeagueTrainerText(string key)
    {
        if(_session.State.ForestTrainerBattle is not {} battle||!_leagueTrainers.TryGetValue(battle.Trainer.Value,out var row))return null;
        string text=row.GetProperty(key).GetString()!;return key=="portrait"?"league-"+text:text;
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
    private bool TryPokemonLeaguePatch(Image canvas,FieldMetatileEdit edit,int layer,MapId map)
    {
        if(_catalog.PokemonLeagueRegion?.NewMaps.ContainsKey(map)!=true)return false;
        var tile=LeagueManifest.GetProperty("tiles").EnumerateArray().SingleOrDefault(t=>t.GetProperty("metatileId").GetInt32()==edit.SourceMetatileId);
        if(tile.ValueKind==JsonValueKind.Undefined)throw new InvalidDataException("Pokemon League metatile art missing.");
        using var image=_leagueTextures["doors-"+layer].GetImage();canvas.BlitRect(image,new(tile.GetProperty("x").GetInt32(),tile.GetProperty("y").GetInt32(),16,16),new(edit.X*16,edit.Y*16));return true;
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
        if(c.Stage==CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame){_leagueDisplayCue=-1;SourceSound("SE_SAVE");}
        if(c.Stage==CampaignPokemonLeagueCompletionStage.Credits)
        {_leagueCreditIndex=-1;_leagueCreditTick=0;_leagueCreditFrames=0;_leagueCreditMapFrames=0;_leagueCreditMap=-1;}
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
            _leagueCreditFrames+=delta*60;int next=(int)_leagueCreditFrames;
            while(_leagueCreditTick<next&&LeagueCompletion?.Stage==CampaignPokemonLeagueCompletionStage.Credits)
            {
                _leagueCreditTick++;_leagueCreditMapFrames++;
                int duration=LeagueCreditDuration;
                if(_leagueCreditTick<duration)continue;
                _leagueCreditFrames-=duration;_leagueCreditTick-=duration;next-=duration;
                _leagueCreditIndex++;
                if(_leagueCreditIndex>=LeagueCredits.GetProperty("commands").GetArrayLength())
                {Apply(new("league_credits_complete",Text:c.RequestId));break;}
                var command=LeagueCreditCommand;
                if(command.GetProperty("op").GetString() is "MAP" or "MAPNEXT")
                {_leagueCreditMap=LeagueCreditMapIndex(command.GetProperty("param").GetString()!);_leagueCreditMapFrames=0;}
                if(command.GetProperty("op").GetString()=="MON")SourceCrySymbol(CreditSpecies(command.GetProperty("param").GetString()!).ToString());
            }
            return true;
        }
        if(c.Stage==CampaignPokemonLeagueCompletionStage.ResetRequested)
        {
            ResetPlayTimeHost();ResetSourceAudio();_held=null;_repeat=0;_tracks.Clear();_nicknameEdit.Visible=false;
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
                else if(c.Stage==CampaignPokemonLeagueCompletionStage.Credits&&_leagueCreditIndex>=0&&LeagueCreditCommand.GetProperty("op").GetString()=="WAITBUTTON")Apply(new("league_credits_complete",Text:c.RequestId));
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
    private static readonly (int X,int Y,int EndX,int EndY)[] HallFull=[(120,210,120,40),(326,220,56,40),(-86,220,184,40),(120,-62,120,88),(-70,-92,200,88),(310,-92,40,88)];
    private static readonly (int X,int Y,int EndX,int EndY)[] HallHalf=[(120,234,120,64),(326,244,56,64),(-86,244,184,64)];
    private static (int Current,int Moving,int MotionTick,int ApplauseTick,int PlayerTick,bool Ready) HallDisplayPosition(int count,int tick)
    {
        tick-=34;if(tick<0)return(-1,-1,0,-1,-1,false);
        for(int i=0;i<count;i++)
        {
            var p=(count>3?HallFull:HallHalf)[i];int motion=Math.Max(Math.Abs(p.X-p.EndX)/15,Math.Abs(p.Y-p.EndY)/10)+1;
            if(tick<motion)return(i-1,i,tick,-1,-1,false);
            tick-=motion;if(tick<122)return(i,-1,0,-1,-1,false);tick-=122;
        }
        if(tick<400)return(count,-1,0,tick,-1,false);tick-=410;
        return(count+1,-1,0,-1,Math.Max(0,tick),tick>=194);
    }
    private bool DrawPokemonLeagueScreen()
    {
        if(LeagueCompletion is not {} c)return false;
        if(c.Stage==CampaignPokemonLeagueCompletionStage.Credits){DrawPokemonLeagueCredits();return true;}
        if(c.Stage is not (CampaignPokemonLeagueCompletionStage.AwaitingSave or CampaignPokemonLeagueCompletionStage.SaveFailed or CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame))return false;
        DrawHallBackground();
        if(c.Stage!=CampaignPokemonLeagueCompletionStage.DisplayingHallOfFame)
        {
            ItemMessageBox(c.Stage==CampaignPokemonLeagueCompletionStage.SaveFailed?"Save failed. Your progress has not\nbeen confirmed. Enter: retry.":LeagueManifest.GetProperty("texts").GetProperty("gText_SavingDontTurnOffThePower2").GetString()!);
            if(_leagueOverwritePrompt)
            {
                ItemMessageBox("A different save already exists.\nOverwrite this save?");_battleSkin.DrawStandardFrame(this,new(136,64,96,48));
                SourceText(_sourceNormal,new(160,72),"YES");SourceText(_sourceNormal,new(160,88),"NO");SourceText(_sourceNormal,new(144,_leagueOverwriteYes?72:88),"▶");
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
            DrawTexture(texture,position-new Vector2(32,32),phase.PlayerTick>=0||i<phase.Current&&phase.Current<team.Members.Length?new Color(.65f,.68f,.75f):Colors.White);
        }
        if(phase.Current>=0&&phase.Current<team.Members.Length&&phase.Moving<0)
        {
            var mon=team.Members[phase.Current];_battleSkin.DrawStandardFrame(this,new(0,112,240,48));
            if(mon.SpeciesOrEgg==412)
            {
                // GetMonData(NICKNAME) returns EGG, not the internal Japanese Egg name.
                // Source HallOfFame_PrintMonInfo omits dex/species/gender/level/ID entirely.
                float width=_sourceNormal.Font.GetStringSize(_sourceNormal.PrepareText("EGG"),fontSize:_sourceNormal.Size).X;
                SourceText(_sourceNormal,new(128-width/2,118),"EGG");
            }
            else
            {
            SourceText(_sourceSmall,new(8,118),"No."+mon.SpeciesOrEgg.ToString("D3")+" "+mon.Nickname+" / "+PresentedBattleSpeciesName(mon.SpeciesOrEgg));
            SourceText(_sourceNormal,new(24,136),"Lv. "+mon.Level+"     IDNo."+((ushort)mon.OriginalTrainerId).ToString("D5"));
            }
        }
        else if(phase.ApplauseTick>=0)
        {
            SourceText(_sourceNormal,new(16,121),LeagueManifest.GetProperty("texts").GetProperty("gText_WelcomeToHOF").GetString()!);
            // Cosmetic motion uses no campaign RNG. The original RNG/trajectory is a remaining fidelity gap.
            for(int i=0;i<72;i++){int age=phase.ApplauseTick-i*4;if(age is <0 or >180)continue;int x=(i*73+29)%240,y=-8+age;DrawTextureRectRegion(_leagueTextures["confetti"],new(x,y,8,8),new(0,i%17*8,8,8));}
        }
        else if(phase.PlayerTick>=0)
        {
            DrawTexture(_leagueTextures[_session.State.PlayerGender==0?"red-front":"leaf-front"],new(Math.Min(192,120+Math.Max(0,phase.PlayerTick-120))-32,40));
            if(phase.Ready)
            {
                _battleSkin.DrawStandardFrame(this,new(8,8,144,64));SourceText(_sourceNormal,new(20,17),"NAME  "+_session.State.PlayerName);
                SourceText(_sourceNormal,new(20,33),"IDNo. "+((ushort)_session.State.TrainerId).ToString("D5"));SourceText(_sourceNormal,new(20,49),"TIME  "+FormatPlayTime(_session.State.PlayTime));
                if(_session.State.PlayTime.SinceCheckpoint)SourceText(_sourceSmall,new(8,83),CheckpointTimeNote);
                ItemMessageBox(LeagueManifest.GetProperty("texts").GetProperty("gText_LeagueChamp").GetString()!);
            }
        }
        return true;
    }
    private void DrawHallBackground()
    {
        var tex=_leagueTextures["hof-tiles"];
        for(int y=0;y<20;y++)for(int x=0;x<30;x++)
        {
            int tile=y<2||y>=14?1:2;
            DrawTextureRectRegion(tex,new(x*8,y*8,8,8),new(tile*8,0,8,8));
        }
    }
    private JsonElement LeagueCreditCommand=>LeagueCredits.GetProperty("commands")[_leagueCreditIndex];
    private int LeagueCreditDuration=>_leagueCreditIndex<0?504:LeagueCreditCommand.GetProperty("op").GetString()=="MON"?300:Math.Max(1,LeagueCreditCommand.GetProperty("ticks").GetInt32());
    private static int CreditSpecies(string symbol)=>symbol switch{"CHARIZARD"=>6,"VENUSAUR"=>3,"BLASTOISE"=>9,"PIKACHU"=>25,_=>throw new InvalidDataException("Unknown credits Pokemon.")};
    private static int LeagueCreditMapIndex(string name)=>Array.IndexOf(new[]{"ROUTE23","VIRIDIAN_CITY","PEWTER_CITY","CERULEAN_CITY","ROUTE25","VERMILION_CITY","ROUTE10","CELADON_CITY","SAFFRON_CITY","ROUTE17","FUCHSIA_CITY","CINNABAR_ISLAND","ROUTE21_NORTH"},name);
    private static readonly string[] CreditMapIds=["map.route-23","map.viridian-city","map.pewter-city","map.cerulean-city","map.route-25","map.vermilion-city","map.route-10","map.celadon-city","map.saffron-city","map.route-17","map.fuchsia-city","map.cinnabar-island","map.route-21-north"];
    private void DrawPokemonLeagueCredits()
    {
        string op=_leagueCreditIndex<0?"TITLE":LeagueCreditCommand.GetProperty("op").GetString()!;
        string param=_leagueCreditIndex<0?"":LeagueCreditCommand.GetProperty("param").GetString()!;
        DrawRect(new(0,0,240,160),Colors.Black);
        if(op is "THEENDGFX" or "WAITBUTTON")
        {DrawTexture(_leagueTextures[op=="THEENDGFX"&&param=="ALLRIGHTSRESERVED"?"credits-copyright":"credits-the_end"],Vector2.Zero);return;}
        if(op=="MON")
        {
            var sprite=_battleArt[(CreditSpecies(param),"front",false)];DrawTexture(sprite.Texture,new(88,48));return;
        }
        MapId map=_leagueCreditMap<0?_session.State.World.Player.MapId:new(CreditMapIds[_leagueCreditMap]);
        MapCache.SetPins([map.Value]);var art=GetMapArt(map);Vector2 camera;
        if(_leagueCreditMap<0)camera=new(11*16-120,6*16-80);
        else
        {
            var scene=LeagueCredits.GetProperty("maps")[_leagueCreditMap];float scroll=(float)(_leagueCreditMapFrames/4);
            camera=new(scene.GetProperty("x").GetInt32()*16-120+scene.GetProperty("dx").GetInt32()*scroll,scene.GetProperty("y").GetInt32()*16-80+scene.GetProperty("dy").GetInt32()*scroll);
        }
        camera=camera.Floor();DrawTexture(art.Bottom,-camera);DrawTexture(art.Middle,-camera);DrawTexture(art.Top,-camera);DrawRect(new(0,0,240,160),new(0,0,0,.5f));
        // Original six-frame character and eight-frame ground sheets, source eight-tick cadence.
        int frame=(int)(_leagueCreditMapFrames/8);string player=_session.State.PlayerGender==0?"credits-player_male":"credits-player_female";
        DrawTextureRectRegion(_leagueTextures["credits-ground_grass"],new(152,96,64,32),new(0,frame%8*32,64,32));
        DrawTextureRectRegion(_leagueTextures[player],new(152,44,64,64),new(0,frame%6*64,64,64));
        if(op=="TITLE"){DrawLeagueCreditText(LeagueCredits.GetProperty("title").GetString()!,8,70,_sourceWhite);return;}
        if(op!="PRINT")return;
        var text=LeagueCredits.GetProperty("texts").GetProperty(param);
        DrawLeagueCreditText(text.GetProperty("title").GetString()!,2,38,_sourceWhite);
        DrawLeagueCreditText(text.GetProperty("names").GetString()!,8,38,_sourceWhite);
    }
    private void DrawLeagueCreditText(string text,int x,int y,SourceGameFont font)
    {
        foreach(string line in text.Split('\n'))
        {
            var clear=Regex.Match(line,@"^\{CLEAR_TO 0x([0-9A-Fa-f]+)\}");int left=x;
            string visible=line;if(clear.Success){left+=Convert.ToInt32(clear.Groups[1].Value,16);visible=line[clear.Length..];}
            SourceText(font,new(left,y),visible);y+=14;
        }
    }
}
