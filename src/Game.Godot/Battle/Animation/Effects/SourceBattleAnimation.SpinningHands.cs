namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateSpinningHandOperand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.FirstOrDefault() is not ("gMegaPunchKickSpriteTemplate" or "gSpinningHandOrFootSpriteTemplate"))return false;
        if(a.Length!=7||!_script.SpriteRecipes.ContainsKey(a[0]))Fail(command,"Missing spinning hand/foot recipe or operands.");
        var recipe=_script.SpriteRecipes[a[0]];int delta=a[0]=="gMegaPunchKickSpriteTemplate"?-4:-8;
        SourceSpriteAffineCommand[] expected=[new("frame",256,256,0,0,0),new("frame",delta,delta,20,1,0),new("jump",0,0,0,0,1)];
        if(recipe.Callback!="AnimSpinningKickOrPunch"||recipe.Asset!="hands-and-feet"||recipe.Width!=32||recipe.Height!=32||recipe.Blend||!recipe.AffineDoubleSize||
            recipe.AffineAnimations.Length!=1||!recipe.AffineAnimations[0].SequenceEqual(expected)||recipe.Animations.Length!=5||
            recipe.LoopStarts.Length!=5||recipe.LoopStarts.Any(x=>x!=-1)||recipe.FrameLoops.Any(x=>x.Length!=0))Fail(command,"Spinning source template differs.");
        for(int i=0;i<5;i++)
            if(recipe.Animations[i].Length!=1||recipe.Animations[i][0]!=new SourceSpriteAnimFrame(i<4?i*16:48,1,i==4,false))Fail(command,"Spinning hand/foot frame table differs.");
        for(int i=1;i<a.Length;i++)
            if(!int.TryParse(a[i] switch{"ANIM_ATTACKER"=>"0","ANIM_TARGET"=>"1",_=>a[i]},out int value)||value is < -32768 or >32767)Fail(command,"Spinning callback requires signed source operands.");
        // WaitAnimForDuration hands off without calling the next function. Its
        // extra affine tick must stay above zero before Finish resets identity.
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||Number(a[3]) is < -128 or >127||Number(a[4]) is < -128 or >127||
            Number(a[5]) is <0 or >1||Number(a[6])<0||Number(a[6])>(delta==-4?62:30))Fail(command,"Spinning hand/foot operands differ.");
        return true;
    }
    private bool TryCreateSpinningHandSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0] is not ("gMegaPunchKickSpriteTemplate" or "gSpinningHandOrFootSpriteTemplate"))return false;
        if(!_loaded.Contains("ANIM_TAG_HANDS_AND_FEET"))Fail(command,"Spinning hand/foot graphics have not been loaded.");
        _sprites.Add(new SpinningHand(slot,priority,_script.SpriteRecipes[command.Args[0]],this));return true;
    }
    private sealed class SpinningHand:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly PixelPoint _position;
        private SourceSpriteAffineProgram _affine;private int _remaining,_state;private bool _paused;
        public SpinningHand(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;var args=owner._animationArgs;var center=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            _position=new(unchecked((short)(center.X+owner.InitialXDirection()*args[0])),unchecked((short)(center.Y+args[1])));
            _frames=new(recipe,args[2]);_affine=new(recipe.AffineAnimations[0]);_remaining=args[3];
        }
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var matrix=_affine.Matrix;var frame=_frames.Frame;
                return new(Slot,_recipe.Template,_position,default,frame.Tile,true,frame.HFlip,false,256,_recipe.Asset,Subpriority)
                    {Width=32,Height=32,Affine=true,DoubleSize=true,VFlip=frame.VFlip,MatrixA=matrix.A,MatrixB=matrix.B,MatrixC=matrix.C,MatrixD=matrix.D};
            }
        }
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_state)
            {
                case 0:if(_remaining>0)_remaining--;else _state=1;break;
                case 1:
                    // StartSpriteAffineAnim followed by affineAnimPaused=TRUE.
                    // BeginAffineAnim still runs on this tick, even while paused.
                    _affine=new(_recipe.AffineAnimations[0]);_paused=true;_remaining=20;_state=2;break;
                case 2:if(_remaining>0)_remaining--;else _state=3;break;
                default:Finished=true;return;
            }
            _frames.Step();if(!_paused)_affine.Step();
        }
    }
}
