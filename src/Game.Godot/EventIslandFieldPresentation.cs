/// <summary>Pure projections of the source field-effect callbacks; never writes gameplay state.</summary>
internal static class EventIslandFieldPresentation
{
    internal const int FragmentFrame=122,DestroyFrames=206;
    internal static (int X,int Y) TrianglePosition(int fromX,int fromY,int toX,int toY,int ticks,int frame)
    {
        if(ticks is not (5 or 60)||frame<0)throw new ArgumentOutOfRangeException(nameof(ticks));
        if(frame>=ticks)return(toX*16,toY*16);
        // Source signed fixed point increment truncates division toward zero, then arithmetic shifts.
        return(((fromX*256+((toX-fromX)*256/ticks)*frame)>>4),((fromY*256+((toY-fromY)*256/ticks)*frame)>>4));
    }
    internal static (int X,int Y) LugiaShake(int frame)
    {
        if(frame<0)throw new ArgumentOutOfRangeException(nameof(frame));
        int step=frame/2;return step==0||step>=4?(0,0):((step&1)==1?-3:3,0);
    }
    internal static int RockShake(int frame)
    {
        if(frame<0)throw new ArgumentOutOfRangeException(nameof(frame));
        if(frame<2||frame>=DestroyFrames)return 0;
        // Setup is task80 tick1. Task90 first callback is tick1; decay begins on tick122.
        int lastPanFrame=frame/2*2;
        int strength=lastPanFrame<FragmentFrame?4:Math.Max(0,4-(lastPanFrame-FragmentFrame+1)/21);
        return(frame/2&1)==1?-strength:strength;
    }
    internal static int BackgroundWhiteBlend(int frame)
    {
        if(frame<0)throw new ArgumentOutOfRangeException(nameof(frame));
        // BeginNormalPaletteFade invokes BG update immediately, then BG/OBJ alternate.
        return frame<FragmentFrame?0:Math.Max(0,16-2*((frame-FragmentFrame+1)/2));
    }
    internal static int BlendChannel(int rgb5,int blend)
    {
        if(rgb5 is <0 or >31||blend is <0 or >16)throw new ArgumentOutOfRangeException(nameof(blend));
        return rgb5+(((31-rgb5)*blend)>>4);
    }
    internal static (int X,int Y) FragmentOffset(int ordinal,int frame)
    {
        if(ordinal is <0 or >3||frame<0)throw new ArgumentOutOfRangeException(nameof(ordinal));
        int ticks=Math.Max(0,frame-FragmentFrame+1);
        return((ordinal%2==0?-16:16)*ticks,(ordinal<2?-12:12)*ticks);
    }
    internal static int WingCount(int frame)=>frame<0?0:Math.Min(3,1+frame/35);
}
