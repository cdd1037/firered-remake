using System.Collections.Immutable;
using Game.Domain.Capture;

public enum CaptureBallPhase { TrainerWindup, Throw, Open, Shrink, Close, Bounce, Rest, Shake, BreakOut, Click, Fade, Complete }
public sealed record CaptureBallFrame(CaptureBallPhase Phase, int X, int Y, int TileFrame,
    bool BallVisible, float TargetScale, int TargetOffsetY, int Rotation, float BallAlpha = 1, float BallBrightness = 1);
public sealed record CaptureBallCue(int Tick, string Symbol);
public sealed record CaptureBallParticle(int X,int Y,int TileFrame,bool FlipH=false);

/// <summary>
/// Read-only 60 Hz playback of a committed result. Uses the actual battle_anim_special throw path,
/// source integer translation, native sine table, bounce and subpixel shake state machine.
/// Sprite callback boundaries are explicit; this is not a GBA scheduler or a particle-engine emulator.
/// </summary>
public sealed class CaptureBallPresentation
{
    private readonly ImmutableArray<int> _sine;
    private readonly int _openTick, _breakOutTick, _clickTick;
    public CaptureBall Ball { get; }
    public int AnimationCode { get; }
    public int ShakeCount => AnimationCode == 4 ? 3 : AnimationCode;
    public ImmutableArray<CaptureBallFrame> Frames { get; }
    public ImmutableArray<CaptureBallCue> Cues { get; }
    public int DurationTicks => Frames.Length - 1;
    public CaptureBallFrame Sample(int tick) => Frames[Math.Clamp(tick, 0, Frames.Length - 1)];
    public bool Complete(int tick) => tick >= DurationTicks;

