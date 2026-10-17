namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string QuestionMarkTemplate="gQuestionMarkSpriteTemplate";
    private bool ValidateQuestionMarkOperand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.FirstOrDefault()!=QuestionMarkTemplate)return false;
        if(a.Length!=3||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||!_script.SpriteRecipes.ContainsKey(QuestionMarkTemplate))Fail(command,"Question mark source operands or recipe differ.");
        var recipe=_script.SpriteRecipes[QuestionMarkTemplate];
        SourceSpriteAnimFrame[] frames=[new(0,6,false,false),new(16,6,false,false),new(32,6,false,false),new(48,6,false,false),new(64,6,false,false),new(80,6,false,false),new(96,18,false,false)];
        SourceSpriteAffineCommand[] affine=[new("frame",0,0,4,4,0),new("frame",0,0,252,8,0),new("frame",0,0,4,4,0),new("loop",0,0,0,0,2),new("end",0,0,0,0,0)];
        if(recipe.Callback!="AnimQuestionMark"||recipe.Asset!="amnesia"||recipe.Width!=32||recipe.Height!=32||recipe.Blend||recipe.AffineDoubleSize||
            recipe.Animations.Length!=1||!recipe.Animations[0].SequenceEqual(frames)||!recipe.LoopStarts.SequenceEqual([-1])||recipe.FrameLoops.Any(p=>p.Length!=0)||
            recipe.AffineAnimations.Length!=1||!recipe.AffineAnimations[0].SequenceEqual(affine))Fail(command,"Question mark source frame or dynamic affine program differs.");
        if(!_script.SpriteTags.TryGetValue("ANIM_TAG_AMNESIA",out int palette)||!_script.SpritePalettes.ContainsKey(palette))Fail(command,"Question mark indexed source palette is missing.");
        return true;
    }
    private sealed class QuestionMark:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;
        private readonly SourceSpriteFrameProgram _frames;
        private SourceSpriteAffineProgram? _affine;
        private readonly PixelPoint _position;
        private int _phase;
        private short _delay;
        private bool _affineStarted;
        public QuestionMark(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);
            var battler=owner._battlers[owner._cue.Actor.Value];var p=battler.Center(owner._doubles,true);
            int x=battler.SourceWidth/2,y=battler.SourceHeight/-2;if(!owner._cue.Actor.IsPlayer)x=-x;
            _position=new(unchecked((short)(p.X+x)),Math.Max(16,(int)unchecked((short)(p.Y+y))));
        }
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var f=new SourceVisualSpriteFrame(Slot,_recipe.Template,_position,default,_frames.Frame.Tile,true,false,false,256,_recipe.Asset,Subpriority){Width=32,Height=32};
                if(_affine is null)return f;var m=_affine.Matrix;return f with{Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};
            }
        }
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_phase)
            {
                case 0:if(_frames.Ended)_phase=1;break; // RunStoredCallbackWhenAnimEnds installs Step1 without calling it.
                case 1:_affine=new(_recipe.AffineAnimations[0]);_affineStarted=true;_phase=2;break;
                case 2:if(_affine!.Ended){_affine=null;_delay=18;_phase=3;}break;
                case 3:if(--_delay==-1)Finished=true;break;
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)
        {
            _frames.Step();
            // InitSpriteAffineAnim resets the source state. BeginAffineAnim then
            // applies the first relative command once in this same sprite pass.
            if(_affineStarted)_affineStarted=false;else _affine?.Step();
        }
    }
}
