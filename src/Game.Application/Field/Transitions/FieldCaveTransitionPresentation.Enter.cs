using System.Collections.ObjectModel;

namespace Game.Application;

public static partial class FieldCaveTransitionPresentation
{
    public const int EnterFrames=28;
    public const int PreviewEnterFrames=EnterFrames-1;
    public const int MinimumPreviewFrames=30;

    public static PhaseSample SampleEnter(IReadOnlyList<ushort> transitionPalette,
        IReadOnlyList<ushort> whitePalette,IReadOnlyList<ushort> blackPalette,
        int elapsedFrames,ushort initialBackdrop=0x7fff)
    {
        if(elapsedFrames<0)throw new ArgumentOutOfRangeException(nameof(elapsedFrames));
        var enter=new Enter(transitionPalette,whitePalette,blackPalette,initialBackdrop);
        for(int frame=0;frame<Math.Min(EnterFrames,elapsedFrames);frame++)enter.Advance();
        return enter.Sample;
    }

    /// <summary>Generic ENTER callbacks. Frame zero follows the map-transition reset.</summary>
    public sealed class Enter
    {
        private readonly ushort[] _transitionPalette,_whitePalette,_blackPalette;
        private readonly ushort[] _palette=new ushort[16];
        private ReadOnlyCollection<ushort> _snapshotPalette;
        private int _phase,_paletteCounter,_alphaCounter,_alphaA,_alphaB;
        private bool _backgroundEnabled,_blendEnabled;
        private ushort _backdrop;
        public int Frames {get;private set;}
        public bool Complete {get;private set;}
        public PhaseSample Sample=>new(Frames,_phase,_backgroundEnabled,_blendEnabled,
            _alphaA,_alphaB,_backdrop,Complete,_snapshotPalette);

        public Enter(IReadOnlyList<ushort> transitionPalette,IReadOnlyList<ushort> whitePalette,
            IReadOnlyList<ushort> blackPalette,ushort initialBackdrop=0x7fff)
        {
            _transitionPalette=CopyPalette(transitionPalette,nameof(transitionPalette));
            _whitePalette=CopyPalette(whitePalette,nameof(whitePalette));
            _blackPalette=CopyPalette(blackPalette,nameof(blackPalette));
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
                    Array.Copy(_whitePalette,_palette,16);SnapshotPalette();
                    _backdrop=_blackPalette[0];_backgroundEnabled=true;_phase=2;
                    break;
                case 2:
                    if(_paletteCounter<16)
                    {
                        int count=_paletteCounter;_paletteCounter+=2;
                        Array.Copy(_transitionPalette,15-count,_palette,0,count+1);
                        SnapshotPalette();
                    }
                    else{_alphaA=_alphaB=16;_blendEnabled=true;_phase=3;}
                    break;
                case 3:
                    _alphaA=16-_alphaCounter;_alphaB=16;
                    if(_alphaA!=0)_alphaCounter++;
                    else{_backdrop=_blackPalette[0];Complete=true;}
                    break;
            }
        }

