namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateArcProjectileOperand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.FirstOrDefault() is not ("gPinMissileSpriteTemplate" or "gIcicleSpearSpriteTemplate"))return false;
        bool needle=a[0]=="gPinMissileSpriteTemplate";
        if(a.Length!=9||!_script.SpriteRecipes.ContainsKey(a[0]))Fail(command,"Missing arc-projectile recipe or operands.");
        var recipe=_script.SpriteRecipes[a[0]];
        if(recipe.Callback!="AnimMissileArc"||recipe.Asset!=(needle?"needle":"icicle-spear")||
            recipe.Width!=(needle?16:32)||recipe.Height!=recipe.Width||recipe.Blend||recipe.AffineDoubleSize||
            recipe.AffineAnimations.Length!=0||recipe.Animations.Length!=1||recipe.Animations[0].Length!=1||
            recipe.Animations[0][0]!=new SourceSpriteAnimFrame(0,1,false,false)||recipe.LoopStarts.Length!=1||recipe.LoopStarts[0]!=0||
            _script.SineTable.Length!=320)Fail(command,"Arc-projectile source template or sine table differs.");
        for(int i=1;i<a.Length;i++)
            if(!int.TryParse(a[i] switch{"ANIM_ATTACKER"=>"0","ANIM_TARGET"=>"1",_=>a[i]},out int value)||value is < -32768 or >32767)Fail(command,"Arc projectile requires signed source operands.");
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||Number(a[7]) is <1 or >255)Fail(command,"Arc-projectile participant, subpriority or duration differs.");
        foreach(int i in new[]{3,4,5,6,8})if(Number(a[i]) is < -128 or >127)Fail(command,"Arc-projectile coordinate or amplitude differs.");
        return true;
    }
    private bool TryCreateArcProjectileSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0] is not ("gPinMissileSpriteTemplate" or "gIcicleSpearSpriteTemplate"))return false;
        var recipe=_script.SpriteRecipes[command.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(command,"Arc-projectile graphics have not been loaded.");
        _sprites.Add(new ArcProjectile(slot,priority,recipe,this));return true;
    }
    private sealed class ArcProjectile:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private readonly PixelPoint _position;private PixelPoint _offset;private bool _visible;
        private readonly PhysicalLinear _linear;private readonly int _amplitude,_angleStep;private int _angle;
        private SourceAffineMatrix _matrix=SourceAffineMatrix.FromScaleRotation(256,256,0);
        public ArcProjectile(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var a=owner._animationArgs;
            var start=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);
            _position=new(unchecked((short)(start.X+owner.InitialXDirection()*a[0])),unchecked((short)(start.Y+a[1])));
            if(!owner._cue.Actor.IsPlayer)a[2]=unchecked((short)-a[2]);
            var target=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            int x=unchecked((short)(target.X+a[2])),y=unchecked((short)(target.Y+a[3]));
            _linear=new(x-_position.X,y-_position.Y,a[4]);_amplitude=a[5];_angleStep=unchecked((short)(0x8000/a[4]));
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,_visible,false,false,256,_recipe.Asset,Subpriority)
            {Width=_recipe.Width,Height=_recipe.Height,Affine=true,MatrixA=_matrix.A,MatrixB=_matrix.B,MatrixC=_matrix.C,MatrixD=_matrix.D};
        private bool Translate(SourceBattleAnimation owner)
        {
            if(_linear.Step(out var offset))return true;
            _angle=unchecked((short)(_angle+_angleStep));
            _offset=new(offset.X,unchecked((short)(offset.Y+((_amplitude*owner._script.SineTable[(byte)(_angle>>8)])>>8))));return false;
        }
        public override void Step(SourceBattleAnimation owner)
        {
            _visible=true;
            if(Translate(owner)){Finished=true;return;}
            // The source snapshots all eight data words, but leaves the second
            // translation's x2/y2 visible. Velocity, amplitude and step are constant;
            // only linear accumulators/remaining and angle need restoring here.
            var state=_linear.Capture();int angle=_angle;var first=_offset;
            if(!Translate(owner))
            {
                int rotation=unchecked((ushort)(SourceBiosAngles.ArcTan2Neg(unchecked((short)(_offset.X-first.X)),unchecked((short)(_offset.Y-first.Y)))+0xc000));
                _matrix=SourceAffineMatrix.FromScaleRotation(256,256,rotation);
                _linear.Restore(state);_angle=angle;
            }
            _frames.Step();
        }
    }
}
