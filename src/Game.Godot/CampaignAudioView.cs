using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.Encounters;
using Game.Domain.FirstBattle;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Godot;

public partial class CampaignView
{
    private ISourceAudioBackend? _sourceAudio;
    private long _audioSequence;
    private bool _audioFaulted;
    private string? _audioMusic, _audioContext, _audioPresentedEvent;
    private string? _audioMap, _audioFieldMusic;
    private readonly HashSet<string> _audioTrainerOneShots=new(StringComparer.Ordinal);
    private string _audioSavedMusic="MUS_DUMMY";
    private const double SourceAudioFramesPerSecond=16777216.0/280896;

    private void LoadSourceAudio()
    {
        if(OS.GetEnvironment("FIRERED_AUDIO")=="0")return;
        string backend=OS.GetEnvironment("FIRERED_AUDIO_BACKEND");
        if(backend==""||backend=="synth")
        {
            try
            {
                var synthesized=new SynthSourceAudioPlayer(_root);
                _sourceAudio=synthesized;SynchronizeSavedOptions();AddChild(synthesized);_audioContext=AudioContext(_session.State);
                synthesized.PlaybackEvent+=cue=>
                {
                    if(cue.Kind=="fault")_audioFaulted=true;
                    Write("audioPlayback",cue);
                };
                Write("audioCatalog",new{backend=SynthSourceAudioPlayer.BackendId,count=synthesized.RegisteredClipCount,
        outputDriver=AudioServer.GetDriverName(),audibleOutputVerified=false,
        queueUpperBoundMs=synthesized.QueueBoundMilliseconds,recordedFallback=false});
                Write("powerPlantAudioCatalog",new{audibleOutputVerified=false,recordedFallback=false});
                Write("silphAudioCatalog",new{audibleOutputVerified=false});
                Write("surfAudioCatalog",new{audibleOutputVerified=false});
                Write("safariAudioCatalog",new{audibleOutputVerified=false});
                Write("fuchsiaRouteAudioCatalog",new{audibleOutputVerified=false});
                Write("snorlaxAudioCatalog",new{audibleOutputVerified=false});
                Write("pokemonTowerAudioCatalog",new{audibleOutputVerified=false});
                Write("rocketHideoutAudioCatalog",new{audibleOutputVerified=false});
                Write("celadonGymAudioCatalog",new{audibleOutputVerified=false});
                Write("celadonApproachAudioCatalog",new{audibleOutputVerified=false});
                Write("rockTunnelAudioCatalog",new{audibleOutputVerified=false});
                Write("route11AudioCatalog",new{                    recordedFallback=false,audibleOutputVerified=false});
            }
            catch(Exception error) when(error is InvalidDataException or IOException or InvalidOperationException or NotSupportedException)
            {
                _audioFaulted=true;Write("audioFault",new{backend="synth",error=error.Message,recordedFallback=false});
            }
            return;
        }
        _audioFaulted=true;
        Write("audioFault",new{backend,error="Only the synth audio backend is supported",recordedFallback=false});
    }

    private bool EnsureAudio(string id)
    {
        if(_sourceAudio is null||_audioFaulted)return false;
        if(_sourceAudio.HasClip(id))return true;
        Write("audioMissing",new{id,backend="synth",recordedFallback=false});return false;
    }

    private void SourceSound(string id,bool fanfare=false)
    {
        if(!EnsureAudio(id))return;
        if(fanfare)_sourceAudio!.PlayFanfare(id);
        else if(SourceAudioPlayerNumber(id)==0)SourceMusic(id);
        else _sourceAudio!.PlaySoundEffect(id);
    }

