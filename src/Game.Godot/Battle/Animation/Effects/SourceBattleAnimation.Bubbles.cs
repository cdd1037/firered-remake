namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    // AnimWaterBubbleProjectile stores its second set of signed16 values in a
    // real, uncounted invisible sprite. Reserve that slot for its actual lifetime.
    private sealed class BubbleHelper(int slot):VisualSprite(slot,255)
    {
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gDummySpriteTemplate",default,default,0,false,false,false,256,null,255);
        public override void Step(SourceBattleAnimation owner){}
    }
    private sealed class WaterBubbleProjectile:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;
        private readonly SourceSpriteAffineProgram _affine;
        private readonly SourceSpriteFrameProgram _frames;
        private readonly PhysicalLinear _linear;
        private readonly PixelPoint _position;
        private readonly BubbleHelper _helper;
        private PixelPoint _offset;
        private readonly int _radiusX,_radiusY,_phaseStep;
        private int _phase,_remaining,_state;
        private bool _animationEnded;
        public WaterBubbleProjectile(int slot,int priority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_affine=new(recipe.AffineAnimations[0]);_frames=new(recipe);
            var actor=owner._battlers[owner._cue.Actor.Value];var target=owner._battlers[owner._cue.Target.Value];int sign=actor.Slot.IsPlayer?1:-1;
            var from=actor.Center(owner._doubles,true);from=new(from.X+sign*args[0],from.Y+args[1]);var to=target.Center(owner._doubles,true);
            _linear=new(to.X-from.X,to.Y-from.Y,args[6]);_radiusX=sign*args[2];_radiusY=args[3];_phase=(args[4]&255)<<8;_phaseStep=args[5];_remaining=args[6];
            _position=new(from.X-((owner._script.SineTable[_phase>>8]*_radiusX)>>8),from.Y-((owner._script.SineTable[(_phase>>8)+64]*_radiusY)>>8));
            int helperSlot=owner.AllocateElectricSprite(slot);if(helperSlot<0)throw new InvalidDataException("Water bubble helper sprite capacity exhausted.");
            _helper=new(helperSlot);owner._sprites.Add(_helper);Move(owner);
        }
        private void Move(SourceBattleAnimation owner)
        {
            if(_linear.Step(out var linear))throw new InvalidDataException("Water bubble translation exceeded its source timer.");
            _offset=new(linear.X+((owner._script.SineTable[_phase>>8]*_radiusX)>>8),linear.Y+((owner._script.SineTable[(_phase>>8)+64]*_radiusY)>>8));
            _phase=(_phase+_phaseStep)&65535;
            if(--_remaining==0){_state=1;owner._sprites.Remove(_helper);}
        }
        public override SourceVisualSpriteFrame Frame
        {
            get
            {
                var f=_frames.Frame;var m=_affine.Matrix;
                return new(Slot,_recipe.Template,_position,_offset,f.Tile,true,f.HFlip,_recipe.Blend,256,_recipe.Asset,Subpriority){VFlip=f.VFlip,Width=_recipe.Width,Height=_recipe.Height,Affine=true,DoubleSize=_recipe.AffineDoubleSize,MatrixA=m.A,MatrixB=m.B,MatrixC=m.C,MatrixD=m.D};
            }
        }
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_state)
            {
                case 0:Move(owner);break;
                case 1:_state=2;break; // Unpause frame animation on the callback after arrival.
                case 2:if(_animationEnded)_state=3;break;
                case 3:_remaining=10;_state=4;break;
                case 4:if(_remaining>0)_remaining--;else _state=5;break;
                case 5:Finished=true;return;
            }
            _affine.Step();
            if(_state<2)return;
            _frames.Step();_animationEnded=_frames.Ended;
        }
    }
}
