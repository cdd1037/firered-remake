namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private const string ThrashAffineProgram="sThrashMoveMonAffineAnimCmds";
    private bool ValidateLockedPairCommand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op!="createvisualtask"||a.FirstOrDefault() is not ("AnimTask_ThrashMoveMonHorizontal" or "AnimTask_ThrashMoveMonVertical"))return false;
        if(a.Length!=2||Number(a[1]) is <0 or >255)Fail(c,"Thrash motion takes only source task priority.");
        if(_validatingInvokedProgram&&!IsSourceBattlerPresent(_cue.Actor.Value))Fail(c,"Thrash motion requires a present source attacker sprite.");
        if(a[0]=="AnimTask_ThrashMoveMonHorizontal")
        {
            SourceTaskAffineCommand[] expected=[new("frame",-10,9,0,7),new("frame",20,-20,0,7),new("frame",-20,20,0,7),new("frame",10,-9,0,7),new("loop",Count:2),new("end")];
            if(!_script.TaskAffinePrograms.TryGetValue(ThrashAffineProgram,out var program)||!program.SequenceEqual(expected))Fail(c,"Thrash source task-affine program differs.");
            SourceTaskAffineProgram.Validate(program!);
        }
        return true;
    }
    private bool TryCreateLockedPairTask(SourceAnimationCommand c)
    {
        if(c.Args[0]=="AnimTask_ThrashMoveMonHorizontal")
        {_tasks.Add(new TaskAffinePlayback(this,_cue.Actor,_script.TaskAffinePrograms[ThrashAffineProgram]));return true;}
        if(c.Args[0]!="AnimTask_ThrashMoveMonVertical")return false;
        _tasks.Add(new ThrashPositionTask(this,_cue.Actor));return true;
    }
    private sealed class ThrashPositionTask:VisualTask
    {
        private readonly int _slot;
        private readonly short[] _data=new short[16];private short _x,_y;
        public ThrashPositionTask(SourceBattleAnimation o,AnimationSlot slot)
        {
            _slot=slot.Value;var p=o.BattlerPosition(_slot);_x=(short)p.X;_y=(short)p.Y;
            _data[2]=(short)(slot.IsPlayer?4:-4);_data[3]=7;_data[4]=3;_data[5]=_x;_data[6]=_y;_data[9]=2;
        }
        public override void Step(SourceBattleAnimation o)
        {
            var d=_data;var p=o.BattlerPosition(_slot);_x=(short)p.X;_y=(short)p.Y;
            if(++d[7]>2){d[7]=0;d[8]++;_y=unchecked((short)(_y+((d[8]&1)!=0?d[9]:-d[9])));}
            switch(d[1])
            {
                case 0:_x+=d[2];if(--d[3]==0){d[3]=14;d[1]=1;}break;
                case 1:_x-=d[2];if(--d[3]==0){d[3]=7;d[1]=2;}break;
                case 2:_x+=d[2];if(--d[3]==0){if(--d[4]!=0){d[3]=7;d[1]=0;}else{if((d[8]&1)!=0)_y-=d[9];Finished=true;}}break;
            }
            o.SetBattlerPosition(_slot,new(_x,_y));
        }
    }
}
