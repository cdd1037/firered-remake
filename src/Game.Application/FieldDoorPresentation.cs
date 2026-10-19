using Game.Domain.World;

namespace Game.Application;

/// <summary>
/// Ordinary FRLG animated doors: source task/door/walk callbacks, split palette
/// banks and outward barn-door windows. This projects already accepted movement;
/// it never moves an authoritative actor, advances scripts or draws gameplay RNG.
/// NONE/SUNNY black fades without map previews are the bounded admission surface.
/// </summary>
public static partial class FieldDoorPresentation
{
    public const int MinimumDepartureFrames=81;
    public const int AnimatedArrivalFrames=81;
    public const int NormalWalkPixels=16;

    /// <summary>
    /// Fresh command-index0 normal walk: SeekSpriteAnim and the same-callback
    /// sprite update display the first foot for eight callbacks, then standing.
    /// A departure's inherited alternate-foot index is outside this fresh seed.
    /// </summary>
    public static int NormalWalkPoseFrame(int pixels)
    {
        if(pixels is <0 or >NormalWalkPixels)throw new ArgumentOutOfRangeException(nameof(pixels));
        return pixels is >=1 and <=8?1:0;
    }

    public readonly record struct PhaseSample(int Frames,int TaskPhase,int BackgroundBlend,
        int ObjectBlend,bool PaletteActive,bool Complete,int DoorFrame,bool DoorAnimationActive,
        bool PlayerVisible,int WalkPixels,bool WipeActive,int WipeLeft,int WipeRight,
        bool MusicFadeRequested,bool TeleportTask,bool OpenDoorSoundRequested);

    /// <summary>Replays the exact observed transfer; no current audio readiness is consulted.</summary>
    public static PhaseSample SampleRecorded(FieldTransitionCause cause,FieldTransitionArrival arrivalKind,int switchFrame,int elapsedFrames,int? departureStairBehavior=null,int? arrivalStairBehavior=null,FieldTransitionDeparture departureKind=FieldTransitionDeparture.Immediate,FieldTransitionInterlude interludeKind=FieldTransitionInterlude.None,FieldArrivalPalette arrivalPalette=FieldArrivalPalette.Normal)
    {
        int interludeFrames=interludeKind switch
        {
            FieldTransitionInterlude.CaveExit=>FieldCaveTransitionPresentation.ExitFrames,
            FieldTransitionInterlude.CaveEnter or FieldTransitionInterlude.CavePreviewEnter=>FieldCaveTransitionPresentation.EnterFrames,
            _=>0
        };
        int departureEnd=switchFrame-interludeFrames;
        int minimum=DepartureFrames(cause,departureKind);
        _=ArrivalFrames(arrivalKind);
        if(departureEnd<minimum)throw new ArgumentOutOfRangeException(nameof(switchFrame));
        if(elapsedFrames<0)throw new ArgumentOutOfRangeException(nameof(elapsedFrames));
        if(elapsedFrames<switchFrame)
        {
            elapsedFrames=Math.Min(elapsedFrames,departureEnd);
            if(cause==FieldTransitionCause.StairWarp)return FromPlain(FieldStairPresentation.SampleDeparture(
                departureStairBehavior??throw new ArgumentException("stair_departure_behavior_missing"),switchFrame,elapsedFrames).Phase);
            if(cause==FieldTransitionCause.AnimatedDoor)
            {
                var departure=new Departure();
                for(int frame=1;frame<=elapsedFrames;frame++)departure.Advance(frame>=departureEnd-1);
                return departure.Sample;
            }
            if(cause==FieldTransitionCause.NonAnimatedDoor||departureKind==FieldTransitionDeparture.StepOntoWarp)
            {
                var step=new StepDeparture();
                for(int frame=1;frame<=elapsedFrames;frame++)step.Advance(frame>=departureEnd-1);
                return step.Sample;
            }
            var plain=new FieldWarpPresentation.Departure();
            for(int frame=1;frame<=elapsedFrames;frame++)plain.Advance(frame>=departureEnd-1);
            return FromPlain(plain.Sample);
        }
        if(arrivalKind==FieldTransitionArrival.Stair)return FromPlain(FieldStairPresentation.SampleArrival(
            arrivalStairBehavior??throw new ArgumentException("stair_arrival_behavior_missing"),elapsedFrames-switchFrame).Phase);
        if(arrivalKind==FieldTransitionArrival.AnimatedDoor)
        {
            var arrival=new AnimatedArrival();
            int frames=Math.Min(AnimatedArrivalFrames,elapsedFrames-switchFrame);
            for(int frame=0;frame<frames;frame++)arrival.Advance();
            return arrival.Sample;
        }
        if(arrivalKind==FieldTransitionArrival.NonAnimatedDoor)
        {
            var step=new NonAnimatedArrival();
            for(int frame=0;frame<Math.Min(NonAnimatedArrivalFrames,elapsedFrames-switchFrame);frame++)step.Advance();
            return step.Sample;
        }
        var plainArrival=new FieldWarpPresentation.PlainArrival(arrivalPalette);
        int plainFrames=Math.Min(arrivalPalette==FieldArrivalPalette.Shade?18:FieldWarpPresentation.PlainArrivalFrames,elapsedFrames-switchFrame);
        for(int frame=0;frame<plainFrames;frame++)plainArrival.Advance();
        return FromPlain(plainArrival.Sample);
    }

