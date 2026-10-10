using Game.Application;
using Game.Content;
using Game.Domain.Events;
using Game.Domain.FirstBattle;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Godot;

public partial class CampaignView
{
    private AudioDeliveryCatalog? _originalAudio;
    private TownRouteAudioCatalog? _townRouteAudio;
    private Route22CryAudioCatalog? _route22CryAudio;
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
        if(OS.GetEnvironment("FIRERED_AUDIO")!="1")return;
        string backend=OS.GetEnvironment("FIRERED_AUDIO_BACKEND");
        if(backend==""||backend=="synth")
        {
            try
            {
                var synthesized=new SynthSourceAudioPlayer(_root,includeRoute11:_catalog.Route11Enabled,includeRockTunnel:_catalog.RockTunnelEnabled,includeCeladonApproach:_catalog.CeladonApproachEnabled,includeCeladonGym:_catalog.CeladonGymEnabled,includeRocketHideout:_catalog.RocketHideoutEnabled,includePokemonTower:_catalog.PokemonTowerEnabled,includeSnorlax:_catalog.SnorlaxEnabled,includeFuchsiaRoute:_catalog.FuchsiaRouteEnabled);
                _sourceAudio=synthesized;AddChild(synthesized);
                synthesized.PlaybackEvent+=cue=>
                {
                    if(cue.Kind=="fault")_audioFaulted=true;
                    Write("audioPlayback",cue);
                };
                Write("audioCatalog",new{backend=SynthSourceAudioPlayer.BackendId,count=synthesized.RegisteredClipCount,
                    SynthManifestIdentity.Sha256,outputDriver=AudioServer.GetDriverName(),audibleOutputVerified=false,
                    sourceSampleRate=48000,queueUpperBoundMs=synthesized.QueueBoundMilliseconds,recordedFallback=false});
                if(_catalog.FuchsiaRouteEnabled)Write("fuchsiaRouteAudioCatalog",new{FuchsiaRouteSynthManifestIdentity.Sha256,FuchsiaRouteSynthManifestIdentity.CueCount,priorCueCount=299,audibleOutputVerified=false});
                if(_catalog.SnorlaxEnabled)Write("snorlaxAudioCatalog",new{SnorlaxSynthManifestIdentity.Sha256,SnorlaxSynthManifestIdentity.CueCount,priorCueCount=297,encounterCryMode=2,audibleOutputVerified=false});
                if(_catalog.PokemonTowerEnabled)Write("pokemonTowerAudioCatalog",new{PokemonTowerSynthManifestIdentity.Sha256,PokemonTowerSynthManifestIdentity.CueCount,priorCueCount=286,audibleOutputVerified=false});
                if(_catalog.RocketHideoutEnabled)Write("rocketHideoutAudioCatalog",new{RocketHideoutSynthManifestIdentity.Sha256,RocketHideoutSynthManifestIdentity.CueCount,priorCueCount=279,audibleOutputVerified=false});
                if(_catalog.CeladonGymEnabled)Write("celadonGymAudioCatalog",new{CeladonGymSynthManifestIdentity.Sha256,CeladonGymSynthManifestIdentity.CueCount,priorCueCount=271,audibleOutputVerified=false});
                if(_catalog.CeladonApproachEnabled)Write("celadonApproachAudioCatalog",new{CeladonApproachSynthManifestIdentity.Sha256,CeladonApproachSynthManifestIdentity.CueCount,priorCueCount=268,audibleOutputVerified=false});
                if(_catalog.RockTunnelEnabled)Write("rockTunnelAudioCatalog",new{RockTunnelSynthManifestIdentity.Sha256,RockTunnelSynthManifestIdentity.CueCount,priorCueCount=253,audibleOutputVerified=false});
                if(_catalog.Route11Enabled)Write("route11AudioCatalog",new{Route11SynthManifestIdentity.Sha256,
                    count=Route11SynthManifestIdentity.CueCount,oldManifestUnchanged=true,recordedFallback=false,audibleOutputVerified=false});
            }
            catch(Exception error) when(error is InvalidDataException or IOException or InvalidOperationException or NotSupportedException)
            {
                _audioFaulted=true;Write("audioFault",new{backend="synth",error=error.Message,recordedFallback=false});
            }
            return;
        }
        if(backend!=""&&backend!="recorded")
        {
            _audioFaulted=true;Write("audioFault",new{backend,error="Unknown audio backend",recordedFallback=false});return;
        }
        if(_catalog.SnorlaxEnabled){_audioFaulted=true;Write("audioFault",new{backend,error="Snorlax mode2 requires the source synthesizer; no recorded fallback is admitted.",recordedFallback=false});return;}
        _originalAudio=PretSmallAudioDelivery.Load(_root);
        var recorded=new SourceAudioPlayer();_sourceAudio=recorded;AddChild(recorded);
        _sourceAudio.PlaybackEvent+=cue=>Write("audioPlayback",cue);
        Write("audioCatalog",new{count=_originalAudio.Clips.Count,PretSmallAudioDelivery.ManifestSha256,sourceManifestSha256=PretEarlyAudio.ManifestSha256,
            sourceSoundMode="mono",soundCntH=0x3302,audioBytes=PretSmallAudioDelivery.AudioBytes,sampleRates=new[]{24000,48000},
            outputDriver=AudioServer.GetDriverName(),audibleOutputVerified=false,seamlessLoopVerified=false,musicCodec="vorbis-q0",effectCodec="qoa-mono"});
    }

    private bool EnsureAudio(string id)
    {
        if(_sourceAudio is null||_audioFaulted)return false;
        if(_sourceAudio.HasClip(id))return true;
        if(_sourceAudio is SynthSourceAudioPlayer)
        {
            Write("audioMissing",new{id,backend="synth",recordedFallback=false});return false;
        }
        if(_originalAudio is null)return false;
        if(_catalog.Route22Enabled&&id is "CRY_SPEAROW_NORMAL" or "CRY_FEAROW_NORMAL" or "CRY_MANKEY_NORMAL")
        {
            try
            {
                _route22CryAudio??=PretRoute22CryAudio.Load(_root);
                var m=_route22CryAudio.Clips[id];
                _sourceAudio.Register(VerifiedSourceAudioClip.FromQoa(new(m.Id,m.SourceCommit,m.SourceSymbol,m.SourcePath,
                    m.SourcePlayer,m.Priority,m.SourceCounter,m.Renderer),_route22CryAudio.ReadEncodedBytes(id),
                    m.EncodedSha256,m.SampleRate,m.Channels,m.LogicalFrameCount,m.LoopStartFrame,m.LoopEndFrame));
                Write("route22CryAudioCatalog",new{id,PretRoute22CryAudio.ManifestSha256,PretRoute22CryAudio.AudioBytes,
                    m.EncodedSha256,m.LogicalFrameCount,m.DirectCryStoppedVBlank,audibleOutputVerified=false});
                return true;
            }
            catch(Exception error) when(error is InvalidDataException or IOException or InvalidOperationException)
            {_audioFaulted=true;_sourceAudio.ResetPlayback();Write("audioFault",new{id,error=error.Message});return false;}
        }
        if(_catalog.TownMapEnabled && id is "MUS_DEX_RATING" or "MUS_ROUTE3" or "MUS_VICTORY_ROAD" or "SE_M_SPIT_UP" or "SE_M_HYPER_BEAM2")
        {
            try
            {
                _townRouteAudio ??= PretTownRouteAudio.Load(_root);
                var m=_townRouteAudio.Clips[id];var bytes=_townRouteAudio.ReadEncodedBytes(id);
                var extraMetadata=new SourceAudioMetadata(m.Id,m.SourceCommit,m.SourceSymbol,m.SourcePath,m.SourcePlayer,m.Priority,m.SourceCounter,m.Renderer);
                _sourceAudio.Register(m.Codec switch
                {
                    "vorbis"=>VerifiedSourceAudioClip.FromOgg(extraMetadata,bytes,m.EncodedSha256,m.SampleRate,m.Channels,m.LogicalFrameCount,m.LoopStartFrame,m.LoopEndFrame),
                    "qoa"=>VerifiedSourceAudioClip.FromQoa(extraMetadata,bytes,m.EncodedSha256,m.SampleRate,m.Channels,m.LogicalFrameCount,m.LoopStartFrame,m.LoopEndFrame),
                    _=>throw new InvalidDataException("Unsupported town/route codec: "+m.Codec)
                });
                Write("townRouteAudioCatalog",new{id,PretTownRouteAudio.ManifestSha256,PretTownRouteAudio.AudioBytes,m.EncodedSha256,m.SourcePcmSha256,m.SourceCounter,m.SourceSequenceStoppedVBlank,audibleOutputVerified=false});
                return true;
            }
            catch(Exception error) when(error is InvalidDataException or IOException or InvalidOperationException)
            {_audioFaulted=true;_sourceAudio.ResetPlayback();Write("audioFault",new{id,error=error.Message});return false;}
        }
        if(id=="SE_REPEL"&&_catalog.RepelEnabled)
        {
            try
            {
                var asset=PretRepelAudio.Load(_root);var m=asset.Metadata;
                _sourceAudio.Register(VerifiedSourceAudioClip.FromQoa(new(m.Id,m.SourceCommit,m.SourceSymbol,m.SourcePath,
                    m.SourcePlayer,m.Priority,m.SourceCounter,m.Renderer),asset.Data,m.EncodedSha256,m.SampleRate,
                    m.Channels,m.LogicalFrameCount,m.LoopStartFrame,m.LoopEndFrame));
                Write("repelAudioCatalog",new{PretRepelAudio.ManifestSha256,PretRepelAudio.EncodedBytes,
                    m.EncodedSha256,m.SourcePcmSha256,m.SourceSequenceStoppedVBlank,audibleOutputVerified=false});
                return true;
            }
            catch(Exception error) when(error is InvalidDataException or IOException or InvalidOperationException)
            { _audioFaulted=true;_sourceAudio.ResetPlayback();Write("audioFault",new{id,error=error.Message});return false; }
        }
        if(!_originalAudio.Clips.TryGetValue(id,out var clip)){Write("audioMissing",new{id});return false;}
        var original=clip.Original;
        var metadata=new SourceAudioMetadata(clip.Id,original.SourceCommit,original.SourceSymbol,original.SourcePath,original.SourcePlayer,
            original.Priority,original.SourceCounter,original.Renderer,original.IsOriginalTestFixture);
        try
        {
            byte[] bytes=_originalAudio.ReadEncodedBytes(id);
            _sourceAudio.Register(clip.Codec switch
            {
                "vorbis"=>VerifiedSourceAudioClip.FromOgg(metadata,bytes,clip.Sha256,clip.SampleRate,clip.Channels,clip.LogicalFrameCount,clip.LoopStartFrame,clip.LoopEndFrame),
                "qoa"=>VerifiedSourceAudioClip.FromQoa(metadata,bytes,clip.Sha256,clip.SampleRate,clip.Channels,clip.LogicalFrameCount,clip.LoopStartFrame,clip.LoopEndFrame),
                "pcm_s16le_wav"=>VerifiedSourceAudioClip.FromWav(metadata,bytes,clip.Sha256,clip.PcmSha256!,clip.LoopStartFrame,clip.LoopEndFrame),
                _=>throw new InvalidDataException("Unsupported audio delivery codec: "+clip.Codec)
            });
            return true;
        }
        catch(Exception error) when(error is InvalidDataException or IOException or InvalidOperationException)
        {
            // Playback must not throw after an authoritative command already committed,
            // drop its evidence record, or strand a source wait. Never substitute another sound.
            _audioFaulted=true;_sourceAudio.ResetPlayback();Write("audioFault",new{id,error=error.Message});return false;
        }
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
        if(id=="MUS_DUMMY"){_sourceAudio?.FadeOutMusic(0);_audioMusic=id;return;}
        if(!EnsureAudio(id))return;
        _audioMusic=id;
        // PLAYBGM is not a player-number declaration: Jigglypuff is actually routed to SE2.
        if(SourceAudioPlayerNumber(id)==0)_sourceAudio!.PlayMusic(id,restart);
        else _sourceAudio!.PlaySoundEffect(id);
    }

    private int SourceAudioPlayerNumber(string id) => _sourceAudio is SynthSourceAudioPlayer synthesized?synthesized.Metadata(id).SourcePlayer:id=="SE_REPEL"?1:
        _townRouteAudio?.Clips.TryGetValue(id,out var extra)==true?extra.SourcePlayer:_originalAudio!.Clips[id].Original.SourcePlayer;

    private void SourceCry(PokemonInstance pokemon)=>SourceCrySymbol(pokemon.Species.Value.Replace("pret:","",StringComparison.Ordinal));
    private void SourceCrySymbol(string species,int mode=0)
    {
        if(_sourceAudio is SynthSourceAudioPlayer synthesized)
        {
            if(int.TryParse(species,out int sourceSpecies))species=synthesized.CrySymbol(sourceSpecies)??species;
            if(mode!=0&&!(mode==2&&_catalog.SnorlaxEnabled&&species=="SPECIES_SNORLAX")){_audioFaulted=true;Write("audioFault",new{species,mode,error="Unsupported source cry mode/species",recordedFallback=false});return;}
            string cueId="CRY_"+species.Replace("SPECIES_","",StringComparison.Ordinal)+(mode==2?"_ENCOUNTER":"_NORMAL");
            if(EnsureAudio(cueId))synthesized.PlayCry(cueId);
            return;
        }
        if(mode!=0){if(_sourceAudio is not null){_audioFaulted=true;Write("audioFault",new{species,mode,error="Recorded backend has no admitted encounter cry",recordedFallback=false});}return;}
        if(_originalAudio is null)return;
        if(int.TryParse(species,out int number))
            species=_catalog.Route22Pokemon?.Catalog.Species.Values.FirstOrDefault(s=>s.SourceSpeciesId==number)?.Id.Value.Replace("pret:","",StringComparison.Ordinal)
                ??_originalAudio.Clips.Values.FirstOrDefault(c=>c.Original.SourcePlayer==4&&_catalog.TryConstant(c.Original.SourceSymbol,out int n)&&n==number)?.Original.SourceSymbol??species;
        string id="CRY_"+species.Replace("SPECIES_","",StringComparison.Ordinal)+"_NORMAL";
        if(EnsureAudio(id))_sourceAudio!.PlayCry(id);
    }

    private void ResetSourceAudio()
    {
        _sourceAudio?.ResetPlayback();_audioMusic=null;_audioContext=null;_audioPresentedEvent=null;_audioMap=null;_audioFieldMusic=null;_audioSavedMusic="MUS_DUMMY";
        _audioSequence=_session.State.Script.EffectSequence;_audioTrainerOneShots.Clear();
    }

    private static string AudioContext(CampaignState s)=>s.OldManTutorial is not null?"old-man":
        s.Battle is {} lab?lab.Id:s.WildBattle is {} wild?wild.Id:
        s.ForestTrainerBattle is {Stage:not CampaignForestTrainerStage.Intro} trainer?trainer.Id:"field";

    private string DesiredSourceMusic(CampaignState s)
    {
        if(s.OldManTutorial is not null)return s.Music;
        if(s.ForestTrainerBattle is {Stage:CampaignForestTrainerStage.Intro} intro)
            return s.Dialogue is {} dialogue&&_audioTrainerOneShots.Contains($"{intro.Id}:intro-text:{dialogue.Page}")
                ? "MUS_ENCOUNTER_GYM_LEADER"
                : intro.Trainer==PewterTrainerPartyFactory.Brock?_catalog.MapDefinition(s.World.Player.MapId).Music:s.Music;
        if(s.Battle is {} lab)return lab.Phase==CampaignBattlePhase.Results&&lab.State.Outcome==FirstBattleOutcome.Won?"MUS_VICTORY_TRAINER":"MUS_VS_TRAINER";
        if(s.ForestTrainerBattle is {Stage:not CampaignForestTrainerStage.Intro} trainer)
        {
            bool leader=trainer.Trainer==PewterTrainerPartyFactory.Brock||trainer.Trainer==CeruleanTrainerPartyFactory.Misty||trainer.Trainer==VermilionGymTrainerPartyFactory.LtSurge||_catalog.CeladonGymEnabled&&trainer.Trainer.Value=="pret:TRAINER_LEADER_ERIKA"||IsFuchsiaGymLeader(trainer.Trainer.Value);
            bool victory=trainer.Combat.Phase==ForestTrainerCombatPhase.Ended&&trainer.Combat.Battle.Outcome==WildBattleOutcome.Won;
            return victory?(leader?"MUS_VICTORY_GYM_LEADER":"MUS_VICTORY_TRAINER"):(leader?"MUS_VS_GYM_LEADER":"MUS_VS_TRAINER");
        }
        if(s.WildBattle is {} wild)
            return wild.Capture is not null?"MUS_CAUGHT":wild.Phase==CampaignBattlePhase.Results&&wild.State.Outcome==WildBattleOutcome.Won?"MUS_VICTORY_WILD":"MUS_VS_WILD";
        if(_catalog.SsAnneEnabled&&s.World.Player.MapId.Value==SsAnneSourceDeparture.Exterior&&s.Music=="MUS_SURF")return s.Music;
        if(_catalog.CityServicesEnabled&&s.IsCycling&&(_audioFieldMusic is null||_audioFieldMusic==_catalog.MapDefinition(s.World.Player.MapId).Music))return "MUS_CYCLING";
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
                case "showmonpic" when _catalog.BillEnabled&&after.World.Player.MapId.Value==BillSourcePresentation.Cottage:
                    SourceCrySymbol(effect.Args[0]);break;
                case "playse":SourceSound(effect.Args[0]);break;
                case "playfanfare":SourceSound(effect.Args[0],fanfare:true);break;
                case "playmoncry":
                    if(effect.Args.Length!=2||!(int.TryParse(effect.Args[1],out int cryMode)||_catalog.TryConstant(effect.Args[1],out cryMode))){_audioFaulted=true;Write("audioFault",new{error="Unresolved source cry mode",recordedFallback=false});break;}
                    SourceCrySymbol(effect.Args[0],cryMode);break;
            }
        }
        SourceMusic(DesiredSourceMusic(after));
        if(IsFuchsiaLedgeStep(before,after))SourceSound("SE_LEDGE");
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
        ProcessFuchsiaTransformAudio();
        ProcessSourceLeaderIntroAudio();
        ProcessSourceLeaderBadgeAudio();
        if(_session.State.Battle is {Phase:CampaignBattlePhase.Events} lab&&lab.EventIndex<lab.Events.Length)
        {
            string key=$"{lab.Id}:{lab.Events[lab.EventIndex].Sequence}:{_battlePage}";
            if(key==_audioPresentedEvent)return;_audioPresentedEvent=key;
            if(lab.Events[lab.EventIndex] is FirstBattleStartedEvent)
            {if(_battlePage==1)SourceCry(lab.DisplayOpponent);else if(_battlePage==2)SourceCry(lab.DisplayPlayer);}
        }
        else if(_catalog.VermilionGymEnabled&&_session.State.ForestTrainerBattle is
            {CurrentTrainerEvent:ForestTrainerOpponentReplacedEvent replacement} replacing)
        {
            bool forcedOpponent=replacing.Presentation.Events.OfType<WildBattleForcedOutEvent>()
                .Any(e=>e.TrainerBattle&&e.Target==WildBattleSide.Opponent&&e.Sequence<replacement.Sequence);
            // Match the real message sequence: a living ordinary switch first withdraws;
            // a faint replacement or forced replacement has its send-out page immediately.
            int page=!forcedOpponent&&!SourceBatonSwitchBefore(replacing.Presentation,WildBattleSide.Opponent,replacement.Sequence)&&
                _catalog.CeruleanEnabled&&replacement.Previous.Pokemon.CurrentHp>0
                ? WildSourcePages("sText_Trainer1WithdrewPkmn",("B_BUFF1",_catalog.PokemonContent!.SpeciesNames[replacement.Previous.Pokemon.SourceSpeciesId])).Length:0;
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
        if(_sourceAudio is null||_audioFaulted||!_catalog.PewterEnabled||!FieldDialogueVisible||
            _session.State.ForestTrainerBattle is not {Stage:CampaignForestTrainerStage.Intro} trainer||
            _session.State.Dialogue is not {} dialogue||
            !(trainer.Trainer==PewterTrainerPartyFactory.Brock||trainer.Trainer==CeruleanTrainerPartyFactory.Misty||
              trainer.Trainer==VermilionGymTrainerPartyFactory.LtSurge||_catalog.CeladonGymEnabled&&trainer.Trainer.Value=="pret:TRAINER_LEADER_ERIKA"||IsFuchsiaGymLeader(trainer.Trainer.Value)))return;
        // All five pinned intro controls are at page end. Waiting for the printer's
        // EOS step includes its final glyph delay; page entry or last glyph is too early.
        string raw=dialogue.Pages[dialogue.Page];
        if(!raw.EndsWith("{PLAY_BGM}{MUS_ENCOUNTER_GYM_LEADER}",StringComparison.Ordinal)&&
            !raw.EndsWith("{PLAY_BGM MUS_ENCOUNTER_GYM_LEADER}",StringComparison.Ordinal))return;
        if(FieldTextProgressEnabled&&(_fieldPrintedDialogue!=dialogue||!FieldTextComplete))return;
        if(_audioTrainerOneShots.Add($"{trainer.Id}:intro-text:{dialogue.Page}"))
            SourceMusic("MUS_ENCOUNTER_GYM_LEADER");
        // This presentation cue neither acknowledges text nor changes campaign Music.
    }

    private void ProcessSourceLeaderBadgeAudio()
    {
        if(!_catalog.VermilionGymEnabled||_session.State.ForestTrainerBattle is not
            {Stage:CampaignForestTrainerStage.Finished,Combat.Battle.Outcome:WildBattleOutcome.Won} trainer||
            trainer.Presentation is not {Phase:CampaignBattlePhase.Results} battle||battle.PageIndex<0||battle.PageIndex>=battle.Pages.Length||
            !(trainer.Trainer==PewterTrainerPartyFactory.Brock||trainer.Trainer==CeruleanTrainerPartyFactory.Misty||trainer.Trainer==VermilionGymTrainerPartyFactory.LtSurge||_catalog.CeladonGymEnabled&&trainer.Trainer.Value=="pret:TRAINER_LEADER_ERIKA"||IsFuchsiaGymLeader(trainer.Trainer.Value)))return;
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
