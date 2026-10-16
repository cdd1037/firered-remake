namespace Game.Presentation.BattleAnimations;
public sealed partial class SourceBattleAnimation
{
    private sealed class SporeParticle:VisualSprite
    {
        private readonly SourceSpriteRecipe _recipe;
        private readonly SourceSpriteFrameProgram _frames;
        private readonly PixelPoint _position;
        private readonly bool _blend;
        private PixelPoint _offset;
        private int _angle,_remaining,_vertical,_priority;
        public SporeParticle(int slot,int subpriority,SourceSpriteRecipe recipe,int[] args,SourceBattleAnimation owner):base(slot,subpriority)
        {
            _recipe=recipe;_frames=new(recipe,args[4]);_blend=args[4]==1;
            var center=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            _position=new(center.X+owner.InitialXDirection()*args[0],center.Y+args[1]);
            _angle=args[2];_remaining=args[3];Move(owner);
        }
        private void Move(SourceBattleAnimation owner)
        {
            _vertical=unchecked((short)(_vertical+24));
            _offset=new((owner._script.SineTable[_angle]*32)>>8,((owner._script.SineTable[_angle+64]*-3)>>8)+(_vertical>>8));
            int priority=owner.BattlerBackgroundPriority(owner._cue.Target.Value);
            _priority=(ushort)(_angle-64)<128?priority:Math.Min(priority+1,3);
            _angle=(_angle+2)&255;if(--_remaining==-1)Finished=true;
        }
        public override void Step(SourceBattleAnimation owner){Move(owner);if(!Finished)_frames.Step();}
        public override SourceVisualSpriteFrame Frame=>new(Slot,_recipe.Template,_position,_offset,_frames.Frame.Tile,!Finished,_frames.Frame.HFlip,_blend,256,_recipe.Asset,Subpriority){Width=16,Height=16,VFlip=_frames.Frame.VFlip,OamPriority=_priority};
    }
}
