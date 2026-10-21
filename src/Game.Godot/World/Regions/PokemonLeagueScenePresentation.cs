/// <summary>Read-only source scene arithmetic. No campaign state, save or gameplay RNG.</summary>
public static class PokemonLeagueScenePresentation
{
    public const int MonCryFrame = 113;
    public const int MonDuration = 260;
    public static int CameraPixels(int frame, int duration) => Math.Clamp(frame / 2, 0, duration);
    public static (int X, int Y, int Ground, bool Rival, bool StaticGround) Runner(int scene, int frame)
        => scene switch
        {
            0 => (Math.Max(208, 272-frame),80,3,false,false),
            1 => (208,80,2,false,false),
            2 => (208,80,3,false,false),
            3 => (208,Math.Max(80,160-frame/2),0,true,true),
            4 => (208,80,0,false,false),
            _ => throw new ArgumentOutOfRangeException(nameof(scene))
        };
    public static int ZoomPicture(int frame) => frame < 54 ? 0 : frame < 59 ? 1 : 2;
    // BG affine parameters are inverse scale: 16 -> 256 reveals the circle.
    public static int CircleAffine(int frame) => Math.Clamp((frame-60)*16,0,256);
    public static float MonFade(int frame) => frame < 19 ? Math.Clamp((19-frame)/16f,0,1) : Math.Clamp((frame-242)/16f,0,1);
}

public sealed class HallConfettiPresentation
{
    public sealed class Piece
    {
        public int X, Y, OffsetX, OffsetY, Angle, Speed, Frame;
    }
    private uint _random;
    private int _tick = -1;
    private readonly List<Piece> _pieces = [];
    public IReadOnlyList<Piece> Pieces => _pieces;
    public HallConfettiPresentation(uint seed) => _random=seed;
    private int Random() { _random=unchecked(_random*1103515245+24691);return (int)(_random>>16); }
    public void AdvanceTo(int tick, IReadOnlyList<int> sine)
    {
        while(_tick<tick)
        {
            _tick++;
            int remaining=399-_tick;
            if(remaining>110&&(remaining&3)==0)
                _pieces.Add(new(){X=Random()%240,Y=-(Random()%8),Frame=Random()%17,Speed=(Random()&3)==0?1:0});
            _pieces.RemoveAll(p=>p.OffsetY>120);
            foreach(var p in _pieces)
            {
                p.OffsetY+=1+p.Speed;
                p.OffsetX=((Random()%4)+8)*sine[p.Angle&255]/256;
                p.Angle+=4;
            }
        }
    }
}
