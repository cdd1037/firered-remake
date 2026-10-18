namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateShadowCopyOperand(SourceAnimationCommand c)
    {
        if(c.Op!="createvisualtask"||c.Args.FirstOrDefault()!="AnimTask_NightmareClone")return false;
        if(c.Args.Length!=2||Number(c.Args[1]) is <0 or >255)Fail(c,"Nightmare clone takes only its source task priority.");
        if(_validatingInvokedProgram)
        {
            var closure=OverlayInvocationCommands();
            // Native raw cloning copies the sprite's matrix index and all fields.
            // The authored root only shakes the source; reject unreviewed affine,
            // material, palette and visibility companions before any allocation.
            if(closure.Any(x=>x.Op is "createsprite" or "loadspritegfx" or "visible" or "invisible"||
                x.Op=="createvisualtask"&&x.Args[0] is not ("AnimTask_NightmareClone" or "AnimTask_ShakeMon"))||
                closure.Count(x=>x.Op=="createvisualtask"&&x.Args[0]=="AnimTask_NightmareClone")!=1)
                Fail(c,"Nightmare requires one reviewed raw clone owner and its source shake companion.");
        }
        return true;
    }

    private bool TryCreateShadowCopyTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_NightmareClone")return false;
        var task=new NightmareCloneTask(this);
        if(!task.Finished)_tasks.Add(task);
        return true;
    }

    private sealed class NightmareCloneTask:VisualTask
    {
        private readonly NightmareCloneSprite? _copy;
        private int _phase,_elapsed,_alphaA=15,_alphaB=2,_clearDelay;
        public NightmareCloneTask(SourceBattleAnimation o)
        {
            int target=o._cue.Target.Value,slot=o.FindFreeSpriteSlot();
            if(!o.IsSourceBattlerPresent(target)||slot==64){Finished=true;return;}
            _copy=new NightmareCloneSprite(slot,o,target);o._sprites.Add(_copy);
            o.SetAlphaBlend(0,63,15,2);
        }
        public override void Step(SourceBattleAnimation o)
        {
            switch(_phase)
            {
                case 0:
                    int phase=++_elapsed&3;
                    if(phase==1&&_alphaA>0)_alphaA--;
                    if(phase==3&&_alphaB<=15)_alphaB++;
                    o._alphaA=_alphaA;o._alphaB=_alphaB;
                    if(_alphaB!=16||_alphaA!=0||_elapsed<=80)return;
                    _copy!.Destroy();o._sprites.Remove(_copy);_phase=1;return;
                case 1:
                    if(++_clearDelay<=1)return;
                    o.ClearAlphaBlend();_phase=2;return;
                case 2:Finished=true;return;
            }
        }
    }

    private sealed class NightmareCloneSprite:VisualSprite
    {
        private readonly AnimationBattler _body;
        private readonly SourcePixelAsset? _material;
        private readonly PixelPoint _position;
        private readonly int _xSpeed,_ySpeed;
        private PixelPoint _offset;
        private short _x,_y;
        private int _remaining=80;
        public NightmareCloneSprite(int slot,SourceBattleAnimation o,int target):base(slot,o._battlers[target].Subpriority)
        {
            _body=o._battlers[target];_position=o.BattlerPosition(target);_offset=o._offsets[target];
            if(o._spriteMaterials is not null)o._spriteMaterials.TryGetValue(_body.Asset,out _material);
            _xSpeed=_body.Slot.IsPlayer?-144:144;_ySpeed=_body.Slot.IsPlayer?112:-112;
            o._oamY[slot]=o._oamY[target];
        }
        public override bool Counted=>false;
        public void Destroy()=>Finished=true;
        public override void Step(SourceBattleAnimation o)
        {
            if(_remaining<=0)return;
            _remaining--;_x=unchecked((short)(_x+_xSpeed));_y=unchecked((short)(_y+_ySpeed));
            _offset=new(_x>>8,_y>>8);
        }
        public override SourceVisualSpriteFrame Frame=>new(Slot,"AnimNightmareClone",_position,_offset,0,true,false,true,256,_body.Asset,Subpriority)
            {Width=64,Height=64,BattlerCopySlot=_body.Slot.Value,Material=_material};
    }
}
