namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string ChargingParticle="gElectricChargingParticlesSpriteTemplate",ChargeOrb="gGrowingChargeOrbSpriteTemplate",ElectricPuff="gElectricPuffSpriteTemplate",ShockOrb="gGrowingShockWaveOrbSpriteTemplate",ShockBolt="sShockWaveProgressingBoltSpriteTemplate",WispOrb="gWillOWispOrbSpriteTemplate",WispFire="gWillOWispFireSpriteTemplate";
    private bool ValidateElectricWispOperand(SourceAnimationCommand c)
    {
        var a=c.Args;string n=a.FirstOrDefault()??"";
        bool charging=n=="AnimTask_ElectricChargingParticles",bolt=n=="AnimTask_ShockWaveProgressingBolt",lightning=n=="AnimTask_ShockWaveLightning";
        if(c.Op=="createvisualtask"&&(charging||bolt||lightning))
        {
            if(a.Length!=(charging?6:2)||Number(a[1]) is <0 or >255)Fail(c,"Electric task operands differ.");
            if(charging&&(AnimationArgument(a[2]) is <0 or >1||Number(a[3]) is <1 or >32767||Number(a[4]) is <0 or >32766||Number(a[5]) is <1 or >32767))Fail(c,"Electric charging count, delay or tier threshold differs.");
            string child=charging?ChargingParticle:bolt?ShockBolt:"gLightningSpriteTemplate";
            if(!_script.SpriteDependencies.Contains(child))Fail(c,"Electric raw-child dependency missing.");
            if(_validatingInvokedProgram)ValidateElectricWispCapacity(c);
            ValidateElectricWispRecipe(c,child);if(bolt&&!_script.SoundEffects.ContainsKey("SE_M_THUNDERBOLT"))Fail(c,"Shock Wave callback sound missing.");return true;
        }
        if(c.Op!="createsprite"||n is not (ChargeOrb or ElectricPuff or ShockOrb or WispOrb or WispFire))return false;
        int count=n==ShockOrb?3:n is ChargeOrb or WispFire?4:6;
        if(a.Length!=count||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>AnimationArgument(v) is <short.MinValue or >short.MaxValue))Fail(c,"Electric/Wisp sprite operands differ.");
        if(n is ChargeOrb or ElectricPuff&&AnimationArgument(a[3]) is <0 or >1||n==WispOrb&&Number(a[5]) is <0 or >3||n==WispFire&&Number(a[3]) is <0 or >255)Fail(c,"Electric/Wisp selector differs.");
        if(n is WispOrb or WispFire&&_script.SineTable.Length!=320)Fail(c,"Wisp sine table missing.");
        if(n==WispOrb)
        {
            if(!_script.SoundEffects.ContainsKey("SE_M_FLAME_WHEEL"))Fail(c,"Wisp callback sound missing.");
            if(_validatingInvokedProgram)
            {
                var start=_battlers[_cue.Actor.Value].Base(_doubles);int x=unchecked((short)(start.X+InitialXDirection()*Number(a[3])));x=unchecked((short)(x+(_script.SineTable[124]*(_cue.Actor.IsPlayer?-4:4)>>8)));
                int duration=Math.Abs(_battlers[_cue.Target.Value].Center(_doubles,true).X-x);if(duration is <1 or >32767)Fail(c,"Wisp linear duration differs.");
            }
        }
        if(_validatingInvokedProgram)ValidateElectricWispCapacity(c);
        ValidateElectricWispRecipe(c,n);return true;
    }
    private void ValidateElectricWispCapacity(SourceAnimationCommand c)
    {
        var commands=OverlayInvocationCommands();
        string[] rawTasks=["AnimTask_ElectricChargingParticles","AnimTask_ShockWaveProgressingBolt","AnimTask_ShockWaveLightning"];
        if(commands.Any(x=>x.Op is "goto" or "jumpargeq" or "jumpifmoveturn" or "choosetwoturnanim" or "monbg_static" or "clearmonbg_static" or "fadetobg" or "restorebg" or "changebg"))Fail(c,"Electric/Wisp allocation needs its bounded linear closure.");
        if(commands.Any(x=>x.Op=="createsprite"&&x.Args.FirstOrDefault() is not(ChargeOrb or ElectricPuff or ShockOrb or WispOrb or WispFire or "gSimplePaletteBlendSpriteTemplate")||x.Op=="createvisualtask"&&!rawTasks.Contains(x.Args.FirstOrDefault())&&x.Args.FirstOrDefault() is not("AnimTask_ShakeMon" or "AnimTask_ShakeMon2" or "AnimTask_BlendBattleAnimPal" or "SoundTask_AdjustPanningVar")))Fail(c,"Electric/Wisp companion allocation exceeds the reviewed closure.");
        if(rawTasks.Any(n=>commands.Count(x=>x.Op=="createvisualtask"&&x.Args.FirstOrDefault()==n)>1)||commands.Count(x=>x.Op=="createsprite"&&x.Args.FirstOrDefault() is ChargeOrb or ShockOrb)>1||commands.Count(x=>x.Op=="monbg")>1||commands.Count(x=>x.Op=="clearmonbg")>1)Fail(c,"Electric/Wisp raw, affine or background owner is duplicated.");
        // Every new raw-child task drains its children before ending. Ignore all
        // other destruction and all delays; only visual waits discharge owners.
        // A charging child lasts at most 49 callbacks (four-frame prelude,
        // deferred callback/restart, then <=40 linear steps). Births are at least
        // delay+1 apart. Bolt children last 13 callbacks, with <=1 birth/tick.
        int live=0,affine=0,visualTasks=0,soundTasks=0,peak=0,affinePeak=0,taskPeak=0;
        foreach(var x in commands)
        {
            if(x.Op=="waitforvisualfinish"){live=affine=visualTasks=0;continue;}
            if(x.Op=="createsprite"){live++;if(x.Args.FirstOrDefault() is ChargeOrb or ShockOrb)affine++;}
            if(x.Op=="createvisualtask")
            {
                visualTasks++;string? name=x.Args.FirstOrDefault();
                if(name==rawTasks[0])
                {
                    if(x.Args.Length!=6||Number(x.Args[3]) is <1 or >32767||Number(x.Args[4]) is <0 or >32766)Fail(c,"Electric charging capacity operands differ.");
                    int spacing=Number(x.Args[4])+1;live+=Math.Min(Number(x.Args[3]),(49+spacing-1)/spacing);
                }
                else if(name==rawTasks[1])live+=13;
                else if(name==rawTasks[2])
                {int end=_battlers[_cue.Target.Value].Base(_doubles).Y+32,start=end;while(start>16)start-=32;live+=(end-start)/32+1;}
            }
            if(x.Op is "createsoundtask" or "loopsewithpan" or "waitplaysewithpan" or "panse" or "panse_adjustall" or "panse_adjustnone")soundTasks++;
            peak=Math.Max(peak,live);affinePeak=Math.Max(affinePeak,affine);taskPeak=Math.Max(taskPeak,visualTasks+soundTasks);
        }
        int retained=_spriteScene.Sprites.Sum(s=>Math.Max(1,s.Frame.Subsprites.Length)),retainedAffine=_spriteScene.Sprites.Count(s=>s.Frame.Affine||s.Frame.Scale!=256);
        if(22+peak+retained>64||4+affinePeak+retainedAffine>32)Fail(c,"Electric/Wisp closure exceeds source OAM or affine-matrix capacity.");
        // Include the script controller and monbg/clearmonbg controllers, using
        // the same conservative source task accounting as the shared runtime.
        int controllers=1+commands.Count(x=>x.Op=="monbg")*(_doubles?2:1)+commands.Count(x=>x.Op=="clearmonbg");
        if(controllers+taskPeak+_tasks.Count(t=>!t.Finished)+_soundTasks.Count>16)Fail(c,"Electric/Wisp closure exceeds source task capacity.");
    }
    private void ValidateElectricWispRecipe(SourceAnimationCommand c,string n)
    {
        if(!_script.SpriteRecipes.TryGetValue(n,out var r)){Fail(c,"Electric/Wisp recipe missing.");return;}
        var expected=n switch{ChargingParticle=>("electric-orbs","SpriteCallbackDummy",8),ChargeOrb=>("circle-of-light","AnimGrowingChargeOrb",64),ElectricPuff=>("electricity","AnimElectricPuff",32),ShockOrb=>("circle-of-light","AnimGrowingShockWaveOrb",64),ShockBolt=>("spark-thin","AnimShockWaveProgressingBolt",8),WispOrb=>("wisp-orb","AnimWillOWispOrb",16),WispFire=>("wisp-fire","AnimWillOWispFire",32),_=>("lightning","AnimLightning",32)};
        bool orb=n is ChargeOrb or ShockOrb;
        if(r.Asset!=expected.Item1||r.Callback!=expected.Item2||r.Width!=expected.Item3||r.Height!=r.Width||r.Blend!=orb||r.AffineDoubleSize||r.Subsprites.Length!=0||r.FrameLoops.Any(p=>p.Length!=0))Fail(c,"Electric/Wisp source shape differs.");
        SourceSpriteAnimFrame F(int tile,int duration)=>new(tile,duration,false,false);
        SourceSpriteAnimFrame[][] frames=n switch{ChargingParticle=>[[F(3,1),F(2,1),F(1,1),F(0,1)],[F(0,5),F(1,5),F(2,5),F(3,5)]],ElectricPuff=>[[F(0,3),F(16,3),F(32,3),F(48,3)]],WispOrb=>[[F(0,5),F(4,5),F(8,5),F(12,5)],[F(16,5)],[F(20,5)],[F(20,5)]],WispFire=>[[F(0,5),F(16,5),F(32,5),F(48,5)]],"gLightningSpriteTemplate"=>[[F(0,5),F(16,5),F(32,8),F(48,5),F(64,5)]],_=>[[F(0,1)]]};
        int[] loops=n==ChargingParticle?[-1,-1]:n==WispOrb?[0,-1,-1,-1]:[orb||n is ShockBolt or WispFire?0:-1];
        if(r.Animations.Length!=frames.Length||!r.LoopStarts.SequenceEqual(loops)||r.AffineAnimations.Length!=(orb?3:0))Fail(c,"Electric/Wisp frame table differs.");
        for(int i=0;i<frames.Length;i++)if(!r.Animations[i].SequenceEqual(frames[i]))Fail(c,"Electric/Wisp frame commands differ.");
        if(orb)
        {
            SourceSpriteAffineCommand Ff(int x,int duration)=>new("frame",x,x,0,duration,0);SourceSpriteAffineCommand End()=>new("end",0,0,0,0,0);
            SourceSpriteAffineCommand[][] programs=[[Ff(16,0),Ff(4,60),Ff(256,0),new("loop",0,0,0,0,0),Ff(-4,5),Ff(4,5),new("loop",0,0,0,0,10),End()],[Ff(16,0),Ff(8,30),Ff(256,0),Ff(-4,5),Ff(4,5),new("jump",0,0,0,0,3)],[Ff(16,0),Ff(8,30),Ff(-8,30),End()]];
            for(int i=0;i<3;i++)if(!r.AffineAnimations[i].SequenceEqual(programs[i]))Fail(c,"Growing electric orb affine commands differ.");
        }
    }
    private bool TryCreateElectricWispSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not(ChargeOrb or ElectricPuff or ShockOrb or WispOrb or WispFire))return false;
        var r=_script.SpriteRecipes[c.Args[0]];if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Electric/Wisp graphics not loaded.");
        _sprites.Add(new ElectricWispSprite(slot,priority,r,this));return true;
    }
    private bool TryCreateElectricWispTask(SourceAnimationCommand c)
    {
        int kind=c.Args[0] switch{"AnimTask_ElectricChargingParticles"=>0,"AnimTask_ShockWaveProgressingBolt"=>1,"AnimTask_ShockWaveLightning"=>2,_=>-1};if(kind<0)return false;
        if(!_loaded.Contains(kind==0?"ANIM_TAG_ELECTRIC_ORBS":kind==1?"ANIM_TAG_SPARK":"ANIM_TAG_LIGHTNING"))Fail(c,"Electric task graphics not loaded.");
        _tasks.Add(new ElectricWispTask(kind,this));return true;
    }
    private sealed class ElectricWispSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;private readonly short[] _d=new short[8];
        private PhysicalLinear? _linear;private PixelPoint _position,_offset;private int _priority=2,_phase;private bool _invisible;
        public ElectricWispSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;var a=o._animationArgs;int actor=o._cue.Actor.Value,target=o._cue.Target.Value;_position=o._battlers[target].Center(o._doubles,true);
            if(r.Template is ChargeOrb or ElectricPuff or ShockOrb)
            {_position=o._battlers[r.Template==ShockOrb||a[0]==0?actor:target].Center(o._doubles,true);if(r.Template==ElectricPuff)_offset=new(a[1],a[2]);else _affine=new(r.AffineAnimations[r.Template==ShockOrb?2:0]);}
            else if(r.Template==WispOrb)
            {var p=o._battlers[actor].Base(o._doubles);_position=new(S(p.X+o.InitialXDirection()*a[0]),S(p.Y+a[1]));_d[7]=a[2];_d[4]=(short)(o._cue.Actor.IsPlayer?-4:4);_d[0]=1;_priority=o.BattlerBackgroundPriority(target);}
            _frames=new(r,r.Template==WispOrb?a[2]:0);if(r.Template==WispFire){_d[1]=a[0];WispFireStep(o);}
        }
        private static short S(int x)=>unchecked((short)x);
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine?.Step();}
        public override void Step(SourceBattleAnimation o)
        {
            if(_recipe.Template is ChargeOrb or ElectricPuff)
            {if(_phase==1)Finished=true;else if(_affine?.Ended??_frames.Ended)_phase=1;return;}
            if(_recipe.Template==ShockOrb){if(_affine!.Ended)Finished=true;return;}
            if(_recipe.Template==WispFire){WispFireStep(o);return;}
            switch(_d[0])
            {
                case 1:_d[1]=S(_d[1]+192);_offset=new(o._script.SineTable[_d[2]]*_d[4]>>8,o._cue.Actor.IsPlayer?_d[1]>>8:-(_d[1]>>8));_d[2]=S((_d[2]+4)&255);_d[0]=2;break;
                case 2:
                    _offset=new(o._script.SineTable[_d[2]]*_d[4]>>8,_offset.Y);_d[2]=S((_d[2]+4)&255);
                    if(++_d[3]==31){_position=new(S(_position.X+_offset.X),S(_position.Y+_offset.Y));_offset=default;var end=o._battlers[o._cue.Target.Value].Center(o._doubles,true);_linear=new(end.X-_position.X,end.Y-_position.Y,Math.Abs(end.X-_position.X));_d[0]=3;}break;
                default:
                    if(_linear!.Step(out var offset)){Finished=true;return;}_offset=new(S(offset.X+(o._script.SineTable[_d[5]]*16>>8)),offset.Y);int prior=_d[5];_d[5]=S((_d[5]+4)&255);
                    if((prior==0||prior>196)&&_d[5]>0&&_d[7]==0)o._audio.Play(new("SE_M_FLAME_WHEEL",o._customPan,o._frame));break;
            }
        }
        private void WispFireStep(SourceBattleAnimation o)
        {_d[3]=S(_d[3]+384);_d[4]=S(_d[4]+160);_offset=new(o._script.SineTable[_d[1]]*(_d[3]>>8)>>8,o._script.SineTable[_d[1]+64]*(_d[4]>>8)>>8);_d[1]=S((_d[1]+7)&255);_priority=(o.BattlerBackgroundPriority(o._cue.Target.Value)+(_d[1]<64||_d[1]>195?0:1))&3;if(++_d[2]>20)_invisible=!_invisible;if(_d[2]==30)Finished=true;}
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);return new(Slot,_recipe.Template,_position,_offset,f.Tile,!_invisible,f.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,OamPriority=_priority,Affine=_affine is not null,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
    }
    private sealed class ElectricWispTask:VisualTask
    {
        private readonly int _kind;private readonly short[] _d=new short[16];
        private static readonly (int X,int Y)[] Offsets=[(58,-60),(-56,-36),(8,-56),(-16,56),(58,-10),(-58,10),(48,-18),(-8,56),(16,-56),(-58,-42),(58,30),(-48,40),(12,-48),(48,-12),(-56,18),(48,48)];
        public ElectricWispTask(int kind,SourceBattleAnimation o)
        {
            _kind=kind;var a=o._animationArgs;
            if(kind==0){var p=o._battlers[a[0]==0?o._cue.Actor.Value:o._cue.Target.Value].Center(o._doubles,true);_d[14]=(short)p.X;_d[15]=(short)p.Y;_d[6]=a[1];_d[11]=a[3];_d[13]=a[2];}
            else if(kind==1){var p=o._battlers[o._cue.Actor.Value].Center(o._doubles,true);_d[6]=(short)p.X;_d[7]=(short)p.Y;_d[8]=4;_d[10]=(short)o._battlers[o._cue.Target.Value].Center(o._doubles,true).X;_d[9]=(short)((_d[10]-_d[6])/5);_d[4]=7;_d[5]=-1;_d[11]=12;_d[12]=(short)o.AdjustPanning(-64);_d[13]=(short)o.AdjustPanning(63);_d[14]=_d[12];_d[15]=(short)((_d[13]-_d[12])/3);_d[0]=1;}
            else{var b=o._battlers[o._cue.Target.Value];_d[15]=(short)(b.Base(o._doubles).Y+32);_d[14]=_d[15];while(_d[14]>16)_d[14]-=32;_d[13]=(short)b.Center(o._doubles,true).X;_d[12]=(short)(b.Subpriority-2);_d[0]=1;}
        }
        public void Release(int counter)=>_d[counter]--;
        public override void Step(SourceBattleAnimation o)
        {
            if(_kind==0){Charging(o);return;}
            if(_kind==2)
            {
                if(_d[0]==1&&++_d[1]>1){_d[1]=0;Add(o,"gLightningSpriteTemplate",new(_d[13],_d[14]),_d[12],10,0,0);if(_d[14]>=_d[15])_d[0]++;else _d[14]+=32;}else if(_d[0]==2&&_d[10]==0)Finished=true;return;
            }
            switch(_d[0])
            {
                case 1:
                    if(++_d[1]>0){_d[1]=0;if(Bolt(o))_d[0]=(short)(_d[2]==5?3:2);}if(_d[11]!=0)_d[11]--;break;
                case 2:
                    if(_d[11]!=0)_d[11]--;if(++_d[1]>4){_d[1]=0;bool down=(_d[2]&1)!=0;_d[7]=(short)(down?4:68);_d[8]=(short)(down?68:4);_d[4]=(short)(down?0:7);_d[5]=(short)(down?1:-1);_d[0]=(short)(_d[11]!=0?4:1);}break;
                case 3:if(_d[3]==0)Finished=true;break;
                case 4:if(_d[11]!=0)_d[11]--;else _d[0]=1;break;
            }
        }
        private void Charging(SourceBattleAnimation o)
        {
            if(_d[6]!=0)
            {
                if(++_d[12]<=_d[13])return;_d[12]=0;var offset=Offsets[_d[9]];
                if(!Add(o,ChargingParticle,new(_d[14]+offset.X,_d[15]+offset.Y),2,7,40-_d[8]*5,0))return;
                if(++_d[9]>15)_d[9]=0;if(++_d[10]>=_d[11]){_d[10]=0;if(_d[8]<=5)_d[8]++;}_d[6]--;
            }
            else if(_d[7]==0)Finished=true;
        }
        private bool Bolt(SourceBattleAnimation o)
        {
            if(Add(o,ShockBolt,new(_d[6],_d[7]),35,3,0,_d[4])){_d[4]+=_d[5];if(_d[4]<0)_d[4]=7;if(_d[4]>7)_d[4]=0;}
            if(_d[4]==0&&_d[5]>0){_d[14]+=_d[15];o._audio.Play(new("SE_M_THUNDERBOLT",unchecked((sbyte)_d[14]),o._frame));}
            if(_d[5]<0&&_d[7]<=_d[8]||_d[5]>0&&_d[7]>=_d[8]){_d[2]++;_d[6]+=_d[9];return true;}_d[7]+=(short)(_d[5]*8);return false;
        }
        private bool Add(SourceBattleAnimation o,string name,PixelPoint p,int priority,int counter,int duration,int tile)
        {
            int slot=o.FindFreeSpriteSlot();if(slot==SourceSpriteRegistry<VisualSprite>.Capacity)return false;
            o._sprites.Add(new ElectricWispRaw(slot,unchecked((byte)priority),o._script.SpriteRecipes[name],this,p,counter,duration,tile,new(_d[14],_d[15])));_d[counter]++;return true;
        }
    }
    private sealed class ElectricWispRaw(int slot,int priority,SourceSpriteRecipe recipe,ElectricWispTask task,PixelPoint position,int counter,int duration,int tile,PixelPoint end):VisualSprite(slot,priority)
    {
        private SourceSpriteFrameProgram? _frames;private readonly PhysicalLinear? _linear=duration==0?null:new(end.X-position.X,end.Y-position.Y,duration);private PixelPoint _offset;private int _phase,_timer;private bool _restart;
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o)
        {
            bool done;
            if(recipe.Template==ChargingParticle)
            {
                if(_phase==0){if(_frames?.Ended==true)_phase=1;return;}
                if(_phase==1){_frames=new(recipe,1);_restart=true;_phase=2;return;}
                done=_linear!.Step(out var offset);if(!done)_offset=offset;
            }
            else done=recipe.Template==ShockBolt?++_timer>12:_frames?.Ended==true;
            if(done){task.Release(counter);Finished=true;}
        }
        public override void AdvanceAnimation(SourceBattleAnimation o)
        {if(_frames is null)_frames=new(recipe);else if(_restart)_restart=false;else _frames.Step();}
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames?.Frame??recipe.Animations[0][0];return new(Slot,recipe.Template,position,_offset,recipe.Template==ShockBolt?tile:f.Tile,true,f.HFlip,recipe.Blend,256,recipe.Asset,Subpriority){Width=recipe.Width,Height=recipe.Height,VFlip=f.VFlip};}}
    }
}
