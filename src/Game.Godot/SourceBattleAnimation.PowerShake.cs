namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private bool ValidatePowerShakeCommand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createvisualtask"||a.FirstOrDefault() is not ("AnimTask_HorizontalShake" or "AnimTask_ShakeTargetBasedOnMovePowerOrDmg" or "AnimTask_IsPowerOver99"))return false;
        int count=a[0] switch{"AnimTask_HorizontalShake"=>5,"AnimTask_ShakeTargetBasedOnMovePowerOrDmg"=>7,_=>2};
        if(a.Length!=count||Number(a[1]) is <0 or >255||a.Skip(2).Any(v=>AnimationArgument(v) is <short.MinValue or >short.MaxValue))Fail(command,"Power shake callback argument widths differ.");
        if(a[0]=="AnimTask_HorizontalShake")
        {
            // A positive initial amplitude of at least two reaches the source's
            // signed decrement-to-zero tail without traversing a wrapped cycle.
            if(AnimationArgument(a[2]) is <0 or >5||AnimationArgument(a[3]) is < -1 or >32764||AnimationArgument(a[4]) is <1 or >32767)Fail(command,"Horizontal shake selector or bounded lifetime differs.");
            if(_validatingInvokedProgram&&AnimationArgument(a[3])==0&&_cue.MovePower is null)Fail(command,"Zero-intensity horizontal shake requires committed source move power.");
        }
        else if(a[0]=="AnimTask_ShakeTargetBasedOnMovePowerOrDmg")
        {
            // Source data[0] is signed16; delay 32767 cannot satisfy ++timer > delay.
            if(AnimationArgument(a[3]) is <0 or >32766||AnimationArgument(a[4]) is <1 or >32767)Fail(command,"Target shake delay or count cannot terminate within its signed source range.");
            if(_validatingInvokedProgram&&(AnimationArgument(a[2])==0?_cue.MovePower is null:_cue.MoveDamage is null))Fail(command,"Target shake requires the invoked source power or damage value.");
        }
        else if(_validatingInvokedProgram&&_cue.MovePower is null)Fail(command,"Power predicate requires committed source move power.");
        return true;
    }

    private bool TryCreatePowerShakeTask(SourceAnimationCommand command)
    {
        switch(command.Args[0])
        {
            case "AnimTask_IsPowerOver99":
                _animationArgs[15]=(short)(_cue.MovePower!.Value>99?1:0);return true;
            case "AnimTask_HorizontalShake":
            {
                int selector=_animationArgs[0];int[] slots;
                if(selector==5)slots=[];
                else if(selector==4)slots=[.._battlers.Values.Where(b=>IsSourceBattlerVisible(b.Slot.Value)).OrderBy(b=>b.Slot.Value).Select(b=>b.Slot.Value)];
                else if(ResolveShakeParticipant(selector,false) is {} slot)slots=[slot.Value];
                else return true;
                int intensity=_animationArgs[1]!=0?_animationArgs[1]:_cue.MovePower!.Value/10;
                _tasks.Add(new HorizontalShakeTask(selector==5,slots,unchecked((short)(intensity+3)),_animationArgs[2],BackgroundScrollX));return true;
            }
            case "AnimTask_ShakeTargetBasedOnMovePowerOrDmg":
                _tasks.Add(new PowerTargetShakeTask(this));return true;
            default:return false;
        }
    }

    private sealed class HorizontalShakeTask(bool terrain,int[] slots,short initial,short maximum,ushort background):VisualTask
    {
        private readonly short _initial=initial;
        private short _state,_delay,_timer,_amplitude=initial;
        public override void Step(SourceBattleAnimation owner)
        {
            if(_state==2)
            {
                if(terrain)owner.BackgroundScrollX=background;
                else foreach(int slot in slots){var p=owner._offsets[slot];owner._offsets[slot]=new(0,p.Y);}
                Finished=true;return;
            }
            if(++_delay<=1)return;
            _delay=0;
            if(terrain)
            {
                int amount=_state==0?_initial:_amplitude;
                owner.BackgroundScrollX=unchecked((ushort)(background+((_timer&1)==0?amount:-amount)));
            }
            else
            {
                // Source computes into u16, then assigns to each sprite's s16 x2.
                short x=unchecked((short)((_timer&1)==0?_amplitude/2+(_amplitude&1):-(_amplitude/2)));
                foreach(int slot in slots){var p=owner._offsets[slot];owner._offsets[slot]=new(x,p.Y);}
            }
            if(++_timer!=(_state==0?maximum:4))return;
            _timer=0;_amplitude=unchecked((short)(_amplitude-1));
            if(_state==0||_amplitude==0)_state++;
        }
    }

    private sealed class PowerTargetShakeTask:VisualTask
    {
        private readonly int _slot;private readonly short _initialX,_amplitude,_low,_high,_delay;private readonly bool _horizontal,_vertical;
        private short _timer,_remaining,_toggle;
        public PowerTargetShakeTask(SourceBattleAnimation owner)
        {
            var a=owner._animationArgs;_slot=owner._cue.Target.Value;
            // The s32 damage quotient is stored into s16 BEFORE source clamping.
            short quotient=unchecked((short)(a[0]==0?owner._cue.MovePower!.Value/12:owner._cue.MoveDamage!.Value/12));
            _amplitude=(short)Math.Clamp((int)quotient,1,16);_low=(short)(_amplitude/2);_high=(short)(_low+(_amplitude&1));
            _initialX=unchecked((short)owner._offsets[_slot].X);_delay=a[1];_remaining=a[2];_horizontal=a[3]!=0;_vertical=a[4]!=0;
        }
        public override void Step(SourceBattleAnimation owner)
        {
            _timer=unchecked((short)(_timer+1));if(_timer<=_delay)return;
            _timer=0;_toggle=(short)((_toggle+1)&1);var p=owner._offsets[_slot];
            if(_horizontal)p=new(unchecked((short)(_initialX+(_toggle!=0?_high:-_low))),p.Y);
            // y2 intentionally ignores captured initial y: source alternates the
            // amplitude and zero, then clears BOTH coordinates at termination.
            if(_vertical)p=new(p.X,_toggle!=0?_amplitude:0);
            _remaining=unchecked((short)(_remaining-1));if(_remaining==0){p=default;Finished=true;}
            owner._offsets[_slot]=p;
        }
    }
}
