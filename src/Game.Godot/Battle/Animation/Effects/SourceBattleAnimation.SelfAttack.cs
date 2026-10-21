namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string SwordTemplate="gSwordsDanceBladeSpriteTemplate",SharpenTemplate="gSharpenSphereSpriteTemplate";
    private bool ValidateSelfAttackOperand(SourceAnimationCommand command)
    {
        if(ValidateQuestionMarkOperand(command))return true;
        var a=command.Args;if(command.Op!="createsprite"||a.FirstOrDefault() is not (SwordTemplate or SharpenTemplate))return false;
        bool sword=a[0]==SwordTemplate;
        if(a.Length!=(sword?5:3)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||!_script.SpriteRecipes.ContainsKey(a[0]))Fail(command,"Self Attack sprite operands or source recipe differ.");
        var recipe=_script.SpriteRecipes[a[0]];string tag=sword?"ANIM_TAG_SWORD":"ANIM_TAG_SPHERE_TO_CUBE";
        if(recipe.Callback!=(sword?"AnimSwordsDanceBlade":"AnimSharpenSphere")||recipe.Asset!=(sword?"sword":"sphere-to-cube")||recipe.Width!=32||recipe.Height!=(sword?64:32)||recipe.Blend!=sword||recipe.AffineDoubleSize||recipe.Animations.Length!=1||recipe.FrameLoops.Any(p=>p.Length!=0)||!recipe.LoopStarts.SequenceEqual([sword?0:-1]))Fail(command,"Self Attack source sprite shape differs.");
        if(!_script.SpriteTags.TryGetValue(tag,out int palette)||!_script.SpritePalettes.ContainsKey(palette))Fail(command,"Self Attack indexed source palette is missing.");
        if(sword)
        {
            SourceSpriteAffineCommand[] affine=[new("frame",16,256,0,0,0),new("frame",20,0,0,12,0),new("frame",0,0,0,32,0),new("end",0,0,0,0,0)];
            if(recipe.Animations[0].Length!=1||recipe.Animations[0][0]!=new SourceSpriteAnimFrame(0,1,false,false)||recipe.AffineAnimations.Length!=1||!recipe.AffineAnimations[0].SequenceEqual(affine))Fail(command,"Sword frame or affine program differs.");
        }
        else
        {
            int[] tiles=[0,0,16,0,16,32,16,32,48,32,48,64,48,64],durations=[18,6,18,6,6,18,6,6,18,6,6,18,6,54];
            if(recipe.AffineAnimations.Length!=0||recipe.Animations[0].Length!=tiles.Length||!_script.SoundEffects.ContainsKey("SE_M_SWAGGER2"))Fail(command,"Sharpen frame program or callback sound dependency differs.");
            for(int i=0;i<tiles.Length;i++)if(recipe.Animations[0][i]!=new SourceSpriteAnimFrame(tiles[i],durations[i],false,false))Fail(command,"Sharpen source frame differs.");
        }
        return true;
    }
    private bool TryCreateSelfAttackSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0]==QuestionMarkTemplate)
        {
            if(!_loaded.Contains("ANIM_TAG_AMNESIA"))Fail(command,"Amnesia graphics have not been loaded.");
            _sprites.Add(new QuestionMark(slot,priority,_script.SpriteRecipes[QuestionMarkTemplate],this));return true;
        }
        if(command.Args[0] is not (SwordTemplate or SharpenTemplate))return false;
        bool sword=command.Args[0]==SwordTemplate;
        if(!_loaded.Contains(sword?"ANIM_TAG_SWORD":"ANIM_TAG_SPHERE_TO_CUBE"))Fail(command,"Self Attack graphics have not been loaded.");
        _sprites.Add(sword?new SwordBlade(slot,priority,_script.SpriteRecipes[SwordTemplate],command.Args.Skip(3).Select(Number).ToArray(),this):
            new SharpenSphere(slot,priority,_script.SpriteRecipes[SharpenTemplate],this));return true;
    }
    private sealed class SwordBlade:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram _affine;
        private readonly PixelPoint _position;private PixelPoint _offset;private int _phase;private PhysicalLinear? _linear;
        public SwordBlade(int slot,int priority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);_affine=new(recipe.AffineAnimations[0]);var p=owner._battlers[owner._cue.Actor.Value].Base(owner._doubles);
            _position=new(unchecked((short)(p.X+owner.InitialXDirection()*args[0])),unchecked((short)(p.Y+args[1])));
        }
        public override SourceVisualSpriteFrame Frame
        {get{var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,true,false,true,256,_recipe.Asset,Subpriority){Width=32,Height=64,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_phase)
            {
                case 0:if(_affine.Ended)_phase=1;break; // Stored callback assignment, no immediate call.
                case 1:_phase=2;break; // Blade step installs StartAnimLinearTranslation.
                case 2:_linear=new(0,unchecked((short)(_position.Y-32))-_position.Y,6);_phase=3;goto case 3;
                case 3:if(_linear!.Step(out var offset))_phase=4;else _offset=offset;break;
                case 4:Finished=true;break;
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner){_frames.Step();_affine.Step();}
    }
    private sealed class SharpenSphere:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly PixelPoint _position;
        private readonly short[] _data=[0,2,0,0,0,0];private bool _hidden;
        public SharpenSphere(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);var p=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);
            _position=new(p.X,unchecked((short)(p.Y-12)));_data[5]=(short)owner.AdjustPanning(-64);
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,default,_frames.Frame.Tile,!_hidden,false,false,256,_recipe.Asset,Subpriority){Width=32,Height=32};
        public override void Step(SourceBattleAnimation owner)
        {
            _data[0]=unchecked((short)(_data[0]+1));
            if(_data[0]>=_data[1])
            {
                _hidden=!_hidden;
                if(!_hidden){_data[4]=unchecked((short)(_data[4]+1));if((_data[4]&1)==0)owner._audio.Play(new("SE_M_SWAGGER2",unchecked((sbyte)_data[5]),owner._frame));}
                _data[0]=0;_data[2]=unchecked((short)(_data[2]+1));
                if(_data[2]>1){_data[2]=0;_data[1]=unchecked((short)(_data[1]+1));}
            }
            if(_frames.Ended&&_data[1]>16&&_hidden)Finished=true;
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)=>_frames.Step();
    }
}