    public static int DepartureFrames(FieldTransitionCause cause,FieldTransitionDeparture departure=FieldTransitionDeparture.Immediate)=>
        cause==FieldTransitionCause.PlainWarp&&departure==FieldTransitionDeparture.StepOntoWarp?MinimumStepDepartureFrames:cause switch
    {
        FieldTransitionCause.StairWarp=>FieldStairPresentation.MinimumDepartureFrames,
        FieldTransitionCause.PlainWarp=>FieldWarpPresentation.MinimumDepartureFrames,
        FieldTransitionCause.AnimatedDoor=>MinimumDepartureFrames,
        FieldTransitionCause.NonAnimatedDoor=>MinimumStepDepartureFrames,
        _=>throw new ArgumentOutOfRangeException(nameof(cause))
    };
    public static int ArrivalFrames(FieldTransitionArrival arrival)=>arrival switch
    {
        FieldTransitionArrival.Stair=>FieldStairPresentation.ArrivalFrames,
        FieldTransitionArrival.Plain=>FieldWarpPresentation.PlainArrivalFrames,
        FieldTransitionArrival.AnimatedDoor=>AnimatedArrivalFrames,
        FieldTransitionArrival.NonAnimatedDoor=>NonAnimatedArrivalFrames,
        _=>throw new ArgumentOutOfRangeException(nameof(arrival))
    };

    public static PhaseSample FromPlain(FieldWarpPresentation.PhaseSample sample)=>new(
        sample.Frames,sample.TaskPhase,sample.BackgroundBlend,sample.ObjectBlend,sample.PaletteActive,
        sample.Complete,-1,false,true,0,false,0,240,false,false,false);

    public sealed class Departure
    {
        private readonly DoorAnimation _door=new();
        private FieldWarpPresentation.NormalPalette? _palette;
        private bool _walking,_visible=true,_teleport,_openSound;
        private int _phase,_walkPixels;
        public int Frames {get;private set;}
        public bool Complete {get;private set;}
        public bool MusicFadeRequested {get;private set;}
        public PhaseSample Sample=>new(Frames,_phase,_palette?.BackgroundBlend??0,_palette?.ObjectBlend??0,
            _palette?.Active??false,Complete,_door.Frame,_door.Active,_visible,_walkPixels,
            false,0,240,MusicFadeRequested,_teleport,_openSound);

