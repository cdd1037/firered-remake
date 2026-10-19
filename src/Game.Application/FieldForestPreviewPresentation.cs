namespace Game.Application;

public enum FieldArrivalPalette { Normal,Shade,FogHorizontal }

/// <summary>
/// Source NONE/SUNNY, SHADE and horizontal-fog arrival fades. One Advance runs priority-80
/// weather, then the frame's global palette callback. No loading delay is
/// invented. SHADE transforms both banks together; normal fades split them.
/// </summary>
public sealed class FieldArrivalPalettePresentation
{
    private FieldWarpPresentation.NormalPalette? _normal;
    private bool _weatherInitialized;
    private int _weatherCounter,_weatherBackground=16,_weatherObjects=16;
    public FieldArrivalPalette Kind {get;}
    public bool Active=>_normal?.Active??false;
    public bool WeatherReady {get;private set;}
    public int BackgroundBlend=>_normal?.BackgroundBlend??_weatherBackground;
    public int ObjectBlend=>_normal?.ObjectBlend??_weatherObjects;
    public int BaseArrivalFrames=>Kind switch
    {
        FieldArrivalPalette.Normal=>FieldWarpPresentation.PlainArrivalFrames,
        FieldArrivalPalette.Shade=>18,
        FieldArrivalPalette.FogHorizontal=>19,
        _=>throw new InvalidOperationException()
    };

    public static FieldArrivalPalette ForWeather(string? weather)=>weather switch
    {
        "WEATHER_NONE" or "WEATHER_SUNNY"=>FieldArrivalPalette.Normal,
        "WEATHER_SHADE"=>FieldArrivalPalette.Shade,
        "WEATHER_FOG_HORIZONTAL"=>FieldArrivalPalette.FogHorizontal,
        _=>throw new ArgumentException("Unsupported field arrival weather.",nameof(weather))
    };

    public FieldArrivalPalettePresentation(FieldArrivalPalette kind=FieldArrivalPalette.Normal,bool weatherInitialized=false)
    {
        if(!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        Kind=kind;
        _weatherInitialized=weatherInitialized;
        FadeFromBlack();
    }

    /// <summary>
    /// FadeScreen resets weather even when BeginNormalPaletteFade refuses an
    /// already active fade. The misleadingly named palette_bg_faded_fill_black
    /// writes PLTT_SIZE: both BG and OBJ banks, without moving the fade cursor.
    /// </summary>
    public void FadeFromBlack()
    {
        if(Kind==FieldArrivalPalette.Normal)
        {
            if(_normal is null||!_normal.Active)_normal=new(true);
            _normal.FillBanks(16);
        }
        else
        {
            _weatherCounter=0;
            _weatherBackground=_weatherObjects=16;
        }
        WeatherReady=false;
    }

    public void Advance()
    {
        // Task_WeatherInit consumes the first callback; a repeated FadeScreen
        // does not recreate that task, so later resets may advance immediately.
        if(!_weatherInitialized)_weatherInitialized=true;
        else if(!WeatherReady)
        {
            if(Kind==FieldArrivalPalette.Normal)
            {
                if(!_normal!.Active)WeatherReady=true;
            }
            else
            {
                _weatherCounter++;
                _weatherBackground=_weatherObjects=Math.Max(0,16-_weatherCounter);
                // Fog returns TRUE on the callback that writes zero, unlike
                // SHADE. Readiness becomes observable one callback later.
                if(_weatherCounter>=(Kind==FieldArrivalPalette.FogHorizontal?17:16))WeatherReady=true;
            }
        }
        _normal?.Advance();
    }
}

/// <summary>
/// Forest-type map preview: destination BG0 overlays the live field. It does
/// not accept B, change maps, move actors, release controls, or own game state.
/// </summary>
public static class FieldForestPreviewPresentation
{
    public const int BlendFrames=47;

    public readonly record struct PhaseSample(int Frames,int TaskPhase,int Counter,
        int HoldDuration,int AlphaA,int AlphaB,bool WindowCopyRequested,
        bool FadeFromBlackRequested,bool MusicStartRequested,bool CleanupCopyRequested,bool Complete)
    {
        public bool PreviewVisible=>!Complete&&TaskPhase<5;
    }

    /// <summary>
    /// One priority-0 Task_RunMapPreviewScreenForest callback. Counter is the
    /// source data[1]: elapsed hold callbacks, then the three-callback alpha
    /// subphase. Readiness describes actual graphics/DMA/weather observations.
    /// </summary>
    public sealed class ForestPreview
    {
        private int _phase,_counter,_alphaA=16,_alphaB;
        private bool _windowCopy,_fade,_music,_cleanupCopy;
        public int Frames {get;private set;}
        public int HoldDuration {get;}
        public bool Complete {get;private set;}
        public PhaseSample Sample=>new(Frames,_phase,_counter,HoldDuration,_alphaA,_alphaB,
            _windowCopy,_fade,_music,_cleanupCopy,Complete);

        public ForestPreview(int duration)
        {
            if(duration is not (40 or 120))throw new ArgumentOutOfRangeException(nameof(duration));
            HoldDuration=duration;
        }

