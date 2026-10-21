namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateBulletSeedOperand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.FirstOrDefault()!="gBulletSeedSpriteTemplate")return false;
        if(a.Length!=5||!_script.SpriteRecipes.ContainsKey(a[0]))Fail(command,"Missing Bullet Seed recipe or operands.");
        var recipe=_script.SpriteRecipes[a[0]];
        SourceSpriteAffineCommand[] affine=[new("frame",0,0,20,1,0),new("jump",0,0,0,0,0)];
        if(recipe.Callback!="AnimBulletSeed"||recipe.Asset!="seed"||recipe.Width!=16||recipe.Height!=16||recipe.Blend||recipe.AffineDoubleSize||
            recipe.AffineAnimations.Length!=1||!recipe.AffineAnimations[0].SequenceEqual(affine)||recipe.Animations.Length!=1||recipe.Animations[0].Length!=1||
            recipe.Animations[0][0]!=new SourceSpriteAnimFrame(0,1,false,false)||recipe.LoopStarts.Length!=1||recipe.LoopStarts[0]!=0||recipe.FrameLoops.Any(x=>x.Length!=0)||
            _script.SineTable.Length!=320)Fail(command,"Bullet Seed source template or sine table differs.");
        if(!_script.SoundEffects.ContainsKey("SE_M_HORN_ATTACK"))Fail(command,"Bullet Seed requires its generated callback sound dependency.");
        for(int i=1;i<a.Length;i++)
            if(!int.TryParse(a[i] switch{"ANIM_ATTACKER"=>"0","ANIM_TARGET"=>"1",_=>a[i]},out int value)||value is < -32768 or >32767)Fail(command,"Bullet Seed requires signed source operands.");
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||Number(a[3]) is < -128 or >127||Number(a[4]) is < -128 or >127)Fail(command,"Bullet Seed coordinate or priority differs.");
        RequireVisualSeed(command);return true;
    }
    private bool TryCreateBulletSeedSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0]!="gBulletSeedSpriteTemplate")return false;
        if(!_loaded.Contains("ANIM_TAG_SEED"))Fail(command,"Seed graphics have not been loaded.");
        _sprites.Add(new BulletSeed(slot,priority,_script.SpriteRecipes[command.Args[0]],this));return true;
    }
    private sealed class BulletSeed:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram _affine;
        private readonly PhysicalLinear _linear;private PixelPoint _position,_offset;private int _phase,_distance,_angle,_amplitude,_speed;
        public BulletSeed(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_affine=new(recipe.AffineAnimations[0]);var a=owner._animationArgs;
            var start=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);var target=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            _position=new(unchecked((short)(start.X+owner.InitialXDirection()*a[0])),unchecked((short)(start.Y+a[1])));
            _linear=new(target.X-_position.X,target.Y-_position.Y,20);
            // BeginAffineAnim runs even while paused; its first +20 rotation is
            // applied by the shared player constructor and retained until impact.
        }
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var m=_affine.Matrix;
                return new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,true,false,false,256,_recipe.Asset,Subpriority)
                    {Width=16,Height=16,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};
            }
        }
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_phase)
            {
                case 0:
                    // Completion installs Step1; it does not call it this tick.
                    if(_linear.Step(out var offset))_phase=1;else _offset=offset;
                    break;
                case 1:
                    owner._audio.Play(new("SE_M_HORN_ATTACK",owner.AdjustPanning(63),owner._frame));
                    _position=new(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)));_offset=default;
                    // The source clears all eight words before these two draws.
                    _distance=_angle=0;_amplitude=unchecked((short)(0xfff4-(owner.NextVisualRandom()&7)));_speed=owner.NextVisualRandom()%160+160;_phase=2;
                    break;
                default:
                    _distance=unchecked((short)(_distance+_speed));int x=_distance>>8;if((_speed&1)!=0)x=-x;
                    _offset=new(x,unchecked((short)((owner._script.SineTable[_angle]*_amplitude)>>8)));_angle+=8;
                    if(_angle>126){Finished=true;return;}
                    break;
            }
            _frames.Step();if(_phase==2)_affine.Step();
        }
    }
}
