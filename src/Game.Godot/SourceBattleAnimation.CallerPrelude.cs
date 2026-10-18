namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string LetterTemplate="gLetterZSpriteTemplate";
    private bool ValidateCallerPreludeOperand(SourceAnimationCommand c)
    {
        var a=c.Args;if(c.Op!="createsprite"||a.FirstOrDefault()!=LetterTemplate)return false;
        if(a.Length!=7||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(x=>Number(x) is <short.MinValue or >short.MaxValue))Fail(c,"Letter Z source operands differ.");
        // The accepted X velocity must reach a horizontal edge before wrapping
        // its signed accumulator. Y remains the source signed16 accumulator.
        if(Number(a[3]) is <-240 or >240||Number(a[5]) is <1 or >255)Fail(c,"Letter Z translation is outside the bounded horizontal lifetime.");
        if(!_script.SpriteRecipes.TryGetValue(LetterTemplate,out var r)){Fail(c,"Letter Z recipe is missing.");return true;}
        SourceSpriteAffineCommand[] affine=[new("frame",-7,-7,253,16,0),new("frame",7,7,3,16,0),new("jump",0,0,0,0,0)];
        if(r.Callback!="AnimLetterZ"||r.Asset!="letter-z"||r.Width!=32||r.Height!=32||r.Blend||r.AffineDoubleSize||r.Subsprites.Length!=0||r.FrameLoops.Any(p=>p.Length!=0)||r.Animations.Length!=1||!r.Animations[0].SequenceEqual(new[]{new SourceSpriteAnimFrame(0,3,false,false)})||!r.LoopStarts.SequenceEqual(new[]{-1})||r.AffineAnimations.Length!=1||!r.AffineAnimations[0].SequenceEqual(affine)||_script.SineTable.Length!=320)Fail(c,"Letter Z source frame or affine program differs.");
        if(_validatingInvokedProgram)ValidateCallerPreludeCapacity(c);
        return true;
    }
    private void ValidateCallerPreludeCapacity(SourceAnimationCommand c)
    {
        var commands=OverlayInvocationCommands();
        // Both source roots have direct counted sprites and one non-emitting
        // SwayMon task at most. Sum all births without credit for destruction.
        if(commands.Any(x=>x.Op is "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"||x.Op=="createsprite"&&x.Args[0] is not (LetterTemplate or MetronomeFingerTemplate or ThoughtTemplate)||x.Op=="createvisualtask"&&x.Args[0]!="AnimTask_SwayMon"))Fail(c,"Caller prelude allocations require a bounded non-emitting closure.");
        int births=commands.Count(x=>x.Op=="createsprite"),affine=commands.Count(x=>x.Op=="createsprite"&&x.Args[0]!=ThoughtTemplate);
        if(22+births+_spriteScene.Sprites.Sum(x=>Math.Max(1,x.Frame.Subsprites.Length))>64||4+affine+_spriteScene.Sprites.Count(x=>x.Frame.Affine||x.Frame.Scale!=256)>32)Fail(c,"Caller prelude exceeds source OAM or affine capacity.");
    }
    private bool TryCreateCallerPreludeSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=LetterTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_LETTER_Z"))Fail(c,"Letter Z graphics have not been loaded.");
        var sprite=new CallerLetterSprite(slot,priority,_script.SpriteRecipes[LetterTemplate],this);if(!sprite.Finished)_sprites.Add(sprite);return true;
    }
    private sealed class CallerLetterSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram _affine;private readonly PixelPoint _position;private readonly short _dx,_dy;private PixelPoint _offset;private short _age,_x,_y;
        private static short S(int n)=>unchecked((short)n);
        public CallerLetterSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_affine=new(recipe.AffineAnimations[0]);var a=o._animationArgs;var p=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);_position=new(S(p.X+o.InitialXDirection()*a[0]),p.Y);int direction=o._cue.Actor.IsPlayer?1:-1;_dx=S(direction*a[2]);_dy=S(direction*a[3]);
            // CreateSpriteAndAnimate invokes AnimLetterZ immediately; unlike
            // AnimSleepLetterZ this first call already moves the sprite.
            Move(o);
        }
        private void Move(SourceBattleAnimation o)
        {_age=S(_age+1);_x=S(_x+_dx);_y=S(_y+_dy);_offset=new(_x/2,S((o._script.SineTable[(_age*20)&255]*5>>8)+_y/2));if(unchecked((ushort)(_position.X+_offset.X))>240)Finished=true;}
        public override void Step(SourceBattleAnimation o)=>Move(o);
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine.Step();}
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,false,false,256,_recipe.Asset,Subpriority){Width=32,Height=32,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
}
