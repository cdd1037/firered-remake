namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string VoltOrb="gVoltTackleOrbSlideSpriteTemplate",VoltBolt="gVoltTackleBoltSpriteTemplate",VoltBoltTask="AnimTask_VoltTackleBolt",VoltReappearTask="AnimTask_VoltTackleAttackerReappear";
    private bool HasVoltTackleClosure()=>OverlayInvocationCommands().Any(c=>c.Op=="createsprite"&&c.Args.FirstOrDefault()==VoltOrb||c.Op=="createvisualtask"&&c.Args.FirstOrDefault() is VoltBoltTask or VoltReappearTask);
    private bool ValidateVoltTackleCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string n=a.FirstOrDefault()??"";
        if(c.Op=="createsprite"&&n==VoltOrb)
        {if(a.Length!=3||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(c,"Volt Tackle orb source operands differ.");ValidateVoltTackleRecipe(c,VoltOrb);}
        else if(c.Op=="createvisualtask"&&n is VoltBoltTask or VoltReappearTask)
        {
            if(a.Length!=(n==VoltBoltTask?3:2)||Number(a[1]) is <0 or >255||n==VoltBoltTask&&Number(a[2]) is <0 or >4)Fail(c,"Volt Tackle task source operands differ.");
            if(n==VoltBoltTask){if(!_script.SpriteDependencies.Contains(VoltBolt))Fail(c,"Volt Tackle raw bolt dependency missing.");ValidateVoltTackleRecipe(c,VoltBolt);}
        }
        else return false;
        if(_validatingInvokedProgram)ValidateVoltTackleCapacity(c);return true;
    }
    private void ValidateVoltTackleCapacity(SourceAnimationCommand c)
    {
        var commands=OverlayInvocationCommands();
        if(!IsSourceBattlerPresent(_cue.Actor.Value)||!IsSourceBattlerPresent(_cue.Target.Value)||_cue.Actor==_cue.Target)Fail(c,"Volt Tackle needs distinct present source participants.");
        if(commands.Any(x=>x.Op is "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim" or "monbg_static" or "clearmonbg_static" or "fadetobg" or "restorebg" or "changebg"))Fail(c,"Volt Tackle allocation requires a bounded linear closure.");
        if(commands.Any(x=>x.Op=="createsprite"&&x.Args.FirstOrDefault() is not(VoltOrb or ElectricPuff)||x.Op=="createvisualtask"&&x.Args.FirstOrDefault() is not(VoltBoltTask or VoltReappearTask or "AnimTask_ShakeMon2" or "AnimTask_BlendBattleAnimPal")))Fail(c,"Volt Tackle companion allocation differs.");
        if(commands.Count(x=>x.Op=="createsprite"&&x.Args[0]==VoltOrb)>1||commands.Count(x=>x.Op=="createvisualtask"&&x.Args[0]==VoltReappearTask)>1)Fail(c,"Volt Tackle has overlapping actor transform owners.");
        int live=0,affine=0,tasks=0,sounds=0,peak=0,matrices=0,taskPeak=0;
        foreach(var x in commands)
        {
            if(x.Op=="waitforvisualfinish"){live=affine=tasks=0;continue;}
            if(x.Op=="createsprite"){live++;if(x.Args[0]==VoltOrb)affine++;}
            if(x.Op=="createvisualtask")
            {
                tasks++;
                // The longest source sweep crosses -16..256 in 16px steps:
                // exactly17 attempts, <=2 per task callback. Its counted parent
                // waits for every successful raw child before a visual wait drains.
                if(x.Args[0]==VoltBoltTask){if(x.Args.Length!=3||Number(x.Args[2]) is <0 or >4)Fail(c,"Volt Tackle sweep selector differs.");live+=17;affine+=17;}
            }
            if(x.Op is "createsoundtask" or "loopsewithpan" or "waitplaysewithpan" or "panse" or "panse_adjustall" or "panse_adjustnone")sounds++;
            peak=Math.Max(peak,live);matrices=Math.Max(matrices,affine);taskPeak=Math.Max(taskPeak,tasks+sounds);
        }
        int retained=_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length)),retainedAffine=_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256);
        int controllers=1+commands.Count(x=>x.Op=="monbg")*(_doubles?2:1)+commands.Count(x=>x.Op=="clearmonbg");
        if(22+peak+retained>64||4+matrices+retainedAffine>32||controllers+taskPeak+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(c,"Volt Tackle closure exceeds source OAM, matrix or task capacity.");
    }
    private void ValidateVoltTackleRecipe(SourceAnimationCommand c,string n)
    {
        if(!_script.SpriteRecipes.TryGetValue(n,out var r)){Fail(c,"Volt Tackle source recipe missing.");return;}
        bool orb=n==VoltOrb;int width=orb?64:8,height=orb?64:16;
        if(r.Asset!=(orb?"circle-of-light":"spark-thin")||r.Callback!=(orb?"AnimVoltTackleOrbSlide":"AnimVoltTackleBolt")||r.Width!=width||r.Height!=height||r.Blend!=orb||r.AffineDoubleSize==orb||r.Subsprites.Length!=0||r.FrameLoops.Any(p=>p.Length!=0)||r.Animations.Length!=(orb?1:4)||!r.LoopStarts.SequenceEqual(orb?[0]:[-1,-1,-1,-1])||r.AffineAnimations.Length!=(orb?3:1))Fail(c,"Volt Tackle frame shape differs.");
        for(int i=0;i<r.Animations.Length;i++)if(!r.Animations[i].SequenceEqual([new SourceSpriteAnimFrame(orb?0:i*2,orb?1:3,false,false)]))Fail(c,"Volt Tackle frame commands differ.");
        SourceSpriteAffineCommand F(int x,int duration)=>new("frame",x,x,0,duration,0);SourceSpriteAffineCommand End()=>new("end",0,0,0,0,0);
        SourceSpriteAffineCommand[][] expected=orb?[[F(16,0),F(4,60),F(256,0),new("loop",0,0,0,0,0),F(-4,5),F(4,5),new("loop",0,0,0,0,10),End()],[F(16,0),F(8,30),F(256,0),F(-4,5),F(4,5),new("jump",0,0,0,0,3)],[F(16,0),F(8,30),F(-8,30),End()]]:[[new("frame",256,256,64,0,0),End()]];
        for(int i=0;i<expected.Length;i++)if(!r.AffineAnimations[i].SequenceEqual(expected[i]))Fail(c,"Volt Tackle affine source commands differ.");
    }
    private bool TryCreateVoltTackleSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0]!=VoltOrb)return false;if(!_loaded.Contains("ANIM_TAG_CIRCLE_OF_LIGHT"))Fail(c,"Volt Tackle orb graphics not loaded.");
        _sprites.Add(new VoltTackleOrbSprite(slot,priority,_script.SpriteRecipes[VoltOrb],this));return true;
    }
    private bool TryCreateVoltTackleTask(SourceAnimationCommand c)
    {
        if(c.Args[0] is not(VoltBoltTask or VoltReappearTask))return false;
        if(c.Args[0]==VoltBoltTask&&!_loaded.Contains("ANIM_TAG_SPARK"))Fail(c,"Volt Tackle bolt graphics not loaded.");
        _tasks.Add(new VoltTackleTask(c.Args[0]==VoltReappearTask,this));return true;
    }
    private sealed class VoltTackleOrbSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram _affine;private readonly int _actor,_speed;private PixelPoint _position;private int _phase,_age;
        public VoltTackleOrbSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {_recipe=recipe;_frames=new(recipe);_affine=new(recipe.AffineAnimations[1]);_actor=o._cue.Actor.Value;_speed=o._cue.Actor.IsPlayer?16:-16;_position=o._battlers[_actor].Center(o._doubles,true);}
        public override void Step(SourceBattleAnimation o)
        {
            if(_phase==0){if(++_age>40)_phase=1;return;}
            _position=new(unchecked((short)(_position.X+_speed)),_position.Y);var offset=o._offsets[_actor];o._offsets[_actor]=new(unchecked((short)(offset.X+_speed)),offset.Y);
            if(unchecked((ushort)(_position.X+80))>400)Finished=true;
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine.Step();}
        public override SourceVisualSpriteFrame Frame{get{var m=_affine.Matrix;return new(Slot,VoltOrb,_position,default,_frames.Frame.Tile,true,false,true,256,_recipe.Asset,Subpriority){Width=64,Height=64,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
    private sealed class VoltTackleTask:VisualTask
    {
        private readonly bool _reappear;private readonly int _actor;private readonly short[] _d=new short[16];
        public VoltTackleTask(bool reappear,SourceBattleAnimation o)
        {
            _reappear=reappear;_actor=o._cue.Actor.Value;
            if(reappear){_d[15]=(short)_actor;_d[14]=(short)(o._cue.Actor.IsPlayer?-32:32);_d[13]=(short)(o._cue.Actor.IsPlayer?2:-2);SetX(o,_d[14]);}
            else
            {
                int side=o._cue.Actor.IsPlayer?1:-1,mode=o._animationArgs[0];var actor=o._battlers[_actor].Center(o._doubles,true);var target=o._battlers[o._cue.Target.Value].Center(o._doubles,true);
                if(mode==0){_d[3]=(short)actor.X;_d[5]=(short)actor.Y;_d[4]=(short)(side*128+120);}
                else if(mode==4){_d[3]=(short)(120-side*128);_d[5]=(short)target.Y;_d[4]=(short)(target.X-side*32);}
                else{_d[3]=(short)((mode&1)!=0?256:-16);_d[4]=(short)((mode&1)!=0?-16:256);_d[5]=(short)(side==1?80-mode*10:mode*10+40);if(side==-1)(_d[3],_d[4])=(_d[4],_d[3]);}
                _d[1]=(short)(_d[3]<_d[4]?1:-1);_d[6]=(short)(_d[1]==1?0:3);
            }
            _d[0]=1;
        }
        private void SetX(SourceBattleAnimation o,int x){var p=o._offsets[_actor];o._offsets[_actor]=new(x,p.Y);}
        public void Release()=>_d[7]--;
        public override void Step(SourceBattleAnimation o)
        {
            if(!_reappear)
            {if(_d[0]==1){if(++_d[2]>0){_d[2]=0;if(Bolt(o)||Bolt(o))_d[0]++;}}else if(_d[7]==0)Finished=true;return;}
            switch(_d[0])
            {
                case 1:if(++_d[1]>1){_d[1]=0;o.SetSourceBattlerSpriteInvisible(_actor,!o.SourceBattlerSpriteInvisible(_actor));if(_d[14]!=0){_d[14]+=_d[13];SetX(o,_d[14]);}else _d[0]++;}break;
                case 2:if(++_d[1]>1){_d[1]=0;o.SetSourceBattlerSpriteInvisible(_actor,!o.SourceBattlerSpriteInvisible(_actor));if(++_d[2]==8)_d[0]++;}break;
                case 3:o.SetSourceBattlerSpriteInvisible(_actor,false);Finished=true;break;
            }
        }
        private bool Bolt(SourceBattleAnimation o)
        {
            int slot=o.FindFreeSpriteSlot();if(slot<SourceSpriteRegistry<VisualSprite>.Capacity){o._sprites.Add(new VoltTackleRawBolt(slot,o._script.SpriteRecipes[VoltBolt],this,new(_d[3],_d[5])));_d[7]++;}
            // Original advances this cycling field even when allocation fails,
            // but never passes it to StartSpriteAnim. Every child uses animation0.
            _d[6]+=_d[1];if(_d[6]<0)_d[6]=3;if(_d[6]>3)_d[6]=0;_d[3]+=(short)(_d[1]*16);
            return _d[1]==1?_d[3]>=_d[4]:_d[3]<=_d[4];
        }
    }
    private sealed class VoltTackleRawBolt(int slot,SourceSpriteRecipe recipe,VoltTackleTask parent,PixelPoint position):VisualSprite(slot,35)
    {
        private int _age;private SourceSpriteFrameProgram? _frames;private SourceSpriteAffineProgram? _affine;
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o){if(++_age>12){parent.Release();Finished=true;}}
        public override void AdvanceAnimation(SourceBattleAnimation o)
        {if(_frames is null){_frames=new(recipe);_affine=new(recipe.AffineAnimations[0]);}else{_frames.Step();_affine!.Step();}}
        public override SourceVisualSpriteFrame Frame{get{var f=_frames?.Frame??recipe.Animations[0][0];var m=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);return new(Slot,VoltBolt,position,default,f.Tile,true,false,false,256,recipe.Asset,Subpriority){Width=8,Height=16,Affine=true,DoubleSize=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
}
