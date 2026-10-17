namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateWebCommand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.Length==0||!_script.SpriteRecipes.TryGetValue(a[0],out var found)||found.Callback is not ("AnimTranslateWebThread" or "AnimStringWrap" or "AnimSpiderWeb"))return false;
        var recipe=_script.SpriteRecipes[a[0]];
        int count=recipe.Callback=="AnimTranslateWebThread"?8:recipe.Callback=="AnimStringWrap"?5:3;
        if(a.Length!=count||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||recipe.Animations.Length!=1)Fail(command,"Web source operands or frame program differ.");
        if(recipe.Callback=="AnimTranslateWebThread")
        {
            if(recipe.Width!=8||recipe.Height!=8||recipe.Blend||recipe.AffineAnimations.Length!=0||Number(a[5])<=0||Number(a[7]) is <0 or >1||_script.SineTable.Length!=320)Fail(command,"Web thread source recipe or speed differs.");
        }
        else if(recipe.Callback=="AnimStringWrap")
        {if(recipe.Width!=64||recipe.Height!=32||recipe.Blend||recipe.AffineAnimations.Length!=0)Fail(command,"String wrap source recipe differs.");}
        else if(recipe.Width!=64||recipe.Height!=64||!recipe.Blend||!recipe.AffineDoubleSize||recipe.AffineAnimations.Length!=1)Fail(command,"Spider web source affine recipe differs.");
        return true;
    }
    private bool TryCreateWebSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(!_script.SpriteRecipes.TryGetValue(command.Args[0],out var recipe)||recipe.Callback is not ("AnimTranslateWebThread" or "AnimStringWrap" or "AnimSpiderWeb"))return false;
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(command,"Web graphics are not loaded.");
        _sprites.Add(new WebSprite(slot,priority,recipe,command.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private sealed class WebSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;
        private readonly SourceSpriteFrameProgram _frames;
        private readonly SourceSpriteAffineProgram? _affine;
        private readonly PhysicalLinear? _linear;
        private readonly PixelPoint _position;
        private readonly int _amplitude;
        private PixelPoint _offset;
        private short _age,_timer,_angle,_alpha=16;
        private bool _hidden,_ending;
        public WebSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);
            if(recipe.Callback=="AnimTranslateWebThread")
            {
                var start=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);
                _position=new(unchecked((short)(start.X+owner.InitialXDirection()*args[0])),unchecked((short)(start.Y+args[1])));
                var end=args[4]==0?owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true):owner.AverageElementalTarget(true);
                int duration=unchecked((short)((Math.Abs(end.X-_position.X)<<8)/args[2]));
                if(duration<=0)throw new InvalidDataException("Web source speed produces a nonpositive translation duration.");
                _linear=new(end.X-_position.X,end.Y-_position.Y,duration);_amplitude=args[3];
            }
            else if(recipe.Callback=="AnimStringWrap")
            {
                var p=owner.AverageElementalTarget(false);
                _position=new(unchecked((short)(p.X+(owner._cue.Actor.IsPlayer?args[0]:-args[0]))),unchecked((short)(p.Y+args[1]+(owner._cue.Target.IsPlayer?8:0))));
            }
            else
            {
                // Cmd_createsprite's participant operand selects subpriority, not position.
                _position=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
                _affine=new(recipe.AffineAnimations[0]);owner._blendEnabled=true;owner._alphaA=16;owner._alphaB=0;
            }
        }
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var f=new SourceVisualSpriteFrame(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!_hidden,_frames.Frame.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=_frames.Frame.VFlip};
                if(_affine is null)return f;
                var m=_affine.Matrix;return f with{Affine=true,DoubleSize=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};
            }
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_recipe.Callback=="AnimTranslateWebThread")
            {
                if(_linear!.Step(out var p)){Finished=true;return;}
                _offset=new(unchecked((short)(p.X+(owner._script.SineTable[_angle]*_amplitude>>8))),p.Y);_angle=(short)((_angle+13)&255);
            }
            else if(_recipe.Callback=="AnimStringWrap")
            {
                if(++_timer==3){_timer=0;_hidden=!_hidden;}if(++_age==51){Finished=true;return;}
            }
            else
            {
                if(_ending){owner._blendEnabled=false;owner._alphaA=owner._alphaB=0;Finished=true;return;}
                if(_age<20)_age++;else if((_timer++&1)!=0){owner._alphaA=--_alpha;owner._alphaB=16-_alpha;if(_alpha==0){_hidden=true;_ending=true;}}
            }
            _frames.Step();_affine?.Step();
        }
    }
}
