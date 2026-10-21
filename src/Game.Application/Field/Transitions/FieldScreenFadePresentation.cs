namespace Game.Application;

/// <summary>In-place FadeScreen: the existing weather task survives. Samples are
/// after the command frame's weather/global palette pass. The native script wait
/// observes normal-palette activity, not completion of a weather-owned fade.</summary>
public sealed class FieldScreenFadePresentation
{
    private readonly FieldWarpPresentation.NormalPalette? _normal;
    private readonly FieldArrivalPalettePresentation? _weather;
    private bool _nativeReady;
    public bool FromWhite {get;}
    public int Frames {get;private set;}
    public int BackgroundBlend=>_normal?.BackgroundBlend??_weather!.BackgroundBlend;
    public int ObjectBlend=>_normal?.ObjectBlend??_weather!.ObjectBlend;
    public bool PaletteActive=>_normal?.Active??_weather!.Active;
    public bool ScriptReady {get;private set;}
    public bool VisualComplete=>FromWhite?_weather!.WeatherReady:!_normal!.Active;

    public FieldScreenFadePresentation(bool fromWhite,FieldArrivalPalette palette)
    {
        if(!Enum.IsDefined(palette))throw new ArgumentOutOfRangeException(nameof(palette));
        FromWhite=fromWhite;
        if(fromWhite)
        {
            _weather=new(palette,weatherInitialized:true);
            _weather.Advance();
        }
        else
        {
            _normal=new(false);
            _normal.Advance();
        }
    }

    public void Advance()
    {
        Frames++;
        // RunScriptCommand's native TRUE only switches mode. Bytecode resumes
        // on the next call, before this frame's weather/palette updates.
        if(_nativeReady)ScriptReady=true;
        else if(!PaletteActive)_nativeReady=true;
        _normal?.Advance();_weather?.Advance();
    }

    public static int ScriptWaitFrames(bool fromWhite,FieldArrivalPalette palette)
    {
        var fade=new FieldScreenFadePresentation(fromWhite,palette);
        while(!fade.ScriptReady)fade.Advance();
        return fade.Frames;
    }

    /// <summary>One source-frame clock for a script fade queued behind the
    /// current entry movement and lockall's task/native/bytecode handoff.</summary>
    public sealed class Timeline
    {
        private readonly bool _fromWhite;
        private readonly FieldArrivalPalette _palette;
        private double _elapsed;
        public int Frames {get;private set;}
        public int DelayFrames {get;}
        public FieldScreenFadePresentation? Fade {get;private set;}
        public bool ScriptReady=>Fade?.ScriptReady==true;
        public Timeline(bool fromWhite,FieldArrivalPalette palette,int movementFrames,bool waitForPlayer)
        {
            if(movementFrames<0)throw new ArgumentOutOfRangeException(nameof(movementFrames));
            _fromWhite=fromWhite;_palette=palette;
            DelayFrames=movementFrames+(waitForPlayer?(movementFrames==0?2:3):0);
            if(DelayFrames==0)Fade=new(fromWhite,palette);
        }
        public void Advance(double frames)
        {
            if(!double.IsFinite(frames)||frames<0)throw new ArgumentOutOfRangeException(nameof(frames));
            _elapsed+=frames;
            while(Frames<(int)Math.Floor(_elapsed+1e-7))
            {
                Frames++;
                if(Fade is null)
                {
                    if(Frames>=DelayFrames)Fade=new(_fromWhite,_palette);
                }
                else Fade.Advance();
            }
        }
    }

}
