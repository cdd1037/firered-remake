namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string ThoughtTemplate="gThoughtBubbleSpriteTemplate",TauntFingerTemplate="gTauntFingerSpriteTemplate",FollowFingerTemplate="gFollowMeFingerSpriteTemplate",MetronomeFingerTemplate="gMetronomeFingerSpriteTemplate",HeartProjectileTemplate="gRedHeartProjectileSpriteTemplate",HeartRisingTemplate="gRedHeartRisingSpriteTemplate";
    private static readonly SourceTaskAffineCommand[] TormentAffine=[new("frame",-12,8,0,4),new("frame",20,-20,0,4),new("frame",-8,12,0,4),new("end")];
    private bool ValidateSocialOperand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="createvisualtask"&&name=="AnimTask_HeartsBackground")
        {
            if(a.Length!=2||Number(a[1])!=5)Fail(c,"Attract overlay task operands differ.");
            if(_validatingInvokedProgram)
            {
                if(!_script.OverlayDependencies.Contains("attract")||!_script.OverlayPalettes.TryGetValue("attract",out var palette)||palette.Length!=16)Fail(c,"Attract source BG1 palette/material is missing.");
                var closure=OverlayInvocationCommands();if(closure.Any(x=>x.Op=="monbg")||closure.Count(x=>IsOverlayCommand(x)||x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_HeartsBackground")!=1)Fail(c,"Attract requires one bounded BG1 overlay owner.");
            }
            return true;
        }
        bool torment=c.Op=="createvisualtask"&&name=="AnimTask_TormentAttacker";
        if(!torment&&(c.Op!="createsprite"||name is not (ThoughtTemplate or TauntFingerTemplate or FollowFingerTemplate or MetronomeFingerTemplate or HeartProjectileTemplate or HeartRisingTemplate)))return false;
        if(torment)
        {
            if(a.Length!=2||Number(a[1])!=2||!_script.SpriteDependencies.Contains(ThoughtTemplate)||!_script.SoundEffects.ContainsKey("SE_M_METRONOME")||!_script.TaskAffinePrograms.TryGetValue("sAffineAnims_Torment",out var affine)||!affine.SequenceEqual(TormentAffine))Fail(c,"Torment child, sound or task-affine dependency differs.");
            if(_validatingInvokedProgram)
            {
                var closure=OverlayInvocationCommands();if(closure.Any(x=>x.Op=="createsprite"&&x.Args[0]==ThoughtTemplate)||closure.Count(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_TormentAttacker")!=1||_spriteScene.Sprites.Any(x=>x.Frame.Template==ThoughtTemplate&&x is not TormentBubble))Fail(c,"Torment global template scan would cross bounded child ownership.");
            }
            name=ThoughtTemplate;
        }
        else
        {
            int length=name==ThoughtTemplate||name==HeartProjectileTemplate?5:name==HeartRisingTemplate?6:4;
            if(a.Length!=length||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(x=>Number(x) is <short.MinValue or >short.MaxValue))Fail(c,"Social sprite source operands differ.");
            if(name is ThoughtTemplate or TauntFingerTemplate or FollowFingerTemplate or MetronomeFingerTemplate){if(Number(a[3]) is <0 or >1)Fail(c,"Social head participant differs.");if(_validatingInvokedProgram)ValidateReactionGeometry(c,_battlers[Resolve(a[3]).Value]);}
            if(name==ThoughtTemplate&&Number(a[4])<1)Fail(c,"Thought bubble duration must terminate.");
            if(name==HeartRisingTemplate&&(Number(a[4])<1||Number(a[5])<0))Fail(c,"Rising heart speed/delay must terminate.");
        }
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Social sprite recipe missing.");return false;}
        string asset=name switch{ThoughtTemplate=>"thought-bubble",TauntFingerTemplate=>"finger-2",FollowFingerTemplate or MetronomeFingerTemplate=>"finger",_=>"red-heart"};string callback=name switch{ThoughtTemplate=>"AnimThoughtBubble",TauntFingerTemplate=>"AnimTauntFinger",FollowFingerTemplate=>"AnimFollowMeFinger",MetronomeFingerTemplate=>"AnimMetronomeFinger",HeartProjectileTemplate=>"AnimRedHeartProjectile",_=>"AnimRedHeartRising"};int width=name is HeartProjectileTemplate or HeartRisingTemplate?16:32;
        if(r.Asset!=asset||r.Callback!=callback||r.Width!=width||r.Height!=width||r.Blend||r.AffineDoubleSize!=(name==MetronomeFingerTemplate)||r.FrameLoops.Any(x=>x.Length!=0)||_script.SineTable.Length!=320)Fail(c,"Social sprite shape differs.");
        SourceSpriteAnimFrame F(int tile,int duration,bool flip=false)=>new(tile,duration,flip,false);
        SourceSpriteAnimFrame[][] frames=name switch
        {
            ThoughtTemplate=>[[F(0,2,true),F(16,2,true),F(32,2,true),F(48,2,true)],[F(0,2),F(16,2),F(32,2),F(48,2)],[F(48,2,true),F(32,2,true),F(16,2,true),F(0,2,true)],[F(48,2),F(32,2),F(16,2),F(0,2)]],
            TauntFingerTemplate=>[[F(0,1)],[F(0,1,true)],[F(0,4),F(16,4),F(32,4),F(16,4),F(0,4),F(16,4),F(32,4)],[F(0,4,true),F(16,4,true),F(32,4,true),F(16,4,true),F(0,4,true),F(16,4,true),F(32,4,true)]],
            _=>[[F(0,1)]]
        };
        if(r.Animations.Length!=frames.Length||!r.LoopStarts.SequenceEqual(Enumerable.Repeat(name is ThoughtTemplate or TauntFingerTemplate?-1:0,frames.Length)))Fail(c,"Social frame program shape differs.");
        for(int i=0;i<frames.Length;i++)if(!r.Animations[i].SequenceEqual(frames[i]))Fail(c,"Social frame commands differ.");
        if(name is FollowFingerTemplate or MetronomeFingerTemplate)
        {
            SourceSpriteAffineCommand[][] affine=[[new("frame",16,16,0,0,0),new("frame",30,30,0,8,0),new("end",0,0,0,0,0)],[new("frame",0,0,4,11,0),new("frame",0,0,252,11,0),new("loop",0,0,0,0,2),new("frame",-30,-30,0,8,0),new("end",0,0,0,0,0)]];
            if(r.AffineAnimations.Length!=2||!r.AffineAnimations[0].SequenceEqual(affine[0])||!r.AffineAnimations[1].SequenceEqual(affine[1]))Fail(c,"Follow Me affine programs differ.");
        }
        else if(r.AffineAnimations.Length!=0)Fail(c,"Unexpected social affine program.");
        if(name==MetronomeFingerTemplate){if(r.Subsprites.Length!=0)Fail(c,"Metronome source recipe has no subsprites.");if(_validatingInvokedProgram)ValidateCallerPreludeCapacity(c);}
        return true;
    }
    private bool TryCreateSocialTask(SourceAnimationCommand c)
    {
        if(c.Args[0]=="AnimTask_HeartsBackground"){_tasks.Add(new HeartsBackgroundTask(this));return true;}
        if(c.Args[0]=="AnimTask_TormentAttacker")
        {if(!_loaded.Contains("ANIM_TAG_THOUGHT_BUBBLE")||_spriteScene.Sprites.Any(x=>x.Frame.Template==ThoughtTemplate&&x is not TormentBubble))Fail(c,"Torment raw children require exclusive loaded template ownership.");_tasks.Add(new TormentTask(this));return true;}
        return false;
    }
    private bool TryCreateSocialSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (ThoughtTemplate or TauntFingerTemplate or FollowFingerTemplate or MetronomeFingerTemplate or HeartProjectileTemplate or HeartRisingTemplate))return false;
        var r=_script.SpriteRecipes[c.Args[0]];if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Social sprite graphics are not loaded.");_sprites.Add(new SocialSprite(slot,priority,r,this));return true;
    }
    private sealed class HeartsBackgroundTask:VisualTask
    {
        private int _phase,_timer,_alpha;
        public HeartsBackgroundTask(SourceBattleAnimation o){o.BeginOverlay("attract");o.SetBackgroundPriority(1,3);o.SetAlphaBlend(2,63,0,16);}
        public override void Step(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:if(++_timer==4){_timer=0;o._alphaA=++_alpha;o._alphaB=16-_alpha;if(_alpha==16){_phase=1;_alpha=0;}}break;
                case 1:if(++_alpha==141){_alpha=16;_phase=2;}break;
                case 2:if(++_timer==4){_timer=0;o._alphaA=--_alpha;o._alphaB=16-_alpha;if(_alpha==0){_phase=3;_alpha=0;}}break;
                case 3:o.EndOverlay(false);_phase=4;break;
                default:o.ClearAlphaBlend();o.SetBackgroundPriority(1,1);Finished=true;break;
            }
        }
    }
    private sealed class SocialSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private SourceSpriteFrameProgram _frames;private SourceSpriteAffineProgram? _affine;private PhysicalLinear? _linear;private PixelPoint _position,_offset;private int _phase,_timer,_animation,_angle,_priority,_lowPriority,_highPriority,_speed;private short _sum;private bool _restartFrame,_restartAffine,_invisible;
        private static short S(int n)=>unchecked((short)n);
        public SocialSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_priority=priority;var a=o._animationArgs;
            if(recipe.Template is ThoughtTemplate or TauntFingerTemplate or FollowFingerTemplate or MetronomeFingerTemplate)
            {
                int participant=a[0]==0?o._cue.Actor.Value:o._cue.Target.Value;var b=o._battlers[participant];var p=b.Center(o._doubles,true);bool player=(participant&1)==0;
                _position=recipe.Template==FollowFingerTemplate?new(p.X,Math.Max(10,p.Y-b.SourceHeight/2)):new(S(p.X+(player?b.SourceWidth/2+8:-b.SourceWidth/2-8)),S(p.Y-b.SourceHeight/4));
                _animation=player?0:1;_frames=new(recipe,recipe.Template is FollowFingerTemplate or MetronomeFingerTemplate?0:_animation);
                if(recipe.Template==ThoughtTemplate){_timer=a[1];_animation+=2;}
                if(recipe.Template==TauntFingerTemplate)_animation+=2;
                if(recipe.Template is FollowFingerTemplate or MetronomeFingerTemplate){_affine=new(recipe.AffineAnimations[0]);_lowPriority=priority;_highPriority=unchecked((byte)(priority+4));}
            }
            else
            {
                _frames=new(recipe);
                if(recipe.Template==HeartProjectileTemplate){var p=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);_position=new(S(p.X+o.InitialXDirection()*a[0]),S(p.Y+a[1]));var target=o._battlers[o._cue.Target.Value].Center(o._doubles,true);_linear=new(S(target.X-_position.X),S(target.Y-_position.Y),95);}
                else{_position=new(a[0],160);_timer=a[2];_speed=a[1];}
            }
        }
        private void StartFrame(int animation){_frames=new(_recipe,animation);_restartFrame=true;}
        public override void Step(SourceBattleAnimation o)
        {
            switch(_recipe.Template)
            {
                case ThoughtTemplate:
                    if(_phase==0){if(_frames.Ended)_phase=1;}
                    else if(_phase==1){if(--_timer==0){StartFrame(_animation);_phase=2;}}
                    else if(_phase==2){if(_frames.Ended)_phase=3;}else Finished=true;break;
                case TauntFingerTemplate:
                    if(_phase==0){if(++_timer>10){_timer=0;StartFrame(_animation);_phase=1;}}
                    else if(_phase==1){if(_frames.Ended)_phase=2;}else if(++_timer>5)Finished=true;break;
                case FollowFingerTemplate:case MetronomeFingerTemplate:AdvanceFinger(o);break;
                case HeartProjectileTemplate:
                    if(_linear!.Step(out var point)){Finished=true;break;}_offset=new(point.X,S(point.Y+(o._script.SineTable[_angle]*14>>8)));_angle=(_angle+4)&255;break;
                default:
                    if(_phase==0){if(_timer>0)_timer--;else _phase=1;break;}
                    _sum=S(_sum+_speed);_offset=new(S(o._script.SineTable[_angle]*4>>8),-((ushort)_sum>>8));_angle=(_angle+3)&255;int y=_position.Y+_offset.Y;if(y<=72){_invisible=(_angle&1)!=0;if(y<=64)Finished=true;}break;
            }
        }
        private void AdvanceFinger(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:if(_affine!.Ended)_phase=_recipe.Template==MetronomeFingerTemplate?3:1;break;
                case 1:if(++_timer>12){_timer=0;_phase=2;}break;
                case 2:
                    _angle+=4;if(_angle>254){_offset=new(0,_offset.Y);_timer=0;_phase=3;break;}
                    if(_angle>79)_priority=_highPriority;if(_angle>159)_priority=_lowPriority;int sine=o._script.SineTable[_angle];int x=sine>>3;_offset=new(x+(x>>1),_offset.Y);break;
                case 3:if(++_timer>16){_affine=new(_recipe.AffineAnimations[1]);_restartAffine=true;_phase=4;}break;
                case 4:if(_affine!.Ended)_phase=5;break;
                default:Finished=true;break;
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_restartFrame)_restartFrame=false;else _frames.Step();if(_restartAffine)_restartAffine=false;else _affine?.Step();}
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);return new(Slot,_recipe.Template,_position,_offset,f.Tile,!_invisible,f.HFlip,false,256,_recipe.Asset,_priority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=_affine is not null,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
    private sealed class TormentTask:VisualTask
    {
        private readonly int _battler;private readonly PixelPoint _center;private readonly SourceSpriteRecipe _recipe;private readonly SourceTaskAffineProgram _affine;private int _phase,_attempt,_x=32,_y=-20,_wait,_remaining;
        public TormentTask(SourceBattleAnimation o){_battler=o._cue.Actor.Value;_center=o._battlers[_battler].Center(o._doubles,true);_recipe=o._script.SpriteRecipes[ThoughtTemplate];_affine=new(o._script.TaskAffinePrograms["sAffineAnims_Torment"]);}
        public override void Step(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:
                    int slot=o._sprites.FindFreeSlot(o.IsReservedSpriteSlot);if(slot<SourceSpriteRegistry<VisualSprite>.Capacity){o._sprites.Add(new TormentBubble(slot,6-_attempt,_recipe,new(_center.X+((_attempt&1)==0?_x:-_x),_center.Y+_y),(_attempt&1)!=0));o._oamY[slot]=0;}
                    o._audio.Play(new("SE_M_METRONOME",o.AdjustPanning(-64),o._frame));if((_attempt&1)!=0){_x-=6;_y-=6;}o.PrepareTaskAffine(_battler,_affine);_attempt++;_phase=1;break;
                case 1:
                    if(!o.RunTaskAffine(_battler,_affine)){if(_attempt==6){_wait=8;_phase=3;}else{_wait=_attempt<=2?10:0;_phase=2;}}break;
                case 2:if(_wait!=0)_wait--;else _phase=0;break;
                case 3:if(_wait!=0)_wait--;else _phase=4;break;
                case 4:
                    var children=o._spriteScene.Sprites.Where(x=>x.Frame.Template==ThoughtTemplate).Cast<TormentBubble>().OrderBy(x=>x.Slot).Take(6).ToArray();_remaining=children.Length;foreach(var child in children)child.FinishFor(this);_phase=5;break;
                default:if(_remaining==0)Finished=true;break;
            }
        }
        public void ChildFinished()=>_remaining--;
    }
    private sealed class TormentBubble(int slot,int priority,SourceSpriteRecipe recipe,PixelPoint position,bool flip):VisualSprite(slot,priority)
    {
        private SourceSpriteFrameProgram? _frames;private TormentTask? _parent;private int _animation;private bool _restart;
        public override bool Counted=>false;
        public void FinishFor(TormentTask parent){_parent=parent;_animation=2;_restart=true;}
        public override void Step(SourceBattleAnimation o){if(_parent is not null&&!_restart&&_frames?.Ended==true){_parent.ChildFinished();Finished=true;}}
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_frames is null||_restart){_frames=new(recipe,_animation);_restart=false;}else _frames.Step();}
        public override SourceVisualSpriteFrame Frame=>new(Slot,recipe.Template,position,default,_frames?.Frame.Tile??0,true,_frames is not null&&(flip^_frames.Frame.HFlip),false,256,recipe.Asset,Subpriority){Width=32,Height=32};
    }
}
