namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string GuardRingTemplate="gGuardRingSpriteTemplate",IngrainRootTemplate="gIngrainRootSpriteTemplate",IngrainOrbTemplate="gIngrainOrbSpriteTemplate",HydroChargeTemplate="gHydroCannonChargeSpriteTemplate",HydroBeamTemplate="gHydroCannonBeamSpriteTemplate",PoisonGasTemplate="gPoisonGasCloudSpriteTemplate";
    private bool ValidateWaterRootOperand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op!="createsprite"||name is not (GuardRingTemplate or IngrainRootTemplate or IngrainOrbTemplate or HydroChargeTemplate or HydroBeamTemplate or PoisonGasTemplate))return false;
        var expected=name switch
        {
            GuardRingTemplate=>("guard-ring","AnimGuardRing",64,32,3,true,true),
            IngrainRootTemplate=>("roots","AnimIngrainRoot",32,32,8,false,false),
            IngrainOrbTemplate=>("orbs","AnimIngrainOrb",8,8,8,false,false),
            HydroChargeTemplate=>("water-orb","AnimHydroCannonCharge",16,16,3,true,true),
            HydroBeamTemplate=>("water-orb","AnimHydroCannonBeam",16,16,9,true,true),
            _=>("purple-gas-cloud","InitPoisonGasCloudAnim",32,16,11,true,false)
        };
        if(a.Length!=expected.Item5||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue)||!_script.SpriteRecipes.ContainsKey(name))Fail(c,"Water/root source operands or recipe differ.");
        var r=_script.SpriteRecipes[name];
        if(r.Asset!=expected.Item1||r.Callback!=expected.Item2||r.Width!=expected.Item3||r.Height!=expected.Item4||r.Blend!=expected.Item6||r.AffineDoubleSize!=expected.Item7||r.FrameLoops.Any(p=>p.Length!=0))Fail(c,"Water/root source template differs.");
        SourceSpriteAnimFrame F(int tile,int duration,bool flip=false)=>new(tile,duration,flip,false);
        SourceSpriteAffineCommand A(int x,int y,int rotation=0,int duration=0)=>new("frame",x,y,rotation,duration,0);
        SourceSpriteAffineCommand end=new("end",0,0,0,0,0);
        SourceSpriteAnimFrame[][] frames;int[] loops;SourceSpriteAffineCommand[][] affine;
        switch(name)
        {
            case GuardRingTemplate:frames=[[F(0,1)]];loops=[0];affine=[[A(256,256),end],[A(512,256),end]];break;
            case IngrainRootTemplate:
                frames=[[F(0,7),F(16,7),F(32,7),F(48,7)],[F(0,7,true),F(16,7,true),F(32,7,true),F(48,7,true)],[F(0,7),F(16,7),F(32,7)],[F(0,7,true),F(16,7,true),F(32,7,true)]];loops=[-1,-1,-1,-1];affine=[];
                if(Number(a[6]) is <0 or >3||Number(a[7]) is <1 or >255)Fail(c,"Ingrain root frame or duration differs.");break;
            case IngrainOrbTemplate:frames=[[F(3,3),F(0,5)]];loops=[0];affine=[];if(Number(a[7]) is <1 or >255||_script.SineTable.Length!=320)Fail(c,"Ingrain orb lifetime or sine differs.");break;
            case HydroChargeTemplate:frames=[[F(0,1),F(4,1),F(8,1),F(12,1)]];loops=[0];affine=[[A(3,3,10,50),A(0,0,0,10),A(-20,-20,246,20),end]];break;
            case HydroBeamTemplate:
                frames=[[F(0,1),F(4,1),F(8,1),F(12,1)]];loops=[0];affine=[[A(336,336),end]];
                if(Number(a[7]) is <1 or >255||Number(a[8]) is not (0 or 1 or 256 or 257))Fail(c,"Hydro beam duration or coordinate modes differ.");break;
            default:
                frames=[[F(0,8),F(8,8)]];loops=[0];affine=[];
                if(Number(a[3]) is <1 or >255||Number(a[10]) is <0 or >1||_script.SineTable.Length!=320)Fail(c,"Poison cloud lifetime or coordinate mode differs.");break;
        }
        if(r.Animations.Length!=frames.Length||!r.LoopStarts.SequenceEqual(loops)||r.AffineAnimations.Length!=affine.Length)Fail(c,"Water/root source animation counts differ.");
        for(int i=0;i<frames.Length;i++)if(!r.Animations[i].SequenceEqual(frames[i]))Fail(c,"Water/root source frame program differs.");
        for(int i=0;i<affine.Length;i++)if(!r.AffineAnimations[i].SequenceEqual(affine[i]))Fail(c,"Water/root source affine program differs.");
        return true;
    }
    private bool TryCreateWaterRootSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not (GuardRingTemplate or IngrainRootTemplate or IngrainOrbTemplate or HydroChargeTemplate or HydroBeamTemplate or PoisonGasTemplate))return false;
        var r=_script.SpriteRecipes[c.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Water/root graphics have not been loaded.");
        _sprites.Add(new WaterRootSprite(slot,priority,r,this));return true;
    }
    private sealed class WaterRootSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;
        private PhysicalLinear? _linear;private PixelPoint _position,_offset;private int _priority,_oamPriority=2,_phase,_angle;
        private short _ticks,_duration,_dx,_amplitude;private readonly bool _targetPlayer;private readonly int _cloudPriority;private bool _invisible;
        public WaterRootSprite(int slot,int priority,SourceSpriteRecipe recipe,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=recipe;_priority=priority;var a=o._animationArgs;var actor=o._battlers[o._cue.Actor.Value];var target=o._battlers[o._cue.Target.Value];_targetPlayer=target.Slot.IsPlayer;
            _frames=new(recipe,recipe.Template==IngrainRootTemplate?a[3]:0);
            var b=actor.Base(o._doubles);var p=actor.Center(o._doubles,true);
            switch(recipe.Template)
            {
                case GuardRingTemplate:
                    bool both=o._doubles&&o.IsSourceBattlerVisible(actor.Slot.Partner.Value);
                    if(both){var partner=o._battlers[actor.Slot.Partner.Value].Base(true);b=new((b.X+partner.X)/2,(b.Y+partner.Y)/2);}
                    _position=new(b.X,unchecked((short)(b.Y+40)));_linear=new(0,-72,13);_affine=new(recipe.AffineAnimations[both?1:0]);break;
                case IngrainRootTemplate:
                    _position=new(p.X,b.Y);_offset=new(a[0],a[1]);_priority=unchecked((byte)(a[2]+30));_duration=a[4];_ticks=1;
                    // Retain the source's unusual addition; do not clamp or reflect it.
                    if(_position.Y+_offset.Y>120)_position=new(_position.X,unchecked((short)(_position.Y+_offset.Y+_position.Y-120)));break;
                case IngrainOrbTemplate:
                    _position=new(unchecked((short)(p.X+a[0])),unchecked((short)(b.Y+a[1])));_dx=a[2];_amplitude=a[3];_duration=a[4];AdvanceOrb(o);break;
                case HydroChargeTemplate:
                    _position=b;_offset=new(actor.Slot.IsPlayer?10:-10,-10);_priority=unchecked((byte)(actor.Subpriority+(actor.Slot.IsPlayer?2:-2)));_affine=new(recipe.AffineAnimations[0]);break;
                case HydroBeamTemplate:
                    if(actor.Slot.IsPlayer==target.Slot.IsPlayer){a[0]=unchecked((short)-a[0]);if(actor.Slot.Value is 0 or 1)a[0]=unchecked((short)-a[0]);}
                    var start=(a[5]&0xff00)==0?p:b;
                    _position=new(unchecked((short)(start.X+o.InitialXDirection()*a[0])),unchecked((short)(start.Y+a[1])));
                    if(!actor.Slot.IsPlayer)a[2]=unchecked((short)-a[2]);
                    var finish=(a[5]&255)==0?target.Center(o._doubles,true):target.Base(o._doubles);
                    _linear=new(unchecked((short)(finish.X+a[2]))-_position.X,unchecked((short)(finish.Y+a[3]))-_position.Y,a[4]);_affine=new(recipe.AffineAnimations[0]);break;
                default:
                    _duration=a[0];_position=p;_cloudPriority=o.BattlerBackgroundPriority(o._cue.Target.Value);
                    if(_targetPlayer){a[1]=unchecked((short)-a[1]);a[3]=unchecked((short)-a[3]);if(p.X<target.Center(o._doubles,true).X&&actor.Slot.IsPlayer)_priority=unchecked((byte)(target.Subpriority+1));}
                    var endPoint=a[7]!=0?target.Center(o._doubles,true):target.Base(o._doubles);
                    _linear=new(unchecked((short)(endPoint.X+a[3]))-unchecked((short)(p.X+a[1])),unchecked((short)(endPoint.Y+a[4]))-unchecked((short)(p.Y+a[2])),_duration);break;
            }
        }
        private void AdvanceOrb(SourceBattleAnimation o)
        {_ticks=unchecked((short)(_ticks+1));_offset=new(unchecked((short)(_dx*_ticks)),unchecked((short)((o._script.SineTable[(_ticks*20)&255]*_amplitude)>>8)));if(_ticks>_duration)Finished=true;}
        public override void Step(SourceBattleAnimation o)
        {
            switch(_recipe.Template)
            {
                case GuardRingTemplate:case HydroBeamTemplate:
                    if(_phase==1){Finished=true;return;}if(_linear!.Step(out var point))_phase=1;else _offset=point;break;
                case IngrainRootTemplate:
                    _ticks=unchecked((short)(_ticks+1));if(_ticks>_duration-10)_invisible=(_ticks&1)!=0;if(_ticks>_duration)Finished=true;break;
                case IngrainOrbTemplate:AdvanceOrb(o);break;
                case HydroChargeTemplate:if(_affine!.Ended)Finished=true;break;
                default:AdvancePoison(o);break;
            }
        }
        private void AdvancePoison(SourceBattleAnimation o)
        {
            var sine=o._script.SineTable;var target=o._battlers[o._cue.Target.Value];
            switch(_phase)
            {
                case 0:
                    if(!_linear!.Step(out var point))_offset=point;
                    _offset=new(unchecked((short)(_offset.X+(sine[_angle]>>4))),_offset.Y);_angle=(_angle+(_targetPlayer?-8:8))&255;
                    if(--_duration<=0)
                    {
                        _duration=80;_position=new(target.Base(o._doubles).X,unchecked((short)(_position.Y+_offset.Y)));_linear=new(0,29,80);_angle=_targetPlayer?80:204;
                        _offset=new(sine[_angle]>>3,0);_angle=(_angle+2)&255;_phase=1;
                    }
                    break;
                case 1:
                    if(!_linear!.Step(out var orbit))_offset=orbit;
                    _offset=new(unchecked((short)(_offset.X+(sine[_angle]>>3))),unchecked((short)(_offset.Y+((sine[_angle+64]*-3)>>8))));
                    _oamPriority=(_cloudPriority+((ushort)(_angle-64)<=127?0:1))&3;_angle=(_angle+4)&255;
                    if(--_duration<=0)
                    {
                        _position=new(unchecked((short)(_position.X+_offset.X)),unchecked((short)(_position.Y+_offset.Y)));_offset=default;
                        int destination=_targetPlayer?-16:256;int duration=(Math.Abs(destination-_position.X)<<8)/0x300;
                        _linear=new(destination-_position.X,4,duration);_phase=2;
                    }
                    break;
                default:if(_linear!.Step(out var exit))Finished=true;else _offset=exit;break;
            }
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner){_frames.Step();_affine?.Step();}
        public override SourceVisualSpriteFrame Frame
        {
            get{var f=_frames.Frame;var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);return new(Slot,_recipe.Template,_position,_offset,f.Tile,!_invisible,f.HFlip,_recipe.Blend,256,_recipe.Asset,_priority){Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=_affine is not null,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D,OamPriority=_oamPriority};}
        }
    }
}
