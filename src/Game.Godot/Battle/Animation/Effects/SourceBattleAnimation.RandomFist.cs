namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string RandomFistTemplate="gFistFootRandomPosSpriteTemplate",RandomFistImpactTemplate="gBasicHitSplatSpriteTemplate";
    private bool ValidateRandomFistOperand(SourceAnimationCommand command)
    {
        var a=command.Args;if(command.Op!="createsprite"||a.FirstOrDefault()!=RandomFistTemplate)return false;
        if(a.Length!=6||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127||AnimationArgument(a[3]) is <0 or >1||
            AnimationArgument(a[4]) is <0 or >32767||AnimationArgument(a[5]) is <short.MinValue or >4)
            Fail(command,"Random fist source selector, duration or frame differs.");
        if(!_script.SpriteRecipes.ContainsKey(RandomFistTemplate)||!_script.SpriteRecipes.ContainsKey(RandomFistImpactTemplate)||
            !_script.SpriteDependencies.Contains(RandomFistImpactTemplate))Fail(command,"Random fist raw impact dependency is missing.");
        var hand=_script.SpriteRecipes[RandomFistTemplate];var impact=_script.SpriteRecipes[RandomFistImpactTemplate];
        if(hand.Callback!="AnimFistOrFootRandomPos"||hand.Asset!="hands-and-feet"||hand.Width!=32||hand.Height!=32||hand.Blend||hand.AffineDoubleSize||hand.AffineAnimations.Length!=0||
            hand.Animations.Length!=5||!hand.LoopStarts.SequenceEqual([-1,-1,-1,-1,-1])||hand.FrameLoops.Any(p=>p.Length!=0))Fail(command,"Random fist source recipe differs.");
        for(int i=0;i<5;i++)
            if(hand.Animations[i].Length!=1||hand.Animations[i][0]!=new SourceSpriteAnimFrame(i<4?16*i:48,1,i==4,false))Fail(command,"Random fist source frame differs.");
        if(impact.Callback!="AnimHitSplatBasic"||impact.Asset!="impact"||impact.Width!=32||impact.Height!=32||!impact.Blend||impact.AffineDoubleSize||impact.AffineAnimations.Length!=4||
            impact.Animations.Length!=1||impact.Animations[0].Length!=1||impact.Animations[0][0]!=new SourceSpriteAnimFrame(0,1,false,false)||!impact.LoopStarts.SequenceEqual([0])||impact.FrameLoops.Any(p=>p.Length!=0))Fail(command,"Raw impact source recipe differs.");
        SourceSpriteAffineCommand[] affine=[new("frame",0,0,0,8,0),new("end",0,0,0,0,0)];
        if(!impact.AffineAnimations[0].SequenceEqual(affine))Fail(command,"Raw impact affine program differs.");
        int[] scales=[216,176,128];
        for(int i=1;i<4;i++)
            if(!impact.AffineAnimations[i].SequenceEqual([new SourceSpriteAffineCommand("frame",scales[i-1],scales[i-1],0,0,0),..affine]))
                Fail(command,"Raw impact affine table differs.");
        RequireVisualSeed(command);
        if(_validatingInvokedProgram)
        {
            var battler=_battlers[(AnimationArgument(a[3])==0?_cue.Actor:_cue.Target).Value];
            if(battler.SourceWidth is <8 or >64||battler.SourceHeight is <8 or >64||(battler.SourceWidth&7)!=0||(battler.SourceHeight&7)!=0)
                Fail(command,"Random fist requires exact source battler dimensions.");
        }
        return true;
    }
    private bool TryCreateRandomFistSprite(SourceAnimationCommand command,int slot,int priority)
    {
        if(command.Args[0]!=RandomFistTemplate)return false;
        if(!_loaded.Contains("ANIM_TAG_HANDS_AND_FEET")||!_loaded.Contains("ANIM_TAG_IMPACT"))Fail(command,"Random fist graphics have not been loaded.");
        // Source inserts the counted parent before its CreateSpriteAndAnimate
        // initializer can allocate the uncounted child in the same global pool.
        var sprite=new RandomFistSprite(slot,priority,_script.SpriteRecipes[RandomFistTemplate]);
        _sprites.Add(sprite);sprite.Initialize(this);return true;
    }
    private sealed class RandomFistSprite(int slot,int priority,SourceSpriteRecipe recipe):VisualSprite(slot,priority)
    {
        private SourceSpriteFrameProgram? _frames;private PixelPoint _position;private short _remaining;
        private RandomFistImpact? _child;
        public override SourceVisualSpriteFrame Frame
        {
            get{var f=_frames?.Frame??recipe.Animations[0][0];return new(Slot,recipe.Template,_position,default,f.Tile,true,f.HFlip,false,256,recipe.Asset,Subpriority){Width=32,Height=32,VFlip=f.VFlip};}
        }
        public void Initialize(SourceBattleAnimation owner)
        {
            int participant=owner._animationArgs[0]==0?owner._cue.Actor.Value:owner._cue.Target.Value;
            if(owner._animationArgs[2]<0)owner._animationArgs[2]=(short)(owner.NextVisualRandom()%5);
            _frames=new(recipe,owner._animationArgs[2]);var battler=owner._battlers[participant];
            var center=battler.Center(owner._doubles,true);
            int x=owner.NextVisualRandom()%(battler.SourceWidth/2),y=owner.NextVisualRandom()%(battler.SourceHeight/4);
            if((owner.NextVisualRandom()&1)!=0)x=-x;if((owner.NextVisualRandom()&1)!=0)y=-y;
            if(battler.Slot.IsPlayer)y-=16;
            _position=new(unchecked((short)(center.X+x)),unchecked((short)(center.Y+y)));_remaining=owner._animationArgs[1];
            int childSlot=owner.FindFreeSpriteSlot();
            if(childSlot!=SourceSpriteRegistry<VisualSprite>.Capacity)
            {
                _child=new(childSlot,unchecked((byte)(Subpriority+1)),owner._script.SpriteRecipes[RandomFistImpactTemplate],_position);
                owner._sprites.Add(_child);
            }
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_remaining==0)
            {
                // Source frees the child's matrix then destroys that raw object.
                // MAX_SPRITES allocation failure owns no child and needs no cleanup.
                if(_child is not null){_child.Retire();owner._sprites.Remove(_child);}
                Finished=true;
            }
            else _remaining=unchecked((short)(_remaining-1));
        }
        public override void AdvanceAnimation(SourceBattleAnimation owner)=>_frames!.Step();
    }
    private sealed class RandomFistImpact(int slot,int priority,SourceSpriteRecipe recipe,PixelPoint position):VisualSprite(slot,priority)
    {
        private SourceSpriteFrameProgram? _frames;private SourceSpriteAffineProgram? _affine;
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame
        {
            get{var f=_frames?.Frame??recipe.Animations[0][0];var m=_affine?.Matrix??SourceAffineMatrix.FromScaleRotation(256,256,0);
                return new(Slot,recipe.Template,position,default,f.Tile,true,f.HFlip,true,256,recipe.Asset,Subpriority){Width=32,Height=32,VFlip=f.VFlip,Affine=true,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};}
        }
        public override void Step(SourceBattleAnimation owner){} // SpriteCallbackDummy
        public override void AdvanceAnimation(SourceBattleAnimation owner)
        {
            if(_frames is null){_frames=new(recipe);_affine=new(recipe.AffineAnimations[0]);}
            else{_frames.Step();_affine!.Step();}
        }
        public void Retire()=>Finished=true;
    }
}