    private void SourceMusic(string id,bool restart=false)
    {
        // This legacy tutorial label predates audio playback; the real source symbol is VS_WILD.
        if(id=="MUS_BATTLE_WILD")id="MUS_VS_WILD";
        if(!restart&&_audioMusic==id)return;
        if(id is "MUS_DUMMY" or "MUS_NONE"){_sourceAudio?.FadeOutMusic(0);_audioMusic=id;return;}
        if(!EnsureAudio(id))return;
        _audioMusic=id;
        // PLAYBGM is not a player-number declaration: Jigglypuff is actually routed to SE2.
        if(SourceAudioPlayerNumber(id)==0)_sourceAudio!.PlayMusic(id,restart);
        else _sourceAudio!.PlaySoundEffect(id);
    }

    private int SourceAudioPlayerNumber(string id) => ((SynthSourceAudioPlayer)_sourceAudio!).Metadata(id).SourcePlayer;

    private void SourceCry(PokemonInstance pokemon)=>SourceCrySymbol(pokemon.Species.Value.Replace("pret:","",StringComparison.Ordinal));
    private void SourceCrySymbol(string species,int mode=0)
    {
        if(_sourceAudio is SynthSourceAudioPlayer synthesized)
        {
            if(int.TryParse(species,out int sourceSpecies))species=synthesized.CrySymbol(sourceSpecies)??species;
            if(synthesized.CryCueId(species,mode) is not {} cueId)
            {_audioFaulted=true;Write("audioFault",new{species,mode,error="Unsupported source cry mode/species",recordedFallback=false});return;}
            if(EnsureAudio(cueId))synthesized.PlayCry(cueId);
            return;
        }
    }

    private void ResetSourceAudio()
    {
        ResetSourceMoveAnimation("audio-reset");
        _sourceAudio?.ResetPlayback();_audioMusic=null;_audioContext=AudioContext(_session.State);_audioPresentedEvent=null;_audioMap=null;_audioFieldMusic=null;_audioSavedMusic="MUS_DUMMY";
        _audioSequence=_session.State.Script.EffectSequence;_audioTrainerOneShots.Clear();
    }

    private string AudioContext(CampaignState s) => OpeningAudioContext ?? (s.TeachyTv is {} tv?tv.Battle?.Ticket??"teachy-tv":s.NpcTrade is {Phase:CampaignNpcTradePhase.Presenting}?"npc-trade":s.Evolution is not null?"evolution":s.EggHatch is not null?"egg-hatch":s.OldManTutorial is not null?"old-man":
        s.SafariBattle is {} safari?safari.Id:s.DoubleBattle is {Stage:not CampaignDoubleBattleStage.Intro} pair?pair.Id:s.Battle is {} lab?lab.Id:s.WildBattle is {} wild?wild.Id:
        s.ForestTrainerBattle is {Stage:not CampaignForestTrainerStage.Intro} trainer?trainer.Id:"field");