        public void Advance(bool musicReady)
        {
            if(Complete)return;
            Frames++;_openSound=false;
            // Warp task priority10 observes the prior callback's sprite state;
            // its spawned door task priority80 runs later in this same callback.
            if(!_teleport)
            {
                switch(_phase)
                {
                    case 0:_door.Start(open:true);_openSound=true;_phase=1;break;
                    case 1:if(!_door.Active){_walking=true;_phase=2;}break;
                    case 2:if(!_walking){_door.Start(open:false);_visible=false;_phase=3;}break;
                    case 3:if(!_door.Active)_phase=4;break;
                    case 4:
                        MusicFadeRequested=true;_palette=new(false);_teleport=true;_phase=0;break;
                }
            }
            else
            {
                switch(_phase)
                {
                    case 0:_phase=1;break;
                    case 1:if(!_palette!.Active&&musicReady)_phase=2;break;
                    case 2:Complete=true;break;
                }
            }
            _door.Advance();
            if(_walking){_walkPixels++;if(_walkPixels==NormalWalkPixels)_walking=false;}
            _palette?.Advance();
        }
    }

    public sealed class AnimatedArrival
    {
        private readonly DoorAnimation _door=new();
        private readonly OutwardWipe _wipe=new();
        private FieldWarpPresentation.NormalPalette? _palette;
        private bool _walking,_visible=true,_weatherInitialized,_weatherReady,_openSound;
        private int _phase,_counter,_walkPixels;
        public int Frames {get;private set;}
        public bool Complete {get;private set;}
        public PhaseSample Sample=>new(Frames,_phase,_palette?.BackgroundBlend??16,_palette?.ObjectBlend??16,
            _palette?.Active??false,Complete,_door.Frame,_door.Active,_visible,_walkPixels,
            _wipe.Active,_wipe.Left,_wipe.Right,false,false,_openSound);

        public void Advance()
        {
            if(Complete)return;
            Frames++;_openSound=false;
            // FRLG redirects phase0 directly into phase5. The legacy RS 1/2/3
            // branch is unreachable; ordinary arrival uses delay3 and the wipe.
            switch(_phase)
            {
                case 0:
                    _visible=false;_wipe.Start();_palette=new(true,3);_phase=6;break;
                case 6:
                    if(++_counter==25){_door.Start(open:true);_openSound=true;_phase=7;_counter=0;}break;
                case 7:
                    if(!_door.Active){_visible=true;_walking=true;_phase=8;}break;
                case 8:
                    if(++_counter==14){_door.Start(open:false);_phase=9;}break;
                case 9:
                    if(_weatherReady&&!_walking&&!_door.Active&&!_wipe.Active)_phase=4;break;
                case 4:Complete=true;break;
            }
            // Weather was created before the new priority80 door/wipe tasks.
            if(!_weatherInitialized)_weatherInitialized=true;
            else if(_palette is {Active:false})_weatherReady=true;
            _wipe.Advance();_door.Advance();
            if(_walking){_walkPixels++;if(_walkPixels==NormalWalkPixels)_walking=false;}
            _palette?.Advance();
        }
    }

    private sealed class DoorAnimation
    {
        private bool _open;
        private int _frameId,_counter;
        public int Frame {get;private set;}=-1;
        public bool Active {get;private set;}
        public void Start(bool open){_open=open;_frameId=_counter=0;Active=true;}
        public void Advance()
        {
            if(!Active)return;
            if(_counter==0)Frame=_open?_frameId-1:2-_frameId;
            // Source tests equality before incrementing: duration4 consumes
            // counters0,1,2,3,4, hence five callbacks per displayed frame.
            if(_counter==4){_counter=0;if(++_frameId==4)Active=false;return;}
            _counter++;
        }
    }

    private sealed class OutwardWipe
    {
        private int _phase,_offset;
        private bool _childActive;
        public bool Active {get;private set;}
        public int Left {get;private set;}
        public int Right {get;private set;}=240;
        public void Start(){Active=true;_phase=_offset=0;}
        public void Advance()
        {
            if(!Active)return;
            switch(_phase)
            {
                case 0:Left=Right=120;_phase=1;break;
                case 1:_childActive=true;_phase=2;break;
                case 2:if(!_childActive)_phase=3;break;
                case 3:Active=false;Left=0;Right=240;return;
            }
            if(!_childActive)return;
            int left=120-_offset;
            if(left<0){_childActive=false;return;}
            Left=left;Right=120+_offset;
            _offset+=left<90?4:2;
        }
    }
}
