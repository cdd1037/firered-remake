namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string StockpileOrbTemplate = "gStockpileAbsorptionOrbSpriteTemplate";
    private const string SwallowOrbTemplate = "gSwallowBlueOrbSpriteTemplate";
    private bool ValidateOrbDefenseCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="loadspritegfx"&&name=="ANIM_TAG_GRAY_ORB")
        {if(a.Length!=1)Fail(c,"Stockpile graphics operands differ.");return true;}
        if(c.Op!="createsprite"||name is not (StockpileOrbTemplate or SwallowOrbTemplate))return false;
        bool stockpile=name==StockpileOrbTemplate;
        if(a.Length!=(stockpile?6:5)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||
            a.Skip(3).Any(v=>Number(v) is < -128 or >127)||stockpile&&Number(a[5])<1)
            Fail(c,"Absorption/swallow sprite operands differ.");
        if(!_script.SpriteRecipes.TryGetValue(name,out var r))Fail(c,"Absorption/swallow source recipe is missing.");r=_script.SpriteRecipes[name];
        if(r.Asset!=(stockpile?"gray-orb":"blue-orb")||r.Callback!=(stockpile?"AnimPowerAbsorptionOrb":"AnimSwallowBlueOrb")||
            r.Width!=8||r.Height!=8||r.Blend||r.AffineDoubleSize!=stockpile||r.Animations.Length!=1||
            !r.Animations[0].SequenceEqual([new SourceSpriteAnimFrame(0,1,false,false)])||!r.LoopStarts.SequenceEqual([0])||r.FrameLoops.Any(x=>x.Length!=0)||
            r.AffineAnimations.Length!=(stockpile?1:0))Fail(c,"Absorption/swallow source template differs.");
        if(stockpile&&!r.AffineAnimations[0].SequenceEqual([new("frame",320,320,0,0,0),new("frame",-14,-14,0,1,0),new("jump",0,0,0,0,1)]))
            Fail(c,"Stockpile shrinking affine program differs.");
        return true;
    }
    private bool TryCreateOrbDefenseSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (StockpileOrbTemplate or SwallowOrbTemplate))return false;
        var r=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Absorption/swallow graphics have not been loaded.");
        _sprites.Add(new OrbDefenseSprite(slot,priority,r,this));return true;
    }
    private sealed class OrbDefenseSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private readonly SourceSpriteAffineProgram? _affine;private readonly PhysicalLinear? _linear;
        private readonly PixelPoint _position;private PixelPoint _offset;private short _velocity=0x900;private readonly int _floor;private bool _destroyNext;
        public OrbDefenseSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe,0);var actor=o._battlers[o._cue.Actor.Value];var a=o._animationArgs;
            bool stockpile=recipe.Template==StockpileOrbTemplate;var center=actor.Center(o._doubles,true);var start=stockpile?center:actor.Base(o._doubles);
            _position=new(unchecked((short)(start.X+o.InitialXDirection()*a[0])),unchecked((short)(start.Y+a[1])));_floor=center.Y;
            if(stockpile){_linear=new(center.X-_position.X,center.Y-_position.Y,a[2]);_affine=new(recipe.AffineAnimations[0]);}
        }
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=8,Height=8,VFlip=f.VFlip,Affine=_affine is not null,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine?.Step();}
        public override void Step(SourceBattleAnimation o)
        {
            if(_linear is not null)
            {
                if(_destroyNext){Finished=true;return;}
                // StartAnimLinearTranslation steps on the next callback. The
                // exhausted translation installs its matrix destructor one tick later.
                if(_linear.Step(out var offset))_destroyNext=true;else _offset=offset;
            }
            else
            {
                _offset=new(0,unchecked((short)(_offset.Y-(_velocity>>8))));_velocity=unchecked((short)(_velocity-96));
                if(_position.Y+_offset.Y>_floor)Finished=true;
            }
        }
    }
}
