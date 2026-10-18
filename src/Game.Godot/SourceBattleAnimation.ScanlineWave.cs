namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    // ScanlineEffect_InitWave's native priority-0 task owns only its cursor and
    // delay. Register memory, DMA destination and deferred stop remain scene-owned.
    private void StartBackgroundScanlineWave(int rank,int startLine,int endLine,int frequency,int amplitude,int delayInterval)
    {
        if(rank is <1 or >2||startLine is <0 or >255||endLine<startLine||endLine>255||frequency is <1 or >255||
            amplitude is <0 or >255||delayInterval is <0 or >255||_script.SineTable.Length<256||
            256/frequency-1+endLine-startLine>256||_tasks.OfType<BackgroundScanlineWaveTask>().Any(t=>!t.Finished))
            throw new InvalidDataException("Source background wave exceeds its byte operands or retained wave-table extent.");
        var state=ScanlineState;state.Clear();StartScanline(rank);
        int theta=0;
        for(int i=0;i<256;i++){state.Write(0,320+i,unchecked((ushort)(_script.SineTable[theta]*amplitude/256)));theta=(theta+frequency)&255;}
        // Native setup copies both banks before the first task turn and does not
        // add gBattle_BGn_X. Later task turns add the live background register.
        for(int i=startLine;i<endLine;i++)for(int bank=0;bank<2;bank++)state.Write(bank,i,state.Read(0,320+i-startLine));
        AddSourceVisualTask(new BackgroundScanlineWaveTask(rank,startLine,endLine,256/frequency,delayInterval),0);
    }
    private sealed class BackgroundScanlineWaveTask(int rank,int startLine,int endLine,int waveLength,int delay):VisualTask
    {
        private readonly int _delay=delay;
        private int _offset,_framesUntilMove=delay;
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o)
        {
            // State3 is consumed by the existing VBlank owner; destruction is on
            // this separate native task's next turn, not the visual task's stop.
            var state=o.ScanlineState;if(state.State==0){Finished=true;return;}
            int background=o.BattlerBackgroundRegister(rank,false);
            for(int line=startLine;line<endLine;line++)state.Write(state.SourceBuffer,line,unchecked((ushort)(state.Read(0,320+_offset+line-startLine)+background)));
            if(_framesUntilMove!=0)_framesUntilMove--;
            else{_framesUntilMove=_delay;if(++_offset==waveLength)_offset=0;}
        }
    }
}
