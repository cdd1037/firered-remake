namespace Game.Application;

/// <summary>
/// Static horizontal field fog after fresh map initialization. Presentation time
/// is measured in weather callbacks after the synchronous initialization loop;
/// camera offsets are relative to that map load, not absolute world coordinates.
/// </summary>
public static class FieldHorizontalFogPresentation
{
    public const int TileSize=64;
    public const int SpriteCount=20;
    public const int Eva=12;
    public const int Evb=8;
    private const int InitializationCalls=93;
    private const int ScrollPeriod=4*65536;

    public readonly record struct PhaseSample(long Frames,int ScrollOffset,int ScrollCounter,int PositionX);
    public readonly record struct FogSprite(int X,int Y);

    /// <summary>
    /// Transient immutable ownership of one reconstructed field. Snapshots retain
    /// this value while a live owner replaces it; no clock is stored in a save.
    /// </summary>
    public sealed record Scene
    {
        public double Frames {get;private init;}
        public int AnchorCameraX {get;}
        public int AnchorCameraY {get;}
        public int SpriteCoordOffsetX {get;private init;}
        public int SpriteCoordOffsetY {get;private init;}
        public bool Initialized=>WholeFrames>=1;
        public long SourceElapsedFrames=>Math.Max(0,WholeFrames-1);
        public PhaseSample Sample=>FieldHorizontalFogPresentation.Sample(SourceElapsedFrames,SpriteCoordOffsetX);
        private long WholeFrames=>(long)Math.Floor(Frames+1e-7);

        public Scene(int anchorCameraX,int anchorCameraY)
        {
            AnchorCameraX=anchorCameraX;AnchorCameraY=anchorCameraY;
        }

        /// <summary>
        /// The caller supplies the camera from before this callback. Weather and
        /// sprites run before camera/panning; the first callback initializes at
        /// offsets 0/0, then subsequent callbacks include the ordinary Y=-40 pan.
        /// Fractional time does not update sprite coordinates before a callback.
        /// </summary>
        public Scene Advance(double deltaFrames,int priorCameraX,int priorCameraY)
        {
            if(!double.IsFinite(deltaFrames)||deltaFrames<0)
                throw new ArgumentOutOfRangeException(nameof(deltaFrames));
            double frames=Frames+deltaFrames;
            if(!double.IsFinite(frames)||frames>=long.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(deltaFrames));
            long whole=(long)Math.Floor(frames+1e-7);
            if(whole<=WholeFrames||whole<2)return this with {Frames=frames};
            // The source camera offsets are signed 16-bit. Reduce wide subtraction
            // explicitly, preserving the hardware wrap even at large origins.
            int x=unchecked((short)((long)AnchorCameraX-priorCameraX));
            int y=unchecked((short)((long)AnchorCameraY-priorCameraY-40));
            return this with {Frames=frames,SpriteCoordOffsetX=x,SpriteCoordOffsetY=y};
        }

        public FogSprite[] Sprites()=>Initialized?
            FieldHorizontalFogPresentation.Sprites(SourceElapsedFrames,SpriteCoordOffsetX,SpriteCoordOffsetY):[];
    }

    /// <summary>
    /// Fresh InitAll finishes at scroll offset23, counter1 and X233. The source
    /// calculates X before advancing its four-callback counter and u16 offset.
    /// </summary>
    public static PhaseSample Sample(long elapsedFrames,int spriteCoordOffsetX=0)
    {
        if(elapsedFrames<0)throw new ArgumentOutOfRangeException(nameof(elapsedFrames));
        // Reduce before adding so even a large recorded presentation clock is safe.
        int calls=(int)(elapsedFrames%ScrollPeriod)+InitializationCalls;
        int scrollBefore=(calls-1)/4;
        int position=(int)(((long)spriteCoordOffsetX-scrollBefore)&255);
        return new(elapsedFrames,(calls/4)&65535,calls&3,position);
    }

    /// <summary>
    /// Twenty 64x64 source OBJ top-left positions. X wraps in nine bits, Y in
    /// eight; sprite order and the five-column layout remain unchanged.
    /// </summary>
    public static FogSprite[] Sprites(long elapsedFrames,int spriteCoordOffsetX,int spriteCoordOffsetY)
    {
        int position=Sample(elapsedFrames,spriteCoordOffsetX).PositionX;
        var sprites=new FogSprite[SpriteCount];
        for(int i=0;i<sprites.Length;i++)
        {
            int column=i%5,centerX=position+32+column*TileSize;
            if(centerX>=272)centerX=(480+position-(4-column)*TileSize)&511;
            int topX=(centerX-32)&511;
            if(topX>=240)topX-=512;
            int topY=(int)(((long)(i/5*TileSize)+spriteCoordOffsetY)&255);
            if(topY>=160)topY-=256;
            sprites[i]=new(topX,topY);
        }
        return sprites;
    }

    /// <summary>
    /// Blend already-transformed fog and destination palette colors with the
    /// hardware's independent 12/8 coefficients. Layer selection belongs to the
    /// field compositor; neither palette fades nor grayscale apply afterward.
    /// </summary>
    public static ushort AlphaBlendRgb555(ushort fog,ushort background)=>
        FieldForestPreviewPresentation.AlphaBlendRgb555(fog,background,Eva,Evb);
}
