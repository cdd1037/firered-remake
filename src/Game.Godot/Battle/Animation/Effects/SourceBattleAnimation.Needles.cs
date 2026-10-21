namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateNeedleOperand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.FirstOrDefault() is not ("gLinearStingerSpriteTemplate" or "gHandleInvertHitSplatSpriteTemplate"))return false;
        bool linear=a[0]=="gLinearStingerSpriteTemplate";
        if(a.Length!=(linear?8:7)||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe))Fail(command,"Missing needle-family recipe or operands.");
        recipe=_script.SpriteRecipes[a[0]];
        if(recipe.Callback!=(linear?"AnimTranslateStinger":"AnimHitSplatHandleInvert")||recipe.Asset!=(linear?"needle":"impact")||
            recipe.Width!=(linear?16:32)||recipe.Height!=(linear?16:32)||recipe.Blend==linear||recipe.AffineDoubleSize||
            recipe.AffineAnimations.Length!=(linear?0:4)||recipe.Animations.Length!=1||recipe.Animations[0].Length!=1||
            recipe.Animations[0][0]!=new SourceSpriteAnimFrame(0,1,false,false)||recipe.LoopStarts.Length!=1||recipe.LoopStarts[0]!=0)Fail(command,"Needle-family source template differs.");
        for(int i=1;i<a.Length;i++)
            if(!int.TryParse(a[i] switch{"ANIM_ATTACKER"=>"0","ANIM_TARGET"=>"1",_=>a[i]},out int value)||value is < -32768 or >32767)Fail(command,"Needle-family requires signed source operands.");
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(command,"Needle-family participant or subpriority differs.");
        if(Number(a[3]) is < -128 or >127||Number(a[4]) is < -128 or >127||
            (linear?(Number(a[5]) is < -128 or >127||Number(a[6]) is < -128 or >127||Number(a[7]) is <1 or >255):
                Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >3))Fail(command,"Needle-family translation or hit animation differs.");
        return true;
    }
    private bool TryCreateNeedleSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0] is not ("gLinearStingerSpriteTemplate" or "gHandleInvertHitSplatSpriteTemplate"))return false;
        var recipe=_script.SpriteRecipes[command.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(command,"Needle-family graphics have not been loaded.");
        if(recipe.Callback=="AnimTranslateStinger")_sprites.Add(new LinearStinger(slot,priority,recipe,this));
        else
        {
            if(!_cue.Actor.IsPlayer)_animationArgs[1]=unchecked((short)-_animationArgs[1]);
            var battler=_battlers[(_animationArgs[2]==0?_cue.Actor:_cue.Target).Value];var center=battler.Center(_doubles,true);
            _sprites.Add(new HitSplat(slot,priority,new(unchecked((short)(center.X+InitialXDirection()*_animationArgs[0])),
                unchecked((short)(center.Y+_animationArgs[1]))),_animationArgs[3],recipe.Asset,recipe.Template));
        }
        return true;
    }
    private sealed class LinearStinger:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private readonly PixelPoint _position;private PixelPoint _offset;
        private readonly PhysicalLinear _linear;private readonly SourceAffineMatrix _matrix;private bool _destroyNext;
        public LinearStinger(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var a=owner._animationArgs;
            if(!owner._cue.Actor.IsPlayer){a[2]=unchecked((short)-a[2]);a[1]=unchecked((short)-a[1]);a[3]=unchecked((short)-a[3]);}
            if(owner._cue.Actor.IsPlayer==owner._cue.Target.IsPlayer&&owner._cue.Target.Value<2)
            {a[2]=unchecked((short)-a[2]);a[0]=unchecked((short)-a[0]);}
            var start=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);
            _position=new(unchecked((short)(start.X+owner.InitialXDirection()*a[0])),unchecked((short)(start.Y+a[1])));
            var target=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            int x=unchecked((short)(target.X+a[2])),y=unchecked((short)(target.Y+a[3]));
            int angle=unchecked((ushort)(SourceBiosAngles.ArcTan2Neg(unchecked((short)(x-_position.X)),unchecked((short)(y-_position.Y)))+0xc000));
            // TrySetSpriteRotScale pauses the affine interpreter. Reuse the BIOS
            // matrix helper while the shared non-affine frame player continues.
            _matrix=SourceAffineMatrix.FromScaleRotation(256,256,angle);
            _linear=new(x-_position.X,y-_position.Y,a[4]);
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,true,false,false,256,_recipe.Asset,Subpriority)
            {Width=_recipe.Width,Height=_recipe.Height,Affine=true,MatrixA=_matrix.A,MatrixB=_matrix.B,MatrixC=_matrix.C,MatrixD=_matrix.D};
        public override void Step(SourceBattleAnimation owner)
        {
            if(_destroyNext){Finished=true;return;}
            if(_linear.Step(out var offset))_destroyNext=true;else _offset=offset;
            _frames.Step();
        }
    }
}
