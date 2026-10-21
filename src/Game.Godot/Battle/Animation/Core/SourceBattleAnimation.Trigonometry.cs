namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private int SampleSourceSine(int index)=>PeriodicSineSample(_script.SineTable,index);

    // The pinned table contains one 256-step period plus its first 64 entries.
    // Keep every defined source entry exact. Beyond that stored extent, use the
    // periodic function rather than reading unrelated adjacent memory.
    internal static int PeriodicSineSample(ReadOnlySpan<int> table,int index)
    {
        if(table.Length!=320)throw new InvalidDataException("Source sine table requires 320 entries.");
        return table[(uint)index<(uint)table.Length?index:index&255];
    }
}
