namespace Game.Application;

/// <summary>
/// Source Flash WIN0 horizontal boundaries for the visible field. Endpoints
/// are GBA register values (0..255); the right endpoint is exclusive. The
/// source's extra y=160 scanline is outside this 240x160 visible sample.
/// </summary>
public static class FieldFlashWindowPresentation
{
    public const int Width=240;
    public const int Height=160;
    public const int MaximumRadius=200;

    /// <summary>
    /// Replace the first 160 entries of each destination, including zeroed rows
    /// outside the window. Any trailing entries are untouched. Radius zero is
    /// an empty window; disabling Flash at level zero belongs to the caller.
    /// </summary>
    public static void FillBoundaries(int radius,Span<int> left,Span<int> right)
    {
        if(radius is <0 or >MaximumRadius)throw new ArgumentOutOfRangeException(nameof(radius));
        if(left.Length<Height)throw new ArgumentException("Flash requires 160 visible left boundaries.",nameof(left));
        if(right.Length<Height)throw new ArgumentException("Flash requires 160 visible right boundaries.",nameof(right));
        left=left[..Height];right=right[..Height];
        if(left.Overlaps(right))throw new ArgumentException("Flash boundary destinations must not overlap.",nameof(right));
        left.Clear();right.Clear();

        // Keep the original integer error update and repeated-write order.
        // A geometric circle or rounded square root produces different edges.
        int xy=radius,error=radius,yx=0;
        while(xy>=yx)
        {
            SetBoundary(left,right,80-yx,120-xy,120+xy);
            SetBoundary(left,right,80+yx,120-xy,120+xy);
            SetBoundary(left,right,80-xy,120-yx,120+yx);
            SetBoundary(left,right,80+xy,120-yx,120+yx);
            error-=(yx*2)-1;
            yx++;
            if(error<0){error+=2*(xy-1);xy--;}
        }
    }

    private static void SetBoundary(Span<int> left,Span<int> right,int y,int start,int end)
    {
        if((uint)y>=Height)return;
        left[y]=Math.Clamp(start,0,255);
        right[y]=Math.Clamp(end,0,255);
    }
}
