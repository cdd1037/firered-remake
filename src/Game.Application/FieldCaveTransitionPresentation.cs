using System.Collections.ObjectModel;

namespace Game.Application;

public enum FieldTransitionInterlude { None,CaveExit,CaveEnter,CavePreviewEnter,RegionPreview }

/// <summary>
/// The generic cave EXIT interlude, between the field's black departure and
/// white arrival fades. Owns presentation callbacks only, never map/game state.
/// Palette input is generated RGB555 data and pixel input uses 4-bit indices.
/// </summary>
public static partial class FieldCaveTransitionPresentation
{
    public const int ExitFrames=38;

    public readonly record struct PhaseSample(int Frames,int TaskPhase,
        bool BackgroundEnabled,bool BlendEnabled,int AlphaA,int AlphaB,
        ushort BackdropColor,bool Complete,ReadOnlyCollection<ushort> Palette);

    public static PhaseSample SampleExit(IReadOnlyList<ushort> transitionPalette,
        IReadOnlyList<ushort> whitePalette,int elapsedFrames,ushort initialBackdrop=0)
    {
        if(elapsedFrames<0)throw new ArgumentOutOfRangeException(nameof(elapsedFrames));
        var exit=new Exit(transitionPalette,whitePalette,initialBackdrop);
        for(int frame=0;frame<Math.Min(ExitFrames,elapsedFrames);frame++)exit.Advance();
        return exit.Sample;
    }

    public sealed class Exit
    {
        private readonly ushort[] _transitionPalette,_whitePalette;
        private readonly ushort[] _palette=new ushort[16];
        private ReadOnlyCollection<ushort> _snapshotPalette;
        private int _phase,_alphaA,_alphaB,_alphaCounter,_paletteCounter;
        private bool _backgroundEnabled;
        private ushort _backdrop;
        public int Frames {get;private set;}
        public bool Complete {get;private set;}
        public PhaseSample Sample=>new(Frames,_phase,_backgroundEnabled,
            _backgroundEnabled,_alphaA,_alphaB,_backdrop,Complete,_snapshotPalette);

        public Exit(IReadOnlyList<ushort> transitionPalette,IReadOnlyList<ushort> whitePalette,
            ushort initialBackdrop=0)
        {
            _transitionPalette=CopyPalette(transitionPalette,nameof(transitionPalette));
            _whitePalette=CopyPalette(whitePalette,nameof(whitePalette));
            if(initialBackdrop>0x7fff)throw new ArgumentOutOfRangeException(nameof(initialBackdrop));
            _backdrop=initialBackdrop;
            _snapshotPalette=Array.AsReadOnly(_palette.ToArray());
        }

        public void Advance()
        {
            if(Complete)return;
            Frames++;
            switch(_phase)
            {
                case 0:_phase=1;break;
                case 1:
                    // Task_Exit_1 loads all sixteen whites into BG palette14,
                    // then overwrites its first eight with source colors8..15.
                    Array.Copy(_whitePalette,_palette,16);
                    Array.Copy(_transitionPalette,8,_palette,0,8);
                    SnapshotPalette();
                    _backgroundEnabled=true;_alphaA=_alphaB=0;_alphaCounter=0;_phase=2;
                    break;
                case 2:
                    // The source writes17 once before advancing the task.
                    // Preserve that register value; hardware clamps it to16.
                    _alphaA=_alphaCounter;_alphaB=16;
                    if(_alphaCounter<=16)_alphaCounter++;
                    else{_paletteCounter=0;_phase=3;}
                    break;
                case 3:
                    _alphaA=_alphaB=16;
                    if(_paletteCounter<8)
                    {
                        int count=_paletteCounter++;
                        Array.Copy(_transitionPalette,count+8,_palette,0,8-count);
                        SnapshotPalette();
                    }
                    else
                    {
                        // A separate LoadPalette at BG slot0 changes backdrop,
                        // not palette14. The following eight callbacks only wait.
                        _backdrop=_whitePalette[0];_paletteCounter=8;_phase=4;
                    }
                    break;
                default:
                    if(_paletteCounter!=0)_paletteCounter--;
                    else Complete=true;
                    break;
            }
        }

        private void SnapshotPalette()=>_snapshotPalette=Array.AsReadOnly(_palette.ToArray());
    }

    /// <summary>
    /// Compose BG0 against its backdrop with the GBA's saturated RGB555 alpha
    /// arithmetic. Index0 is transparent even if palette14 color0 is nonzero.
    /// A disabled background displays only the backdrop. Output is opaque RGBA.
    /// </summary>
    public static void CompositeRgba(ReadOnlySpan<byte> indices,PhaseSample sample,Span<byte> rgba)
    {
        if(rgba.Length!=checked(indices.Length*4))throw new ArgumentException("RGBA output must contain four bytes per index.",nameof(rgba));
        int alphaA=Math.Clamp(sample.AlphaA,0,16),alphaB=Math.Clamp(sample.AlphaB,0,16);
        for(int pixel=0;pixel<indices.Length;pixel++)
        {
            int index=indices[pixel];
            if(index>15)throw new ArgumentOutOfRangeException(nameof(indices),"Cave pixels must use 4-bit palette indices.");
            ushort color=sample.BackdropColor;
            if(sample.BackgroundEnabled&&index!=0)
            {
                color=sample.Palette[index];
                if(sample.BlendEnabled)color=Blend(color,sample.BackdropColor,alphaA,alphaB);
            }
            int offset=pixel*4;
            rgba[offset]=Expand(color&31);
            rgba[offset+1]=Expand((color>>5)&31);
            rgba[offset+2]=Expand((color>>10)&31);
            rgba[offset+3]=255;
        }
    }

    private static ushort Blend(ushort foreground,ushort background,int alphaA,int alphaB)
    {
        int red=Math.Min(31,((foreground&31)*alphaA+(background&31)*alphaB)>>4);
        int green=Math.Min(31,(((foreground>>5)&31)*alphaA+((background>>5)&31)*alphaB)>>4);
        int blue=Math.Min(31,(((foreground>>10)&31)*alphaA+((background>>10)&31)*alphaB)>>4);
        return (ushort)(red|(green<<5)|(blue<<10));
    }

    private static byte Expand(int value)=>(byte)((value<<3)|(value>>2));

    private static ushort[] CopyPalette(IReadOnlyList<ushort> palette,string name)
    {
        ArgumentNullException.ThrowIfNull(palette,name);
        if(palette.Count!=16)throw new ArgumentException("A cave palette must contain sixteen RGB555 colors.",name);
        var result=new ushort[16];
        for(int index=0;index<16;index++)
        {
            ushort color=palette[index];
            if(color>0x7fff)throw new ArgumentException("Cave palettes must contain RGB555 colors.",name);
            result[index]=color;
        }
        return result;
    }
}
