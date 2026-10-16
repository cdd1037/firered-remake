namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool _splitBackgroundPriority;
    private bool ValidateSharedCommand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="splitbgprio")
        {if(a.Length!=1||a[0] is not ("ANIM_ATTACKER" or "ANIM_TARGET"))Fail(c,"Background priority participant differs.");return true;}
        if(c.Op=="panse")
        {
            if(a.Length!=5||a[0]!="SE_M_FLAMETHROWER"||a[1] is not ("SOUND_PAN_ATTACKER" or "SOUND_PAN_TARGET")||a[2] is not ("SOUND_PAN_ATTACKER" or "SOUND_PAN_TARGET")||Number(a[3]) is < -127 or >127||Number(a[4]) is <0 or >255)Fail(c,"Source pan sweep operands differ.");
            return true;
        }
        if(c.Op=="call")
        {if(a.Length!=1||!_script.Subroutines.ContainsKey(a[0]))Fail(c,"Missing source subroutine.");return true;}
        if(c.Op=="return")
        {if(a.Length!=0)Fail(c,"Return operand differs.");return true;}
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_BlendBattleAnimPal")
        {
            if(a.Length!=7||Number(a[1]) is <0 or >255||PaletteNumber(a[2]) is <0 or >2047||Number(a[3]) is <0 or >255||Number(a[4]) is <0 or >16||Number(a[5]) is <0 or >16||PaletteNumber(a[6]) is <0 or >65535)Fail(c,"Palette task operands differ.");
            return true;
        }
        return false;
    }
    // Cmd_panse creates the source priority-1 task without an immediate initializer.
    // It updates the owned SE channels once in RunTasks, including the creation frame.
    private sealed class PanSweep(int initial,int target,int step,int delay):SoundTask
    {
        private int _pan=initial,_counter;
        private readonly int _target=target;
        private readonly int _increment=initial<target?Math.Abs(step):initial>target?-Math.Abs(step):0;
        public override void Step(SourceBattleAnimation owner)
        {
            if(_counter++<delay)return;
            _counter=0;_pan+=_increment;
            if(_increment==0||(_increment>0?_pan>=_target:_pan<=_target)){_pan=_target;Finished=true;}
            owner._audio.SetPan(_pan,owner._frame);
        }
    }
}