    public CaptureBallPresentation(CaptureBall ball, CaptureOutcome outcome, int animationCode,
        ImmutableArray<int> sine, int targetX = 176, int targetY = 40, int startX = 32, int startY = 80, int windupTicks = 0)
    {
        if (!Enum.IsDefined(ball) || animationCode is < 0 or > 4 ||
            outcome != (animationCode == 4 ? CaptureOutcome.Caught : CaptureOutcome.Escaped))
            throw new ArgumentException("Capture playback requires a consistent committed result.");
        if (sine.Length != 320) throw new ArgumentException("Native sine table required.", nameof(sine));
        if(windupTicks is <0 or >60)throw new ArgumentOutOfRangeException(nameof(windupTicks));
        Ball = ball; AnimationCode = animationCode; _sine=sine; _breakOutTick=-1; _clickTick=-1;
        var frames = ImmutableArray.CreateBuilder<CaptureBallFrame>();
        var cues = ImmutableArray.CreateBuilder<CaptureBallCue>();
        int x = startX, y = startY, rotation = 0;
        void Add(CaptureBallPhase phase, int tile = 0, float scale = 0, int offset = 0, bool visible = true, float alpha = 1, float brightness = 1)
            => frames.Add(new(phase,x,y,tile,visible,scale,offset,rotation,alpha,brightness));
        void Cue(string symbol) => cues.Add(new(frames.Count,symbol));
        int Sin(int angle, int amplitude) => (sine[angle & 255] * amplitude) >> 8;
        int Cos(int angle, int amplitude) => (sine[(angle & 255) + 64] * amplitude) >> 8;
        int Delta(int distance) { int result = Math.Abs(distance) * 256 / 34; return distance < 0 ? result | 1 : result & ~1; }
        int dx = Delta(targetX-startX), dy = Delta(targetY-16-startY);
        for(int t=0;t<windupTicks;t++)Add(CaptureBallPhase.TrainerWindup,scale:1,visible:false);
        Cue("SE_BALL_THROW"); Add(CaptureBallPhase.Throw, scale:1);
        for (int t=1;t<=34;t++)
        {
            x=startX+((dx&1)!=0 ? -(dx*t>>8) : dx*t>>8);
            y=startY+((dy&1)!=0 ? -(dy*t>>8) : dy*t>>8)+Sin((0x8000/34*t)>>8,-40);
            Add(CaptureBallPhase.Throw,scale:1);
        }
        // The flight callback commits its translated position; do not snap to a floating-point endpoint.
        int ballY=y; _openTick=frames.Count; Cue("SE_BALL_OPEN");
        for(int t=0;t<10;t++) Add(CaptureBallPhase.Open,t<5?1:2,1);
        Add(CaptureBallPhase.Shrink,2,1);
        int shrinkDelta=(targetY-ballY)*256/28;
        for(int t=1;t<=28;t++)
        {
            if(t==10)Cue("SE_BALL_TRADE");
            Add(CaptureBallPhase.Shrink,2,256f/(256+32*t),(-shrinkDelta*t)>>8);
        }
        Add(CaptureBallPhase.Shrink,2);
        for(int t=0;t<10;t++) Add(CaptureBallPhase.Close,t<5?1:0);
        int groundY=ballY+40, amplitude=40, angle=0, bounce=0; bool rising=false;
        while(bounce<4)
        {
            y=groundY-Cos(angle,amplitude); Add(CaptureBallPhase.Bounce);
            if(!rising)
            {
                angle+=bounce+4;
                if(angle>=64) { amplitude-=10; bounce++; rising=true; Cue("SE_BALL_BOUNCE_"+bounce); }
            }
            else
            {
                angle-=bounce+4;
                if(angle<=0){angle=0;rising=false;}
            }
        }
        y=groundY; int baseX=x;
        for(int shake=0;shake<ShakeCount;shake++)
        {
            for(int t=0;t<31;t++)Add(CaptureBallPhase.Rest);
            Cue("SE_BALL");
            int direction=shake%2==0?1:-1, subpixel=0;
            rotation=0;
            // Actual callback runs 8, 13 and 5 movement ticks, with the reversal callback between.
            void Move(int ticks)
            {
                for(int t=0;t<ticks;t++)
                {
                    if(subpixel>255){x+=direction;subpixel&=255;}else subpixel+=0xB0;
                    rotation-=direction*3; Add(CaptureBallPhase.Shake);
                }
                subpixel=0;
            }
            Move(8); direction=-direction; Add(CaptureBallPhase.Shake);
            Move(13); direction=-direction; Move(5);
            Add(CaptureBallPhase.Shake); // result branch callback
        }
        if(animationCode==4)
        {
            Add(CaptureBallPhase.Click);
            for(int t=1;t<=315;t++)
            {
                if(t==40){_clickTick=frames.Count;Cue("SE_BALL_CLICK");}
                if(t==95)Cue("MUS_CAUGHT_INTRO");
                float brightness=t<40?1:t<60?.625f:Math.Min(1,.625f+(t-60)/80f);
                Add(CaptureBallPhase.Click,brightness:brightness);
            }
            for(int t=0;t<34;t++)Add(CaptureBallPhase.Fade,alpha:1-t/34f);
            Add(CaptureBallPhase.Complete,visible:false);
        }
        else
        {
            for(int t=0;t<31;t++)Add(CaptureBallPhase.Rest);
            _breakOutTick=frames.Count; Cue("SE_BALL_OPEN"); rotation=0;
            // data.c sAffineAnim_Battler_Emerge: scale 0x28, then twelve +0x12 updates.
            // Keep the independent palette restoration visible after the affine finishes.
            for(int t=0;t<34;t++)Add(CaptureBallPhase.BreakOut,t<5?1:2,Math.Min(1,(40+18*t)/256f),t<12?Math.Max(0,(4096-288*(t+1))>>8):0,t<10);
            x=baseX; Add(CaptureBallPhase.Complete,scale:1,visible:false);
        }
        Frames=frames.ToImmutable(); Cues=cues.ToImmutable();
    }
    public int TargetBlend(int tick)
    {
        if(tick<_openTick)return 0;
        if(_breakOutTick>=0&&tick>=_breakOutTick)return Math.Clamp(16-Math.Max(0,tick-_breakOutTick-16),0,16);
        return Math.Clamp(tick-_openTick,0,16);
    }

