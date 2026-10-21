namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string YawnCloudTemplate="gYawnCloudSpriteTemplate",SpikesTemplate="gSpikesSpriteTemplate",SkillSwapOrbTemplate="sSkillSwapOrbSpriteTemplate",GrudgeFlameTemplate="gGrudgeFlameSpriteTemplate";
    private bool ValidateUtilityCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        bool task=c.Op=="createvisualtask"&&name is "AnimTask_SkillSwap" or "AnimTask_GrudgeFlames";
        bool sprite=c.Op=="createsprite"&&name is YawnCloudTemplate or SpikesTemplate;
        if(!task&&!sprite)return false;
        bool swap=name=="AnimTask_SkillSwap",grudge=name=="AnimTask_GrudgeFlames",yawn=name==YawnCloudTemplate;
        if(a.Length!=(task?swap?3:2:yawn?4:8)||Number(a[1]) is <0 or >255||sprite&&(Number(a[1])>1||Number(a[2]) is <0 or >127)||a.Skip(task?2:3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Utility source callback operands differ.");
        if(swap&&Number(a[2]) is not(0 or 1)||yawn&&Number(a[3]) is <0 or >2||name==SpikesTemplate&&Number(a[7]) is <1 or >32767)Fail(c,"Utility participant, cloud selector or arc duration differs.");
        string template=swap?SkillSwapOrbTemplate:grudge?GrudgeFlameTemplate:name;
        if(task&&!_script.SpriteDependencies.Contains(template))Fail(c,"Utility task child dependency is missing.");
        ValidateUtilityRecipe(c,template);
        if(!_validatingInvokedProgram)return true;
        if(swap||grudge)ValidateReactionGeometry(c,_battlers[_cue.Actor.Value]);if(swap)ValidateReactionGeometry(c,_battlers[_cue.Target.Value]);
        var commands=OverlayInvocationCommands();
        if(commands.Any(x=>x.Op is "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim"))Fail(c,"Utility ownership requires a bounded source closure.");
        if(swap||yawn)
        {
            // Conservative total-birth bound proves native affine allocation even
            // with retained scene owners. Raw creation still uses actual sprite cells.
            int births=commands.Count(x=>x.Op=="createsprite")+12*commands.Count(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_SkillSwap");
            if(commands.Any(x=>x.Op=="createvisualtask"&&x.Args[0] is not("AnimTask_SkillSwap" or "AnimTask_BlendMonInAndOut" or "AnimTask_SetPsychicBackground" or "AnimTask_DeepInhale")||x.Op=="createsprite"&&x.Args[0]!=YawnCloudTemplate))Fail(c,"Utility affine companions exceed the reviewed capacity closure.");
            if(4+births+_spriteScene.Sprites.Count(x=>x.Frame.Affine||x.Frame.Scale!=256)>32)Fail(c,"Utility source closure exceeds affine capacity.");
            if(yawn&&Enumerable.Range(0,64).Count(i=>!IsReservedSpriteSlot(i)&&_spriteScene.Sprites[i] is null)<commands.Count(x=>x.Op=="createsprite"))Fail(c,"Yawn source closure lacks counted sprite cells.");
        }
        if(grudge&&(commands.Count(x=>x.Op=="createvisualtask"&&x.Args[0]==name)!=1||commands.Any(x=>x.Op is "setalpha" or "blendoff"||x.Op=="createvisualtask"&&x.Args[0]!=name||x.Op=="createsprite")))Fail(c,"Grudge requires one alpha-register owner and its raw children.");
        return true;
    }
    private void ValidateUtilityRecipe(SourceAnimationCommand c,string name)
    {
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Utility source sprite recipe is missing.");return;}
        bool yawn=name==YawnCloudTemplate,swap=name==SkillSwapOrbTemplate,grudge=name==GrudgeFlameTemplate;
        string asset=yawn?"pink-cloud":swap?"bluegreen-orb":grudge?"purple-flame":"spikes";
        string callback=yawn?"AnimYawnCloud":swap?"AnimSkillSwapOrb":grudge?"AnimGrudgeFlame":"AnimSpikes";
        if(r.Asset!=asset||r.Callback!=callback||r.Width!=(yawn?32:16)||r.Height!=(yawn||grudge?32:16)||r.Blend!=grudge||r.AffineDoubleSize||r.Subsprites.Length!=0||r.Animations.Length!=1||!r.LoopStarts.SequenceEqual(new[]{0})||r.FrameLoops.Any(p=>p.Length!=0)||_script.SineTable.Length!=320)Fail(c,"Utility source shape/frame ownership differs.");
        SourceSpriteAnimFrame[] frames=grudge?[new(0,4,false,false),new(8,4,false,false),new(16,4,false,false),new(24,4,false,false)]:[new(0,1,false,false)];
        if(!r.Animations[0].SequenceEqual(frames)||r.AffineAnimations.Length!=(yawn?3:swap?4:0))Fail(c,"Utility source frame/affine table differs.");
        if(yawn)for(int i=0;i<3;i++)
        {
            int delta=i==0?-8:8;SourceSpriteAffineCommand[] expected=[new("frame",i==0?128:i==1?192:256,i==0?128:i==1?192:256,0,0,0),new("frame",delta,delta,0,8,0),new("frame",-delta,-delta,0,8,0),new("jump",0,0,0,0,0)];
            if(!r.AffineAnimations[i].SequenceEqual(expected))Fail(c,"Yawn source affine commands differ.");
        }
        if(swap)for(int i=0;i<4;i++)
        {
            SourceSpriteAffineCommand[] expected=i==0?[new("frame",-8,-8,0,8,0),new("frame",8,8,0,8,0),new("jump",0,0,0,0,0)]:[new("frame",272-i*32,272-i*32,0,0,0),new("frame",-8,-8,0,8-i*2,0),new("frame",8,8,0,8,0),new("frame",-8,-8,0,i*2,0),new("jump",0,0,0,0,1)];
            if(!r.AffineAnimations[i].SequenceEqual(expected))Fail(c,"Skill Swap source affine commands differ.");
        }
        RequireOverlaySpritePalette(c,"ANIM_TAG_"+asset.ToUpperInvariant().Replace('-','_'));
    }
    private bool TryCreateUtilitySprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not(YawnCloudTemplate or SpikesTemplate))return false;
        var r=_script.SpriteRecipes[c.Args[0]];if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Utility graphics are not loaded.");
        if(c.Args[0]==YawnCloudTemplate)_sprites.Add(new YawnCloudSprite(slot,priority,r,this));else _sprites.Add(new SpikesSprite(slot,priority,r,this));return true;
    }
    private bool TryCreateUtilityTask(SourceAnimationCommand c)
    {
        if(c.Args[0] is not("AnimTask_SkillSwap" or "AnimTask_GrudgeFlames"))return false;
        bool swap=c.Args[0]=="AnimTask_SkillSwap";if(!_loaded.Contains(swap?"ANIM_TAG_BLUEGREEN_ORB":"ANIM_TAG_PURPLE_FLAME"))Fail(c,"Utility raw child graphics are not loaded.");
        _tasks.Add(swap?new SkillSwapTask(this,_animationArgs[0]):new GrudgeFlamesTask(this));return true;
    }
    private sealed class YawnCloudSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram _affine;
        private PixelPoint _position,_offset;private short _x,_y,_dx,_dy,_age,_delay,_blinks;private bool _hidden;
        public YawnCloudSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;_frames=new(r);_affine=new(r.AffineAnimations[o._animationArgs[0]]);
            var end=o._battlers[o._cue.Target.Value].Center(o._doubles,true);_position=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);
            _x=unchecked((short)(_position.X<<4));_y=unchecked((short)(_position.Y<<4));_dx=unchecked((short)(((end.X-_position.X)<<4)/64));_dy=unchecked((short)(((end.Y-_position.Y)<<4)/64));
        }
        public override void Step(SourceBattleAnimation o)
        {
            _age++;_x=unchecked((short)(_x+_dx));_y=unchecked((short)(_y+_dy));_position=new(_x>>4,_y>>4);_offset=new(0,o._script.SineTable[(_age*8)&255]*8>>8);
            if(_age>58&&++_delay>1){_delay=0;_hidden=(++_blinks&1)!=0;if(_blinks>3)Finished=true;}
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine.Step();}
        public override SourceVisualSpriteFrame Frame
        {get{var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!_hidden,false,false,256,_recipe.Asset,Subpriority){Width=32,Height=32,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
    private sealed class SpikesSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly PixelPoint _position;private PixelPoint _offset;private readonly HorizontalArc _arc;
        private int _phase,_delay,_blinks;private bool _hidden;
        public SpikesSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;_frames=new(r);var a=o._animationArgs;var p=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);_position=new(unchecked((short)(p.X+o.InitialXDirection()*a[0])),unchecked((short)(p.Y+a[1])));
            // SetAverageBattlerPositions uses both doubles positions even when the
            // partner sprite is hidden. It does not query IsBattlerSpriteVisible.
            var t=o._battlers[o._cue.Target.Value].Base(o._doubles);if(o._doubles){var q=o._battlers[o._cue.Target.Value^2].Base(true);t=new((t.X+q.X)/2,(t.Y+q.Y)/2);}
            if(!o._cue.Actor.IsPlayer)a[2]=unchecked((short)-a[2]);_arc=new(unchecked((short)(t.X+a[2]))-_position.X,unchecked((short)(t.Y+a[3]))-_position.Y,a[4],-50);
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_phase==0){if(_arc.Step(o._script.SineTable,out var p)){_phase=1;_delay=30;}else _offset=p;}
            else if(_phase==1){if(_delay!=0)_delay--;else _phase=2;}
            else{if((_blinks&1)!=0)_hidden=!_hidden;if(++_blinks==16)Finished=true;}
        }
        public override void AdvanceAnimation(SourceBattleAnimation o)=>_frames.Step();
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!_hidden,false,false,256,_recipe.Asset,Subpriority){Width=16,Height=16};
    }
    private sealed class SkillSwapTask:VisualTask
    {
        private readonly PixelPoint _start,_end;private readonly int _height;private int _phase,_delay=6,_births;
        public SkillSwapTask(SourceBattleAnimation o,int mode)
        {
            _height=mode==1?-10:10;
            PixelPoint Edge(int n){var b=o._battlers[n];var p=b.Center(o._doubles,true);return mode==1?new(p.X-b.SourceWidth/2+8,p.Y-b.SourceHeight/2+8):new(p.X+b.SourceWidth/2-8,p.Y+b.SourceHeight/2-8);}
            _start=Edge(mode==1?o._cue.Target.Value:o._cue.Actor.Value);_end=Edge(mode==1?o._cue.Actor.Value:o._cue.Target.Value);
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_phase!=0){if(++_delay>17)Finished=true;return;}
            if(++_delay<=6)return;_delay=0;int slot=o.FindFreeSpriteSlot();
            if(slot<64)o._sprites.Add(new SkillSwapOrb(slot,o._script.SpriteRecipes[SkillSwapOrbTemplate],_start,_end,_height,_births&3));
            if(++_births==12)_phase++;
        }
    }
    private sealed class SkillSwapOrb:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly PixelPoint _position;private PixelPoint _offset;private readonly HorizontalArc _arc;private readonly int _program;
        private SourceSpriteFrameProgram? _frames;private SourceSpriteAffineProgram? _affine;
        public SkillSwapOrb(int slot,SourceSpriteRecipe r,PixelPoint start,PixelPoint end,int height,int program):base(slot,0){_recipe=r;_position=start;_arc=new(end.X-start.X,end.Y-start.Y,16,height);_program=program;}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o){if(_arc.Step(o._script.SineTable,out var p))Finished=true;else _offset=p;}
        public override void AdvanceAnimation(SourceBattleAnimation o)
        {if(_frames is null){_frames=new(_recipe);_affine=new(_recipe.AffineAnimations[_program]);}else{_frames.Step();_affine!.Step();}}
        public override SourceVisualSpriteFrame Frame
        {get{var m=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);return new(Slot,_recipe.Template,_position,_offset,_frames?.Frame.Tile??0,true,false,false,256,_recipe.Asset,Subpriority){Width=16,Height=16,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
    private sealed class GrudgeFlamesTask:VisualTask
    {
        private readonly PixelPoint _position;private readonly int _radius,_subpriority,_priority;private readonly bool _player;
        private int _phase,_delay=16,_a,_b=16;public int Children{get;set;}public bool KillChildren{get;private set;}
        public GrudgeFlamesTask(SourceBattleAnimation o)
        {
            var b=o._battlers[o._cue.Actor.Value];var p=b.Base(o._doubles);_position=new(p.X,unchecked((byte)(p.Y-(b.Slot.IsPlayer?0:b.Elevation))));_radius=b.SourceWidth/2+8;_subpriority=unchecked((byte)(b.Subpriority-2));_priority=o.BattlerBackgroundPriority(b.Slot.Value);_player=b.Slot.IsPlayer;
            o.SetAlphaBlend(0,63,0,16);
        }
        public override void Step(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:for(int i=0;i<6;i++){int slot=o.FindFreeSpriteSlot();if(slot<64){o._sprites.Add(new GrudgeFlame(slot,_subpriority,o._script.SpriteRecipes[GrudgeFlameTemplate],_position,_radius,_priority,_player,i,this));Children++;}}_phase++;break;
                case 1:if((++_delay&1)!=0){if(_a<14)_a++;}else if(_b>4)_b--;if(_a==14&&_b==4){_delay=0;_phase++;}o._alphaA=_a;o._alphaB=_b;break;
                case 2:if(++_delay>30){_delay=0;_phase++;}break;
                case 3:if((++_delay&1)!=0){if(_a>0)_a--;}else if(_b<16)_b++;if(_a==0&&_b==16){KillChildren=true;_phase++;}o._alphaA=_a;o._alphaB=_b;break;
                case 4:if(Children==0)_phase++;break;
                case 5:o.ClearAlphaBlend();o._blendSecondTargets=0;Finished=true;break;
            }
        }
    }
    private sealed class GrudgeFlame:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly PixelPoint _position;private PixelPoint _offset;private readonly int _radius,_basePriority,_direction;private readonly GrudgeFlamesTask _task;
        private int _angle,_priority;private short _age;private SourceSpriteFrameProgram? _frames;
        public GrudgeFlame(int slot,int subpriority,SourceSpriteRecipe r,PixelPoint p,int radius,int priority,bool player,int index,GrudgeFlamesTask task):base(slot,subpriority)
        {_recipe=r;_position=p;_radius=radius;_basePriority=priority;_priority=2;_direction=player?-2:2;_angle=index*42;_age=(short)(index*6);_task=task;}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o)
        {
            _angle=(_angle+_direction)&255;_priority=(_basePriority+(unchecked((ushort)(_angle-65))<127?1:0))&3;_age=unchecked((short)(_age+1));_offset=new(o._script.SineTable[_angle]*_radius>>8,o._script.SineTable[(_age*8)&255]*7>>8);
            if(_task.KillChildren){_task.Children--;Finished=true;}
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_frames is null)_frames=new(_recipe);else _frames.Step();}
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames?.Frame.Tile??0,true,false,true,256,_recipe.Asset,Subpriority){Width=16,Height=32,OamPriority=_priority};
    }
}
