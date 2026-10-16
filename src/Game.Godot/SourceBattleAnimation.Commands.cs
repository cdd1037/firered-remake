namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool _splitBackgroundPriority;
    private bool ValidateSharedCommand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op is "splitbgprio" or "splitbgprio_foes")
        {if(a.Length!=1||a[0] is not ("ANIM_ATTACKER" or "ANIM_TARGET"))Fail(c,"Background priority participant differs.");return true;}
        if(c.Op=="panse")
        {
            if(a.Length!=5||a[0] is not ("SE_M_FLAMETHROWER" or "SE_M_HYDRO_PUMP" or "SE_M_WHIRLPOOL" or "SE_M_BUBBLE_BEAM" or "SE_M_SCREECH")||a[1] is not ("SOUND_PAN_ATTACKER" or "SOUND_PAN_TARGET")||a[2] is not ("SOUND_PAN_ATTACKER" or "SOUND_PAN_TARGET")||Number(a[3]) is < -127 or >127||Number(a[4]) is <0 or >255)Fail(c,"Source pan sweep operands differ.");
            return true;
        }
        if(c.Op=="waitplaysewithpan")
        {if(a.Length!=3||a[1] is not ("SOUND_PAN_ATTACKER" or "SOUND_PAN_TARGET")||Number(a[2]) is <0 or >255||a[0] is not ("SE_M_DOUBLE_TEAM" or "SE_M_COMET_PUNCH"))return false;return true;}
        if(c.Op=="call")
        {if(a.Length!=1||!_script.Subroutines.ContainsKey(a[0]))Fail(c,"Missing source subroutine.");return true;}
        if(c.Op=="return")
        {if(a.Length!=0)Fail(c,"Return operand differs.");return true;}
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_SwayMon")
        {if(a.Length!=7||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >1||Number(a[3]) is < -32767 or >32767||Number(a[4]) is <1 or >32767||Number(a[5]) is <1 or >32767||Number(a[6]) is <0 or >1||_script.SineTable.Length!=320)Fail(c,"Sway task operands differ.");return true;}
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_BlendColorCycle")
        {if(a.Length!=8||Number(a[1]) is <0 or >255||PaletteNumber(a[2]) is <0 or >2047||Number(a[3]) is < -128 or >127||Number(a[4]) is <1 or >32767||Number(a[5]) is <0 or >16||Number(a[6]) is <0 or >16||PaletteNumber(a[7]) is <0 or >65535)Fail(c,"Palette cycle operands differ.");return true;}
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_InvertScreenColor")
        {if(a.Length!=5||a.Skip(1).Any(v=>Number(v) is <0 or >511))Fail(c,"Palette inversion operands differ.");return true;}
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_BlendBattleAnimPal")
        {
            if(a.Length!=7||Number(a[1]) is <0 or >255||PaletteNumber(a[2]) is <0 or >2047||Number(a[3]) is <0 or >255||Number(a[4]) is <0 or >16||Number(a[5]) is <0 or >16||PaletteNumber(a[6]) is <0 or >65535)Fail(c,"Palette task operands differ.");
            return true;
        }
        return false;
    }
    // Source SwayMon advances an unsigned 8.8 phase and counts half-wave crossings.
    private sealed class SwayMonTask:VisualTask
    {
        private readonly int _slot,_axis,_amplitude,_speed;private int _phase,_remaining,_half;
        public SwayMonTask(SourceBattleAnimation owner,int[] args)
        {_slot=owner.Resolve(args[4].ToString(System.Globalization.CultureInfo.InvariantCulture)).Value;_axis=args[0];_amplitude=args[1]*(owner._cue.Actor.IsPlayer?1:-1);_speed=args[2];_remaining=args[3];}
        public override void Step(SourceBattleAnimation owner)
        {
            _phase=(_phase+_speed)&65535;int angle=_phase>>8;
            int value=(short)((owner._script.SineTable[angle]*_amplitude)>>8);var old=owner._offsets[_slot];
            owner._offsets[_slot]=_axis==0?new(value,old.Y):new(old.X,(_slot%2==0?1:-1)*Math.Abs(value));
            if((angle>127&&_half==0)||(angle<127&&_half==1))
            { _half^=1;if(--_remaining==0){owner._offsets[_slot]=default;Finished=true;} }
        }
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