    private string DesiredSourceMusic(CampaignState s)
    {
        if (OpeningSourceMusic is {} openingMusic) return openingMusic;
        if(s.TeachyTv is not null)return s.Music;
        if(s.NpcTrade is {Phase:CampaignNpcTradePhase.Presenting})return "MUS_EVOLUTION";
        if(s.Evolution is not null)return _evolutionAnimation?.Phase == EvolutionScenePhase.Intro ? "MUS_EVOLUTION_INTRO" : "MUS_EVOLUTION";
        if(s.EggHatch is {} hatch)return hatch.Stage==CampaignEggHatchStage.Animation&&EggHatchFrame<61?"MUS_EVOLUTION_INTRO":"MUS_EVOLUTION";
        if(PokemonLeagueMusic(s) is {} leagueMusic)return leagueMusic;
        if(s.OldManTutorial is not null)return s.Music;
        if(s.SafariBattle is {} safari)return safari.Capture is not null?"MUS_CAUGHT":"MUS_VS_WILD";
        if(s.DoubleBattle is {} pair)return pair.Stage==CampaignDoubleBattleStage.Intro?s.Music:pair.Stage==CampaignDoubleBattleStage.Finished&&pair.Frame.Outcome==WildBattleOutcome.Won?"MUS_VICTORY_TRAINER":"MUS_VS_TRAINER";
        if(s.ForestTrainerBattle is {Stage:CampaignForestTrainerStage.Intro} intro)
            return s.Dialogue is {} dialogue&&_audioTrainerOneShots.Contains($"{intro.Id}:intro-text:{dialogue.Page}")
                ? SourceLeaderIntroMusic(intro.Trainer.Value)
                : intro.Trainer==PewterTrainerPartyFactory.Brock?_catalog.MapDefinition(s.World.Player.MapId).Music:s.Music;
        if(s.Battle is {} lab)return lab.Phase==CampaignBattlePhase.Results&&lab.State.Outcome==FirstBattleOutcome.Won?"MUS_VICTORY_TRAINER":"MUS_VS_TRAINER";
        if(s.ForestTrainerBattle is {Stage:not CampaignForestTrainerStage.Intro} trainer)
        {
            bool leader=trainer.Trainer==PewterTrainerPartyFactory.Brock||trainer.Trainer==CeruleanTrainerPartyFactory.Misty||trainer.Trainer==VermilionGymTrainerPartyFactory.LtSurge||trainer.Trainer.Value=="pret:TRAINER_LEADER_ERIKA"||IsFuchsiaGymLeader(trainer.Trainer.Value)||IsSaffronGymLeader(trainer.Trainer.Value)||IsCinnabarGymLeader(trainer.Trainer.Value)||IsViridianGymLeader(trainer.Trainer.Value);
            bool victory=trainer.Combat.Phase==ForestTrainerCombatPhase.Ended&&trainer.Combat.Battle.Outcome==WildBattleOutcome.Won;
            return victory?(leader?"MUS_VICTORY_GYM_LEADER":"MUS_VICTORY_TRAINER"):(leader?"MUS_VS_GYM_LEADER":"MUS_VS_TRAINER");
        }
        if(s.WildBattle is {} wild)
            return wild.Capture is not null?"MUS_CAUGHT":wild.Phase==CampaignBattlePhase.Results&&wild.State.Outcome==WildBattleOutcome.Won?"MUS_VICTORY_WILD":
                SourceScriptedEncounterRegistry.Find(wild.Frame?.WildEncounter?.SourceTable)?.Legendary==true?"MUS_VS_LEGEND":
                wild.Frame?.WildEncounter?.Seafoam?.Kind==SeafoamEncounterKind.Articuno?"MUS_VS_LEGEND":
                wild.Frame?.WildEncounter?.PowerPlant?.Kind==PowerPlantEncounterKind.Zapdos?"MUS_VS_LEGEND":
                wild.EventIslandReceipt is {} eventIsland?(eventIsland.Origin.Kind==EventIslandEncounterKind.Deoxys?"MUS_VS_DEOXYS":"MUS_VS_LEGEND"):
                wild.Frame?.WildEncounter?.CeruleanCave?.Kind==CeruleanCaveEncounterKind.Mewtwo?"MUS_VS_MEWTWO":"MUS_VS_WILD";
        if(s.IsSurfing&&_catalog.SeafoamRegion.NewMaps.ContainsKey(s.World.Player.MapId))return s.Music;
        if(s.World.Player.MapId.Value==SsAnneSourceDeparture.Exterior&&s.Music=="MUS_SURF")return s.Music;
        if(s.IsCycling&&(_audioFieldMusic is null||_audioFieldMusic==_catalog.MapDefinition(s.World.Player.MapId).Music))return "MUS_CYCLING";
        return _audioFieldMusic??_catalog.MapDefinition(s.World.Player.MapId).Music;
    }

