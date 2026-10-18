namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string RayBounceTemplate="gConfuseRayBallBounceSpriteTemplate",RaySpiralTemplate="gConfuseRayBallSpiralSpriteTemplate",DevilTemplate="gDevilSpriteTemplate",PinkHeartTemplate="gPinkHeartSpriteTemplate",AngelTemplate="gAngelSpriteTemplate",HeartBurstTemplate="gRedHeartBurstSpriteTemplate",ForesightTemplate="gForesightMagnifyingGlassSpriteTemplate";
    private int _customPan;
    private bool ValidateConfusionKissOperand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op=="createvisualtask"&&name=="SoundTask_AdjustPanningVar")
        {
            if(a.Length!=6||Number(a[1]) is <0 or >255||SoundPan(a[2]) is <-64 or >63||SoundPan(a[3]) is <-64 or >63||Number(a[4]) is <-127 or >127||Number(a[5]) is <0 or >255)Fail(c,"Custom-panning task operands differ.");
            int from=AdjustPanning(SoundPan(a[2])),to=AdjustPanning(SoundPan(a[3])),step=Math.Abs(Number(a[4]));
            if(from!=to&&(step==0||to is not (-64 or 63)&&Math.Abs(to-from)%step!=0))Fail(c,"Custom pan must reach the source target before visual completion.");
            return true;
        }
        if(c.Op=="createvisualtask"&&name=="AnimTask_BlendColorCycleByTag")
        {
            if(a.Length!=8||Number(a[1]) is <0 or >255||!_script.SpriteTags.ContainsKey(a[2])||Number(a[3]) is <-13 or >63||Number(a[4]) is <1 or >32767||Number(a[5]) is <0 or >16||Number(a[6]) is <0 or >16||PaletteNumber(a[7]) is <0 or >65535)Fail(c,"Tag palette-cycle operands differ.");
            return true;
        }
        if(c.Op!="createsprite"||name is not (RayBounceTemplate or RaySpiralTemplate or DevilTemplate or PinkHeartTemplate or AngelTemplate or HeartBurstTemplate or ForesightTemplate))return false;
        var expected=name switch
        {
            RayBounceTemplate=>("yellow-ball","AnimConfuseRayBallBounce",16,6,false,true),
            RaySpiralTemplate=>("yellow-ball","AnimConfuseRayBallSpiral",16,5,true,false),
            DevilTemplate=>("devil","AnimDevil",32,5,false,false),
            PinkHeartTemplate=>("pink-heart","AnimPinkHeart",16,5,false,false),
            AngelTemplate=>("angel","AnimAngel",32,5,false,false),
            HeartBurstTemplate=>("red-heart","AnimParticleBurst",16,5,false,false),
            _=>("magnifying-glass","AnimForesightMagnifyingGlass",32,4,true,false)
        };
        if(a.Length!=expected.Item4||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||!_script.SpriteRecipes.TryGetValue(name,out var r))Fail(c,"Confusion/kiss source operands or recipe differ.");
        r=_script.SpriteRecipes[name];
        if(r.Asset!=expected.Item1||r.Callback!=expected.Item2||r.Width!=expected.Item3||r.Height!=r.Width||r.Blend!=expected.Item5||r.AffineDoubleSize!=expected.Item6||r.FrameLoops.Any(p=>p.Length!=0)||_script.SineTable.Length!=320)Fail(c,"Confusion/kiss source sprite shape differs.");
        SourceSpriteAnimFrame F(int tile,int duration)=>new(tile,duration,false,false);
        SourceSpriteAnimFrame[][] frames=name==DevilTemplate?[[F(0,3)],[F(16,3)]]:[[F(0,name==AngelTemplate?24:1)]];
        int[] loops=name==DevilTemplate?[0,0]:[name==AngelTemplate?-1:0];
        if(r.Animations.Length!=frames.Length||!r.LoopStarts.SequenceEqual(loops))Fail(c,"Confusion/kiss frame count differs.");
        for(int i=0;i<frames.Length;i++)if(!r.Animations[i].SequenceEqual(frames[i]))Fail(c,"Confusion/kiss frame program differs.");
        if(name==RayBounceTemplate)
        {
            SourceSpriteAffineCommand[] affine=[new("frame",30,30,10,5,0),new("frame",-30,-30,10,5,0),new("jump",0,0,0,0,0)];
            if(r.AffineAnimations.Length!=1||!r.AffineAnimations[0].SequenceEqual(affine)||Number(a[5]) is <1 or >32767||!_script.SoundEffects.ContainsKey("SE_M_CONFUSE_RAY"))Fail(c,"Confuse Ray speed, sound or affine program differs.");
            if(_validatingInvokedProgram)
            {var p=_battlers[_cue.Actor.Value].Center(_doubles,true);int start=unchecked((short)(p.X+InitialXDirection()*Number(a[3])));int dx=_battlers[_cue.Target.Value].Center(_doubles,true).X-start;int duration=(Math.Abs(dx)<<8)/Number(a[5]);if(duration is <1 or >32767)Fail(c,"Confuse Ray source duration is outside positive s16 range.");}
        }
        else if(r.AffineAnimations.Length!=0)Fail(c,"Unexpected confusion/kiss affine program.");
        if(name==ForesightTemplate)
        {if(Number(a[3]) is <0 or >1)Fail(c,"Foresight participant differs.");ValidateReactionGeometry(c,_battlers[Resolve(a[3]).Value]);}
        if(name==PinkHeartTemplate)RequireVisualSeed(c);
        return true;
    }
    private bool TryCreateConfusionKissTask(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(a[0]=="SoundTask_AdjustPanningVar")
        {var task=new CustomPanTask(AdjustPanning(SoundPan(a[2])),AdjustPanning(SoundPan(a[3])),Number(a[4]),Number(a[5]));task.Step(this);if(!task.Finished)_tasks.Add(task);return true;}
        if(a[0]=="AnimTask_BlendColorCycleByTag")
        {int slot=FindSpritePalette(_script.SpriteTags[a[2]]);if(slot==255)Fail(c,"Tag palette-cycle requires a loaded palette.");_tasks.Add(new BlendCycleTask(this,[0,Number(a[3]),Number(a[4]),Number(a[5]),Number(a[6]),PaletteNumber(a[7])],1u<<(16+slot)));return true;}
        return false;
    }
    private bool TryCreateConfusionKissSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (RayBounceTemplate or RaySpiralTemplate or DevilTemplate or PinkHeartTemplate or AngelTemplate or HeartBurstTemplate or ForesightTemplate))return false;
        var r=_script.SpriteRecipes[c.Args[0]];if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Confusion/kiss graphics are not loaded.");
        _sprites.Add(new ConfusionKissSprite(slot,priority,r,this));return true;
    }
    // Source visual task publishes only gAnimCustomPanning. It never changes
    // existing sound channels: callback PlaySE12WithPanning reads this value.
    private sealed class CustomPanTask:VisualTask
    {
        private readonly int _target,_increment,_delay;private short _pan;private ushort _counter;
        public CustomPanTask(int from,int to,int increment,int delay){_pan=(short)from;_target=to;_increment=from<to?Math.Abs(increment):from>to?-Math.Abs(increment):0;_delay=delay;}
        public override void Step(SourceBattleAnimation owner)
        {if(_counter++==_delay){_counter=0;_pan=(short)Math.Clamp((int)unchecked((short)(_pan+_increment)),-64,63);}owner._customPan=_pan;if(_pan==_target)Finished=true;}
    }
    private sealed class ConfusionKissSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;
        private readonly short[] _d=new short[8];private PhysicalLinear? _linear;private PixelPoint _position,_offset;private int _phase,_priority,_oamPriority=2,_participant;private bool _invisible,_flip;
        private static short S(int value)=>unchecked((short)value);
        private static int Sin(SourceBattleAnimation o,int angle,int amplitude)=>S(o._script.SineTable[angle]*amplitude>>8);
        private static int Cos(SourceBattleAnimation o,int angle,int amplitude)=>S(o._script.SineTable[angle+64]*amplitude>>8);
        public ConfusionKissSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_priority=priority;_frames=new(recipe);var a=o._animationArgs;var actor=o._battlers[o._cue.Actor.Value];var target=o._battlers[o._cue.Target.Value];_position=target.Center(o._doubles,true);
            switch(recipe.Template)
            {
                case RayBounceTemplate:
                    var p=actor.Center(o._doubles,true);_position=new(S(p.X+o.InitialXDirection()*a[0]),S(p.Y+a[1]));var end=target.Center(o._doubles,true);
                    int duration=(Math.Abs(end.X-_position.X)<<8)/a[2];_linear=new(end.X-_position.X,end.Y-_position.Y,duration);_affine=new(recipe.AffineAnimations[0]);_d[6]=16;o.SetAlphaBlend(0,63,16,0);break;
                case RaySpiralTemplate:
                    _position=new(S(_position.X+o.InitialXDirection()*a[0]),S(_position.Y+a[1]));AdvanceSpiral(o);break;
                case DevilTemplate:_position=new(S(_position.X+a[0]),S(_position.Y+a[1]));_priority=unchecked((byte)(target.Subpriority-1));_d[2]=1;AdvanceDevil(o);break;
                case PinkHeartTemplate:case HeartBurstTemplate:_d[1]=a[0];_d[2]=a[1];_d[0]=1;break;
                case AngelTemplate:_position=new(S(_position.X+a[0]),S(_position.Y+a[1]));AdvanceAngel(o);break;
                case ForesightTemplate:
                    _participant=a[0]==0?o._cue.Actor.Value:o._cue.Target.Value;
                    if(a[0]==0){var start=actor.Center(o._doubles,true);_position=new(S(start.X+o.InitialXDirection()*a[0]),S(start.Y+a[1]));}
                    _flip=(_participant&1)!=0;_oamPriority=o.BattlerBackgroundPriority(_participant);break;
            }
        }
        public override void Step(SourceBattleAnimation o)
        {
            switch(_recipe.Template)
            {
                case RayBounceTemplate:AdvanceBounce(o);break;
                case RaySpiralTemplate:AdvanceSpiral(o);break;
                case DevilTemplate:AdvanceDevil(o);break;
                case PinkHeartTemplate:case HeartBurstTemplate:AdvanceHeart(o);break;
                case AngelTemplate:AdvanceAngel(o);break;
                default:AdvanceGlass(o);break;
            }
        }
        private void UpdateRayBlend(SourceBattleAnimation o)
        {
            if(_d[6]>255){_d[6]++;if(_d[6]==0x10d)_d[6]=0;return;}
            short old=_d[7];_d[7]++;
            if((old&255)!=0)return;
            _d[7]=S(_d[7]&0xff00);_d[6]+= (short)((_d[7]&256)!=0?1:-1);o._alphaA=_d[6];o._alphaB=16-_d[6];
            if(_d[6] is 0 or 16)_d[7]^=256;if(_d[6]==0)_d[6]=256;
        }
        private void AdvanceBounce(SourceBattleAnimation o)
        {
            if(_phase==2){o.ClearAlphaBlend();Finished=true;return;}
            if(_phase==0)
            {UpdateRayBlend(o);if(_linear!.Step(out var p)){_phase=1;return;}_offset=p;}
            else{_linear!.StepContinuing(out var p);_offset=p;}
            _offset=new(S(_offset.X+Sin(o,_d[5],10)),S(_offset.Y+Cos(o,_d[5],15)));int old=_d[5];_d[5]=S((_d[5]+5)&255);
            if((old==0||old>196)&&_d[5]>0)o._audio.Play(new("SE_M_CONFUSE_RAY",_phase==0?o._customPan:0,o._frame){ApplyPanning=_phase==0});
            if(_phase==1){if(_d[6]==0){_invisible=true;_phase=2;}else UpdateRayBlend(o);}
        }
        private void AdvanceSpiral(SourceBattleAnimation o)
        {
            _offset=new(Sin(o,_d[0],32),Cos(o,_d[0],8));_oamPriority=unchecked((ushort)(_d[0]-65))<=130?2:1;_d[0]=S((_d[0]+19)&255);_d[2]=S(_d[2]+80);_offset=new(_offset.X,S(_offset.Y+(_d[2]>>8)));if(++_d[7]==61)Finished=true;
        }
        private void AdvanceHeart(SourceBattleAnimation o)
        {
            if(_phase==1)
            {_d[5]++;_offset=new(Sin(o,_d[3],5),_d[5]/2);_d[3]=S((_d[3]+3)&255);if(_d[5]>20)_invisible=(_d[5]&1)!=0;if(_d[5]>30)Finished=true;return;}
            _d[4]=S(_d[4]+_d[1]);_offset=new(_d[4]>>8,Sin(o,_d[3],_d[2]));_d[3]=S((_d[3]+3)&255);
            if(_recipe.Template==PinkHeartTemplate)
            {if(_d[3]>70){_phase=1;_position=new(S(_position.X+_offset.X),S(_position.Y+_offset.Y));_offset=default;_d[3]=S(o.NextVisualRandom()%180);}}
            else{if(_d[3]>100)_invisible=(_d[3]&1)!=0;if(_d[3]>120)Finished=true;}
        }
        private void AdvanceAngel(SourceBattleAnimation o)
        {
            _d[0]++;int angle=(_d[0]*10)&255;int y=_offset.Y;int x=Sin(o,angle,80)>>8;if(_d[0]<80)y=(_d[0]/2)+(Cos(o,angle,80)>>8);
            if(_d[0]>90){_d[2]++;x-=_d[2]/2;}_offset=new(S(x),S(y));if(_d[0]>100)Finished=true;
        }
        private void AdvanceDevil(SourceBattleAnimation o)
        {
            _d[0]=S(_d[0]+_d[2]);_d[1]=S((_d[0]*4)%256);if(_d[1]<0)_d[1]=0;_offset=new(Cos(o,_d[1],30-_d[0]/4),Sin(o,_d[1],10-_d[0]/8));
            if(_d[1]>128&&_d[2]>0)_d[2]=-1;if(_d[1]==0&&_d[2]<0)_d[2]=1;_d[3]++;_invisible=(_d[3]<10||_d[3]>80)&&(_d[0]&1)!=0;if(_d[3]>90)Finished=true;
        }
        private void AdvanceGlass(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:
                    var b=o._battlers[_participant];var center=b.Center(o._doubles,true);int halfW=b.SourceWidth/2,halfH=b.SourceHeight/2;
                    var destination=_d[6] switch{0 or 4=>new PixelPoint(center.X+halfW-4,center.Y+halfH-4),1=>new(center.X+halfW-4,center.Y-halfH+4),2=>new(center.X-halfW+4,center.Y+halfH-4),3=>new(center.X-halfW+4,center.Y-halfH-4),_=>center};
                    _linear=new(S(destination.X-_position.X),S(destination.Y-_position.Y),_d[6]==4?24:_d[6]==5?6:12);_phase=1;break;
                case 1:
                    if(!_linear!.Step(out var point)){_offset=point;break;}
                    if(_d[6]==5){_d[0]=0;_d[1]=16;_d[2]=0;_phase=3;break;}
                    _position=new(S(_position.X+_offset.X),S(_position.Y+_offset.Y));_offset=default;_d[0]=0;_phase=_d[6]==4?0:2;_d[6]++;break;
                case 2:if(++_d[0]==4)_phase=0;break;
                case 3:
                    if((_d[0]&1)==0)_d[1]--;else _d[2]++;o._alphaA=_d[1];o._alphaB=_d[2];if(++_d[0]==32){_invisible=true;_phase=4;}break;
                default:Finished=true;break;
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner){_frames.Step();_affine?.Step();}
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);return new(Slot,_recipe.Template,_position,_offset,f.Tile,!_invisible,_flip^f.HFlip,_recipe.Blend,256,_recipe.Asset,_priority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=_affine is not null,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D,OamPriority=_oamPriority};}}
    }
}
