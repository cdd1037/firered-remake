namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    public sealed partial class SpriteScene
    {
        internal readonly SourceScanlineState Scanlines=new();
        internal readonly ushort[] BackgroundRegisters=new ushort[4];
        internal SourceBattleAnimation? ScanlineOwner;
        internal SourceScanlineFrame? ScanlineFrame;
        internal SourceScanlineState OwnScanlines(SourceBattleAnimation owner)
        {
            EnsureController(owner);ScanlineOwner=owner;return Scanlines;
        }
        internal bool IsController(SourceBattleAnimation owner)=>ReferenceEquals(_current,owner);
        internal void CancelScanlines(SourceBattleAnimation owner)
        {
            if(!ReferenceEquals(ScanlineOwner,owner))return;
            Scanlines.Stop();ScanlineFrame=null;ScanlineOwner=null;
        }
        internal void ResetScanlines()
        {Scanlines.Clear();ScanlineFrame=null;ScanlineOwner=null;Array.Clear(BackgroundRegisters);}
    }
    private SourceScanlineState ScanlineState=>_spriteScene.OwnScanlines(this);
    private ushort[][] ScanlineBuffers=>ScanlineState.Buffers;
    private int ScanlineWriteBuffer=>ScanlineState.SourceBuffer;
    private void StartScanline(int rank,bool pairedOffsets=false)=>ScanlineState.SetParams(rank switch
        {1=>SourceScanlineDestination.Bg1Horizontal,2=>SourceScanlineDestination.Bg2Horizontal,_=>throw new ArgumentOutOfRangeException(nameof(rank))},pairedOffsets);
    private void StartVerticalScanline(int rank)=>ScanlineState.SetParams(rank switch
        {1=>SourceScanlineDestination.Bg1Vertical,2=>SourceScanlineDestination.Bg2Vertical,_=>throw new ArgumentOutOfRangeException(nameof(rank))},false);
    private void StartAlphaScanline()=>ScanlineState.SetParams(SourceScanlineDestination.BlendAlpha,false);
    private void StopScanlineDeferred()=>ScanlineState.RequestStop();
    private PixelPoint ScanlineBackgroundOffset(int rank)=>new(BattlerBackgroundRegister(rank,false),BattlerBackgroundRegister(rank,true));
    private void SetScanlineBackgroundOffset(int rank,PixelPoint value)
    {SetBattlerBackgroundRegister(rank,false,value.X);SetBattlerBackgroundRegister(rank,true,value.Y);}
    private int BattlerBackgroundRegister(int rank,bool vertical)
    {if(rank is <1 or >2)throw new ArgumentOutOfRangeException(nameof(rank));return _spriteScene.BackgroundRegisters[(rank-1)*2+(vertical?1:0)];}
    private void SetBattlerBackgroundRegister(int rank,bool vertical,int value)
    {
        if(rank is <1 or >2)throw new ArgumentOutOfRangeException(nameof(rank));
        ushort wrapped=unchecked((ushort)value);_spriteScene.BackgroundRegisters[(rank-1)*2+(vertical?1:0)]=wrapped;
        if(rank==1&&_overlay is not null)_overlay=vertical?_overlay with{ScrollY=wrapped}:_overlay with{ScrollX=wrapped};
    }
    private readonly bool[] _queuedBackgroundRegisters=new bool[4];
    private bool _queuedAlphaRegister;
    private int? _scanlineTerminalAlpha;
    private void QueueBackgroundRegisters(int rank)
    {_queuedBackgroundRegisters[(rank-1)*2]=true;_queuedBackgroundRegisters[(rank-1)*2+1]=true;}
    private int SnapshotAlpha(bool second)
    {
        if(ReferenceEquals(_spriteScene.ScanlineOwner,this)&&_spriteScene.Scanlines.Destination==SourceScanlineDestination.BlendAlpha&&_scanlineTerminalAlpha is {} value)
            return second?(value>>8)&31:value&31;
        return second?_alphaB:_alphaA;
    }
    private void AdvanceScanlineVBlank()
    {
        // Explicit frame-phase model: main-loop SetGpuReg writes queue; the
        // VBlank base-register writes are immediate. Existing dirty entries
        // still flush their latest shadow after the scanline row-zero seed.
        var state=_spriteScene.Scanlines;SourceScanlineFrame? frame;
        if(state.Destination==SourceScanlineDestination.BlendAlpha)
            frame=state.CaptureVBlank(_queuedAlphaRegister?(ushort)(_alphaA|(_alphaB<<8)):null);
        else
        {
            int first=state.Destination switch{SourceScanlineDestination.Bg1Horizontal=>0,SourceScanlineDestination.Bg1Vertical=>1,SourceScanlineDestination.Bg2Horizontal=>2,SourceScanlineDestination.Bg2Vertical=>3,_=>throw new InvalidDataException("Unknown BG scanline destination.")};
            frame=state.CaptureVBlank(_queuedBackgroundRegisters[first]?_spriteScene.BackgroundRegisters[first]:null,
                state.Word32&&_queuedBackgroundRegisters[first+1]?_spriteScene.BackgroundRegisters[first+1]:null);
        }
        _spriteScene.ScanlineFrame=frame;
        if(_queuedAlphaRegister)_scanlineTerminalAlpha=_alphaA|(_alphaB<<8);
        if(frame is {Destination:SourceScanlineDestination.BlendAlpha})_scanlineTerminalAlpha=frame.Words[160];
        Array.Clear(_queuedBackgroundRegisters);_queuedAlphaRegister=false;
    }
    private void InitializeBattlerBackgroundRegisters(int slot,bool follow)
    {
        int rank=_battlers[slot].BackgroundRank;var p=BattlerPosition(slot);var offset=_offsets[slot];
        SetScanlineBackgroundOffset(rank,new(32-p.X-offset.X,32-p.Y-offset.Y));QueueBackgroundRegisters(rank);
        foreach(var task in _tasks.OfType<BattlerBackgroundFollowTask>().Where(t=>t.Rank==rank).ToArray())_tasks.Remove(task);
        if(follow)_tasks.Add(new BattlerBackgroundFollowTask(slot,rank){Priority=10});
    }
    private sealed class BattlerBackgroundFollowTask(int slot,int rank):VisualTask
    {
        public int Rank=>rank;
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation owner)
        {
            if(!owner._backgrounds.Contains(slot)){Finished=true;return;}
            var p=owner.BattlerPosition(slot);var offset=owner._offsets[slot];
            owner.SetScanlineBackgroundOffset(rank,new(32-p.X-offset.X,32-p.Y-offset.Y));
        }
    }
    private void ResetBattlerBackgroundRegisters(int rank)
    {
        if(_spriteScene.IsController(this))SetScanlineBackgroundOffset(rank,default);
        foreach(var task in _tasks.OfType<BattlerBackgroundFollowTask>().Where(t=>t.Rank==rank).ToArray())_tasks.Remove(task);
    }
}
