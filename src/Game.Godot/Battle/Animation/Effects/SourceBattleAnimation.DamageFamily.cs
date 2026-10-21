namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string RevengeSmall="gRevengeSmallScratchSpriteTemplate",RevengeBig="gRevengeBigScratchSpriteTemplate",PersistentImpact="gPersistHitSplatSpriteTemplate",EdgeImpact="gMonEdgeHitSplatSpriteTemplate",RapidRing="gRapidSpinSpriteTemplate",SpeedDust="gSpeedDustSpriteTemplate",AirWave="gAirWaveProjectileSpriteTemplate",AirSlice="gAirCutterSliceSpriteTemplate";
    private const string RapidElevation="AnimTask_RapinSpinMonElevation",SpeedStretch="AnimTask_AttackerStretchAndDisappear",SpeedImpact="AnimTask_ExtremeSpeedImpact",SpeedReappear="AnimTask_ExtremeSpeedMonReappear",SpeedDustTask="AnimTask_SpeedDust",AirProjectile="AnimTask_AirCutterProjectile";
    private static bool DamageFamilyTask(string n)=>n is RapidElevation or SpeedStretch or SpeedImpact or SpeedReappear or SpeedDustTask or AirProjectile;
    private static bool DamageFamilySprite(string n)=>n is RevengeSmall or RevengeBig or PersistentImpact or EdgeImpact or RapidRing or AirSlice;
    private bool ValidateDamageFamilyCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string n=a.FirstOrDefault()??"";bool task=c.Op=="createvisualtask"&&DamageFamilyTask(n),sprite=c.Op=="createsprite"&&DamageFamilySprite(n);
        if(!task&&!sprite)return false;
        int width=n switch{RevengeSmall or RevengeBig=>5,PersistentImpact=>8,EdgeImpact or AirSlice=>7,RapidRing=>9,RapidElevation=>5,AirProjectile=>7,_=>2};
        if(a.Length!=width||Number(a[task?1:2]) is <0 or >255||sprite&&(Number(a[1]) is <0 or >1||Number(a[2])>127)||a.Skip(task?2:3).Any(v=>AnimationArgument(v) is <short.MinValue or >short.MaxValue))Fail(c,"Damage-family source operand width differs.");
        if(n==RapidElevation&&(Number(a[2]) is <0 or >1||Number(a[3]) is <1 or >64||Number(a[4]) is <0 or >1))Fail(c,"Rapid Spin scanline selector, speed or phase differs.");
        if(n==RapidRing&&(Number(a[3]) is <0 or >1||Number(a[5])==Number(a[6])||Number(a[8])==0||Math.Sign(Number(a[6])-Number(a[5]))!=Math.Sign(Number(a[8]))||Math.Abs(Number(a[5]))>128||Math.Abs(Number(a[6]))>128||Number(a[7]) is <0 or >255))Fail(c,"Rapid Spin sprite must cross its bounded endpoint.");
        if(n is PersistentImpact or EdgeImpact&&(Number(a[n==PersistentImpact?5:3]) is <0 or >1||Number(a[6]) is <0 or >3)||n==PersistentImpact&&Number(a[7]) is <1 or >32767)Fail(c,"Impact participant, affine variant or timer differs.");
        if(n==PersistentImpact&&Number(a[5])==1&&Number(a[1])!=1)Fail(c,"Persistent target impact requires its source-created target center.");
        if(n==AirSlice&&(Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >2))Fail(c,"Air Cutter slice selector differs.");
        // The earliest raw child lasts 1 initial + 9 acceleration + 31 tail callbacks.
        // With delay <=19 the third birth occurs at most 40 ticks after the first,
        // before that first death can decrement the shared emission/count register.
        if(n==AirProjectile&&(Math.Abs(Number(a[2]))>64||Math.Abs(Number(a[3]))>64||Number(a[4]) is <2 or >32766||Number(a[5]) is <0 or >19||Number(a[6]) is <0 or >255))Fail(c,"Air Cutter fixed-point speed, offsets or cadence differs.");
        if(_script.SineTable.Length!=320)Fail(c,"Damage-family source sine table is missing.");
        if(sprite)ValidateDamageFamilyRecipe(c,n);
        if(n==SpeedStretch&&(!_script.TaskAffinePrograms.TryGetValue("sStretchAttackerAffineAnimCmds",out var program)||!program.SequenceEqual(new SourceTaskAffineCommand[]{new("frame",96,-13,0,8),new("end")})))Fail(c,"Extreme Speed source stretch program differs.");
        if(n is SpeedDustTask or AirProjectile)
        {
            string child=n==SpeedDustTask?SpeedDust:AirWave;if(!_script.SpriteDependencies.Contains(child))Fail(c,"Task-owned damage child dependency missing.");ValidateDamageFamilyRecipe(c,child);
            if(n==AirProjectile&&!_script.SoundEffects.ContainsKey("SE_M_BLIZZARD2"))Fail(c,"Air Cutter callback sound is missing.");
        }
        if(_validatingInvokedProgram)
        {
            foreach(var b in _battlers.Values)ValidateReactionGeometry(c,b);
            if(!IsSourceBattlerPresent(_cue.Actor.Value)||!IsSourceBattlerPresent(_cue.Target.Value))Fail(c,"Damage animation requires present actor and target.");
            // Signed reciprocal wrap is source-defined; only a zero derived duration divides by zero.
            if(n==AirProjectile){var p=DamageAirDestination(this,Number(a[2]),Number(a[3]));int diff=Math.Abs(p.X-_battlers[_cue.Actor.Value].Base(_doubles).X);if(DamageQMul(diff,DamageQInv(Number(a[4])&~1))==0)Fail(c,"Air Cutter source reciprocal would divide by zero.");}
            ValidateDamageFamilyCapacity(c);
        }
        return true;
    }
    private void ValidateDamageFamilyCapacity(SourceAnimationCommand c)
    {
        var commands=OverlayInvocationCommands();
        int births=MaximumInvocationCost(x=>x.Op=="createsprite"?1:x.Op=="createvisualtask"?x.Args[0]==SpeedDustTask?24:x.Args[0]==AirProjectile?3:0:0);
        int matrices=MaximumInvocationCost(x=>x.Op=="createsprite"&&x.Args[0] is PersistentImpact or EdgeImpact or "gBasicHitSplatSpriteTemplate"?1:x.Op=="createvisualtask"&&x.Args[0]==SpeedStretch?1:0);
        int tasks=1+MaximumInvocationCost(x=>x.Op is "createvisualtask" or "createsoundtask" or "loopsewithpan" or "waitplaysewithpan" or "panse" or "fadetobg" or "restorebg" or "clearmonbg"?1:x.Op=="monbg"?(_doubles?2:1):0);
        if(22+births+_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length))>64||4+matrices+_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256)>32||tasks+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(c,"Damage-family closure exceeds native object, matrix or task capacity.");
        if(commands.Count(x=>x.Op=="createvisualtask"&&x.Args[0] is SpeedDustTask or AirProjectile)>1)Fail(c,"Concurrent raw emitters exceed the reviewed family ownership.");
        bool scanline=false;
        foreach(var x in commands){if(x.Op=="waitforvisualfinish")scanline=false;if(x.Op=="createvisualtask"&&x.Args[0]==RapidElevation){if(scanline)Fail(c,"Overlapping Rapid Spin scanline owners.");scanline=true;}}
    }
    private void ValidateDamageFamilyRecipe(SourceAnimationCommand c,string n)
    {
        if(!_script.SpriteRecipes.TryGetValue(n,out var r)){Fail(c,"Damage-family sprite recipe missing.");return;}
        var expected=n switch{RevengeSmall=>("purple-scratch","AnimRevengeScratch",32,32,false),RevengeBig=>("purple-swipe","AnimRevengeScratch",64,64,false),PersistentImpact=>("impact","AnimHitSplatPersistent",32,32,true),EdgeImpact=>("impact","AnimHitSplatOnMonEdge",32,32,true),RapidRing=>("rapid-spin","AnimRapidSpin",32,16,false),SpeedDust=>("speed-dust","AnimSpeedDust",16,16,false),AirWave=>("air-wave","AnimAirWaveProjectile",32,16,true),_=>("cut","AnimAirCutterSlice",32,32,true)};
        if((r.Asset,r.Callback,r.Width,r.Height,r.Blend)!=expected||r.AffineDoubleSize||r.Subsprites.Length!=0||r.FrameLoops.Any(f=>f.Length!=0))Fail(c,"Damage-family source shape, material or callback differs.");
        SourceSpriteAnimFrame F(int tile,int duration,bool h=false,bool v=false)=>new(tile,duration,h,v);
        SourceSpriteAnimFrame[][] frames=n switch{RevengeSmall=>[[F(0,4),F(16,4),F(32,4)],[F(0,4,false,true),F(16,4,false,true),F(32,4,false,true)],[F(0,4,true),F(16,4,true),F(32,4,true)]],RevengeBig=>[[F(0,6),F(64,6)],[F(0,6,true,true),F(64,6,true,true)],[F(0,6,true),F(64,6,true)]],RapidRing=>[[F(0,2),F(8,2),F(16,2)]],SpeedDust=>[[F(0,3),F(4,3),F(8,3),F(4,3),F(0,3)]],AirSlice=>[[F(0,5),F(16,5),F(32,5),F(48,5)]],_=>[[F(0,1)]]};
        int loop=n is RapidRing or AirWave or PersistentImpact or EdgeImpact?0:-1;
        if(r.Animations.Length!=frames.Length||!r.LoopStarts.SequenceEqual(Enumerable.Repeat(loop,frames.Length)))Fail(c,"Damage-family source frame control differs.");
        for(int i=0;i<frames.Length;i++)if(!r.Animations[i].SequenceEqual(frames[i]))Fail(c,"Damage-family source frames differ.");
        bool impact=n is PersistentImpact or EdgeImpact;if(r.AffineAnimations.Length!=(impact?4:0))Fail(c,"Damage-family affine table count differs.");
        if(impact)for(int i=0;i<4;i++){SourceSpriteAffineCommand hold=new("frame",0,0,0,8,0),end=new("end",0,0,0,0,0);SourceSpriteAffineCommand[] p=i==0?[hold,end]:[new("frame",i==1?216:i==2?176:128,i==1?216:i==2?176:128,0,0,0),hold,end];if(!r.AffineAnimations[i].SequenceEqual(p))Fail(c,"Impact affine table differs.");}
        // These callbacks never mutate an OBJ palette. Existing RGB materials
        // (Air Wave and Cut) retain their source colors without an indexed bank.
    }
    private bool TryCreateDamageFamilyTask(SourceAnimationCommand c)
    {
        if(!DamageFamilyTask(c.Args[0]))return false;
        if(c.Args[0] is SpeedDustTask or AirProjectile)
        {string child=c.Args[0]==SpeedDustTask?SpeedDust:AirWave;string tag="ANIM_TAG_"+_script.SpriteRecipes[child].Asset.ToUpperInvariant().Replace('-','_');if(!_loaded.Contains(tag))Fail(c,"Damage-family raw child material is not loaded.");}
        VisualTask task=c.Args[0] switch{RapidElevation=>new RapidElevationTask(this),SpeedStretch=>new SpeedStretchTask(this),SpeedImpact=>new SpeedImpactTask(this),SpeedReappear=>new SpeedReappearTask(this),SpeedDustTask=>new DamageDustTask(this),_=>new AirProjectileTask(this)};
        _tasks.Add(task);return true;
    }
    private bool TryCreateDamageFamilySprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(!DamageFamilySprite(c.Args[0]))return false;var r=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Damage-family material is not loaded.");
        _sprites.Add(new DamageFamilyVisual(slot,priority,r,this));return true;
    }
    private static short DamageShort(int n)=>unchecked((short)n);
    private static short DamageQMul(int x,int y)=>DamageShort(DamageShort(x)*DamageShort(y)/256);
    private static short DamageQInv(int y)=>DamageShort(65536/DamageShort(y));
    private static PixelPoint DamageAirDestination(SourceBattleAnimation o,int dx,int dy)
    {
        var target=o._cue.Target;var p=o._battlers[target.Value].Base(o._doubles);
        if(o._doubles&&o.IsSourceBattlerVisible(target.Partner.Value)){var q=o._battlers[target.Partner.Value].Base(true);p=new((p.X+q.X)/2,(p.Y+q.Y)/2);}
        int sign=target.IsPlayer?-1:1;return new(DamageShort(p.X+sign*dx),DamageShort(p.Y+sign*dy));
    }
    private sealed class RapidElevationTask:VisualTask
    {
        private readonly short[] _d=new short[16];
        public RapidElevationTask(SourceBattleAnimation o)
        {
            int slot=o._animationArgs[0]==0?o._cue.Actor.Value:o._cue.Target.Value,rank=o._battlers[slot].BackgroundRank,y=o.ScanlineBattlerY(slot),x=o.ScanlineBackgroundOffset(rank).X;
            _d[0]=_d[1]=_d[3]=DamageShort(y+36);_d[2]=DamageShort(Math.Max(0,y-33));_d[4]=8;_d[5]=o._animationArgs[1];_d[8]=DamageShort(x);_d[9]=DamageShort(x+240);_d[10]=o._animationArgs[2];_d[11]=_d[_d[10]==0?9:8];
            for(int i=_d[2];i<=_d[3];i++)Both(o,i,_d[_d[10]==0?8:9]);o.StartScanline(rank);
        }
        private static void Both(SourceBattleAnimation o,int row,int value)=>o.ScanlineBuffers[0][row]=o.ScanlineBuffers[1][row]=unchecked((ushort)value);
        public override void Step(SourceBattleAnimation o)
        {
            _d[0]=DamageShort(_d[0]-_d[5]);if(_d[0]<_d[2])_d[0]=_d[2];
            if(_d[4]==0){_d[1]=DamageShort(_d[1]-_d[5]);if(_d[1]<_d[2]){_d[1]=_d[2];_d[15]=1;}}else _d[4]--;
            if(++_d[6]>1){_d[6]=0;_d[7]=(short)(_d[7]==0?1:0);_d[12]=_d[_d[7]!=0?8:9];}
            for(int i=_d[0];i<_d[1];i++)Both(o,i,_d[12]);for(int i=_d[1];i<=_d[3];i++)Both(o,i,_d[11]);
            if(_d[15]!=0){if(_d[10]!=0)o.StopScanlineDeferred();Finished=true;}
        }
    }
    private sealed class SpeedStretchTask:VisualTask
    {
        private readonly int _slot;private readonly SourceTaskAffineProgram _program;
        public SpeedStretchTask(SourceBattleAnimation o){_slot=o._cue.Actor.Value;_program=new(o._script.TaskAffinePrograms["sStretchAttackerAffineAnimCmds"]);o.PrepareTaskAffine(_slot,_program);}
        public override void Step(SourceBattleAnimation o){if(!o.RunTaskAffine(_slot,_program)){o._offsets[_slot]=new(o._offsets[_slot].X,0);o.SetSourceBattlerSpriteInvisible(_slot,true);Finished=true;}}
    }
    private sealed class SpeedImpactTask:VisualTask
    {
        private readonly int _slot,_push,_return;private short _state,_timer,_toggle,_count,_remaining=3;
        public SpeedImpactTask(SourceBattleAnimation o){_slot=o._cue.Target.Value;_push=o._cue.Target.IsPlayer?8:-8;_return=o._cue.Target.IsPlayer?-1:1;}
        private void Add(SourceBattleAnimation o,int x)=>o._offsets[_slot]=new(DamageShort(o._offsets[_slot].X+x),o._offsets[_slot].Y);
        public override void Step(SourceBattleAnimation o)
        {
            switch(_state){case 0:Add(o,_push);_timer=_toggle=_count=0;_state++;break;case 1:if(++_timer>1){_timer=0;_toggle++;Add(o,(_toggle&1)!=0?6:-6);if(++_count>4){if((_toggle&1)!=0)Add(o,-6);_state++;}}break;case 2:if(--_remaining!=0)_state=0;else _state++;break;case 3:Add(o,_return);if(o._offsets[_slot].X==0)Finished=true;break;}
        }
    }
    private sealed class SpeedReappearTask:VisualTask
    {
        private readonly int _slot;private short _timer,_toggle,_count,_delay=1;
        public SpeedReappearTask(SourceBattleAnimation o){_slot=o._cue.Actor.Value;}
        public override void Step(SourceBattleAnimation o){if(++_timer<=_delay)return;_timer=0;o.SetSourceBattlerSpriteInvisible(_slot,(++_toggle&1)==0);if(++_count>=14&&++_delay>=2){o.SetSourceBattlerSpriteInvisible(_slot,false);Finished=true;}}
    }
    private sealed class DamageDustTask:VisualTask
    {
        private readonly short[] _d=new short[16];private readonly PixelPoint _position;private readonly SourceSpriteRecipe _recipe;
        public DamageDustTask(SourceBattleAnimation o){_d[1]=4;_position=o._battlers[o._cue.Actor.Value].Base(o._doubles);_recipe=o._script.SpriteRecipes[SpeedDust];}
        public bool Invisible=>_d[5]!=0;
        public void Release()=>_d[13]--;
        public override void Step(SourceBattleAnimation o)
        {
            switch(_d[8]){case 0:if(++_d[4]>1){_d[4]=0;_d[5]=(short)((_d[5]+1)&1);if(++_d[6]>20){if(_d[7]==0){_d[6]=0;_d[8]=1;}else _d[8]=2;}}break;case 1:_d[5]=0;if(++_d[4]>20){_d[7]=1;_d[8]=0;}break;case 2:_d[5]=1;break;}
            if(_d[0]==0){if(++_d[1]>4){_d[1]=0;int slot=o.RequireFreeSpriteSlot();PixelPoint[] offsets=[new(30,28),new(-20,24),new(16,26),new(-10,28)];o._sprites.Add(new DamageDustVisual(slot,_recipe,_position,offsets[_d[2]],this));o._oamY[slot]=0;_d[13]++;if(++_d[2]>3){_d[2]=0;if(++_d[3]>5)_d[0]++;}}}else if(_d[13]==0)Finished=true;
        }
    }
    private sealed class DamageDustVisual(int slot,SourceSpriteRecipe recipe,PixelPoint position,PixelPoint offset,DamageDustTask parent):VisualSprite(slot,0)
    {
        private readonly SourceSpriteFrameProgram _frames=new(recipe);private bool _invisible,_begin=true;
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o){_invisible=parent.Invisible;if(_frames.Ended){parent.Release();Finished=true;}}
        public override void AdvanceAnimation(SourceBattleAnimation o){if(_begin)_begin=false;else _frames.Step();}
        public override SourceVisualSpriteFrame Frame=>new(Slot,SpeedDust,position,offset,_frames.Frame.Tile,!_invisible,false,false,256,recipe.Asset,0){Width=16,Height=16,OamPriority=2};
    }
    private sealed class AirProjectileTask:VisualTask
    {
        internal readonly short[] Data=new short[16];private readonly SourceSpriteRecipe _recipe;private bool _waiting;
        public AirProjectileTask(SourceBattleAnimation o)
        {
            var a=o._animationArgs;if(o._cue.Target.IsPlayer){Data[4]=1;a[0]=DamageShort(-a[0]);a[1]=DamageShort(-a[1]);a[2]^=1;}
            var p=o._battlers[o._cue.Actor.Value].Base(o._doubles);var q=DamageAirDestination(o,o._cue.Target.IsPlayer?-a[0]:a[0],o._cue.Target.IsPlayer?-a[1]:a[1]);Data[9]=DamageShort(p.X);Data[10]=DamageShort(p.Y);Data[11]=DamageShort(q.X);Data[12]=DamageShort(q.Y);
            Data[5]=DamageQMul(Math.Abs(q.X-p.X),DamageQInv(a[2]&~1));Data[6]=DamageQMul(Data[5],128);Data[7]=a[2];Data[8]=DamageShort((DamageQMul(Math.Abs(q.Y-p.Y),DamageQInv(Data[5]))&~1)|(q.Y<p.Y?1:0));Data[3]=a[3];
            if((a[4]&128)!=0)a[4]^=128;int priority=o._battlers[o._cue.Target.Value].Subpriority;Data[2]=DamageShort(Math.Max(3,(int)DamageShort(priority+(a[4]>=64?a[4]-64:-a[4]))));_recipe=o._script.SpriteRecipes[AirWave];
        }
        public override void Step(SourceBattleAnimation o)
        {
            if(_waiting){if(Data[1]==0)Finished=true;return;}if(Data[0]--<=0){int slot=o.RequireFreeSpriteSlot();o._sprites.Add(new AirProjectileVisual(slot,Data[2]-Data[1],_recipe,this));o._oamY[slot]=0;Data[Data[1]+13]=DamageShort(slot);Data[0]=Data[3];Data[1]++;o._audio.Play(new("SE_M_BLIZZARD2",o.AdjustPanning(-63),o._frame));if(Data[1]>2)_waiting=true;}
        }
    }
    private sealed class AirProjectileVisual:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly AirProjectileTask _parent;private readonly short[] _d=new short[8];private PixelPoint _position,_offset;private int _phase;
        public AirProjectileVisual(int slot,int priority,SourceSpriteRecipe recipe,AirProjectileTask parent):base(slot,priority){_recipe=recipe;_parent=parent;var t=parent.Data;_position=new(t[9],t[10]);_d[0]=DamageShort(t[5]-t[6]);}
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,AirWave,_position,_offset,0,true,_parent.Data[4]!=0,true,256,_recipe.Asset,Subpriority){Width=32,Height=16,VFlip=_parent.Data[4]==1,OamPriority=2};
        private void UpdateOffset(){var t=_parent.Data;_offset=new(((ushort)_d[1]>>8)*((t[7]&1)!=0?-1:1),((ushort)_d[2]/256)*((t[8]&1)!=0?-1:1));}
        public override void Step(SourceBattleAnimation o)
        {
            var t=_parent.Data;
            if(_phase==2){if(_d[0]--<=0){t[1]--;Finished=true;}return;}
            if(_phase==1){int sign=_d[0]>t[5]?1:-1;_d[5]=DamageShort(_d[5]+sign*_d[3]);_d[6]=DamageShort(_d[6]+sign*_d[4]);_d[1]=DamageShort(_d[1]+_d[5]);_d[2]=DamageShort(_d[2]+_d[6]);UpdateOffset();if(_d[0]--<=0){_d[0]=30;_phase=2;}return;}
            _d[1]=DamageShort(_d[1]+(t[7]&~1));_d[2]=DamageShort(_d[2]+(t[8]&~1));UpdateOffset();
            if(_d[0]--<=0){_d[0]=8;t[5]=4;_position=new(DamageShort(_position.X+_offset.X),DamageShort(_position.Y+_offset.Y));_offset=default;short b=DamageShort(Math.Abs(t[11]-_position.X)<<8),c=DamageShort(Math.Abs(t[12]-_position.Y)<<8),a=DamageQInv(4096);_d[1]=_d[2]=_d[5]=_d[6]=0;_d[3]=DamageQMul(DamageQMul(b,a),DamageQInv(448));_d[4]=DamageQMul(DamageQMul(c,a),DamageQInv(448));_phase=1;}
        }
    }
    private sealed class DamageFamilyVisual:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;private readonly short[] _d=new short[8];private PixelPoint _position,_offset;private bool _hflip;private int _phase;
        public DamageFamilyVisual(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;var a=o._animationArgs;int animation=0;
            if(r.Template is RevengeSmall or RevengeBig){int b=a[2]==0?o._cue.Actor.Value:o._cue.Target.Value;var p=o._battlers[b].Base(o._doubles);_position=new(DamageShort(p.X+o.InitialXDirection()*a[0]),DamageShort(p.Y+a[1]));animation=o._cue.Actor.IsPlayer?0:1;}
            else if(r.Template==RapidRing){int b=a[0]==0?o._cue.Actor.Value:o._cue.Target.Value;var p=o._battlers[b].Base(o._doubles);_position=new(DamageShort(p.X+a[1]),p.Y);_offset=new(0,a[2]);_d[0]=(short)(a[2]>a[3]?1:0);_d[2]=a[4];_d[3]=a[5];_d[4]=a[3];}
            else if(r.Template==AirSlice){var target=o._cue.Target;int b=a[3]==1?target.Partner.Value:target.Value;var p=o._battlers[b].Base(o._doubles);if(a[3]==2&&o.IsSourceBattlerVisible(target.Partner.Value)){var q=o._battlers[target.Partner.Value].Base(o._doubles);p=new((p.X+q.X)/2,(p.Y+q.Y)/2);}_hflip=a[2]!=0;_position=new(DamageShort((byte)p.X+(_hflip?-a[0]:a[0])),DamageShort((byte)p.Y+(target.IsPlayer?8:0)+a[1]));_d[1]=(short)(_hflip?1024:-1024);_d[2]=1024;_d[5]=a[2];}
            else{int b=(r.Template==EdgeImpact?a[0]:a[2])==0?o._cue.Actor.Value:o._cue.Target.Value;if(r.Template==EdgeImpact){var p=o.BattlerPosition(b);_position=new(DamageShort(p.X+o._offsets[b].X),DamageShort(p.Y+o._offsets[b].Y));_offset=new(a[1],a[2]);}else{var p=o._battlers[b].Center(o._doubles,true);_position=new(DamageShort(p.X+o.InitialXDirection()*a[0]),DamageShort(p.Y+a[1]));_d[0]=a[4];}_affine=new(r.AffineAnimations[a[3]]);}
            _frames=new(r,animation);
        }
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine?.Step();}
        public override SourceVisualSpriteFrame Frame{get{var f=_frames.Frame;var m=_affine?.Matrix??new SourceAffineMatrix(256,0,0,256);return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,_hflip^f.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,OamPriority=2,Affine=_affine is not null,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void Step(SourceBattleAnimation o)
        {
            if(_recipe.Template is RevengeSmall or RevengeBig){if(_phase!=0)Finished=true;else if(_frames.Ended)_phase=1;return;}
            if(_recipe.Template==RapidRing){_d[1]=DamageShort((_d[1]+_d[2])&255);_offset=new(o._script.SineTable[_d[1]]>>4,DamageShort(_offset.Y+_d[3]));if(_d[0]!=0?_offset.Y<_d[4]:_offset.Y>_d[4])Finished=true;return;}
            if(_recipe.Template==AirSlice){if(_phase==2){Finished=true;return;}if(_phase==1){if(_d[0]>0)_d[0]--;else _phase=2;return;}_d[3]=DamageShort(_d[3]+_d[1]);_d[4]=DamageShort(_d[4]+_d[2]);_d[1]=DamageShort(_d[1]+(_d[5]==0?24:-24));_d[2]=DamageShort(_d[2]-24);_offset=new(_d[3]>>8,_d[4]>>8);if(++_d[0]==20){_d[0]=3;_phase=1;}return;}
            if(_phase==0){if(_affine!.Ended)_phase=1;return;}if(_recipe.Template==EdgeImpact){Finished=true;return;}if(_d[0]--<=0)Finished=true;
        }
    }
}