        private void SnapshotPalette()=>_snapshotPalette=Array.AsReadOnly(_palette.ToArray());
    }

    public enum CavePreviewPhase
    {
        Initialize,WaitGraphics,WaitWindowCopy,FadeFromBlack,Hold,FadeToWhite,CaveEnter,Complete
    }

    public readonly record struct PreviewSample(int Frames,int PreviewFrames,CavePreviewPhase Phase,int HoldFrames,
        int HoldDuration,bool NameWindowCreated,int BackgroundBlend,int ObjectBlend,
        ushort FadeColor,bool PaletteActive,PhaseSample? Cave,bool Complete)
    {
        public bool GraphicsRequested=>Frames!=0;
        public bool PreviewVisible=>Phase is CavePreviewPhase.WaitGraphics or CavePreviewPhase.WaitWindowCopy or
            CavePreviewPhase.FadeFromBlack or CavePreviewPhase.Hold or CavePreviewPhase.FadeToWhite;
    }

    /// <summary>
    /// Compose two generated preview palette banks with the source's signed
    /// RGB555 palette fade. Index zero of either 4bpp bank selects the black
    /// backdrop before that same fade; it never selects the bank's color zero.
    /// </summary>
    public static void CompositePreviewRgba(ReadOnlySpan<byte> indices,ReadOnlySpan<ushort> palette32,
        PreviewSample sample,Span<byte> rgba)
    {
        if(palette32.Length!=32)throw new ArgumentException("A preview requires two sixteen-color palette banks.",nameof(palette32));
        if(rgba.Length!=checked(indices.Length*4))throw new ArgumentException("RGBA output must contain four bytes per index.",nameof(rgba));
        foreach(ushort color in palette32)
            if(color>0x7fff)throw new ArgumentException("Preview palettes must contain RGB555 colors.",nameof(palette32));
        if(sample.BackgroundBlend is <0 or >16||sample.FadeColor>0x7fff)
            throw new ArgumentOutOfRangeException(nameof(sample));
        for(int pixel=0;pixel<indices.Length;pixel++)
        {
            int index=indices[pixel];
            if(index>=palette32.Length)throw new ArgumentOutOfRangeException(nameof(indices),"Preview pixels must select one of the two palette banks.");
            ushort color=(index&15)==0?(ushort)0:palette32[index];
            for(int channel=0;channel<3;channel++)
            {
                int value=(color>>(channel*5))&31,target=(sample.FadeColor>>(channel*5))&31;
                // Arithmetic right shift preserves BlendPalette's negative rounding.
                value+=((target-value)*sample.BackgroundBlend)>>4;
                rgba[pixel*4+channel]=Expand(value);
            }
            rgba[pixel*4+3]=255;
        }
    }

    /// <summary>
    /// Cave-type preview followed by the shared ENTER program. Loading readiness
    /// is supplied by the host; no hardware or decompression delay is invented.
    /// Each Advance is one CB2_ChangeMapMain callback, including its task-local
    /// and global palette updates and the preceding palette-transfer boundary.
    /// </summary>
    public sealed class CavePreview
    {
        private readonly Enter _enter;
        private FieldWarpPresentation.NormalPalette? _palette;
        private CavePreviewPhase _phase;
        private int _holdFrames,_previewFrames;
        private bool _nameWindowCreated,_transferPending,_enterSelected;
        private ushort _fadeColor;
        public int Frames {get;private set;}
        public int TotalFrames=>Frames;
        public int PreviewFrames=>_enterSelected?_previewFrames:Frames;
        public PhaseSample? EnterSample=>_enterSelected?_enter.Sample:null;
        public int HoldDuration {get;}
        public bool Complete=>_phase==CavePreviewPhase.Complete;
        public PreviewSample Sample=>new(Frames,PreviewFrames,_phase,_holdFrames,HoldDuration,_nameWindowCreated,
            _palette?.BackgroundBlend??16,_palette?.ObjectBlend??16,_fadeColor,
            _palette?.Active??false,_enterSelected?_enter.Sample:null,Complete);

        public CavePreview(IReadOnlyList<ushort> transitionPalette,IReadOnlyList<ushort> whitePalette,
            IReadOnlyList<ushort> blackPalette,bool hasVisited)
        {
            _enter=new Enter(transitionPalette,whitePalette,blackPalette);
            // Task_MapPreviewScreen_0 changes directly to Enter_1, skipping Enter_0.
            _enter.Advance();
            HoldDuration=hasVisited?40:120;
        }

        public void Advance(bool graphicsReady,bool windowCopyReady,bool cancelHeld)
        {
            if(Complete)return;
            Frames++;
            // VBlank transfers the preceding callback's palette. The controller
            // represents source callbacks, not guessed GPU/decompression cycles.
            _transferPending=false;
            switch(_phase)
            {
                case CavePreviewPhase.Initialize:
                    _phase=CavePreviewPhase.WaitGraphics;break;
                case CavePreviewPhase.WaitGraphics:
                    if(graphicsReady){_nameWindowCreated=true;_phase=CavePreviewPhase.WaitWindowCopy;}
                    break;
                case CavePreviewPhase.WaitWindowCopy:
                    if(windowCopyReady)
                    {
                        _palette=new FieldWarpPresentation.NormalPalette(true,-1);
                        _phase=CavePreviewPhase.FadeFromBlack;
                    }
                    break;
                case CavePreviewPhase.FadeFromBlack:
                    if(!UpdatePalette())_phase=CavePreviewPhase.Hold;
                    break;
                case CavePreviewPhase.Hold:
                    _holdFrames++;
                    // JOY_HELD is deliberately not an edge-triggered press.
                    if(_holdFrames>HoldDuration||cancelHeld)
                    {
                        _fadeColor=0x7fff;
                        _palette=new FieldWarpPresentation.NormalPalette(false,-2);
                        _phase=CavePreviewPhase.FadeToWhite;
                    }
                    break;
                case CavePreviewPhase.FadeToWhite:
                    if(!UpdatePalette())
                    {
                        _enterSelected=true;_previewFrames=Frames;_phase=CavePreviewPhase.CaveEnter;
                        _nameWindowCreated=false;
                    }
                    break;
                case CavePreviewPhase.CaveEnter:
                    _enter.Advance();
                    if(_enter.Complete)_phase=CavePreviewPhase.Complete;
                    break;
            }
            UpdatePalette(); // CB2_ChangeMapMain also calls this after RunTasks.
        }

        private bool UpdatePalette()
        {
            if(_palette is null)return false;
            if(_transferPending)return true; // PALETTE_FADE_STATUS_LOADING
            _palette.Advance();
            _transferPending=_palette.HasSelectedPalettes;
            return _palette.Active;
        }
    }
}
