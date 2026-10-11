namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private int InitialXDirection()
    {
        int actorX=_battlers[_cue.Actor.Value].Base(_doubles).X,targetX=_battlers[_cue.Target.Value].Base(_doubles).X;
        return actorX>targetX?-1:actorX<targetX?1:_cue.Actor.IsPlayer?1:-1;
    }
    // Source BattleAnimAdjustPanning. Ally targets and attacker-side sounds use the
    // original asymmetric -64/+63 endpoints, including the +64 clamp after reversal.
    private int AdjustPanning(int pan)
    {
        if(_cue.Actor.IsPlayer)
        {
            if(_cue.Target.IsPlayer)
            {if(pan==63)pan=-64;else if(pan!=-64)pan=-pan;}
        }
        else if(!_cue.Target.IsPlayer){if(pan==-64)pan=63;}
        else pan=-pan;
        return Math.Clamp(pan,-64,63);
    }
    private void ValidateOperands(SourceAnimationCommand c)
    {
        void Range(int index,int min,int max)
        {
            if(index>=c.Args.Length)Fail(c,"Missing operand.");
            int value;
            try{value=Number(c.Args[index]);}catch(FormatException){Fail(c,"Invalid numeric operand.");return;}catch(OverflowException){Fail(c,"Numeric operand overflow.");return;}
            if(value<min||value>max)Fail(c,$"Operand {index} outside accepted range {min}..{max}.");
        }
        if(c.Op=="loopsewithpan"){Range(2,0,255);Range(3,2,255);}
        if(c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_StatsChange"){Range(1,5,5);return;}
        if(c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_Flash"){Range(1,2,2);return;}
        if(c.Op=="delay")Range(0,0,255);
        if(c.Op=="setalpha"){Range(0,0,16);Range(1,0,16);}
        if(c.Op=="createvisualtask")
        {
            Range(1,2,2);Range(2,0,1);Range(3,-127,127);Range(4,-127,127);Range(5,1,32);
            Range(6,0,c.Args[0]=="AnimTask_TranslateMonEllipticalRespectSide"?5:16);
            if(c.Args[0]=="AnimTask_TranslateMonEllipticalRespectSide"&&(_script.SineTable.Length!=320||_script.SineTable.Any(x=>x is < -256 or >256)))Fail(c,"Authenticated sine table is missing or malformed.");
        }
        if(c.Op!="createsprite")return;
        Range(1,0,1);Range(2,0,127);
        for(int i=3;i<c.Args.Length;i++)Range(i,short.MinValue,short.MaxValue);
        switch(c.Args[0])
        {
            case "gBasicHitSplatSpriteTemplate":Range(3,-127,127);Range(4,-127,127);Range(5,0,1);Range(6,0,3);break;
            case "gHorizontalLungeSpriteTemplate":Range(3,1,127);Range(4,-127,127);break;
            case "gScratchSpriteTemplate":Range(5,0,1);Range(6,0,1);break;
            case "gCuttingSliceSpriteTemplate":Range(5,0,1);break;
            case "gSlashSliceSpriteTemplate":Range(3,0,1);break;
            case "gSlideMonToOffsetSpriteTemplate":Range(3,0,1);Range(4,-127,127);Range(5,-127,127);Range(6,0,1);Range(7,1,127);break;
            case "gSlideMonToOriginalPosSpriteTemplate":Range(3,0,1);Range(4,0,2);Range(5,1,127);break;
            case "gFistFootSpriteTemplate":Range(3,-127,127);Range(4,-127,127);Range(5,0,255);Range(6,0,1);Range(7,0,4);break;
        }
    }
    private abstract class VisualTask
    {
        public bool Finished{get;protected set;}
        public abstract void Step(SourceBattleAnimation owner);
    }
    private sealed class EllipseTask(AnimationSlot slot,int width,int height,int loops,int speed):VisualTask
    {
        private int _angle,_loops=loops;
        public override void Step(SourceBattleAnimation owner)
        {
            var sine=owner._script.SineTable;
            owner._offsets[slot.Value]=new((short)((width*sine[_angle])>>8),(short)(-((height*sine[_angle+64])>>8)+height));
            _angle=(_angle+(1<<speed))&255;
            if(_angle==0)_loops--;
            if(_loops==0){owner._offsets[slot.Value]=default;Finished=true;}
        }
    }
    // The dummy sprite holds the source signed-16-bit fixed-point accumulators.
    // Offset completes through a stored callback one frame after translation;
    // restoration destroys directly and explicitly zeros only the chosen axes.
    private sealed class SlideSprite:VisualSprite
    {
        private readonly string _template;
        private readonly AnimationSlot _battler;
        private readonly PixelPoint _position,_origin;
        private readonly int _vx,_vy,_direction;
        private readonly bool _restore;
        private int _remaining,_fx,_fy,_state;
        public SlideSprite(int slot,int priority,string template,int[] args,SourceBattleAnimation owner):base(slot,priority)
        {
            _template=template;_battler=args[0]==0?owner._cue.Actor:owner._cue.Target;
            _position=owner._battlers[owner._cue.Target.Value].Center(owner._doubles,true);
            _restore=template=="gSlideMonToOriginalPosSpriteTemplate";
            if(_restore)
            {
                _origin=owner._offsets[_battler.Value];_remaining=args[2];_direction=args[1];
                _vx=_direction==2?0:(short)(-_origin.X<<8)/_remaining;
                _vy=_direction==1?0:(short)(-_origin.Y<<8)/_remaining;
            }
            else
            {
                _remaining=args[4];int x=args[1]*(_battler.IsPlayer?1:-1),y=args[2]*(!_battler.IsPlayer&&args[3]==1?-1:1);
                _vx=(short)(x<<8)/_remaining;_vy=(short)(y<<8)/_remaining;
            }
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,_template,_position,default,0,false,false,false,256,null,Subpriority);
        public override void Step(SourceBattleAnimation owner)
        {
            if(_remaining>0)
            {
                _remaining--;_fx=(short)(_fx+_vx);_fy=(short)(_fy+_vy);
                owner._offsets[_battler.Value]=new((_fx>>8)+_origin.X,(_fy>>8)+_origin.Y);
            }
            else if(_restore)
            {
                var p=owner._offsets[_battler.Value];owner._offsets[_battler.Value]=new(_direction is 0 or 1?0:p.X,_direction is 0 or 2?0:p.Y);Finished=true;
            }
            else if(_state++>0)Finished=true;
        }
    }
}
