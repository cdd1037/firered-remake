namespace Game.Presentation.BattleAnimations;
public sealed partial class SourceBattleAnimation
{
    private sealed class FallingRock:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;
        private readonly SourceSpriteFrameProgram _frames;
        private readonly int _bounce;
        private PixelPoint _position,_offset;
        private int _phase,_angle,_remaining=16,_amplitudeX,_amplitudeY=-70;
        public FallingRock(int slot,int priority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation owner):base(slot,priority)
        {
            _recipe=recipe;_frames=new(recipe,args[1]);_frames.Step();
            var center=args[3]==0?owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true):owner.AverageElementalTarget(false);
            _position=new(center.X+args[0],center.Y+14);_bounce=args[2];Translate(owner,1);
        }
        private void Translate(SourceBattleAnimation owner,int nextPhase)
        {
            if(_remaining==0){_phase=nextPhase;return;}
            _offset=new((owner._script.SineTable[_angle]*_amplitudeX)>>8,(owner._script.SineTable[_angle+64]*_amplitudeY)>>8);
            _angle=(_angle+4)&255;_remaining--;
        }
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_phase)
            {
                case 0:Translate(owner,1);break;
                case 1:_position=new(unchecked((short)(_position.X+_bounce)),_position.Y);_angle=192;_remaining=32;_amplitudeX=_bounce;_amplitudeY=-24;_phase=2;Translate(owner,3);break;
                case 2:Translate(owner,3);break;
                default:Finished=true;return;
            }
            _frames.Step();
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!Finished,_frames.Frame.HFlip,false,256,_recipe.Asset,Subpriority){Width=32,Height=32,VFlip=_frames.Frame.VFlip};
    }
    private sealed class TerrainShakeController:VisualSprite
    {
        private readonly ushort _original;
        private readonly int _period;
        private int _delta,_delay,_remaining;
        public TerrainShakeController(int slot,int priority,int[] args,SourceBattleAnimation owner):base(slot,priority)
        {_original=owner.BackgroundScrollY;_delta=unchecked((short)-args[0]);_period=_delay=args[1];_remaining=args[2];}
        public override void Step(SourceBattleAnimation owner)
        {
            if(_remaining<=0){owner.BackgroundScrollY=_original;Finished=true;return;}
            _remaining--;
            if(_delay>0){_delay--;return;}
            _delay=_period;owner.BackgroundScrollY=unchecked((ushort)(owner.BackgroundScrollY+_delta));_delta=unchecked((short)-_delta);
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gShakeMonOrTerrainSpriteTemplate",default,default,0,false,false,false,256,null,Subpriority);
    }
}
