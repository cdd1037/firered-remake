namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateImpactAdapterOperand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createsprite"||a.FirstOrDefault() is not ("gDizzyPunchDuckSpriteTemplate" or "gConstrictBindingSpriteTemplate" or "gKnockOffStrikeSpriteTemplate" or "gNeedleArmSpikeSpriteTemplate" or "gFrenzyPlantRootSpriteTemplate"))return false;
        var (callback,asset,width,height,count)=a[0] switch
        {
            "gDizzyPunchDuckSpriteTemplate"=>("AnimDizzyPunchDuck","duck",16,16,7),
            "gConstrictBindingSpriteTemplate"=>("AnimConstrictBinding","tendrils",64,32,7),
            "gKnockOffStrikeSpriteTemplate"=>("AnimKnockOffStrike","slam-hit-2",64,64,5),
            "gNeedleArmSpikeSpriteTemplate"=>("AnimNeedleArmSpike","green-spike",16,16,8),
            _=>("AnimFrenzyPlantRoot","roots",32,32,9)
        };
        if(a.Length!=count||!_script.SpriteRecipes.ContainsKey(a[0]))Fail(command,"Impact adapter recipe or operand count differs.");
        var recipe=_script.SpriteRecipes[a[0]];
        if(recipe.Callback!=callback||recipe.Asset!=asset||recipe.Width!=width||recipe.Height!=height||recipe.Blend||recipe.AffineDoubleSize||recipe.FrameLoops.Any(p=>p.Length!=0))Fail(command,"Impact adapter source template differs.");
        if(Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||a.Skip(3).Any(v=>Number(v) is <short.MinValue or >short.MaxValue))Fail(command,"Impact adapter source-width operands differ.");
        SourceSpriteAnimFrame F(int tile,int duration,bool flip=false)=>new(tile,duration,flip,false);
        SourceSpriteAffineCommand A(int x,int y,int rotation=0,int duration=0)=>new("frame",x,y,rotation,duration,0);
        SourceSpriteAffineCommand end=new("end",0,0,0,0,0);
        SourceSpriteAnimFrame[][] frames;SourceSpriteAffineCommand[][] affine;int[] loops;
        switch(callback)
        {
            case "AnimDizzyPunchDuck":
                frames=[[F(0,1)]];loops=[0];affine=[];
                if(_script.SineTable.Length!=320)Fail(command,"Dizzy Punch requires the source sine table.");
                break;
            case "AnimConstrictBinding":
                frames=[[F(0,4),F(32,4),F(64,4),F(96,4)],[F(0,4,true),F(32,4,true),F(64,4,true),F(96,4,true)]];loops=[-1,-1];
                affine=[[A(256,256),A(-11,0,0,6),A(11,0,0,6),end],[A(-256,256),A(11,0,0,6),A(-11,0,0,6),end]];
                if(Number(a[5]) is <0 or >1||Number(a[6]) is <1 or >255)Fail(command,"Constrict affine selector or squeeze count differs.");
                break;
            case "AnimKnockOffStrike":
                frames=[[F(0,4),F(64,4)]];loops=[-1];affine=[[A(256,256),A(0,0,252,8),end],[A(-256,256),A(0,0,4,8),end]];
                if(_script.SineTable.Length!=320)Fail(command,"Knock Off requires the source sine table.");
                break;
            case "AnimNeedleArmSpike":
                frames=[[F(0,1)]];loops=[0];affine=[];
                if(Number(a[3]) is <0 or >1||Number(a[4]) is <0 or >1||Number(a[7]) is <0 or >255)Fail(command,"Needle Arm selector or duration differs.");
                break;
            default:
                frames=[[F(0,7),F(16,7),F(32,7),F(48,7)],[F(0,7,true),F(16,7,true),F(32,7,true),F(48,7,true)],
                    [F(0,7),F(16,7),F(32,7)],[F(0,7,true),F(16,7,true),F(32,7,true)]];loops=[-1,-1,-1,-1];affine=[];
                if(Number(a[7]) is <0 or >3||Number(a[8]) is <0 or >255)Fail(command,"Frenzy Plant frame selector or lifetime differs.");
                break;
        }
        if(recipe.Animations.Length!=frames.Length||!recipe.LoopStarts.SequenceEqual(loops)||recipe.AffineAnimations.Length!=affine.Length)Fail(command,"Impact source frame/affine tables differ.");
        for(int i=0;i<frames.Length;i++)if(!recipe.Animations[i].SequenceEqual(frames[i]))Fail(command,"Impact source frame program differs.");
        for(int i=0;i<affine.Length;i++)if(!recipe.AffineAnimations[i].SequenceEqual(affine[i]))Fail(command,"Impact source affine program differs.");
        return true;
    }
    private bool TryCreateImpactAdapterSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0] is not ("gDizzyPunchDuckSpriteTemplate" or "gConstrictBindingSpriteTemplate" or "gKnockOffStrikeSpriteTemplate" or "gNeedleArmSpikeSpriteTemplate" or "gFrenzyPlantRootSpriteTemplate"))return false;
        var recipe=_script.SpriteRecipes[command.Args[0]];
        if(!_loaded.Contains("ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_')))Fail(command,"Impact adapter graphics have not been loaded.");
        // The original zero-duration Needle Arm initializer destroys itself in
        // CreateSpriteAndAnimate, before it can contribute to the next OAM frame.
        if(recipe.Callback=="AnimNeedleArmSpike"&&_animationArgs[4]==0)return true;
        _sprites.Add(new ImpactAdapterSprite(slot,priority,recipe,Resolve(command.Args[1]),this));return true;
    }
    private sealed class ImpactAdapterSprite:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;private readonly SourceSpriteFrameProgram _frames;
        private SourceSpriteAffineProgram? _affine;private SourceAffineMatrix _matrix=SourceAffineMatrix.FromScaleRotation(256,256,0);
        private PixelPoint _position,_offset;private short _x,_y,_vx,_vy,_remaining;private int _angle,_ticks,_affineIndex,_priority;
        private bool _invisible,_paused,_restart;
        public ImpactAdapterSprite(int slot,int priority,SourceSpriteRecipe recipe,AnimationSlot initialBattler,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_priority=priority;var a=owner._animationArgs;_frames=new(recipe,recipe.Callback=="AnimFrenzyPlantRoot"?a[4]:0);
            var actor=owner._battlers[owner._cue.Actor.Value].Center(owner._doubles,true);var target=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            switch(recipe.Callback)
            {
                case "AnimDizzyPunchDuck":
                    _position=new(unchecked((short)(target.X+owner.InitialXDirection()*a[0])),unchecked((short)(target.Y+a[1])));_vx=a[2];_vy=a[3];break;
                case "AnimConstrictBinding":
                    var center=owner._battlers[owner._cue.Target.Value].Base(owner._doubles);
                    _position=new(unchecked((short)(center.X+owner.InitialXDirection()*a[0])),unchecked((short)(center.Y+a[1])));
                    _affineIndex=a[2];_affine=new(recipe.AffineAnimations[_affineIndex]);_paused=true;_remaining=a[3];break;
                case "AnimKnockOffStrike":
                    var initial=owner._battlers[initialBattler.Value].Center(owner._doubles,true);bool player=owner._cue.Target.IsPlayer;
                    _position=new(unchecked((short)(initial.X+(player?-a[0]:a[0]))),unchecked((short)(initial.Y+a[1])));
                    _vx=(short)(player?-11:11);_angle=192;_affine=new(recipe.AffineAnimations[player?1:0]);break;
                case "AnimNeedleArmSpike":
                    var origin=a[0]==0?actor:target;int endX,endY;
                    if(a[1]==0){_position=new(unchecked((short)(origin.X+a[2])),unchecked((short)(origin.Y+a[3])));endX=origin.X;endY=origin.Y;}
                    else{_position=origin;endX=unchecked((short)(origin.X+a[2]));endY=unchecked((short)(origin.Y+a[3]));}
                    _x=unchecked((short)((ushort)_position.X*16));_y=unchecked((short)((ushort)_position.Y*16));_remaining=a[4];
                    _vx=unchecked((short)((endX-_position.X)*16/_remaining));_vy=unchecked((short)((endY-_position.Y)*16/_remaining));
                    _matrix=SourceAffineMatrix.FromScaleRotation(256,256,SourceBiosAngles.ArcTan2Neg(unchecked((short)(endX-(ushort)_position.X)),unchecked((short)(endY-(ushort)_position.Y))));break;
                case "AnimFrenzyPlantRoot":
                    _position=new(unchecked((short)(actor.X+(target.X-actor.X)*a[0]/100)),unchecked((short)(actor.Y+(target.Y-actor.Y)*a[0]/100)));
                    _offset=new(a[1],a[2]);_priority=unchecked((byte)(a[3]+30));_remaining=a[5];break;
            }
        }
        public override SourceVisualSpriteFrame Frame
        {
            get{var f=_frames.Frame;var m=_affine?.Matrix??_matrix;return new(Slot,_recipe.Template,_position,_offset,f.Tile,!_invisible,f.HFlip,false,256,_recipe.Asset,_priority)
                {Width=_recipe.Width,Height=_recipe.Height,VFlip=f.VFlip,Affine=_recipe.Callback is "AnimConstrictBinding" or "AnimKnockOffStrike" or "AnimNeedleArmSpike",MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)
        {
            _frames.Step();
            // StartSpriteAffineAnim's BeginAffineAnim applies the first absolute
            // frame on the restart tick even when paused. The constructor already
            // applied it; do not also advance to the first relative command.
            if(_restart){_restart=false;return;}if(!_paused)_affine?.Step();
        }
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_recipe.Callback)
            {
                case "AnimDizzyPunchDuck":
                    _x=unchecked((short)(_x+_vx));_offset=new(_x>>8,(_vy*owner._script.SineTable[_angle])>>8);_angle=(_angle+3)&255;
                    if(_angle>100)_invisible=(_angle&1)!=0;if(_angle>120)Finished=true;break;
                case "AnimConstrictBinding":
                    if(_paused){if(owner._animationArgs[7]==-1)_paused=false;break;}
                    // Source data[0..2] counts a squeeze but never writes a
                    // battler transform. Only the sprite affine program is visible.
                    if(_affine!.Ended){if(--_remaining>0){_affine=new(_recipe.AffineAnimations[_affineIndex]);_restart=true;}else Finished=true;}break;
                case "AnimKnockOffStrike":
                    _angle=(_angle+_vx)&255;_offset=new((20*owner._script.SineTable[_angle+64])>>8,(20*owner._script.SineTable[_angle])>>8);
                    if(_frames.Ended)Finished=true;break;
                case "AnimNeedleArmSpike":
                    if(_remaining==0){Finished=true;break;}_x=unchecked((short)(_x+_vx));_y=unchecked((short)(_y+_vy));_position=new(_x>>4,_y>>4);_remaining--;break;
                case "AnimFrenzyPlantRoot":
                    if(++_ticks>_remaining-10)_invisible=(_ticks&1)!=0;if(_ticks>_remaining)Finished=true;break;
            }
        }
    }
}
