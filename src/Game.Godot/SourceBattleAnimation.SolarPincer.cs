namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string SolarBig="gSolarBeamBigOrbSpriteTemplate",SolarSmall="gSolarBeamSmallOrbSpriteTemplate",GuillotinePincer="gGuillotineSpriteTemplate";
    private bool ValidateSolarPincerCommand(SourceAnimationCommand c)
    {
        var a=c.Args;bool task=c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_CreateSmallSolarBeamOrbs";
        if(!task&&(c.Op!="createsprite"||a.FirstOrDefault() is not(SolarBig or GuillotinePincer)))return false;
        if(task)
        {
            if(a.Length!=2||Number(a[1]) is <0 or >255||!_script.SpriteDependencies.Contains(SolarSmall)||_script.SineTable.Length!=320)Fail(c,"Solar secondary-orb task operands or dependency differ.");
            ValidateSolarPincerRecipe(c,SolarSmall);return true;
        }
        bool pincer=a[0]==GuillotinePincer;
        if(a.Length!=(pincer?4:7)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(c,"Solar/pincer sprite operand count differs.");
        if(pincer){if(Number(a[3]) is <0 or >1)Fail(c,"Pincer frame selector differs.");}
        else if(a.Skip(3).Take(2).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||Number(a[5]) is <1 or >32767||Number(a[6]) is <0 or >6)Fail(c,"Solar source position, duration or frame index differs.");
        ValidateSolarPincerRecipe(c,a[0]);return true;
    }
    private void ValidateSolarPincerRecipe(SourceAnimationCommand c,string name)
    {
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Solar/pincer recipe missing.");return;}
        bool pincer=name==GuillotinePincer,small=name==SolarSmall;int n=pincer?2:small?1:7;
        if(r.Callback!=(pincer?"AnimGuillotinePincer":small?"AnimSolarBeamSmallOrb":"AnimSolarBeamBigOrb")||r.Asset!=(pincer?"cut":"orbs")||r.Width!=(pincer?32:8)||r.Height!=r.Width||r.Blend!=pincer||r.AffineDoubleSize||r.AffineAnimations.Length!=0||r.Animations.Length!=n||r.FrameLoops.Any(p=>p.Length!=0)||!r.LoopStarts.SequenceEqual(Enumerable.Repeat(-1,n)))Fail(c,"Solar/pincer source template differs.");
        for(int i=0;i<n;i++)
        {
            SourceSpriteAnimFrame[] expected=pincer?[new(0,2,i!=0,i!=0),new(16,2,i!=0,i!=0),new(32,1,i!=0,i!=0)]:[new(small?7:i,1,false,false)];
            if(!r.Animations[i].SequenceEqual(expected))Fail(c,"Solar/pincer frame sequence differs.");
        }
    }
    private bool TryCreateSolarPincerTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_CreateSmallSolarBeamOrbs")return false;
        if(!_loaded.Contains("ANIM_TAG_ORBS"))Fail(c,"Solar orb graphics not loaded.");
        var task=new SolarSecondaryTask();task.Step(this);if(!task.Finished)_tasks.Add(task);return true;
    }
    private bool TryCreateSolarPincerSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not(SolarBig or GuillotinePincer))return false;
        bool pincer=c.Args[0]==GuillotinePincer;
        if(!_loaded.Contains(pincer?"ANIM_TAG_CUT":"ANIM_TAG_ORBS"))Fail(c,"Solar/pincer graphics not loaded.");
        var r=_script.SpriteRecipes[c.Args[0]];
        _sprites.Add(pincer?new GuillotineSprite(slot,priority,r,this):new SolarOrbSprite(slot,priority,r,this,false));return true;
    }
    private sealed class SolarSecondaryTask:VisualTask
    {
        private short _delay,_created;
        public override void Step(SourceBattleAnimation o)
        {
            if(--_delay==-1)
            {
                _created++;_delay=6;o._animationArgs[0]=15;o._animationArgs[1]=0;o._animationArgs[2]=80;o._animationArgs[3]=0;
                int priority=unchecked((byte)(o._battlers[o._cue.Target.Value].Subpriority+1));
                o._sprites.CreateAndAnimate(slot=>new SolarOrbSprite(slot,priority,o._script.SpriteRecipes[SolarSmall],o,true),o.StepSourceSprite,s=>s.AdvanceAnimation(o),o.IsReservedSpriteSlot);
            }
            if(_created==15)Finished=true;
        }
    }
    private sealed class SolarOrbSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly bool _raw;private SourceSpriteFrameProgram? _frames;
        private PhysicalLinear? _linear;private PixelPoint _position,_offset;private int _angle,_animation,_priority;private bool _initialized,_destroyNext;
        public SolarOrbSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o,bool raw):base(slot,priority)
        {_recipe=recipe;_raw=raw;_priority=priority;if(!raw){Initialize(o);_frames=new(recipe,_animation);}}
        private void Initialize(SourceBattleAnimation o)
        {
            var a=o._animationArgs;var start=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);var end=o._battlers[o._cue.Target.Value].Center(o._doubles,true);
            _position=new(unchecked((short)(start.X+o.InitialXDirection()*a[0])),unchecked((short)(start.Y+a[1])));
            _linear=new(end.X-_position.X,end.Y-_position.Y,a[2]);_angle=a[3];_animation=_raw?0:a[3];_initialized=true;
        }
        public override bool Counted=>!_raw;
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames?.Frame??_recipe.Animations[0][0];return new(Slot,_recipe.Template,_position,_offset,f.Tile,!Finished,f.HFlip,false,256,_recipe.Asset,_priority){Width=8,Height=8,VFlip=f.VFlip};}}
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_frames is null)_frames=new(_recipe,_animation);else _frames.Step();}
        public override void Step(SourceBattleAnimation o)
        {
            if(!_initialized)Initialize(o);
            if(_destroyNext){Finished=true;return;}
            if(_linear!.Step(out var offset)){if(_raw)Finished=true;else _destroyNext=true;return;}
            if(_raw)
            {
                _priority=unchecked((byte)(o._battlers[o._cue.Target.Value].Subpriority+(_angle>127?1:6)));
                offset=new(unchecked((short)(offset.X+(o._script.SineTable[_angle]*5>>8))),unchecked((short)(offset.Y+(o._script.SineTable[_angle+64]*14>>8))));_angle=(_angle+15)&255;
            }
            _offset=offset;
        }
    }
    private sealed class GuillotineSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private SourceSpriteFrameProgram _frames;private PhysicalLinear _linear;
        private readonly int _animation,_dx,_dy;private PixelPoint _position,_offset;private int _phase,_age,_toggle;private bool _paused;
        public GuillotineSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_animation=o._animationArgs[0];_frames=new(recipe,_animation);
            var center=o._battlers[o._cue.Target.Value].Center(o._doubles,true);int sign=_animation==0?1:-1;
            _position=new(unchecked((short)(center.X+32*sign)),unchecked((short)(center.Y-32*sign)));_dx=-16*sign;_dy=16*sign;_linear=new(_dx,_dy,6);
        }
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;return new(Slot,_recipe.Template,_position,_offset,f.Tile,!Finished,f.HFlip,true,256,_recipe.Asset,Subpriority){Width=32,Height=32,VFlip=f.VFlip};}}
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_skipBegin){_skipBegin=false;return;}if(!_paused)_frames.Step();}
        public override void Step(SourceBattleAnimation o)
        {
            if(_phase==0)
            {
                if(!_linear.Step(out var offset)){_offset=offset;return;}
                if(!_frames.Ended)return;
                _frames.SeekFrame(0);_paused=true;_position=new(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)));_offset=new(2,-2);_linear=new(-_dx,-_dy,6);_phase=1;
            }
            else if(_phase==1)
            {
                if(_toggle!=0)_offset=new(-_offset.X,-_offset.Y);_toggle^=1;
                if(++_age==51){_offset=default;_paused=false;_frames=new(_recipe,_animation^1);_phase=2;_skipBegin=true;}
            }
            else if(_linear.Step(out var offset))Finished=true;else _offset=offset;
        }
        private bool _skipBegin;
    }
}