    private void SynchronizeSourceAudio(CampaignState? before,CampaignState after)
    {
        if(_sourceAudio is null)return;
        string context=AudioContext(after);
        if(_audioMap!=after.World.Player.MapId.Value)
        {
            _audioMap=after.World.Player.MapId.Value;_audioFieldMusic=_catalog.MapDefinition(after.World.Player.MapId).Music;
            _audioSavedMusic="MUS_DUMMY";
        }
        if(_audioContext!=context)
        {
            // Original battle entry stops the map players; return starts the map song anew.
            _sourceAudio.ResetPlayback();_audioMusic=null;_audioContext=context;_audioPresentedEvent=null;_audioTrainerOneShots.Clear();
            if(context=="field")_audioFieldMusic=_catalog.MapDefinition(after.World.Player.MapId).Music;
        }
        if(before is null){_audioSequence=after.Script.EffectSequence;SourceMusic(DesiredSourceMusic(after));return;}
        // PC animations and their sounds are separate source commands. The original
        // home/center scripts already emit PLAYSE; the visual state edge is not a cue.
        foreach(var effect in after.PresentationEffects.Where(e=>e.Sequence>_audioSequence))
        {
            _audioSequence=effect.Sequence;
            switch(effect.Opcode)
            {
                case "playbgm":
                    _audioFieldMusic=effect.Args[0];if(effect.Args[1] is "1" or "TRUE")_audioSavedMusic=effect.Args[0];
                    SourceMusic(effect.Args[0],restart:true);break;
                case "savebgm":_audioSavedMusic=effect.Args[0];break;
                case "fadedefaultbgm":
                    _audioFieldMusic=_audioSavedMusic=="MUS_DUMMY"?_catalog.MapDefinition(after.World.Player.MapId).Music:_audioSavedMusic;
                    SourceMusic(_audioFieldMusic,restart:true);break;
                case "fadeoutbgm":
                    int speed=int.Parse(effect.Args[0],System.Globalization.CultureInfo.InvariantCulture);
                    _sourceAudio.FadeOutMusic(16*(speed==0?4:4*speed)/SourceAudioFramesPerSecond,temporarily:true);break;
                case "fadeinbgm":
                    int fadeInSpeed=int.Parse(effect.Args[0],System.Globalization.CultureInfo.InvariantCulture);
                    _sourceAudio.FadeInMusic(16*(fadeInSpeed==0?4:4*fadeInSpeed)/SourceAudioFramesPerSecond);break;
                case "showmonpic" when after.World.Player.MapId.Value==BillSourcePresentation.Cottage:
                    SourceCrySymbol(effect.Args[0]);break;
                case "playse":SourceSound(effect.Args[0]);break;
                case "playfanfare":SourceSound(effect.Args[0],fanfare:true);break;
                case "playmoncry":
                    if(effect.Args.Length!=2||!(int.TryParse(effect.Args[1],out int cryMode)||_catalog.TryConstant(effect.Args[1],out cryMode))){_audioFaulted=true;Write("audioFault",new{error="Unresolved source cry mode",recordedFallback=false});break;}
                    SourceCrySymbol(effect.Args[0],cryMode);break;
            }
        }
        SourceMusic(DesiredSourceMusic(after));
        if(IsLedgeStep(before,after))SourceSound("SE_LEDGE");
    }

    private bool SourceAudioWaitReady()
    {
        if(_sourceAudio is null||_audioFaulted)return true;
        var snapshot=_sourceAudio.Snapshot();
        var opcode=_session.State.PresentationEffects.LastOrDefault()?.Opcode;
        return opcode switch
        {
            "waitfanfare"=>snapshot.FanfareToken is null,
            "waitse"=>!snapshot.Effects.Any(e=>e.SourcePlayer is 1 or 2&&e.Playing),
            "waitmoncry"=>snapshot.Cry is null||!snapshot.Cry.Playing,
            "fadeoutbgm"=>snapshot.Music is null||snapshot.Music.Paused,
            _=>_session.State.HealingEffect is null||snapshot.FanfareToken is null
        };
    }

