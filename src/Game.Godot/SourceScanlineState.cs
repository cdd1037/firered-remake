namespace Game.Presentation.BattleAnimations;

/// <summary>The selected native HBlank DMA destination; paired writes also update VOFS.</summary>
public enum SourceScanlineDestination { Bg1Horizontal, Bg2Horizontal, BlendAlpha }

/// <summary>Detached register samples for visible rows 0–159 and the terminal HBlank transfer.</summary>
public sealed record SourceScanlineFrame(SourceScanlineDestination Destination,bool Word32,ushort[] Words)
{
    public SourceScanlineFrame Detach()=>this with{Words=(ushort[])Words.Clone()};
    public void Validate()
    {
        if(!Enum.IsDefined(Destination)||Word32&&Destination==SourceScanlineDestination.BlendAlpha||Words is null||Words.Length!=(Word32?322:161))
            throw new ArgumentException("Unsupported source scanline destination, width, or register sample count.");
    }
}

/// <summary>
/// The source scanline engine's retained two-bank register memory. Its selected
/// callbacks and generic wave tasks share this one authority. Invocation ownership,
/// task scheduling and GPU write ordering belong to the scene; snapshots do not
/// advance this state.
/// </summary>
public sealed class SourceScanlineState
{
    public const int BankWords=0x3C0;
    internal ushort[][] Buffers { get; }=[new ushort[BankWords],new ushort[BankWords]];
    public int SourceBuffer { get; private set; }
    public int State { get; private set; }
    public SourceScanlineDestination Destination { get; private set; }
    public bool Word32 { get; private set; }
    public ushort Read(int bank,int index)=>Buffers[CheckedBank(bank)][CheckedIndex(index)];
    public void Write(int bank,int index,ushort value)=>Buffers[CheckedBank(bank)][CheckedIndex(index)]=value;
    public void SetParams(SourceScanlineDestination destination,bool word32)
    {
        if(!Enum.IsDefined(destination)||word32&&destination==SourceScanlineDestination.BlendAlpha)
            throw new ArgumentOutOfRangeException(nameof(destination),"Only selected native 16-bit registers and paired BG offsets are supported.");
        Destination=destination;Word32=word32;State=1;
    }
    public void RequestStop()=>State=3;
    public void Stop()=>State=0;
    public void Clear()
    {
        foreach(var bank in Buffers)Array.Clear(bank);
        SourceBuffer=0;State=0;Destination=default;Word32=false;
    }
    public SourceScanlineFrame? CaptureVBlank(ushort? firstLowOverride=null,ushort? firstHighOverride=null)
    {
        if(State==0)return null;
        if(State==3){State=0;return null;}
        var words=Buffers[SourceBuffer][..(Word32?322:161)];
        // Buffered SetGpuReg entries flush after the source manual row-zero copy.
        // The scene supplies only dirty selected halfwords under its explicit
        // frame-phase model; this class does not infer CPU/VCOUNT cycle timing.
        if(firstLowOverride is {} low)words[0]=low;
        if(Word32&&firstHighOverride is {} high)words[1]=high;
        SourceBuffer^=1;
        return new(Destination,Word32,words);
    }
    private static int CheckedBank(int bank)=>bank is 0 or 1?bank:throw new ArgumentOutOfRangeException(nameof(bank));
    private static int CheckedIndex(int index)=>index is >=0 and <BankWords?index:throw new ArgumentOutOfRangeException(nameof(index));
}
