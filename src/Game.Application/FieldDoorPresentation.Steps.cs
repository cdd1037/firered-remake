namespace Game.Application;

public static partial class FieldDoorPresentation
{
    public const int MinimumStepDepartureFrames=NormalWalkPixels+FieldWarpPresentation.MinimumDepartureFrames;
    public const int NonAnimatedArrivalFrames=41;

    /// <summary>
    /// The normal step reaches its tile on sprite callback16. The next CB1
    /// observes tile center, starts DoWarp, then runs its task and palette.
    /// </summary>
    public sealed class StepDeparture
    {
        private FieldWarpPresentation.Departure? _warp;
        public int Frames {get;private set;}
        public bool Complete=>_warp?.Complete??false;
        public bool MusicFadeRequested=>_warp is not null;
        public PhaseSample Sample=>new(Frames,_warp?.Phase??0,_warp?.Palette.BackgroundBlend??0,
            _warp?.Palette.ObjectBlend??0,_warp?.Palette.Active??false,Complete,-1,false,true,
            Math.Min(Frames,NormalWalkPixels),false,0,240,MusicFadeRequested,_warp is not null,false);

        public void Advance(bool musicReady)
        {
            if(Complete)return;
            Frames++;
            if(Frames<=NormalWalkPixels)return;
            _warp??=new();
            _warp.Advance(musicReady);
        }
    }

    /// <summary>
    /// Task_ExitNonAnimDoor starts its normal held walk after the weather task
    /// observes the delay-zero palette finish. Facing belongs to the receipt.
    /// </summary>
    public sealed class NonAnimatedArrival
    {
        private readonly FieldWarpPresentation.NormalPalette _palette=new(true);
        private bool _walking,_visible=true,_weatherInitialized,_weatherReady;
        private int _phase,_walkPixels;
        public int Frames {get;private set;}
        public bool Complete {get;private set;}
        public PhaseSample Sample=>new(Frames,_phase,_palette.BackgroundBlend,_palette.ObjectBlend,
            _palette.Active,Complete,-1,false,_visible,_walkPixels,false,0,240,false,false,false);

        public void Advance()
        {
            if(Complete)return;
            Frames++;
            switch(_phase)
            {
                case 0:_visible=false;_phase=1;break;
                case 1:if(_weatherReady){_visible=true;_walking=true;_phase=2;}break;
                case 2:if(!_walking)_phase=3;break;
                case 3:Complete=true;break;
            }
            if(!_weatherInitialized)_weatherInitialized=true;
            else if(!_palette.Active)_weatherReady=true;
            if(_walking){_walkPixels++;if(_walkPixels==NormalWalkPixels)_walking=false;}
            _palette.Advance();
        }
    }
}
