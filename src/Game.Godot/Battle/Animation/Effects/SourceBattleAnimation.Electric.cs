namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateElectricOperand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="loadspritegfx"&&a.Length==1&&a[0] is "ANIM_TAG_SPARK" or "ANIM_TAG_SPARK_2" or "ANIM_TAG_SPARK_H")return true;
        if(c.Op is "playsewithpan" or "loopsewithpan" or "waitplaysewithpan"&&a.FirstOrDefault() is "SE_M_THUNDERBOLT" or "SE_M_THUNDERBOLT2" or "SE_M_THUNDER_WAVE")
        {
            if(a.Length!=(c.Op=="loopsewithpan"?4:c.Op=="waitplaysewithpan"?3:2)||a[1] is not ("SOUND_PAN_ATTACKER" or "SOUND_PAN_TARGET"))Fail(c,"Electric sound operands differ.");
            if(a.Length==3&&Number(a[2]) is <0 or >255)Fail(c,"Electric delayed sound differs.");
            if(a.Length==4&&(Number(a[2]) is <0 or >255||Number(a[3]) is <2 or >255))Fail(c,"Electric loop operands differ.");return true;
        }
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_ElectricBolt")
        {
            if(a.Length!=5||Number(a[1]) is <0 or >255||Number(a[2]) is < -128 or >127||Number(a[3]) is < -128 or >127||Number(a[4]) is <0 or >1)Fail(c,"Electric bolt operands differ.");
            if(!_script.SpriteRecipes.TryGetValue("sElectricBoltSegmentSpriteTemplate",out var recipe)||recipe.Callback!="AnimElectricBoltSegment")Fail(c,"Electric bolt source recipe missing.");return true;
        }
        if(c.Op!="createsprite"||a.FirstOrDefault() is not ("gElectricitySpriteTemplate" or "gThunderWaveSpriteTemplate"))return false;
        bool wave=a[0]=="gThunderWaveSpriteTemplate";
        if(a.Length!=(wave?5:7)||Number(a[1]) is <0 or >1||Number(a[2]) is <0 or >127)Fail(c,"Electric sprite shape differs.");
        if(!_script.SpriteRecipes.TryGetValue(a[0],out var sprite)||sprite.Callback!=(wave?"AnimThunderWave":"AnimElectricity")||sprite.Asset!=(wave?"spark-h":"spark-2"))Fail(c,"Electric sprite recipe differs.");
        if(Number(a[3]) is < -128 or >127||Number(a[4]) is < -128 or >127)Fail(c,"Electric offsets differ.");
        if(!wave&&(Number(a[5]) is <1 or >32767||Number(a[6]) is <0 or >2))Fail(c,"Electric duration/frame differs.");return true;
    }
    private int AllocateElectricSprite(int excluded=-1)
    {
        int slot=FindFreeSpriteSlot(excluded);if(slot==SourceSpriteRegistry<VisualSprite>.Capacity)return -1;
        _oamY[slot]=0;return slot;
    }
    private bool TryCreateElectricSprite(SourceAnimationCommand c,int slot,int priority)
    {
        var a=c.Args;
        if(a[0]=="gElectricitySpriteTemplate")
        {
            if(!_loaded.Contains("ANIM_TAG_SPARK_2"))Fail(c,"Electricity graphics have not been loaded.");
            var p=_battlers[_cue.Target.Value].Base(_doubles);
            _sprites.Add(new ElectricitySprite(slot,priority,new(p.X+InitialXDirection()*Number(a[3]),p.Y+Number(a[4])),Number(a[5]),Number(a[6])));return true;
        }
        if(a[0]!="gThunderWaveSpriteTemplate")return false;
        if(!_loaded.Contains("ANIM_TAG_SPARK_H"))Fail(c,"Thunder Wave graphics have not been loaded.");
        var center=_battlers[_cue.Target.Value].Center(_doubles,true);var position=new PixelPoint(center.X+Number(a[3]),center.Y+Number(a[4]));
        int second=AllocateElectricSprite(slot);
        if(second<0)Fail(c,"Thunder Wave secondary sprite capacity exhausted.");
        _sprites.Add(new ThunderWaveBand(slot,priority,position,0));
        _sprites.Add(new ThunderWaveBand(second,priority,new(position.X+32,position.Y),8));return true;
    }
    private bool TryCreateElectricTask(SourceAnimationCommand c)
    {
        if(c.Args[0]!="AnimTask_ElectricBolt")return false;
        if(!_loaded.Contains("ANIM_TAG_SPARK"))Fail(c,"Electric bolt graphics have not been loaded.");
        var p=_battlers[_cue.Target.Value].Base(_doubles);
        _tasks.Add(new ElectricBoltTask(new(p.X+Number(c.Args[2]),p.Y+Number(c.Args[3])),Number(c.Args[4])!=0));return true;
    }
    private sealed class ElectricitySprite(int slot,int priority,PixelPoint position,int duration,int variant):VisualSprite(slot,priority)
    {
        private int _remaining=duration,_state;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gElectricitySpriteTemplate",position,default,variant*4,true,variant==1,false,256,"spark-2",Subpriority){Width=16,Height=16,VFlip=variant==2};
        public override void Step(SourceBattleAnimation owner)
        {if(_state==1)Finished=true;else if(_remaining>0)_remaining--;else _state=1;}
    }
    private sealed class ThunderWaveBand(int slot,int priority,PixelPoint position,int tile):VisualSprite(slot,priority)
    {
        private int _age,_blink;private bool _hidden;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"gThunderWaveSpriteTemplate",position,default,tile,!_hidden,false,false,256,"spark-h",Subpriority){Width=32,Height=16};
        public override void Step(SourceBattleAnimation owner)
        {if(++_blink==3){_blink=0;_hidden=!_hidden;}if(++_age==51)Finished=true;}
    }
    private sealed class ElectricBoltTask(PixelPoint position,bool wide):VisualTask
    {
        private int _state;
        public override void Step(SourceBattleAnimation owner)
        {
            if(_state==10){Finished=true;return;}
            if((_state&1)==0)
            {
                int slot=owner.AllocateElectricSprite();
                if(slot<0)throw new InvalidDataException("Electric bolt sprite capacity exhausted.");
                int row=_state/2;int tile=row==4?0:row*(wide?4:1);
                // Source CreateSprite, rather than CreateSpriteAndAnimate: callback
                // runs once now after BuildOamBuffer, then begins rendering next frame.
                owner._sprites.Add(new ElectricBoltSegment(slot,new(position.X,position.Y+16*(row+1)),tile,wide));
            }
            _state++;
        }
    }
    private sealed class ElectricBoltSegment(int slot,PixelPoint position,int tile,bool wide):VisualSprite(slot,2)
    {
        private int _age=1;private bool _built;
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"sElectricBoltSegmentSpriteTemplate",position,default,tile,_built,false,false,256,wide?"spark-wide":"spark-thin",Subpriority){Width=wide?16:8,Height=16};
        public override void Step(SourceBattleAnimation owner){_built=true;if(++_age==15)Finished=true;}
    }
}
