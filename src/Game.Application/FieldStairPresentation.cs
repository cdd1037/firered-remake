namespace Game.Application;

/// <summary>
/// Pinned Task_StairWarp/Task_ExitStairs callbacks for an on-foot directional
/// stair, no inherited held movement, NONE/SUNNY weather and a black fade.
/// Offsets are signed source sprite x2/y2 pixels, never authoritative movement.
/// Sprite animation starts at the same fresh command-index-zero seed as doors.
/// </summary>
public static class FieldStairPresentation
{
    public const int MinimumDepartureFrames=36;
    public const int ArrivalFrames=24;

    public readonly record struct PhaseSample(FieldWarpPresentation.PhaseSample Phase,
        int OffsetX,int OffsetY,int WalkFrame,bool MusicFadeRequested);

    public static PhaseSample SampleDeparture(int behavior,int switchFrame,int elapsedFrames)
    {
        if(switchFrame<MinimumDepartureFrames)throw new ArgumentOutOfRangeException(nameof(switchFrame));
        if(elapsedFrames<0)throw new ArgumentOutOfRangeException(nameof(elapsedFrames));
        var departure=new Departure(behavior);
        for(int frame=1;frame<=Math.Min(switchFrame,elapsedFrames);frame++)
            departure.Advance(frame>=switchFrame-1);
        return departure.Sample;
    }

    public static PhaseSample SampleArrival(int behavior,int elapsedFrames)
    {
        if(elapsedFrames<0)throw new ArgumentOutOfRangeException(nameof(elapsedFrames));
        var arrival=new Arrival(behavior);
        for(int frame=0;frame<Math.Min(ArrivalFrames,elapsedFrames);frame++)arrival.Advance();
        return arrival.Sample;
    }

    public static PhaseSample SampleRecorded(int departureBehavior,int arrivalBehavior,int switchFrame,int elapsedFrames)
    {
        _=Speeds(departureBehavior);_=Speeds(arrivalBehavior);
        if(switchFrame<MinimumDepartureFrames)throw new ArgumentOutOfRangeException(nameof(switchFrame));
        if(elapsedFrames<0)throw new ArgumentOutOfRangeException(nameof(elapsedFrames));
        return elapsedFrames<switchFrame?SampleDeparture(departureBehavior,switchFrame,elapsedFrames)
            :SampleArrival(arrivalBehavior,elapsedFrames-switchFrame);
    }

    private static (int X,int Y) Speeds(int behavior)=>behavior switch
    {
        0x6c=>(16,-10),0x6d=>(-17,-10),0x6e=>(17,3),0x6f=>(-17,3),
        _=>throw new ArgumentOutOfRangeException(nameof(behavior))
    };

    public sealed class Departure
    {
        private readonly int _speedX,_speedY;
        private int _phase=1,_driftFrames,_offsetX,_offsetY;
        private FieldWarpPresentation.NormalPalette? _palette;
        public int Frames {get;private set;}
        public bool Complete {get;private set;}
        public bool MusicFadeRequested {get;private set;}
        // Normal in-place lasts16 callbacks, then UpdateStairsMovement restarts
        // it on the next callback, preserving the source alternate-foot index.
        private int WalkFrame=>!MusicFadeRequested?0:((Frames-1)/8%4) switch{0=>1,2=>3,_=>0};
        public PhaseSample Sample=>new(new(Frames,_phase,_palette?.BackgroundBlend??0,
            _palette?.ObjectBlend??0,_palette?.Active??false,Complete),
            _offsetX>>5,_offsetY>>5,WalkFrame,MusicFadeRequested);

        public Departure(int behavior)
        {
            (_speedX,_speedY)=Speeds(behavior);
            // DoStairWarp calls phase0 synchronously: lock/freeze/reset camera.
            // The fresh on-foot seed is already in phase1 at elapsed frame0.
        }

        public void Advance(bool musicReady)
        {
            if(Complete)return;
            Frames++;
            switch(_phase)
            {
                case 1:MusicFadeRequested=true;_phase=2;break;
                case 2:
                    UpdateOffsets();
                    if(_driftFrames>=12){_palette=new(false);_phase=3;}
                    break;
                case 3:
                    UpdateOffsets();
                    if(!_palette!.Active&&musicReady)_phase=4;
                    break;
                default:Complete=true;break;
            }
            _palette?.Advance();
        }

        private void UpdateOffsets()
        {
            // Source data[2..6] are s16; negative >>5 must remain arithmetic,
            // rather than division which would round toward zero.
            if(_speedY>0||_driftFrames>6)_offsetY=unchecked((short)(_offsetY+_speedY));
            _offsetX=unchecked((short)(_offsetX+_speedX));
            _driftFrames=unchecked((short)(_driftFrames+1));
        }
    }

    public sealed class Arrival
    {
        private readonly int _speedX,_speedY;
        private int _phase,_offsetX,_offsetY,_timer;
        private bool _weatherInitialized,_weatherReady;
        public FieldWarpPresentation.NormalPalette Palette {get;}=new(true);
        public int Frames {get;private set;}
        public bool Complete {get;private set;}
        public PhaseSample Sample=>new(new(Frames,_phase,Palette.BackgroundBlend,Palette.ObjectBlend,
            Palette.Active,Complete),_offsetX>>5,_offsetY>>5,Frames is >=1 and <=4?1:0,false);

        public Arrival(int behavior)=>(_speedX,_speedY)=Speeds(behavior);

        public void Advance()
        {
            if(Complete)return;
            Frames++;
            switch(_phase)
            {
                case 0:
                    // Setup already began the fade. The source's duplicate
                    // phase0 BeginNormalPaletteFade returns false while active.
                    _offsetX=_speedX*16;_offsetY=_speedY*16;_timer=16;_phase=1;
                    break;
                case 1:
                    if(_timer!=0){_offsetX-=_speedX;_offsetY-=_speedY;_timer--;}
                    else{_offsetX=_offsetY=0;_phase=2;}
                    break;
                default:if(_weatherReady)Complete=true;break;
            }
            if(!_weatherInitialized)_weatherInitialized=true;
            else if(!Palette.Active)_weatherReady=true;
            Palette.Advance();
        }
    }
}