    /// <summary>Native 8x8 particle trajectories; no synthesized replacement art or random draws.</summary>
    public ImmutableArray<CaptureBallParticle> Particles(int tick)
    {
        var output=ImmutableArray.CreateBuilder<CaptureBallParticle>();
        int Sin(int angle,int radius)=>(_sine[angle&255]*radius)>>8;
        int Cos(int angle,int radius)=>(_sine[(angle&255)+64]*radius)>>8;
        void Burst(int start)
        {
            int age=tick-start;
            if(start<0||age<0||age>=61)return;
            var origin=Frames[start]; int ox=origin.X,oy=origin.Y-5;
            void Particle(int delay,int angle,int speed,int rx,int ry,int mode=0)
            {
                int t=age-delay;
                if(t<0||t>=50)return;
                int a=(angle+speed*t)&255;
                int px=Sin(a,t*rx),py=mode switch
                {
                    1=>Cos(a,Sin(a,t*ry)),
                    2=>Cos(a,Sin(a&63,t*ry)),
                    _=>Cos(a,t*ry)
                };
                int frame=Ball switch
                {
                    CaptureBall.Master=>3,
                    CaptureBall.Net or CaptureBall.Dive=>4,
                    CaptureBall.Nest=>5,
                    CaptureBall.Ultra or CaptureBall.Repeat or CaptureBall.Timer=>7,
                    CaptureBall.Luxury or CaptureBall.Premier=>6+t/4%2,
                    _=>new[]{0,1,2,0,2,1}[t%6]
                };
                output.Add(new(ox+px,oy+py,frame,
                    Ball is CaptureBall.Poke or CaptureBall.Great or CaptureBall.Safari && t%6==3));
            }
            switch(Ball)
            {
                case CaptureBall.Poke:
                    output.AddRange(PokeBallBurst(age, ox, oy, _sine));
                    break;
                case CaptureBall.Great:case CaptureBall.Luxury:
                    for(int wave=0;wave<2;wave++)for(int i=0;i<8;i++)Particle(wave*9,i*32,8,2,2);
                    break;
                case CaptureBall.Master:
                    for(int i=0;i<8;i++){Particle(0,i*32,8,2,1);Particle(0,i*32,8,1,2);}break;
                case CaptureBall.Ultra:case CaptureBall.Nest:
                    for(int i=0;i<10;i++)Particle(0,i*25,5,1,1);break;
                case CaptureBall.Safari:case CaptureBall.Net:
                    for(int i=0;i<8;i++)Particle(0,i*32,4,1,1);break;
                case CaptureBall.Dive:
                    for(int i=0;i<8;i++)Particle(0,i*32,10,1,2);break;
                case CaptureBall.Timer:
                    for(int i=0;i<8;i++)Particle(0,i*32,10,2,1);break;
                case CaptureBall.Repeat:
                    for(int i=0;i<12;i++)Particle(0,i*21,6,1,1,1);break;
                case CaptureBall.Premier:
                    for(int i=0;i<8;i++)Particle(0,i*32,10,1,1,2);break;
            }
        }
        Burst(_openTick); Burst(_breakOutTick);
        int starAge=tick-_clickTick;
        if(_clickTick>=0&&starAge is >=0 and <24&&starAge%2==1)
        {
            var origin=Frames[_clickTick];
            foreach(var (dx,dy,amplitude) in new[]{(10,2,-3),(15,0,-4),(-10,2,-4)})
            {
                int vx=Math.Abs(dx)*256/24; vx=dx<0?vx|1:vx&~1;
                int vy=(dy*256/24)&~1, t=starAge+1;
                int x=origin.X+(dx<0?-(vx*t>>8):vx*t>>8);
                int y=origin.Y+(vy*t>>8)+Sin((0x8000/24*t)>>8,amplitude);
                output.Add(new(x,y,3));
            }
        }
        return output.ToImmutable();
    }

    /// <summary>Shared source Pokéball particle burst for battle, Oak and trade presentation.</summary>
    public static ImmutableArray<CaptureBallParticle> PokeBallBurst(int age, int x, int y, ImmutableArray<int> sine)
    {
        var particles = ImmutableArray.CreateBuilder<CaptureBallParticle>();
        for (int i = 0; i < 16; i++)
        {
            int t = age - i - 1;
            if (t is >= 0 and < 25)
            {
                int angle = i % 8 * 32;
                particles.Add(new(x + (sine[angle] * t * 2 >> 8), y + (sine[angle + 64] * t * 2 >> 8),
                    new[] { 0, 1, 2, 0, 2, 1 }[(t + 1) % 6], (t + 1) % 6 == 3));
            }
        }
        return particles.ToImmutable();
    }

}
