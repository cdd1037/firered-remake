/// <summary>Source single-party geometry and cursor semantics, independent of engine state.</summary>
internal static class PartyPresentationAssets
{
    internal readonly record struct Box(int X,int Y,int Width,int Height)
    {
        internal bool Contains(double x,double y)=>x>=X&&x<X+Width&&y>=Y&&y<Y+Height;
    }
    internal static Box Slot(int index)=>index is <0 or >5?throw new ArgumentOutOfRangeException(nameof(index)):
        index==0?new(8,24,80,56):new(96,8+(index-1)*24,144,24);
    internal static Box Cancel=>new(184,136,56,16);
    internal static int Move(int cursor,int count,bool cancel,int dx,int dy,ref int lastRight)
    {
        if(count is <1 or >6)throw new ArgumentOutOfRangeException(nameof(count));
        int rows=count+(cancel?1:0);
        if(dy!=0)return (cursor+rows+Math.Sign(dy))%rows;
        if(dx<0&&cursor>0&&cursor<count){lastRight=cursor;return 0;}
        if(dx>0&&cursor==0&&count>1)return Math.Clamp(lastRight,1,count-1);
        return cursor;
    }
}
