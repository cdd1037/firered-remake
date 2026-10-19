namespace Game.Application;

/// <summary>
/// Shared delay-zero normal-palette and ordinary warp callbacks from the pinned
/// palette.c, field_fadetransition.c and field_weather.c. This owns presentation
/// time only. The caller supplies music readiness and owns the actual map swap.
/// Doors, stairs and previews own their separate callbacks and share these
/// palette phases through FieldArrivalPalettePresentation.
/// </summary>
public static class FieldWarpPresentation
{
    public const int MinimumDepartureFrames=24;
    public const int PlainArrivalFrames=24;
    public readonly record struct PhaseSample(int Frames,int TaskPhase,int BackgroundBlend,
        int ObjectBlend,bool PaletteActive,bool Complete);

    /// <summary>Replay an observed map-transfer boundary through the same live callbacks.</summary>
    public static PhaseSample SampleRecorded(int switchFrame,int elapsedFrames)
    {
        if(switchFrame<MinimumDepartureFrames)throw new ArgumentOutOfRangeException(nameof(switchFrame));
        if(elapsedFrames<0)throw new ArgumentOutOfRangeException(nameof(elapsedFrames));
        if(elapsedFrames<switchFrame)
        {
            var departure=new Departure();
            // Readiness is the recorded observation, never a new audio query.
            // Task phase1 admits it one callback before the transfer callback.
            for(int frame=1;frame<=elapsedFrames;frame++)departure.Advance(frame>=switchFrame-1);
            return departure.Sample;
        }
        var arrival=new PlainArrival();
        int frames=Math.Min(PlainArrivalFrames,elapsedFrames-switchFrame);
        for(int frame=0;frame<frames;frame++)arrival.Advance();
        return arrival.Sample;
    }

    public sealed class NormalPalette
    {
        private int _y,_target,_finishingCounter,_delayCounter;
        private readonly int _delay,_delta;
        private bool _object,_finishing;
        public bool Active {get;private set;}=true;
        /// <summary>Source selected-palettes mask remains nonzero until both target banks are written.</summary>
        public bool HasSelectedPalettes=>Active&&!_finishing;
        public int BackgroundBlend {get;private set;}
        public int ObjectBlend {get;private set;}

        public NormalPalette(bool fromBlack,int delay=0)
        {
            if(delay is <-2 or >127)throw new ArgumentOutOfRangeException(nameof(delay));
            // The cave preview uses source delays -1/-2: no waiting, increments 3/4.
            _delta=delay<0?2-delay:2;
            _delay=_delayCounter=Math.Max(0,delay);
            _y=fromBlack?16:0;_target=fromBlack?0:16;
            BackgroundBlend=ObjectBlend=_y;
            Advance(); // BeginNormalPaletteFade immediately updates the BG half.
        }

        /// <summary>Direct source palette fills do not move the normal-fade cursor.</summary>
        public void FillBanks(int coefficient)
        {
            if(coefficient is <0 or >16)throw new ArgumentOutOfRangeException(nameof(coefficient));
            BackgroundBlend=ObjectBlend=coefficient;
        }

        public void Advance()
        {
            if(!Active)return;
            if(_finishing)
            {
                if(_finishingCounter==4){Active=false;_finishing=false;_finishingCounter=0;}
                else _finishingCounter++;
                return;
            }
            // Delay applies before each BG half, never between BG and OBJ.
            // BeginNormalPaletteFade primes the counter to admit its first BG.
            if(!_object)
            {
                if(_delayCounter<_delay){_delayCounter++;return;}
                _delayCounter=0;
            }
            if(_object)ObjectBlend=_y;else BackgroundBlend=_y;
            _object=!_object;
            if(_object)return;
            if(_y==_target)_finishing=true;
            else _y=_target>_y?Math.Min(_target,_y+_delta):Math.Max(_target,_y-_delta);
        }
    }

    public sealed class Departure
    {
        // The constructor boundary includes the first Task_Teleport2Warp callback
        // which freezes actors. This preserves the existing common-fall clock.
        private int _phase=1;
        public NormalPalette Palette {get;}=new(false);
        public int Frames {get;private set;}
        public int Phase=>_phase;
        public bool Complete {get;private set;}
        public PhaseSample Sample=>new(Frames,Phase,Palette.BackgroundBlend,Palette.ObjectBlend,Palette.Active,Complete);

        public void Advance(bool musicReady)
        {
            if(Complete)return;
            Frames++;
            // Tasks observe readiness before this frame's palette update. In the
            // source BGMusicStopped means no pending music-stop request, so an
            // unchanged song can be ready while it continues playing.
            if(_phase==1){if(!Palette.Active&&musicReady)_phase=2;}
            else if(_phase==2)Complete=true;
            Palette.Advance();
        }
    }

    public sealed class PlainArrival
    {
        private int _phase;
        public FieldArrivalPalettePresentation Palette {get;}
        public int Frames {get;private set;}
        public int Phase=>_phase;
        public bool Complete {get;private set;}
        public PhaseSample Sample=>new(Frames,Phase,Palette.BackgroundBlend,Palette.ObjectBlend,Palette.Active,Complete);

        public PlainArrival(FieldArrivalPalette paletteKind=FieldArrivalPalette.Normal)=>Palette=new(paletteKind);

        public void Advance()
        {
            if(Complete)return;
            Frames++;
            // Arrival priority10 observes weather before priority80 and palette.
            if(_phase==0)_phase=1;
            else if(Palette.WeatherReady)Complete=true;
            Palette.Advance();
        }
    }
}
