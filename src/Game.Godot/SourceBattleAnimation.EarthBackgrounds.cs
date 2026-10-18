namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string ScatterRock="gRockScatterSpriteTemplate",FissurePosition="AnimTask_PositionFissureBgOnBattler",SeismicLevel="AnimTask_GetSeismicTossDamageLevel",SeismicRise="AnimTask_MoveSeismicTossBg",SeismicFall="AnimTask_SeismicTossBgAccelerateDownAtEnd";
    private static bool EarthBackgroundTaskName(string name)=>name is FissurePosition or SeismicLevel or SeismicRise or SeismicFall;
    private bool ValidateEarthBackgroundCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";bool sprite=c.Op=="createsprite"&&name==ScatterRock,task=c.Op=="createvisualtask"&&EarthBackgroundTaskName(name);if(!sprite&&!task)return false;
        if(a.Length!=(sprite?7:name==FissurePosition?5:2)||Number(a[task?1:2]) is <0||Number(a[task?1:2])>(task?255:127)||sprite&&Number(a[1]) is <0 or >1)Fail(c,"Earth background source operand width differs.");
        if(a.Skip(task?2:3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Earth background operands exceed source words.");
        if(name==FissurePosition&&(Number(a[2]) is <0 or >3||Number(a[3]) is <0 or >255)||sprite&&Number(a[6]) is <0 or >5)Fail(c,"Earth background participant, priority or animation differs.");
        if(sprite)
        {
            if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Scatter rock source recipe missing.");return true;}
            if(r.Asset!="rocks"||r.Callback!="AnimRockScatter"||r.Width!=32||r.Height!=32||r.Blend||r.AffineDoubleSize||r.Subsprites.Length!=0||r.Animations.Length!=6||!r.LoopStarts.SequenceEqual(Enumerable.Repeat(-1,6))||r.FrameLoops.Any(p=>p.Length!=0)||r.AffineAnimations.Length!=2)Fail(c,"Scatter rock source recipe shape differs.");
            for(int i=0;i<6;i++)if(!r.Animations[i].SequenceEqual(new[]{new SourceSpriteAnimFrame(i*16,1,false,false)}))Fail(c,"Scatter rock source frames differ.");
            for(int i=0;i<2;i++)if(!r.AffineAnimations[i].SequenceEqual(new[]{new SourceSpriteAffineCommand("frame",0,0,i==0?251:5,5,0),new SourceSpriteAffineCommand("jump",0,0,0,0,0)}))Fail(c,"Scatter rock source affine program differs.");
        }
        if(_script.SineTable.Length!=320)Fail(c,"Earth background source sine table missing.");
        if(_validatingInvokedProgram)
        {
            if(name==FissurePosition&&!_battlers.ContainsKey(((Number(a[2])&1)!=0?_cue.Target.Value:_cue.Actor.Value)^(Number(a[2])>1?2:0)))Fail(c,"Fissure position requires registered source battler coordinates.");
            if(name==SeismicLevel&&_cue.MoveDamage is null or <0)Fail(c,"Seismic predicate requires committed nonnegative source damage.");
            ValidateEarthBackgroundCapacity(c);
        }
        return true;
    }
    private void ValidateEarthBackgroundCapacity(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a[0];
            var closure=OverlayInvocationCommands();bool fissure=closure.Any(x=>x.Op=="createvisualtask"&&x.Args[0]==FissurePosition);
            if(closure.Any(x=>x.Op=="createsprite"&&x.Args[0] is not (ScatterRock or DigPlume or "gBasicHitSplatSpriteTemplate" or "gComplexPaletteBlendSpriteTemplate")||x.Op=="createvisualtask"&&!EarthBackgroundTaskName(x.Args[0])&&x.Args[0] is not ("AnimTask_HorizontalShake" or "AnimTask_ShakeMon")))Fail(c,"Earth background companion ownership exceeds source closure.");
            if(closure.Where(x=>x.Op=="createvisualtask"&&EarthBackgroundTaskName(x.Args[0])).GroupBy(x=>x.Args[0]).Any(g=>g.Count()>1)||fissure&&closure.Any(x=>x.Op=="createvisualtask"&&x.Args[0] is SeismicLevel or SeismicRise or SeismicFall))Fail(c,"Duplicate or competing earth background owners.");
            if(name is FissurePosition or SeismicFall)
            {
                int stop=name==FissurePosition?Number(a[4]):4095;
                if(!closure.Any(x=>x.Op=="setarg"&&Number(x.Args[0])==7&&unchecked((short)AnimationArgument(x.Args[1]))==stop))Fail(c,"Earth background holder requires its source register-7 terminator.");
            }
            int retained=_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length));
            int births=MaximumInvocationCost(x=>x.Op=="createsprite"?1:0),matrices=MaximumInvocationCost(x=>x.Op=="createsprite"&&x.Args[0] is ScatterRock or "gBasicHitSplatSpriteTemplate"?1:0);
            int tasks=1+MaximumInvocationCost(x=>x.Op is "createvisualtask" or "createsoundtask" or "loopsewithpan" or "waitplaysewithpan" or "panse" or "fadetobg" or "restorebg" or "clearmonbg"?1:x.Op=="monbg"?(_doubles?2:1):0);
            if(22+births+retained>64||4+matrices+_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256)>32||tasks+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(c,"Earth background complete path exceeds native object, matrix or task capacity.");
    }
    private bool TryCreateEarthBackgroundTask(SourceAnimationCommand c)
    {
        if(!EarthBackgroundTaskName(c.Args[0]))return false;
        if(c.Args[0]==SeismicLevel){int damage=_cue.MoveDamage!.Value;if(damage<33)_animationArgs[7]=0;if(unchecked((uint)(damage-33))<33)_animationArgs[7]=1;if(damage>65)_animationArgs[7]=2;return true;}
        var task=new EarthBackgroundTask(c.Args[0],this);if(!task.Finished)_tasks.Add(task);return true;
    }
    private bool TryCreateEarthBackgroundSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=ScatterRock)return false;if(!_loaded.Contains("ANIM_TAG_ROCKS"))Fail(c,"Scatter rock source graphics not loaded.");_sprites.Add(new EarthScatterRock(slot,priority,_script.SpriteRecipes[ScatterRock],this));return true;
    }
    private sealed class EarthBackgroundTask:VisualTask
    {
        private readonly string _name;private readonly short[] _d=new short[16];
        private static short S(int v)=>unchecked((short)v);
        public override bool Counted=>_name!=FissurePosition;
        public EarthBackgroundTask(string name,SourceBattleAnimation o)
        {
            _name=name;
            if(name==FissurePosition)
            {
                var a=o._animationArgs;int slot=(a[0]&1)!=0?o._cue.Target.Value:o._cue.Actor.Value;if(a[0]>1)slot^=2;
                var p=o._battlers[slot].Center(o._doubles,true);_d[1]=S((32-p.X)&511);_d[2]=S((64-p.Y)&255);_d[3]=a[2];Priority=a[1];o.BackgroundScrollX=(ushort)_d[1];o.BackgroundScrollY=(ushort)_d[2];
            }
            else Step(o);
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_name==FissurePosition)
            {if(o._animationArgs[7]==_d[3]){o.BackgroundScrollX=o.BackgroundScrollY=0;Finished=true;}else{o.BackgroundScrollX=(ushort)_d[1];o.BackgroundScrollY=(ushort)_d[2];}return;}
            if(_name==SeismicRise)
            {
                if(_d[0]==0){o.BackgroundScreenSize=0;_d[1]=200;}o.BackgroundScrollY=unchecked((ushort)(o.BackgroundScrollY+_d[1]/10));_d[1]=S(_d[1]-3);
                if(_d[0]==120){o.BackgroundScreenSize=1;Finished=true;}_d[0]++;return;
            }
            if(_d[0]==0){o.BackgroundScreenSize=0;_d[0]++;_d[2]=S(o.BackgroundScrollY);}_d[1]=S((_d[1]+80)&255);o.BackgroundScrollY=unchecked((ushort)(_d[2]+(o._script.SineTable[68]*_d[1]>>8)));
            if(o._animationArgs[7]==4095){o.BackgroundScrollY=0;o.BackgroundScreenSize=1;Finished=true;}
        }
    }
    private sealed class EarthScatterRock:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram _affine;private readonly short[] _d=new short[8];private readonly PixelPoint _position;private PixelPoint _offset;
        public EarthScatterRock(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {var a=o._animationArgs;_recipe=recipe;var p=o._battlers[o._cue.Target.Value].Base(o._doubles);_position=new(unchecked((short)(p.X+a[0])),unchecked((short)(p.Y+a[1])));_d[1]=a[0];_d[2]=a[1];_d[5]=a[2];_frames=new(recipe,a[3]);_affine=new(recipe.AffineAnimations[0]);}
        public override void Step(SourceBattleAnimation o)
        {_d[0]+=8;_d[3]=unchecked((short)(_d[3]+_d[1]));_d[4]=unchecked((short)(_d[4]+_d[2]));_offset=new(unchecked((short)(_offset.X+_d[3]/40)),unchecked((short)(_offset.Y-(o._script.SineTable[_d[0]]*_d[5]>>8))));if(_d[0]>140)Finished=true;}
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine.Step();}
        public override SourceVisualSpriteFrame Frame{get{var f=_frames.Frame;var m=_affine.Matrix;return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=32,Height=32,VFlip=f.VFlip,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
}
