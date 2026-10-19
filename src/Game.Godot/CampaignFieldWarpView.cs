using System.Collections.Immutable;
using Game.Application;
using Game.Content;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private sealed class FieldWarpAnimation
    {
        public required FieldTransitionReceipt Receipt;
        public required CampaignState Origin;
        public required CampaignState ArrivalState;
        public required FieldSnapshot OriginView;
        public required FieldSnapshot DestinationView;
        public readonly FieldWarpPresentation.Departure Departure=new();
        public FieldWarpPresentation.PlainArrival? Arrival;
        public FieldDoorPresentation.Departure? DoorDeparture;
        public FieldDoorPresentation.StepDeparture? StepDeparture;
        public FieldDoorPresentation.NonAnimatedArrival? StepArrival;
        public FieldDoorPresentation.AnimatedArrival? DoorArrival;
        public FieldStairPresentation.Departure? StairDeparture;
        public FieldStairPresentation.Arrival? StairArrival;
        public FieldDoorArtEntry? DepartureArt,ArrivalArt;
        public FieldTransitionInterlude Interlude;
        public FieldCaveTransitionPresentation.Exit? CaveExit;
        public FieldCaveTransitionPresentation.Enter? CaveEnter;
        public FieldCaveTransitionPresentation.CavePreview? Preview;
        public Game.Content.MapPreviewEntry? PreviewArt;
        public int PreviewDuration;
        public bool Arriving;
        public FieldDoorPresentation.PhaseSample Sample=>Arriving
            ?StairArrival is {} sa?FieldDoorPresentation.FromPlain(sa.Sample.Phase):DoorArrival?.Sample??StepArrival?.Sample??FieldDoorPresentation.FromPlain(Arrival!.Sample)
            :StairDeparture is {} sd?FieldDoorPresentation.FromPlain(sd.Sample.Phase):DoorDeparture?.Sample??StepDeparture?.Sample??FieldDoorPresentation.FromPlain(Departure.Sample);
        public double Frames;
        public int ProcessedFrames,SwitchFrame;
        public bool WaitForMusic;
    }
    private FieldWarpAnimation? _fieldWarp;
    private bool _fieldWarpSynchronizing,_fieldSnapshotReleasePending;
    private bool FieldWarpInputLocked=>_fieldWarp is not null;

    private static bool ConnectedFieldTransition(CampaignState? before,CampaignState after)=>
        before is not null&&after.FieldTransition is {Cause:FieldTransitionCause.ConnectedEdge} receipt&&
        receipt.From==before.World.Player&&receipt.To==after.World.Player;

    private FieldSnapshot CaptureFieldSnapshot(CampaignState state,object owner,int revision,bool keepPresentedPoses)
    {
        var scene=CampaignQuestLogRecorder.SavedScene(state,_catalog.QuestLogSource);
        // Source invisible script objects have no sprite. This decision comes from
        // the captured state, never the already-committed destination's actors.
        var actors=scene.Actors.Where(actor=>actor.Id==CampaignQuestLogRecorder.PlayerId||
            state.Actors.TryGetValue(actor.Id,out var source)&&source.MovementType!="MOVEMENT_TYPE_INVISIBLE").ToImmutableArray();
        var poses=new Dictionary<string,Pose>(StringComparer.Ordinal);
        foreach(var actor in actors)
        {
            Pose pose;
            if(keepPresentedPoses&&_poses.TryGetValue(actor.Id,out var visible))
                pose=new(){Position=visible.Position,Facing=visible.Facing,Visible=visible.Visible,Frame=visible.Frame,DrawYOffset=visible.DrawYOffset,IsJump=visible.IsJump};
            else pose=new(){Position=new(actor.X*16,actor.Y*16),Facing=actor.Facing,Visible=actor.Visible};
            poses.Add(actor.Id,pose);
        }
        return new(owner,0,revision,false,new(scene.Map,actors,scene.Layout,scene.Tiles),poses,keepPresentedPoses?_fieldSurfBlobFrames:0,state.PlayerGender);
    }

    // The authoritative movement command has already committed. This owns only
    // detached old/new visuals and the observed presentation boundary.
    private bool BeginFieldWarpPresentation(CampaignState? before,CampaignState after)
    {
        if(_fieldWarpSynchronizing||before is null||before.FieldWeather is not null||
            after.FieldTransition is not {Cause:FieldTransitionCause.PlainWarp or FieldTransitionCause.AnimatedDoor or FieldTransitionCause.NonAnimatedDoor or FieldTransitionCause.StairWarp} receipt||
            receipt.From!=before.World.Player||!_session.TryGetFieldTransitionArrival(before,receipt,out var arrivalState))return false;
        var owner=new object();
        var animation=new FieldWarpAnimation
        {
            Receipt=receipt,Origin=before,ArrivalState=arrivalState,Interlude=_session.FieldTransitionInterludeFor(receipt),
            OriginView=CaptureFieldSnapshot(before,owner,0,true),
            DestinationView=CaptureFieldSnapshot(arrivalState,owner,1,false)
        };
        if(animation.Interlude is FieldTransitionInterlude.CavePreviewEnter or FieldTransitionInterlude.RegionPreview)
        {
            animation.PreviewDuration=_session.FieldTransitionPreviewDuration(before,receipt);
            animation.PreviewArt=MapPreviewCatalog.Find(_catalog.MapDefinition(receipt.To.MapId).RegionMapSection)
                ??throw new InvalidDataException("Missing admitted cave preview art.");
            if(animation.PreviewDuration is not (40 or 120))return false;
        }
        if(receipt.Cause==FieldTransitionCause.StairWarp)animation.StairDeparture=new(receipt.DepartureStairBehavior!.Value);
        if(receipt.Cause==FieldTransitionCause.AnimatedDoor)
        {
            animation.DepartureArt=FindFieldDoor(animation.OriginView.Projection,receipt.From.X,receipt.From.Y-1);
            if(animation.DepartureArt is null)return false;
            animation.DoorDeparture=new();
        }
        if(receipt.Departure==FieldTransitionDeparture.StepOntoWarp)animation.StepDeparture=new();
        if(receipt.Arrival==FieldTransitionArrival.AnimatedDoor)
        {
            animation.ArrivalArt=FindFieldDoor(animation.DestinationView.Projection,receipt.ArrivalAnchor.X,receipt.ArrivalAnchor.Y);
            if(animation.ArrivalArt is null)return false;
        }
        _fieldWarp=animation;
        _held=null;_repeat=0;_tracks.Clear();_fade=0;ResetCampaignButtonInput(preserveHeld:animation.Interlude is FieldTransitionInterlude.CavePreviewEnter or FieldTransitionInterlude.RegionPreview);
        if(animation.DoorDeparture is null&&animation.StepDeparture is null&&animation.StairDeparture is null){BeginFieldWarpMusicFade(animation);SourceSound("SE_EXIT");}
        Write("fieldWarpPresentation",new{phase="departure",cause=receipt.Cause.ToString(),from=receipt.From,to=receipt.To,animation.WaitForMusic});
        return true;
    }

    private void BeginFieldWarpMusicFade(FieldWarpAnimation animation)
    {
        // Source FadeOutMapMusic skips an unchanged song and the script flag.
        // Read the actual backend's stop condition; no guessed fade duration gates
        // the map transfer. An absent/faulted backend is immediately ready.
        if(!(animation.Receipt.SuppressMusicFade??animation.Origin.Script.Flags.GetValueOrDefault("FLAG_DONT_TRANSITION_MUSIC"))&&
            _audioMusic!=_catalog.MapDefinition(animation.Receipt.To.MapId).Music&&!_audioFaulted&&
            _sourceAudio is SynthSourceAudioPlayer synth&&synth.Snapshot().Music?.Playing==true)
        {
            animation.WaitForMusic=true;
            synth.BeginSourceMusicFade(_catalog.MapDefinition(animation.Receipt.To.MapId).MapType=="MAP_TYPE_INDOOR"?2:4);
        }
    }

    private bool ProcessFieldWarpPresentation(double delta)
    {
        if(_fieldWarp is not {} animation)return false;
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        // Its VBlank timer continues while field input is locked. Retain the
        // elapsed frames; the ordinary clock path commits after release.
        ObserveTrainerTowerTime(delta);
        _held=null;_repeat=0;animation.Frames+=delta*60;
        int frame=(int)Math.Floor(animation.Frames+1e-7);
        while(animation.ProcessedFrames<frame)
        {
            animation.ProcessedFrames++;
            if(!animation.Arriving)
            {
                bool musicReady=!animation.WaitForMusic||_sourceAudio is null||_audioFaulted||_sourceAudio.Snapshot().Music?.Playing!=true;
                if(animation.Preview is {} preview)
                {
                    // Generated art is fully loaded before construction. Each host
                    // callback still passes through both source readiness phases.
                    preview.Advance(graphicsReady:true,windowCopyReady:true,
                        cancelHeld:(_buttons.Held&CampaignButton.B)!=0);
                    if(!preview.Complete)continue;
                }
                else if(animation.CaveEnter is {} enter)
                {
                    enter.Advance();if(!enter.Complete)continue;
                }
                else if(animation.CaveExit is {} cave)
                {
                    cave.Advance();if(!cave.Complete)continue;
                }
                else
                {
                    if(animation.StairDeparture is {} stair)
                    {
                        bool requested=stair.Sample.MusicFadeRequested;stair.Advance(musicReady);
                        if(!requested&&stair.Sample.MusicFadeRequested){BeginFieldWarpMusicFade(animation);SourceSound("SE_EXIT");}
                        if(!stair.Complete)continue;
                    }
                    else if(animation.DoorDeparture is {} door)
                    {
                        bool requested=door.MusicFadeRequested;
                        door.Advance(musicReady);
                        if(!requested&&door.MusicFadeRequested)BeginFieldWarpMusicFade(animation);
                        if(door.Sample.OpenDoorSoundRequested)SourceSound(animation.DepartureArt!.SoundEffect);
                        if(!door.Complete)continue;
                    }
                    else if(animation.StepDeparture is {} step)
                    {
                        bool requested=step.MusicFadeRequested;step.Advance(musicReady);
                        if(!requested&&step.MusicFadeRequested){BeginFieldWarpMusicFade(animation);SourceSound("SE_EXIT");}
                        if(!step.Complete)continue;
                    }
                    else{animation.Departure.Advance(musicReady);if(!animation.Departure.Complete)continue;}
                    if(animation.Interlude!=FieldTransitionInterlude.None)
                    {
                        var art=CaveTransitionArt;
                        if(animation.Interlude==FieldTransitionInterlude.CaveExit)animation.CaveExit=new(art.TransitionPalette,art.WhitePalette);
                        else if(animation.Interlude==FieldTransitionInterlude.CaveEnter)animation.CaveEnter=new(art.TransitionPalette,art.WhitePalette,art.BlackPalette);
                        else animation.Preview=new(art.TransitionPalette,art.WhitePalette,art.BlackPalette,animation.PreviewDuration==40);
                        continue;
                    }
                }
                animation.SwitchFrame=animation.ProcessedFrames;
                animation.Arriving=true;
                if(animation.Receipt.Arrival==FieldTransitionArrival.Stair)animation.StairArrival=new(animation.Receipt.ArrivalStairBehavior!.Value);
                else if(animation.ArrivalArt is not null)animation.DoorArrival=new();
                else if(animation.Receipt.Arrival==FieldTransitionArrival.NonAnimatedDoor)animation.StepArrival=new();
                else animation.Arrival=new();
                // Synchronize presentation at the source map-load callback. No
                // command, State replacement, RNG change or event is generated.
                // A requested source fade stopped the backend, even when the
                // destination selector chooses the same song again (e.g. cycling).
                // Clear only that stopped song's view cache so SourceMusic restarts it.
                if(animation.WaitForMusic&&_sourceAudio is {} audio&&audio.Snapshot().Music?.Playing!=true)_audioMusic=null;
                // Load the captured entry scene, not the precommitted OnFrame
                // movement/effects. Those become eligible only after arrival.
                LoadMap(animation.ArrivalState.World.Player.MapId,false);
                ApplyMapEdits(animation.ArrivalState);
                SynchronizeSourceAudio(animation.Origin,animation.ArrivalState);
                _fade=0;
                Write("fieldWarpPresentation",new{phase="arrival",animation.SwitchFrame,animation.Receipt.To});
            }
            else
            {
                if(animation.StairArrival is {} stair){stair.Advance();if(!stair.Complete)continue;}
                else if(animation.DoorArrival is {} door)
                {
                    door.Advance();
                    if(door.Sample.OpenDoorSoundRequested)SourceSound(animation.ArrivalArt!.SoundEffect);
                    if(!door.Complete)continue;
                }
                else if(animation.StepArrival is {} step){step.Advance();if(!step.Complete)continue;}
                else{animation.Arrival!.Advance();if(!animation.Arrival.Complete)continue;}
                bool recorded=_session.CompleteFieldTransitionPresentation(animation.Receipt,animation.SwitchFrame,animation.ProcessedFrames,animation.Preview?.PreviewFrames??0);
                Write("fieldWarpPresentation",new{phase="complete",animation.SwitchFrame,endFrame=animation.ProcessedFrames,recorded});
                _fieldWarp=null;_fieldSnapshotReleasePending=true;
                _fieldWarpSynchronizing=true;
                try{Synchronize(animation.Origin,_session.State,animation.ArrivalState);}
                finally{_fieldWarpSynchronizing=false;}
                _held=null;_repeat=0;ResetCampaignButtonInput();
                break;
            }
        }
        QueueRedraw();return true;
    }

    private bool DrawFieldWarpPresentation()
    {
        if(_fieldWarp is not {} animation)return false;
        if(!animation.Arriving)
        {
            if(animation.Preview is {} preview){DrawMapPreview(animation.PreviewArt!,preview.Sample);return true;}
            if(animation.CaveEnter is {} enter){DrawCaveExit(enter.Sample);return true;}
            if(animation.CaveExit is {} cave){DrawCaveExit(cave.Sample);return true;}
        }
        var sample=animation.Sample;
        var snapshot=animation.Arriving?animation.DestinationView:animation.OriginView;
        if(animation.Arriving&&animation.StairArrival is {} sa)snapshot=WithFieldStairPose(snapshot,sa.Sample);
        else if(!animation.Arriving&&animation.StairDeparture is {} sd)snapshot=WithFieldStairPose(snapshot,sd.Sample);
        else if(animation.Arriving&&animation.ArrivalArt is {} arrival)
            snapshot=WithFieldDoorPose(snapshot,sample,arrival,animation.Receipt.ArrivalAnchor.X,animation.Receipt.ArrivalAnchor.Y,true);
        else if(!animation.Arriving&&animation.DepartureArt is {} departure)
            snapshot=WithFieldDoorPose(snapshot,sample,departure,animation.Receipt.From.X,animation.Receipt.From.Y-1,false);
        else if(animation.Arriving&&animation.StepArrival is not null)
            snapshot=WithFieldWalkPose(snapshot,sample,animation.Receipt.ArrivalAnchor.X,animation.Receipt.ArrivalAnchor.Y,Direction.South);
        else if(!animation.Arriving&&animation.Receipt.DepartureStep is {} step)
            snapshot=WithFieldWalkPose(snapshot,sample,animation.Receipt.From.X,animation.Receipt.From.Y,step.Facing);
        DrawFieldSnapshot(snapshot,0,sample.BackgroundBlend,sample.ObjectBlend,
            (animation.Arriving&&animation.Interlude==FieldTransitionInterlude.CaveExit||!animation.Arriving&&animation.Interlude==FieldTransitionInterlude.CaveEnter)?(ushort)0x7fff:(ushort)0);
        DrawFieldDoorWipe(sample);
        return true;
    }

    private static FieldDoorPresentation.PhaseSample QuestLogFieldWarpSample(CampaignQuestLogPlayback playback)=>
        playback.ActiveTransitionTiming is {Cause:FieldTransitionCause.PlainWarp or FieldTransitionCause.AnimatedDoor or FieldTransitionCause.NonAnimatedDoor or FieldTransitionCause.StairWarp} timing
            ?FieldDoorPresentation.SampleRecorded(timing.Cause,
                timing.Arrival,timing.SwitchFrame,
                (int)Math.Floor(playback.TransitionElapsedFrames+1e-7),timing.DepartureStairBehavior,timing.ArrivalStairBehavior,timing.EffectiveDeparture,timing.Interlude)
            :default;

    private void ReleaseFinishedFieldSnapshot()
    {
        if(!_fieldSnapshotReleasePending)return;
        _fieldSnapshotReleasePending=false;ClearCaveTransitionTexture();ClearQuestLogPaletteTextures();ResetQuestLogArt();
    }
    private void ResetFieldWarpPresentation()
    {
        _fieldWarp=null;_fieldWarpSynchronizing=false;_fieldSnapshotReleasePending=false;ClearCaveTransitionTexture();
        ClearQuestLogPaletteTextures();ResetQuestLogArt();
    }
}
