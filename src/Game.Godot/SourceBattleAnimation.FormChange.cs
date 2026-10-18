namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateFormChangeCommand(SourceAnimationCommand command)
    {
        var a=command.Args;if(command.Op!="createvisualtask"||a.Length==0)return false;
        if(a[0] is not ("AnimTask_IsMonInvisible" or "AnimTask_CastformGfxChange" or "AnimTask_TransformMon" or "AnimTask_MonToSubstitute"))return false;
        int length=a[0] is "AnimTask_IsMonInvisible" or "AnimTask_MonToSubstitute"?2:3;
        int mode=length==3&&a.Length==length?Number(a[2]):-1;
        if(a.Length!=length||Number(a[1])!=2||length==3&&(a[0]=="AnimTask_TransformMon"?mode is <0 or >1:mode!=1))
            Fail(command,"Form transition task source operands differ.");
        if(a[0]!="AnimTask_IsMonInvisible"&&_validatingInvokedProgram&&!_battlerReplacements.ContainsKey(_cue.Actor.Value))
            Fail(command,"Form transition requires an authenticated replacement material.");
        return true;
    }
    private bool TryCreateFormChangeTask(SourceAnimationCommand command)
    {
        switch(command.Args[0])
        {
            case "AnimTask_IsMonInvisible":_animationArgs[7]=(short)(SourceBattlerSpriteInvisible(_cue.Actor.Value)?1:0);return true;
            case "AnimTask_CastformGfxChange":SwapBattlerMaterial(_cue.Actor.Value);return true;
            case "AnimTask_MonToSubstitute":
                var substitute=new MonToSubstituteTask(_cue.Actor.Value);substitute.Step(this);_tasks.Add(substitute);return true;
            case "AnimTask_TransformMon":
                var task=new FormChangeMosaicTask(_cue.Actor.Value);task.Step(this);_tasks.Add(task);return true;
            default:return false;
        }
    }
    private sealed class FormChangeMosaicTask(int slot):VisualTask
    {
        private short _state,_stretch,_delay;
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_state)
            {
                case 0:owner.SetBackgroundMosaic(0,0);owner.SetBattlerBackgroundMosaic(slot,true);_state++;break;
                case 1:
                    if(_delay++>1){_delay=0;_stretch++;owner.SetBackgroundMosaic(_stretch,_stretch);if(_stretch==15)_state++;}
                    break;
                case 2:owner.SwapBattlerMaterial(slot);_state++;break;
                case 3:
                    if(_delay++>1){_delay=0;_stretch--;owner.SetBackgroundMosaic(_stretch,_stretch);if(_stretch==0)_state++;}
                    break;
                case 4:owner.SetBackgroundMosaic(0,0);owner.SetBattlerBackgroundMosaic(slot,false);Finished=true;break;
            }
        }
    }
    // The source changes task function after the hidden material load. Both
    // phases retain the same counted visual task; no replacement sprite exists.
    private sealed class MonToSubstituteTask(int slot):VisualTask
    {
        private readonly short[] _data=new short[16];
        private bool _doll;
        public override void Step(SourceBattleAnimation owner)
        {
            var d=_data;
            if(!_doll)
            {
                if(d[0]==0)
                {
                    owner.PrepareBattlerRotScale(slot,0);d[1]=d[2]=256;d[0]++;
                }
                else if(d[0]==1)
                {
                    d[1]+=0x60;d[2]-=0xD;owner.SetBattlerRotScale(slot,d[1],d[2],0);
                    if(++d[3]==9)
                    {d[3]=0;owner.ResetBattlerRotScale(slot);owner.SetSourceBattlerSpriteInvisible(slot,true);d[0]++;}
                }
                else
                {
                    owner.SwapBattlerMaterial(slot);Array.Clear(d);_doll=true;
                }
                return;
            }
            var offset=owner._offsets[slot];
            switch(d[0])
            {
                case 0:
                    owner._offsets[slot]=new(200,-200);owner.SetSourceBattlerSpriteInvisible(slot,false);d[10]=0;d[0]++;break;
                case 1:
                    d[10]+=112;offset=new(offset.X,unchecked((short)(offset.Y+(d[10]>>8))));
                    if(owner.BattlerPosition(slot).Y+offset.Y>=-32)offset=new(0,offset.Y);
                    if(offset.Y>0)offset=new(offset.X,0);
                    owner._offsets[slot]=offset;
                    if(offset.Y==0){Sound(owner);d[10]-=0x800;d[0]++;}
                    break;
                case 2:
                    d[10]-=112;if(d[10]<0)d[10]=0;
                    owner._offsets[slot]=new(offset.X,unchecked((short)(offset.Y-(d[10]>>8))));
                    if(d[10]==0)d[0]++;
                    break;
                case 3:
                    d[10]+=112;offset=new(offset.X,unchecked((short)(offset.Y+(d[10]>>8))));
                    if(offset.Y>0)offset=new(offset.X,0);
                    owner._offsets[slot]=offset;
                    if(offset.Y==0){Sound(owner);Finished=true;}
                    break;
            }
        }
        private static void Sound(SourceBattleAnimation owner)
            =>owner._audio.Play(new("SE_M_BUBBLE2",owner.AdjustPanning(-64),owner._frame));
    }

}
