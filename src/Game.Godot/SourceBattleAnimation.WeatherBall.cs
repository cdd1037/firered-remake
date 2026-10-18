namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string WeatherBallUp="gWeatherBallUpSpriteTemplate",WeatherBallNormal="gWeatherBallNormalDownSpriteTemplate",WeatherBallFire="gWeatherBallFireDownSpriteTemplate",WeatherBallWater="gWeatherBallWaterDownSpriteTemplate",WeatherBallRock="gWeatherBallRockDownSpriteTemplate",WeatherBallIce="gWeatherBallIceDownSpriteTemplate",WeatherPredicate="AnimTask_GetWeather";
    private static bool WeatherBallSpriteName(string name)=>name is WeatherBallUp or WeatherBallNormal or WeatherBallFire or WeatherBallWater or WeatherBallRock or WeatherBallIce;
    private bool ValidateWeatherBallCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="createvisualtask"&&name==WeatherPredicate)
        {
            if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(c,"Weather predicate takes only source task priority.");
            if(_validatingInvokedProgram&&(_cue.AnimationWeather is null or >255))Fail(c,"Weather predicate requires committed source controller weather bits.");
            return true;
        }
        if(c.Op!="createsprite"||!WeatherBallSpriteName(name))return false;
        bool up=name==WeatherBallUp;
        if(a.Length!=(up?3:9)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||!up&&Number(a[5]) is <1 or >short.MaxValue)Fail(c,"Weather projectile source operand width differs.");
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Weather projectile source recipe missing.");return true;}
        string asset=name is WeatherBallUp or WeatherBallNormal?"weather-ball":name==WeatherBallFire?"small-ember":name==WeatherBallWater?"small-bubbles":name==WeatherBallRock?"rocks":"hail";
        int size=name is WeatherBallWater or WeatherBallIce?16:32,count=name==WeatherBallRock?4:1;
        if(r.Callback!=(up?"AnimWeatherBallUp":"AnimWeatherBallDown")||r.Asset!=asset||r.Width!=size||r.Height!=size||r.Blend||r.AffineDoubleSize||r.Subsprites.Length!=0||r.Animations.Length!=count||r.LoopStarts.Length!=count||r.FrameLoops.Any(p=>p.Length!=0))Fail(c,"Weather projectile recipe shape differs.");
        for(int i=0;i<count;i++)
        {
            SourceSpriteAnimFrame[] expected=name==WeatherBallFire?Enumerable.Range(0,5).Select(n=>new SourceSpriteAnimFrame(n*16,4,false,false)).ToArray():[new(name==WeatherBallRock?(i+2)*16:name==WeatherBallWater?4:0,name is WeatherBallUp or WeatherBallNormal?3:1,false,false)];
            int loop=name is WeatherBallUp or WeatherBallNormal or WeatherBallFire or WeatherBallIce?0:-1;
            if(!r.Animations[i].SequenceEqual(expected)||r.LoopStarts[i]!=loop)Fail(c,"Weather projectile source frames differ.");
        }
        SourceSpriteAffineCommand[][] affine=name==WeatherBallRock?[[new("frame",0,0,251,5,0),new("jump",0,0,0,0,0)],[new("frame",0,0,5,5,0),new("jump",0,0,0,0,0)]]:name==WeatherBallWater?[[new("frame",336,336,0,0,0),new("frame",0,0,0,15,0),new("end",0,0,0,0,0)]]:name==WeatherBallIce?[[new("frame",336,336,0,0,0),new("end",0,0,0,0,0)]]:[];
        if(r.AffineAnimations.Length!=affine.Length||Enumerable.Range(0,affine.Length).Any(i=>!r.AffineAnimations[i].SequenceEqual(affine[i])))Fail(c,"Weather projectile source affine program differs.");
        return true;
    }
    private bool TryRunWeatherPredicate(SourceAnimationCommand c)
    {
        if(c.Args[0]!=WeatherPredicate)return false;
        int weather=_cue.AnimationWeather!.Value;
        // AnimTask_GetWeather has sun-first precedence, unlike battle damage/type.
        _animationArgs[7]=(short)((weather&0x60)!=0?1:(weather&0x07)!=0?2:(weather&0x18)!=0?3:(weather&0x80)!=0?4:0);return true;
    }
    private bool TryCreateWeatherBallSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(!WeatherBallSpriteName(c.Args[0]))return false;
        var recipe=_script.SpriteRecipes[c.Args[0]];string tag="ANIM_TAG_"+recipe.Asset.Replace('-','_').ToUpperInvariant();
        if(!_loaded.Contains(tag))Fail(c,"Weather projectile source graphics not loaded.");
        _sprites.Add(new WeatherBallProjectile(slot,priority,recipe,this));return true;
    }
    private sealed class WeatherBallProjectile:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;private readonly short[] _d=new short[4];private readonly PhysicalLinear? _linear;private readonly PixelPoint _position;private PixelPoint _offset;private bool _destroyNext;
        private static short S(int value)=>unchecked((short)value);
        public WeatherBallProjectile(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);if(recipe.AffineAnimations.Length!=0)_affine=new(recipe.AffineAnimations[0]);
            if(recipe.Template==WeatherBallUp)
            {_position=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);_d[0]=(short)(o._cue.Actor.IsPlayer?5:-10);_d[1]=-40;return;}
            // Cmd_createsprite always supplies target picture coordinates, even
            // when its priority owner selector names the attacker.
            var p=o._battlers[o._cue.Target.Value].Center(o._doubles,true);var a=o._animationArgs;
            var destination=new PixelPoint(S(p.X+a[4]),S(p.Y+a[5]));
            int x=(ushort)a[4]+(o._cue.Target.IsPlayer?30:-30);
            _position=new(S(p.X+x),S(a[5]-(o._cue.Target.IsPlayer?20:80)));
            _linear=new(destination.X-_position.X,destination.Y-_position.Y,a[2]);
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_linear is null)
            {_d[2]=S(_d[2]+_d[0]);_d[3]=S(_d[3]+_d[1]);_offset=new(_d[2]/10,_d[3]/10);if(_d[1]<-20)_d[1]++;if(_position.Y+_offset.Y< -32)Finished=true;return;}
            // StartAnimLinearTranslation performs its first translate on the
            // callback after creation. Completion installs DestroyAnimSprite;
            // that callback runs one tick later, preserving the terminal OAM.
            if(_destroyNext){Finished=true;return;}
            if(_linear.Step(out var offset))_destroyNext=true;else _offset=offset;
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine?.Step();}
        public override SourceVisualSpriteFrame Frame
        {
            get{var f=_frames.Frame;var m=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=_affine is not null,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}
        }
    }
}
