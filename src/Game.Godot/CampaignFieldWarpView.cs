using System.Collections.Immutable;
using Game.Application;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    private sealed class FieldWarpAnimation
    {
        public required FieldTransitionReceipt Receipt;
        public required CampaignState Origin;
        public required FieldSnapshot OriginView;
        public required FieldSnapshot DestinationView;
        public readonly FieldWarpPresentation.Departure Departure=new();
        public FieldWarpPresentation.PlainArrival? Arrival;
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
            after.FieldTransition is not {Cause:FieldTransitionCause.PlainWarp} receipt||
            receipt.From!=before.World.Player||!_session.CanPresentFieldTransition(receipt))return false;
        var owner=new object();
        var animation=new FieldWarpAnimation
        {
            Receipt=receipt,Origin=before,
            OriginView=CaptureFieldSnapshot(before,owner,0,true),
            DestinationView=CaptureFieldSnapshot(after,owner,1,false)
        };
        _fieldWarp=animation;
        _held=null;_repeat=0;_tracks.Clear();_fade=0;ResetCampaignButtonInput();
        // Source FadeOutMapMusic skips an unchanged song and the script flag.
        // Read the actual backend's stop condition; no guessed fade duration gates
        // the map transfer. An absent/faulted backend is immediately ready.
        if(!before.Script.Flags.GetValueOrDefault("FLAG_DONT_TRANSITION_MUSIC")&&
            _audioMusic!=_catalog.MapDefinition(receipt.To.MapId).Music&&!_audioFaulted&&
            _sourceAudio is SynthSourceAudioPlayer synth&&synth.Snapshot().Music?.Playing==true)
        {
            animation.WaitForMusic=true;
            synth.BeginSourceMusicFade(_catalog.MapDefinition(receipt.To.MapId).MapType=="MAP_TYPE_INDOOR"?2:4);
        }
        Write("fieldWarpPresentation",new{phase="departure",cause=receipt.Cause.ToString(),from=receipt.From,to=receipt.To,animation.WaitForMusic});
        return true;
    }

    private bool ProcessFieldWarpPresentation(double delta)
    {
        if(_fieldWarp is not {} animation)return false;
        if(!double.IsFinite(delta)||delta<0)throw new ArgumentOutOfRangeException(nameof(delta));
        _held=null;_repeat=0;animation.Frames+=delta*60;
        int frame=(int)Math.Floor(animation.Frames+1e-7);
        while(animation.ProcessedFrames<frame)
        {
            animation.ProcessedFrames++;
            if(animation.Arrival is null)
            {
                bool musicReady=!animation.WaitForMusic||_sourceAudio is null||_audioFaulted||_sourceAudio.Snapshot().Music?.Playing!=true;
                animation.Departure.Advance(musicReady);
                if(!animation.Departure.Complete)continue;
                animation.SwitchFrame=animation.ProcessedFrames;
                animation.Arrival=new();
                // Synchronize presentation at the source map-load callback. No
                // command, State replacement, RNG change or event is generated.
                // A requested source fade stopped the backend, even when the
                // destination selector chooses the same song again (e.g. cycling).
                // Clear only that stopped song's view cache so SourceMusic restarts it.
                if(animation.WaitForMusic&&_sourceAudio is {} audio&&audio.Snapshot().Music?.Playing!=true)_audioMusic=null;
                _fieldWarpSynchronizing=true;
                try{Synchronize(animation.Origin,_session.State);}
                finally{_fieldWarpSynchronizing=false;}
                _fade=0;
                Write("fieldWarpPresentation",new{phase="arrival",animation.SwitchFrame,animation.Receipt.To});
            }
            else
            {
                animation.Arrival.Advance();
                if(!animation.Arrival.Complete)continue;
                bool recorded=_session.CompleteFieldTransitionPresentation(animation.Receipt,animation.SwitchFrame,animation.ProcessedFrames);
                Write("fieldWarpPresentation",new{phase="complete",animation.SwitchFrame,endFrame=animation.ProcessedFrames,recorded});
                _fieldWarp=null;_fieldSnapshotReleasePending=true;
                _held=null;_repeat=0;ResetCampaignButtonInput();
                break;
            }
        }
        QueueRedraw();return true;
    }

    private bool DrawFieldWarpPresentation()
    {
        if(_fieldWarp is not {} animation)return false;
        var sample=animation.Arrival?.Sample??animation.Departure.Sample;
        DrawFieldSnapshot(animation.Arrival is null?animation.OriginView:animation.DestinationView,0,sample.BackgroundBlend,sample.ObjectBlend);
        return true;
    }

    private static FieldWarpPresentation.PhaseSample QuestLogFieldWarpSample(CampaignQuestLogPlayback playback)=>
        playback.ActiveTransitionTiming is {Cause:FieldTransitionCause.PlainWarp} timing
            ?FieldWarpPresentation.SampleRecorded(timing.SwitchFrame,(int)Math.Floor(playback.TransitionElapsedFrames+1e-7))
            :default;

    private void ReleaseFinishedFieldSnapshot()
    {
        if(!_fieldSnapshotReleasePending)return;
        _fieldSnapshotReleasePending=false;ClearQuestLogPaletteTextures();ResetQuestLogArt();
    }
    private void ResetFieldWarpPresentation()
    {
        _fieldWarp=null;_fieldWarpSynchronizing=false;_fieldSnapshotReleasePending=false;
        ClearQuestLogPaletteTextures();ResetQuestLogArt();
    }
}
