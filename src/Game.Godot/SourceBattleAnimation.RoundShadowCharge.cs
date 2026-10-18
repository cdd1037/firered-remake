namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateRoundShadowChargeCommand(SourceAnimationCommand c)
    {
        var a=c.Args;string name=a.FirstOrDefault()??"";
        if(c.Op!="createsprite"||name is not ("gFlyBallUpSpriteTemplate" or "gFlyBallAttackSpriteTemplate" or "gBounceBallShrinkSpriteTemplate" or "gBounceBallLandSpriteTemplate" or "gDiveBallSpriteTemplate" or "gDiveWaterSplashSpriteTemplate"))return false;
        string callback=name switch{"gFlyBallUpSpriteTemplate"=>"AnimFlyBallUp","gFlyBallAttackSpriteTemplate"=>"AnimFlyBallAttack","gBounceBallShrinkSpriteTemplate"=>"AnimBounceBallShrink","gBounceBallLandSpriteTemplate"=>"AnimBounceBallLand","gDiveBallSpriteTemplate"=>"AnimDiveBall",_=>"AnimDiveWaterSplash"};
        int count=callback switch{"AnimFlyBallUp" or "AnimDiveBall"=>7,"AnimFlyBallAttack" or "AnimDiveWaterSplash"=>4,"AnimBounceBallShrink"=>5,_=>3};
        if(a.Length!=count||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(c,"Round-shadow sprite source operands differ.");
        if(callback is "AnimFlyBallUp" or "AnimDiveBall"&&(Number(a[3]) is <-127 or >127||Number(a[4]) is <-127 or >127||Number(a[5]) is <0 or >255||Number(a[6]) is <256 or >4096))Fail(c,"Round-shadow rise delay/acceleration exceeds the bounded source closure.");
        if(callback=="AnimDiveBall"&&_validatingInvokedProgram&&_battlers[_cue.Actor.Value].Center(_doubles,true).Y+Number(a[4])<=-32)Fail(c,"Dive rise must cross its source top boundary with a nonzero velocity.");
        if(callback=="AnimFlyBallAttack"&&Number(a[3]) is <1 or >255)Fail(c,"Fly attack requires a positive source translation duration.");
        if(callback=="AnimDiveWaterSplash"&&Number(a[3]) is <0 or >1)Fail(c,"Dive splash requires a source participant.");
        if(_validatingInvokedProgram&&!IsSourceBattlerPresent(_cue.Actor.Value))Fail(c,"Round-shadow callbacks require a present attacker sprite.");
        if(!_script.SpriteRecipes.TryGetValue(name,out var r)){Fail(c,"Round-shadow source recipe is missing.");return true;}
        bool splash=callback=="AnimDiveWaterSplash",attack=callback=="AnimFlyBallAttack";
        if(r.Callback!=callback||r.Asset!=(splash?"splash":"round-shadow")||r.Width!=64||r.Height!=64||r.Blend||r.AffineDoubleSize==attack||r.Subsprites.Length!=0||r.Animations.Length!=1||!r.Animations[0].SequenceEqual([new SourceSpriteAnimFrame(0,1,false,false)])||!r.LoopStarts.SequenceEqual([0])||r.FrameLoops.Any(p=>p.Length!=0))Fail(c,"Round-shadow source frame recipe differs.");
        SourceSpriteAffineCommand[] rise=[new("frame",16,256,0,0,0),new("frame",40,0,0,6,0),new("frame",0,-32,0,5,0),new("frame",-16,32,0,10,0),new("end",0,0,0,0,0)];
        SourceSpriteAffineCommand[][] expected=callback switch {
            "AnimFlyBallAttack"=>[[new("frame",0,0,50,1,0),new("end",0,0,0,0,0)],[new("frame",0,0,216,1,0),new("end",0,0,0,0,0)]],
            "AnimBounceBallShrink"=>[[new("frame",16,256,0,0,0),new("frame",40,0,0,6,0),new("frame",0,-32,0,5,0),new("frame",-20,0,0,7,0),new("frame",-20,-20,0,5,0),new("end",0,0,0,0,0)]],
            "AnimBounceBallLand"=>[[new("frame",160,256,0,0,0),new("end",0,0,0,0,0)]],
            "AnimDiveWaterSplash"=>[],_=>[rise]};
        if(r.AffineAnimations.Length!=expected.Length||expected.Where((p,i)=>!r.AffineAnimations[i].SequenceEqual(p)).Any())Fail(c,"Round-shadow source affine program differs.");
        return true;
    }
    private bool TryCreateRoundShadowChargeSprite(SourceAnimationCommand c,int slot,int priority)
    {
        if(c.Args[0] is not ("gFlyBallUpSpriteTemplate" or "gFlyBallAttackSpriteTemplate" or "gBounceBallShrinkSpriteTemplate" or "gBounceBallLandSpriteTemplate" or "gDiveBallSpriteTemplate" or "gDiveWaterSplashSpriteTemplate"))return false;
        var r=_script.SpriteRecipes[c.Args[0]];if(!_loaded.Contains("ANIM_TAG_"+r.Asset.ToUpperInvariant().Replace('-','_')))Fail(c,"Round-shadow graphics have not been loaded.");
        _sprites.Add(new RoundShadowChargeSprite(slot,priority,r,this));return true;
    }
    private sealed class RoundShadowChargeSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;private readonly SourceSpriteAffineProgram? _affine;
        private readonly short[] _data=new short[8];private readonly PhysicalLinear? _linear;private PixelPoint _position,_offset;private SourceAffineMatrix _matrix=new(256,0,0,256);private bool _invisible;private int _phase;
        public RoundShadowChargeSprite(int slot,int priority,SourceSpriteRecipe r,SourceBattleAnimation o):base(slot,priority)
        {
            _recipe=r;_frames=new(r);var a=o._animationArgs;var actor=o._battlers[o._cue.Actor.Value];var target=o._battlers[o._cue.Target.Value];
            _position=target.Center(o._doubles,true);
            if(r.AffineAnimations.Length!=0)_affine=new(r.AffineAnimations[r.Callback=="AnimFlyBallAttack"&&!o._cue.Actor.IsPlayer?1:0]);
            switch(r.Callback)
            {
                case "AnimFlyBallUp":case "AnimDiveBall":case "AnimBounceBallShrink":
                    var p=actor.Center(o._doubles,true);_position=new(unchecked((short)(p.X+o.InitialXDirection()*a[0])),unchecked((short)(p.Y+a[1])));
                    o.SetSourceBattlerSpriteInvisible(o._cue.Actor.Value,true);
                    if(r.Callback=="AnimBounceBallShrink")_data[0]=1;else{_data[0]=a[2];_data[1]=a[3];}break;
                case "AnimFlyBallAttack":
                    _position=new(o._cue.Actor.IsPlayer?-32:272,-32);var end=target.Center(o._doubles,true);_linear=new(end.X-_position.X,end.Y-_position.Y,a[0]);break;
                case "AnimBounceBallLand":
                    _position=new(_position.X,target.Base(o._doubles).Y);_offset=new(0,-_position.Y-32);_data[0]=1;break;
                case "AnimDiveWaterSplash":
                    _position=(a[0]==0?actor:target).Base(o._doubles);_data[0]=1;_data[1]=512;_matrix=SourceAffineMatrix.FromScaleRotation(256,_data[1],0);break;
            }
        }
        public override SourceVisualSpriteFrame Frame
        {get{var f=_frames.Frame;var m=_affine?.Matrix??_matrix;return new(Slot,_recipe.Template,_position,_offset,f.Tile,!_invisible&&!Finished,f.HFlip,false,256,_recipe.Asset,Subpriority){Width=64,Height=64,VFlip=f.VFlip,Affine=true,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}}
        public override void AdvanceAnimation(SourceBattleAnimation o){_frames.Step();_affine?.Step();}
        public override void Step(SourceBattleAnimation o)
        {
            var d=_data;
            switch(_recipe.Callback)
            {
                case "AnimFlyBallUp":
                    if(d[0]>0)--d[0];else{d[2]=unchecked((short)(d[2]+d[1]));_offset=new(_offset.X,unchecked((short)(_offset.Y-(d[2]>>8))));}
                    if(_position.Y+_offset.Y<-32)Finished=true;break;
                case "AnimFlyBallAttack":
                    _linear!.StepContinuing(out _offset);var state=_linear.Capture();
                    if((state.X>>8)>200){_position=new(unchecked((short)(_position.X+_offset.X)),_position.Y);_offset=new(0,_offset.Y);_linear.Restore((state.Remaining,state.X&255,state.Y));}
                    if(_position.X+_offset.X is <-32 or >272||_position.Y+_offset.Y>160){o.SetSourceBattlerSpriteInvisible(o._cue.Actor.Value,false);Finished=true;}break;
                case "AnimBounceBallShrink":if(_affine!.Ended)Finished=true;break;
                case "AnimBounceBallLand":
                    if(d[0]==1){_offset=new(0,unchecked((short)(_offset.Y+10)));if(_offset.Y>=0)d[0]++;}
                    else{_offset=new(0,unchecked((short)(_offset.Y-10)));if(_position.Y+_offset.Y<-32){o.SetSourceBattlerSpriteInvisible(o._cue.Actor.Value,false);Finished=true;}}break;
                case "AnimDiveBall":
                    if(_phase==0)
                    {
                        if(d[0]>0)--d[0];else if(_position.Y+_offset.Y>-32){d[2]=unchecked((short)(d[2]+d[1]));_offset=new(0,unchecked((short)(_offset.Y-(d[2]>>8))));}
                        else{_invisible=true;short before=d[3];d[3]=unchecked((short)(d[3]+1));if(before>20)_phase=1;}
                    }
                    else{_offset=new(0,unchecked((short)(_offset.Y+(d[2]>>8))));if(_position.Y+_offset.Y>-32)_invisible=false;if(_offset.Y>0)Finished=true;}break;
                case "AnimDiveWaterSplash":
                    d[1]=unchecked((short)(d[1]+(d[2]<=11?-40:40)));d[2]++;_matrix=SourceAffineMatrix.FromScaleRotation(256,d[1],0);
                    _offset=new(0,(64-Math.Min(128,15616/_matrix.D+1))/2);if(d[2]==24)Finished=true;break;
            }
        }
    }
}
