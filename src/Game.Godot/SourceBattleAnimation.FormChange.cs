namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidateFormChangeCommand(SourceAnimationCommand command)
    {
        var a=command.Args;if(command.Op!="createvisualtask"||a.Length==0)return false;
        if(a[0] is not ("AnimTask_IsMonInvisible" or "AnimTask_CastformGfxChange" or "AnimTask_TransformMon"))return false;
        int length=a[0]=="AnimTask_IsMonInvisible"?2:3;
        if(a.Length!=length||Number(a[1])!=2||length==3&&Number(a[2])!=1)
            Fail(command,"Form transition task source operands differ.");
        if(length==3&&_validatingInvokedProgram&&!_battlerReplacements.ContainsKey(_cue.Actor.Value))
            Fail(command,"Form transition requires an authenticated replacement material.");
        return true;
    }
    private bool TryCreateFormChangeTask(SourceAnimationCommand command)
    {
        switch(command.Args[0])
        {
            case "AnimTask_IsMonInvisible":_animationArgs[7]=(short)(_battlers[_cue.Actor.Value].Visible?0:1);return true;
            case "AnimTask_CastformGfxChange":SwapBattlerMaterial(_cue.Actor.Value);return true;
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
}
