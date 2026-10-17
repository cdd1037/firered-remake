namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateProjectileAdapterOperand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.FirstOrDefault() is not ("gSonicBoomSpriteTemplate" or "gSmallDriftingBubblesSpriteTemplate" or "gBonemerangSpriteTemplate"))return false;
        bool sonic=a[0]=="gSonicBoomSpriteTemplate",bubbles=a[0]=="gSmallDriftingBubblesSpriteTemplate";
        if(a.Length!=(sonic?8:bubbles?5:3)||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe))Fail(command,"Projectile recipe or operand count differs.");
        recipe=_script.SpriteRecipes[a[0]];
        if(recipe.Callback!=(sonic?"AnimSonicBoomProjectile":bubbles?"AnimSmallDriftingBubbles":"AnimBonemerangProjectile")||
            recipe.Asset!=(sonic?"air-wave":bubbles?"small-bubbles":"bone")||recipe.Width!=(bubbles?8:32)||recipe.Height!=(sonic?16:bubbles?8:32)||
            recipe.Blend!=sonic||recipe.AffineDoubleSize!=sonic||recipe.Animations.Length!=1||recipe.Animations[0].Length!=1||
            recipe.Animations[0][0]!=new SourceSpriteAnimFrame(0,1,false,false)||!recipe.LoopStarts.SequenceEqual([0])||recipe.FrameLoops.Any(p=>p.Length!=0)||
            recipe.AffineAnimations.Length!=(sonic||bubbles?0:1)||_script.SineTable.Length!=320)Fail(command,"Projectile source template differs.");
        if(!sonic&&!bubbles&&!recipe.AffineAnimations[0].SequenceEqual([new SourceSpriteAffineCommand("frame",0,0,15,1,0),new("jump",0,0,0,0,0)]))Fail(command,"Bonemerang affine source program differs.");
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(command,"Projectile source-width operands differ.");
        if(sonic&&(Number(a[7]) is <1 or >255||a.Skip(3).Take(4).Any(v=>Number(v) is < -128 or >127)))Fail(command,"Sonic Boom translation operands differ.");
        if(bubbles)RequireVisualSeed(command);
        return true;
    }
    private bool TryCreateProjectileAdapterSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0] is not ("gSonicBoomSpriteTemplate" or "gSmallDriftingBubblesSpriteTemplate" or "gBonemerangSpriteTemplate"))return false;
        var recipe=_script.SpriteRecipes[command.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(command,"Projectile graphics have not been loaded.");
        _sprites.Add(new ProjectileAdapterSprite(slot,priority,recipe,this));return true;
    }
    private sealed class ProjectileAdapterSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;
        private readonly SourceSpriteFrameProgram _frames;
        private readonly SourceSpriteAffineProgram? _affine;
        private SourceAffineMatrix _matrix=SourceAffineMatrix.FromScaleRotation(256,256,0);
        private PixelPoint _position,_offset;
        private readonly PhysicalLinear? _linear;
        private HorizontalArc? _arc;
        private short _vx,_vy,_x,_y,_ticks;
        private int _phase;
        public ProjectileAdapterSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var a=owner._animationArgs;
            var start=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);
            var target=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            switch(recipe.Callback)
            {
                case "AnimSonicBoomProjectile":
                    if(!owner._cue.Actor.IsPlayer){a[2]=unchecked((short)-a[2]);a[1]=unchecked((short)-a[1]);a[3]=unchecked((short)-a[3]);}
                    _position=new(unchecked((short)(start.X+owner.InitialXDirection()*a[0])),unchecked((short)(start.Y+a[1])));
                    int x=unchecked((short)(target.X+a[2])),y=unchecked((short)(target.Y+a[3]));
                    int rotation=unchecked((ushort)(SourceBiosAngles.ArcTan2Neg(unchecked((short)(x-_position.X)),unchecked((short)(y-_position.Y)))+0xf000));
                    _matrix=SourceAffineMatrix.FromScaleRotation(256,256,rotation);
                    _linear=new(x-_position.X,y-_position.Y,a[4]);break;
                case "AnimSmallDriftingBubbles":
                    _position=new(unchecked((short)(target.X+owner.InitialXDirection()*a[0])),unchecked((short)(target.Y+a[1])));
                    _vx=(short)((owner.NextVisualRandom()&255)|256);int vertical=owner.NextVisualRandom()&511;
                    _vy=(short)(vertical>255?256-vertical:vertical);break;
                case "AnimBonemerangProjectile":
                    _position=start;_arc=new(target.X-start.X,target.Y-start.Y,20,-40);_affine=new(recipe.AffineAnimations[0]);break;
            }
        }
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var f=_frames.Frame;var m=_affine?.Matrix??_matrix;
                return new(Slot,_recipe.Template,_position,_offset,f.Tile+(_recipe.Callback=="AnimSmallDriftingBubbles"?8:0),true,f.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority)
                {VFlip=f.VFlip,Width=_recipe.Width,Height=_recipe.Height,Affine=_recipe.Callback!="AnimSmallDriftingBubbles",DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner){_frames.Step();_affine?.Step();}
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_recipe.Callback)
            {
                case "AnimSonicBoomProjectile":
                    if(_phase==1){Finished=true;return;}
                    if(_linear!.Step(out var linear))_phase=1;else _offset=linear;
                    break;
                case "AnimSmallDriftingBubbles":
                    _x=unchecked((short)(_x+_vx));_y=unchecked((short)(_y+_vy));
                    _offset=new((_vx&1)!=0?-(_x>>8):_x>>8,_y>>8);
                    if(++_ticks==21)Finished=true;
                    break;
                case "AnimBonemerangProjectile":
                    if(!_arc!.Step(owner._script.SineTable,out var arc)){_offset=arc;break;}
                    if(_phase==1){Finished=true;return;}
                    // The first arc's final quantized secondary coordinates are
                    // committed, without snapping to the requested destination.
                    _position=new(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)));_offset=default;
                    var target=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);
                    _arc=new(target.X-_position.X,target.Y-_position.Y,20,40);_phase=1;
                    break;
            }
        }
    }
}
