namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateFocusPunchCommand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.FirstOrDefault()!="gFocusPunchFistSpriteTemplate")return false;
        if(a.Length!=3||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe))
        {Fail(command,"Missing Focus Punch fist recipe or operands.");return true;}
        SourceSpriteAffineCommand[] expected=[new("frame",512,512,0,0,0),new("frame",-32,-32,0,8,0),new("end",0,0,0,0,0)];
        if(recipe.Callback!="AnimFocusPunchFist"||recipe.Asset!="hands-and-feet"||recipe.Width!=32||recipe.Height!=32||recipe.Blend||!recipe.AffineDoubleSize||
            recipe.AffineAnimations.Length!=1||!recipe.AffineAnimations[0].SequenceEqual(expected)||recipe.Animations.Length!=5||
            recipe.LoopStarts.Length!=5||recipe.LoopStarts.Any(x=>x!=-1)||recipe.FrameLoops.Any(x=>x.Length!=0)||recipe.Subsprites.Length!=0||_script.SineTable.Length!=320)
            Fail(command,"Focus Punch source template differs.");
        for(int i=0;i<5;i++)
            if(recipe.Animations[i].Length!=1||recipe.Animations[i][0]!=new SourceSpriteAnimFrame(i<4?i*16:48,1,i==4,false))
                Fail(command,"Focus Punch hand/foot frame table differs.");
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(command,"Focus Punch sprite priority differs.");
        return true;
    }
    private bool TryCreateFocusPunchSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0]!="gFocusPunchFistSpriteTemplate")return false;
        if(!_loaded.Contains("ANIM_TAG_HANDS_AND_FEET"))Fail(command,"Focus Punch fist graphics have not been loaded.");
        _sprites.Add(new FocusPunchFist(slot,priority,_script.SpriteRecipes[command.Args[0]],this));return true;
    }
    private sealed class FocusPunchFist:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;
        private readonly SourceSpriteFrameProgram _frames;
        private readonly SourceSpriteAffineProgram _affine;
        private readonly PixelPoint _position;
        private int _angle,_count,_x;
        public FocusPunchFist(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe,0);_affine=new(recipe.AffineAnimations[0]);
            // Cmd_createsprite supplies the target picture center. Its second
            // operand selects priority; this callback does not reposition it.
            _position=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
        }
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var frame=_frames.Frame;var matrix=_affine.Matrix;
                return new(Slot,_recipe.Template,_position,new(_x,0),frame.Tile,true,frame.HFlip,false,256,_recipe.Asset,Subpriority)
                    {Width=32,Height=32,Affine=true,DoubleSize=true,VFlip=frame.VFlip,MatrixA=matrix.A,MatrixB=matrix.B,MatrixC=matrix.C,MatrixD=matrix.D};
            }
        }
        public override void Step(SourceBattleAnimation owner)
        {
            // AnimateSprites invokes the callback before the shared frame and
            // affine players, so shaking begins one tick after affine END.
            if(_affine.Ended)
            {
                _angle=(_angle+40)&255;_x=(owner._script.SineTable[_angle]*2)>>8;
                if(++_count>40){Finished=true;return;}
            }
            _frames.Step();_affine.Step();
        }
    }
}
