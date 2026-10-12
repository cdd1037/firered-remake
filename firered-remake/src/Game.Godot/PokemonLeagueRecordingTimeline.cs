/// <summary>Display clock port of field_effect.c's HallOfFame/glowing-ball callbacks.
/// The application owns completion; this clock only supplies the correlated visual receipt.</summary>
internal static class PokemonLeagueRecordingTimeline
{
    internal static int LastBallTick(int count) => count is >= 1 and <= 6 ? (count - 1) * 25 : throw new ArgumentOutOfRangeException(nameof(count));
    internal static int State(int count, int tick)
    {
        int last=LastBallTick(count), t=tick-last;
        if(t<0)return 0;if(t<32)return 1;if(t<128)return 2;if(t<152)return 3;
        if(t<182)return 4;if(t<183)return 5;if(t<184)return 6;return 7;
    }
    // RunTasks observes sprite state 7 on the following frame, then removes the field effect.
    internal static int RemovalTick(int count) => LastBallTick(count)+185;
    internal static int BallCount(int count,int tick) => State(count,tick)>4?0:Math.Min(count,Math.Max(0,tick/25+1));
    internal static int Palette(int count,int tick)
    {
        int t=tick-LastBallTick(count)-32;
        return t<1?0:t<=96?1+(t/8&3):5+Math.Min(3,(t-96)/8);
    }
    internal static int MonitorFrame(int count,int tick)
    {
        int t=tick-LastBallTick(count)-33;if(t<0||t>=160)return -1;
        return t<144?new[]{3,2,1,0,1,2}[t/8%6]:t<152?1:0;
    }
}
