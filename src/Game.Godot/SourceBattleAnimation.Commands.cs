namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    // Hardware BG1/BG2 priority registers start at the battle template values.
    private readonly int[] _battlerBackgroundPriorities=[0,1];
    private int BattlerBackgroundPriority(int slot)=>_battlerBackgroundPriorities[_battlers[slot].BackgroundRank-1];
    private void SetBackgroundPriority(int bgId,int priority)
    {
        if(bgId is <1 or >2||priority is <0 or >3)throw new InvalidDataException("Battler background priority exceeds source hardware fields.");
        _battlerBackgroundPriorities[bgId-1]=priority;
    }
    private void SplitBackgroundPriority(){SetBackgroundPriority(1,1);SetBackgroundPriority(2,2);}
    private bool ValidateSharedCommand(SourceAnimationCommand c)
    {
        var a=c.Args;
        if(c.Op=="loadspritegfx"&&a.Length==1&&_script.SpriteTags.ContainsKey(a[0]))return true;
        if(c.Op is "playsewithpan" or "waitplaysewithpan" or "loopsewithpan"&&a.Length>0&&_script.SoundEffects.ContainsKey(a[0]))
        {
            if(a.Length!=(c.Op=="playsewithpan"?2:c.Op=="waitplaysewithpan"?3:4)||SoundPan(a[1]) is < -64 or >63||a.Skip(2).Any(v=>Number(v) is <0 or >255))Fail(c,"Source sound operand width differs.");
            return true;
        }
        if(c.Op=="playse")
        {if(a.Length!=1||!_script.SoundEffects.ContainsKey(a[0]))Fail(c,"Source unpanned sound needs one generated song identity.");return true;}
        if(c.Op=="waitsound")
        {if(a.Length!=0)Fail(c,"Sound wait takes no operands.");return true;}
        if(c.Op=="createvisualtask"&&a.FirstOrDefault() is "SoundTask_PlaySE1WithPanning" or "SoundTask_PlaySE2WithPanning")
        {if(a.Length!=4||Number(a[1]) is <0 or >255||!_script.SoundEffects.ContainsKey(a[2])||SoundPan(a[3]) is < -64 or >63)Fail(c,"Source visual sound-player wrapper operands differ.");return true;}
        if(c.Op=="createsoundtask")
        {
            if(a.FirstOrDefault()=="SoundTask_FireBlast")
            {if(a.Length!=3||a[1]!="SE_M_FLAME_WHEEL"||a[2]!="SE_M_FLAME_WHEEL2")Fail(c,"Fire sound sequence operands differ.");return true;}
            if(a.Length!=8||a[0]!="SoundTask_LoopSEAdjustPanning"||!_script.SoundEffects.ContainsKey(a[1])||SoundPan(a[2]) is < -64 or >63||SoundPan(a[3]) is < -64 or >63||Number(a[4]) is < -128 or >127||Number(a[5]) is <1 or >255||Number(a[6]) is <0 or >255||Number(a[7]) is <0 or >255)Fail(c,"Unsupported sound callback or source-width operands.");
            return true;
        }
        if(c.Op=="splitbgprio_all")
        {if(a.Length!=0)Fail(c,"Unconditional background priority split takes no operands.");return true;}
        if(c.Op is "splitbgprio" or "splitbgprio_foes")
        {if(a.Length!=1||a[0] is not ("ANIM_ATTACKER" or "ANIM_TARGET"))Fail(c,"Background priority participant differs.");return true;}
        if(c.Op=="panse_adjustall")
        {
            if(a.Length!=5||!_script.SoundEffects.ContainsKey(a[0])||SoundPan(a[1]) is < -128 or >127||SoundPan(a[2]) is < -128 or >127||Number(a[3]) is < -128 or >127||Number(a[4]) is <0 or >255)Fail(c,"Source adjusted pan sweep operands differ.");
            int start=AdjustPanning2(SoundPan(a[1])),end=AdjustPanning2(SoundPan(a[2])),step=AdjustPanning2(Number(a[3]));
            if(step!=0&&(start==end||(start<end)!=(step>0)))Fail(c,"Adjusted source pan sweep must reach its target.");
            return true;
        }
        if(c.Op=="panse")
        {
            if(a.Length!=5||!_script.SoundEffects.ContainsKey(a[0])||a[1] is not ("SOUND_PAN_ATTACKER" or "SOUND_PAN_TARGET")||a[2] is not ("SOUND_PAN_ATTACKER" or "SOUND_PAN_TARGET")||Number(a[3]) is < -127 or >127||Number(a[4]) is <0 or >255)Fail(c,"Source pan sweep operands differ.");
            return true;
        }
        if(c.Op=="waitplaysewithpan")
        {if(a.Length!=3||a[1] is not ("SOUND_PAN_ATTACKER" or "SOUND_PAN_TARGET")||Number(a[2]) is <0 or >255||a[0] is not ("SE_M_DOUBLE_TEAM" or "SE_M_COMET_PUNCH"))return false;return true;}
        if(ValidateArgumentCommand(c))return true;
        if(c.Op=="jumpifmoveturn")
        {if(_cue.MoveTurn is null||a.Length!=2||Number(a[0]) is <0 or >255||!ValidProgramLabel(a[1]))Fail(c,"Move-turn branch needs committed turn, byte comparison and valid source label.");return true;}
        if(c.Op=="choosetwoturnanim")
        {if(_cue.MoveTurn is null||a.Length!=2||a.Any(label=>!ValidProgramLabel(label)))Fail(c,"Two-turn branch needs committed turn and two valid source labels.");return true;}
        if(c.Op=="goto")
        {if(a.Length!=1||!ValidProgramLabel(a[0]))Fail(c,"Missing or invalid source label target.");return true;}
        if(c.Op=="playsewithpan"&&a.FirstOrDefault()=="SE_M_BIND")
        {if(a.Length!=2||a[1] is not ("SOUND_PAN_ATTACKER" or "SOUND_PAN_TARGET"))Fail(c,"Binding sound operands differ.");return true;}
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_RockMonBackAndForth")
        {if(a.Length!=5||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >1||Number(a[3]) is <0 or >32767||Number(a[4]) is < -32768 or >32767)Fail(c,"Battler rocking operands differ.");return true;}
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_SetGrayscaleOrOriginalPal")
        {if(a.Length!=4||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >7||Number(a[3]) is <0 or >1)Fail(c,"Grayscale participant or mode differs.");return true;}
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_MetallicShine")
        {if(a.Length!=5||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >1||Number(a[3]) is <0 or >1||PaletteNumber(a[4]) is <0 or >65535)Fail(c,"Metallic palette selection differs.");return true;}
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_BlendMonInAndOut")
        {if(a.Length!=7||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >3||PaletteNumber(a[3]) is <0 or >65535||Number(a[4]) is <1 or >16||Number(a[5]) is <0 or >32767||Number(a[6]) is <1 or >32767)Fail(c,"Battler color cycle operands differ.");return true;}
        if(c.Op=="createvisualtask"&&a.FirstOrDefault()=="AnimTask_ScaleMonAndRestore")
        {if(a.Length!=7||Number(a[1]) is <0 or >255||Number(a[2]) is < -32768 or >32767||Number(a[3]) is < -32768 or >32767||Number(a[4]) is <1 or >32767||Number(a[5]) is <0 or >1||Number(a[6]) is <0 or >1)Fail(c,"Battler scaling needs signed increments, positive duration and normal or blend object mode.");return true;}
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
    private sealed class RockMonTask:VisualTask
    {
        private readonly int _slot,_leg,_rotationStep,_xStep;
        private int _phase,_age,_rotation,_cycles;
        public RockMonTask(SourceBattleAnimation owner,AnimationSlot slot,int rocks,int speed)
        {
            _slot=slot.Value;speed=Math.Clamp(speed,0,2);_leg=8-2*speed;
            int direction=slot.IsPlayer?1:-1;_rotationStep=(256+128*speed)*direction;_xStep=(speed+2)*direction;_cycles=rocks-1;
            owner.PrepareBattlerRotScale(_slot,0);
        }
        public override void Step(SourceBattleAnimation owner)
        {
            if(_phase==3){owner.SetBattlerRotScale(_slot,256,256,0);owner.ResetBattlerRotScale(_slot);Finished=true;return;}
            int direction=_phase==1?-1:1;var offset=owner._offsets[_slot];
            _rotation=(short)(_rotation-direction*_rotationStep);owner.SetBattlerRotScale(_slot,256,256,_rotation);
            owner._offsets[_slot]=new((short)(offset.X+direction*_xStep),Math.Abs(owner._matrixC[_slot])>>3);
            if(++_age<(_phase==1?2*_leg:_leg))return;
            _age=0;if(_phase<2)_phase++;else if(_cycles-->0)_phase=0;else _phase=3;
        }
    }
    // Source setup changes the task callback but does not run its first step.
    private sealed class FireBlastSound:SoundTask
    {
        private readonly string _first,_last;private readonly int _increment;
        private int _pan,_counter=10,_age,_phase;
        public FireBlastSound(string first,string last,int initial,int target)
        {_first=first;_last=last;_pan=initial;_increment=initial<target?2:initial>target?-2:0;}
        public override void Step(SourceBattleAnimation owner)
        {
            if(_phase==0)
            {
                if(++_age==111){_counter=5;_age=0;_phase=1;return;}
                if(++_counter==11){_counter=0;owner._audio.Play(new(_first,_pan,owner._frame));}
                _pan=Math.Clamp(_pan+_increment,-64,63);
            }
            else if(++_counter==6)
            {_counter=0;owner._audio.Play(new(_last,owner.AdjustPanning(63),owner._frame));if(++_age==2)Finished=true;}
        }
    }
    private sealed class BlendMonInAndOutTask:VisualTask
    {
        private readonly uint _palette;private readonly ushort _color;private readonly int _peak,_delay;
        private int _cycles,_counter,_coefficient;private bool _descending;
        public BlendMonInAndOutTask(AnimationSlot slot,ushort color,int peak,int delay,int cycles)
        {_palette=1u<<(16+slot.Value);_color=color;_peak=peak;_delay=delay;_cycles=cycles;}
        public override void Step(SourceBattleAnimation owner)
        {
            if(++_counter<_delay)return;_counter=0;_coefficient+=_descending?-1:1;
            // Palette entry zero is transparent and never sampled by the compositor.
            owner.BlendPalettes(_palette,_coefficient,_color);
            if(!_descending&&_coefficient==_peak)_descending=true;
            else if(_descending&&_coefficient==0){if(--_cycles==0)Finished=true;else _descending=false;}
        }
    }
    private static int SoundPan(string value)=>value switch{"SOUND_PAN_ATTACKER"=>-64,"SOUND_PAN_TARGET"=>63,_=>Number(value)};
    // The initializer plays immediately; RunTasks steps it again in the creation frame.
    private sealed class PanningSoundLoop:SoundTask
    {
        private readonly string _sound;private readonly int _increment,_panDelay,_playDelay;
        private int _pan,_remaining,_panCounter,_playCounter;
        public PanningSoundLoop(string sound,int initial,int target,int increment,int count,int panDelay,int playDelay)
        {_sound=sound;_pan=initial;_increment=initial<target?Math.Abs(increment):initial>target?-Math.Abs(increment):0;_remaining=count;_panDelay=panDelay;_playDelay=playDelay;_playCounter=playDelay;}
        public override void Step(SourceBattleAnimation owner)
        {
            if(_playCounter++==_playDelay)
            {_playCounter=0;owner._audio.Play(new(_sound,_pan,owner._frame));if(--_remaining==0){Finished=true;return;}}
            if(_panCounter++==_panDelay){_panCounter=0;_pan=Math.Clamp(_pan+_increment,-64,63);}
        }
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
    // BattleAnimAdjustPanning2 keeps the full signed-byte result and depends only
    // on actor side for ordinary move roots; unlike AdjustPanning it never clamps.
    private int AdjustPanning2(int pan)=>_cue.Actor.IsPlayer?pan:unchecked((sbyte)-pan);
    private sealed class PanSweep(int initial,int target,int step,int delay,bool normalizeIncrement=true):SoundTask
    {
        private int _pan=initial,_counter;
        private readonly int _target=target;
        private readonly int _initial=initial;
        private readonly int _increment=normalizeIncrement?(initial<target?Math.Abs(step):initial>target?-Math.Abs(step):0):step;
        public override void Step(SourceBattleAnimation owner)
        {
            if(_counter++<delay)return;
            _counter=0;_pan=unchecked((short)(_pan+_increment));int output=_pan;
            if(_increment==0||(_initial<_target?_pan>=_target:_pan<=_target)){output=_target;Finished=true;}
            owner._audio.SetPan(unchecked((sbyte)output),owner._frame);
        }
    }
}
