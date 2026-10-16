namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateFlyingCommand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_AnimateGustTornadoPalette")
        {
            if(a.Length!=4||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >32767||Number(a[3]) is <1 or >32767||!_script.SpriteTags.ContainsKey("ANIM_TAG_GUST"))Fail(command,"Gust palette task operands or palette tag differ.");
            return true;
        }
        if(command.Op!="createsprite"||a.Length==0||!_script.SpriteRecipes.TryGetValue(a[0],out var recipe)||recipe.Callback is not ("AnimEllipticalGust" or "AnimGustToTarget"))return false;
        bool projectile=recipe.Callback=="AnimGustToTarget";
        if(a.Length!=(projectile?8:5)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||recipe.Width!=32||recipe.Height!=64||recipe.Blend||recipe.AffineDoubleSize||recipe.AffineAnimations.Length!=(projectile?1:0)||_script.SineTable.Length!=320)Fail(command,"Gust sprite recipe or operands differ.");
        if(projectile&&Number(a[7]) is <1 or >32767)Fail(command,"Gust translation duration differs.");
        return true;
    }
    private bool TryCreateFlyingSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(!_script.SpriteRecipes.TryGetValue(command.Args[0],out var recipe)||recipe.Callback is not ("AnimEllipticalGust" or "AnimGustToTarget"))return false;
        if(!_loaded.Contains("ANIM_TAG_GUST"))Fail(command,"Gust graphics are not loaded.");
        _sprites.Add(new GustSprite(slot,priority,recipe,command.Args.Skip(3).Select(Number).ToArray(),this));return true;
    }
    private bool TryCreateFlyingTask(SourceAnimationCommand command)
    {
        if(command.Args[0]!="AnimTask_AnimateGustTornadoPalette")return false;
        if(!_loaded.Contains("ANIM_TAG_GUST"))Fail(command,"Gust palette is not loaded.");
        _tasks.Add(new GustPaletteTask(_script.SpriteTags["ANIM_TAG_GUST"],Number(command.Args[2]),Number(command.Args[3])));return true;
    }
    private sealed class GustPaletteTask(int tag,int delay,int duration):VisualTask
    {
        private int _timer,_remaining=duration;
        public override void Step(SourceBattleAnimation owner)
        {if(_timer++==delay){_timer=0;owner.RotateSpritePalette(tag,1,8);}if(--_remaining==0)Finished=true;}
    }
    private sealed class GustSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;private readonly PhysicalLinear? _linear;private readonly PixelPoint _position;
        private PixelPoint _offset;private int _angle=191,_elapsed,_state;
        public GustSprite(int slot,int priority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe);
            if(recipe.Callback=="AnimEllipticalGust")
            {var p=owner._battlers[owner._cue.Target.Value].Base(owner._doubles);_position=new(p.X+owner.InitialXDirection()*args[0],p.Y+args[1]+20);Ellipse(owner);}
            else
            {
                var p=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);_position=new(p.X+owner.InitialXDirection()*args[0],p.Y+args[1]);
                if(!owner._cue.Actor.IsPlayer)owner._animationArgs[2]=unchecked((short)-args[2]);
                var target=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);_linear=new(target.X+owner._animationArgs[2]-_position.X,target.Y+args[3]-_position.Y,args[4]);
                _affine=new(recipe.AffineAnimations[0]);
            }
        }
        private void Ellipse(SourceBattleAnimation owner)
        {_offset=new((owner._script.SineTable[_angle]*32)>>8,(owner._script.SineTable[_angle+64]*8)>>8);_angle=(_angle+5)&255;if(++_elapsed==71)Finished=true;}
        public override void Step(SourceBattleAnimation owner)
        {
            if(_affine is null)Ellipse(owner);
            else if(_state==0){if(_affine.Ended)_state=1;}
            else if(_linear!.Step(out var offset))Finished=true;else _offset=offset;
            if(Finished)return;_affine?.Step();_frames.Step();
        }
        public override SourceVisualSpriteFrame Frame
        {
            get{var matrix=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);return new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!Finished,_frames.Frame.HFlip,false,256,_recipe.Asset,Subpriority){Width=32,Height=64,VFlip=_frames.Frame.VFlip,Affine=_affine is not null,MatrixA=matrix.A,MatrixB=matrix.B,MatrixC=matrix.C,MatrixD=matrix.D};}
        }
    }
}