        public void AdvanceTask(bool graphicsReady,bool windowCopyReady,bool weatherReady,bool cleanupCopyReady)
        {
            if(Complete)return;
            Frames++;
            _windowCopy=_fade=_music=_cleanupCopy=false;
            switch(_phase)
            {
                case 0:
                    if(graphicsReady&&windowCopyReady){_windowCopy=true;_phase=1;}
                    break;
                case 1:
                    if(windowCopyReady){_fade=true;_phase=2;}
                    break;
                case 2:
                    if(weatherReady){_music=true;_phase=3;}
                    break;
                case 3:
                    if(++_counter>HoldDuration){_counter=0;_phase=4;}
                    break;
                case 4:
                    if(_counter==0)_alphaB=Math.Min(16,_alphaB+1);
                    else if(_counter==1)_alphaA=Math.Max(0,_alphaA-1);
                    _counter=(_counter+1)%3;
                    if(_alphaA==0&&_alphaB==16){_cleanupCopy=true;_phase=5;}
                    break;
                case 5:
                    if(cleanupCopyReady)Complete=true;
                    break;
            }
        }
    }

    /// <summary>
    /// Plain arrival plus the forest overlay. Source order is forest priority0,
    /// arrival priority10, weather priority80, then global UpdatePaletteFade.
    /// Cleanup releases the forest gate before arrival checks it that same frame.
    /// </summary>
    public sealed class ForestArrival
    {
        private int _phase;
        public ForestPreview Preview {get;}
        public FieldArrivalPalettePresentation Palette {get;}
        public int Frames {get;private set;}
        public bool Complete {get;private set;}
        public int BaseArrivalFrames=>Palette.BaseArrivalFrames;
        public int ExtraWaitFrames=>Complete?Frames-BaseArrivalFrames:
            throw new InvalidOperationException("The forest arrival is still active.");
        public FieldWarpPresentation.PhaseSample Sample=>new(Frames,_phase,Palette.BackgroundBlend,
            Palette.ObjectBlend,Palette.Active,Complete);

        public ForestArrival(int duration,FieldArrivalPalette paletteKind=FieldArrivalPalette.Normal)
        {
            Preview=new(duration);
            Palette=new(paletteKind);
        }

        public void Advance(bool graphicsReady=true,bool windowCopyReady=true,bool cleanupCopyReady=true)
        {
            if(Complete)return;
            Frames++;
            Preview.AdvanceTask(graphicsReady,windowCopyReady,Palette.WeatherReady,cleanupCopyReady);
            if(Preview.Sample.FadeFromBlackRequested)Palette.FadeFromBlack();
            if(_phase==0)_phase=1;
            else if(Palette.WeatherReady&&Preview.Complete)Complete=true;
            Palette.Advance();
        }
    }

    /// <summary>
    /// GBA two-layer alpha. Saturate the five-bit channels before expansion,
    /// independently clamp EVA/EVB, and never use host RGBA interpolation.
    /// Colors must already include their own palette fade/weather treatment.
    /// </summary>
    public static ushort AlphaBlendRgb555(ushort foreground,ushort background,int alphaA,int alphaB)
    {
        if(foreground>0x7fff)throw new ArgumentOutOfRangeException(nameof(foreground));
        if(background>0x7fff)throw new ArgumentOutOfRangeException(nameof(background));
        alphaA=Math.Clamp(alphaA,0,16);alphaB=Math.Clamp(alphaB,0,16);
        int red=Math.Min(31,((foreground&31)*alphaA+(background&31)*alphaB)>>4);
        int green=Math.Min(31,(((foreground>>5)&31)*alphaA+((background>>5)&31)*alphaB)>>4);
        int blue=Math.Min(31,(((foreground>>10)&31)*alphaA+((background>>10)&31)*alphaB)>>4);
        return (ushort)(red|(green<<5)|(blue<<10));
    }

    /// <summary>
    /// Generated 4bpp palette-bank indices over destination RGB555 pixels.
    /// Index0 in every bank is transparent, regardless of that palette color.
    /// The map-name window shares a source art palette bank.
    /// </summary>
    public static void CompositeRgb555(ReadOnlySpan<byte> indices,ReadOnlySpan<ushort> palette,
        ReadOnlySpan<ushort> field,int alphaA,int alphaB,Span<ushort> output)
    {
        if(palette.Length!=32)
            throw new ArgumentException("A forest preview requires two sixteen-color palette banks.",nameof(palette));
        if(indices.Length!=field.Length||output.Length!=field.Length)
            throw new ArgumentException("Preview, field and output pixel counts must match.",nameof(output));
        foreach(ushort color in palette)
            if(color>0x7fff)throw new ArgumentException("Palette colors must use RGB555.",nameof(palette));
        for(int pixel=0;pixel<indices.Length;pixel++)
        {
            int index=indices[pixel];
            if(index>=palette.Length)throw new ArgumentOutOfRangeException(nameof(indices));
            if(field[pixel]>0x7fff)throw new ArgumentOutOfRangeException(nameof(field));
            output[pixel]=(index&15)==0?field[pixel]:AlphaBlendRgb555(palette[index],field[pixel],alphaA,alphaB);
        }
    }
}