    // The normal source cry is wired only to ordinary appearance/send-out pages.
    // Faint/attack cries need their own source mode, not a renamed normal recording.
    private void ProcessSourceBattleAudio()
    {
        if(_sourceAudio is null)return;
        ProcessSafariBattleAudio();
        ProcessDoubleBattleAudio();
        ProcessSourceLeaderIntroAudio();
        ProcessSourceLeaderBadgeAudio();
        if(_session.State.Battle is {Phase:CampaignBattlePhase.Events} lab&&lab.EventIndex<lab.Events.Length)
        {
            string key=$"{lab.Id}:{lab.Events[lab.EventIndex].Sequence}:{_battlePage}";
            if(key==_audioPresentedEvent)return;_audioPresentedEvent=key;
            if(lab.Events[lab.EventIndex] is FirstBattleStartedEvent)
            {if(_battlePage==1)SourceCry(lab.DisplayOpponent);else if(_battlePage==2)SourceCry(lab.DisplayPlayer);}
        }
        else if(_session.State.ForestTrainerBattle is
            {CurrentTrainerEvent:ForestTrainerOpponentReplacedEvent replacement} replacing)
        {
            bool forcedOpponent=replacing.Presentation.Events.OfType<WildBattleForcedOutEvent>()
                .Any(e=>e.TrainerBattle&&e.Target==WildBattleSide.Opponent&&e.Sequence<replacement.Sequence);
            // Match the real message sequence: a living ordinary switch first withdraws;
            // a faint replacement or forced replacement has its send-out page immediately.
            int page=!forcedOpponent&&!SourceBatonSwitchBefore(replacing.Presentation,WildBattleSide.Opponent,replacement.Sequence)&&replacement.Previous.Pokemon.CurrentHp>0
                ? WildSourcePages("sText_Trainer1WithdrewPkmn",("B_BUFF1",PresentedBattleSpeciesName(replacement.Previous.Pokemon.SourceSpeciesId))).Length:0;
            if(_wildPage==page&&_audioTrainerOneShots.Add($"{replacing.Id}:opponent-replaced:{replacement.Sequence}"))
                SourceCry(replacement.Replacement.Pokemon);
        }
        else if(_session.State.ForestTrainerBattle is {CurrentTrainerEvent:SsAnneTrainerPlayerForcedOutEvent forced} shipTrainer)
        {
            string key=$"{shipTrainer.Id}:forced-player:{shipTrainer.TrainerEventIndex}:{_wildPage}";
            if(key==_audioPresentedEvent)return;_audioPresentedEvent=key;
            if(_wildPage==0)SourceCry(forced.Replacement.Pokemon);
        }
        else if(_session.State.ForestTrainerBattle is {CurrentTrainerEvent:CeruleanTrainerItemUsedEvent} trainer)
        {
            string key=$"{trainer.Id}:trainer-item:{trainer.TrainerEventIndex}:{_wildPage}";
            if(key==_audioPresentedEvent)return;_audioPresentedEvent=key;
            if(_wildPage==0)SourceSound("SE_USE_ITEM");
        }
        else if(PresentedWildBattle is {Phase:CampaignBattlePhase.Events} wild&&WildCurrentEvent(wild) is {} e)
        {
            string key=$"{wild.Id}:{e.Sequence}:{_wildPage}";
            if(key==_audioPresentedEvent)return;_audioPresentedEvent=key;
            if(e is WildBattleStartedEvent)
            {if(!PokemonTowerStartedAudio(wild)){if(_wildPage==0)SourceCry(wild.DisplayOpponent);else if(_wildPage==1)SourceCry(wild.DisplayPlayer);}}
            if(e is WildBattlePokeFluteEvent {Phase:PokeFluteBattlePhase.Fanfare})SourceSound("MUS_POKE_FLUTE",fanfare:true);
            if(e is WildBattlePlayerSwitchedEvent sw&&_wildPage==(SourcePlayerBatonSwitch(wild)?0:1))SourceCry(sw.Replacement.Pokemon);
            if(e is WildBattlePlayerReplacedEvent re&&_wildPage==0)SourceCry(re.Replacement.Pokemon);
            if(e is WildBattleMedicineAppliedEvent&&_wildPage==0)SourceSound("SE_USE_ITEM");
        }
    }
    private void ProcessSourceLeaderIntroAudio()
    {
        if(_sourceAudio is null||_audioFaulted||!FieldDialogueVisible||
            _session.State.ForestTrainerBattle is not {Stage:CampaignForestTrainerStage.Intro} trainer||
            _session.State.Dialogue is not {} dialogue||
            !(trainer.Trainer==PewterTrainerPartyFactory.Brock||trainer.Trainer==CeruleanTrainerPartyFactory.Misty||
              trainer.Trainer==VermilionGymTrainerPartyFactory.LtSurge||trainer.Trainer.Value=="pret:TRAINER_LEADER_ERIKA"||IsFuchsiaGymLeader(trainer.Trainer.Value)||IsSaffronGymLeader(trainer.Trainer.Value)||IsCinnabarGymLeader(trainer.Trainer.Value)||IsViridianGymLeader(trainer.Trainer.Value)||_leagueTrainers.ContainsKey(trainer.Trainer.Value)))return;
        // The admitted source intro controls are at page end. Waiting for the printer's
        // EOS step includes its final glyph delay; page entry or last glyph is too early.
        string raw=dialogue.Pages[dialogue.Page];
        string music=SourceLeaderIntroMusic(trainer.Trainer.Value);
        if(!raw.EndsWith("{PLAY_BGM}{"+music+"}",StringComparison.Ordinal)&&
            !raw.EndsWith("{PLAY_BGM "+music+"}",StringComparison.Ordinal))return;
        if(FieldTextProgressEnabled&&(_fieldPrintedDialogue!=dialogue||!FieldTextComplete))return;
        if(_audioTrainerOneShots.Add($"{trainer.Id}:intro-text:{dialogue.Page}"))
            SourceMusic(music);
        // This presentation cue neither acknowledges text nor changes campaign Music.
    }

    private void ProcessSourceLeaderBadgeAudio()
    {
        if(_session.State.ForestTrainerBattle is not
            {Stage:CampaignForestTrainerStage.Finished,Combat.Battle.Outcome:WildBattleOutcome.Won} trainer||
            trainer.Presentation is not {Phase:CampaignBattlePhase.Results} battle||battle.PageIndex<0||battle.PageIndex>=battle.Pages.Length||
            !(trainer.Trainer==PewterTrainerPartyFactory.Brock||trainer.Trainer==CeruleanTrainerPartyFactory.Misty||trainer.Trainer==VermilionGymTrainerPartyFactory.LtSurge||trainer.Trainer.Value=="pret:TRAINER_LEADER_ERIKA"||IsFuchsiaGymLeader(trainer.Trainer.Value)||IsSaffronGymLeader(trainer.Trainer.Value)||IsCinnabarGymLeader(trainer.Trainer.Value)||IsViridianGymLeader(trainer.Trainer.Value)||_leagueTrainers.ContainsKey(trainer.Trainer.Value)))return;
        string raw=battle.Pages[battle.PageIndex].Text;
        int cue=raw.IndexOf("{PLAY_BGM}{MUS_OBTAIN_BADGE}",StringComparison.Ordinal);
        if(cue<0||!raw.Contains("{PAUSE_MUSIC}",StringComparison.Ordinal)||!raw.Contains("{RESUME_MUSIC}",StringComparison.Ordinal))return;
        // Source text.c performs these controls when the printer reaches them. Battle
        // text is currently displayed a whole page at a time; account for its two-line
        // scrolling projection, then play only when the control's page is presented.
        int page=Math.Max(0,raw.AsSpan(0,cue).Count('\n')-1);
        if(_wildPage==page&&_audioTrainerOneShots.Add($"{trainer.Id}:badge-text:{battle.PageIndex}"))
            SourceSound("MUS_OBTAIN_BADGE",fanfare:true);
        // The existing 340-frame source fanfare counter restores the paused music.
        // Audio never acknowledges a page, changes a wait, or edits campaign state.
    }

}
