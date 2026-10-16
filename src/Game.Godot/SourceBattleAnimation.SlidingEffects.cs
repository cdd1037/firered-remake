namespace Game.Presentation.BattleAnimations;
public sealed partial class SourceBattleAnimation
{
    private bool ValidateSlidingEffectCommand(SourceAnimationCommand command)
    {
        var a=command.Args;if(command.Op!="createvisualtask"||a.Length==0)return false;
        if(a[0]=="AnimTask_ShakeTargetInPattern")
        {
            if(a.Length!=6||Number(a[1]) is <0 or >255||Number(a[2]) is <1 or >32767||Number(a[3]) is <short.MinValue or >short.MaxValue||AnimationArgument(a[4]) is <0 or >1||Number(a[5]) is <0 or >1)Fail(command,"Pattern shake source operands differ.");return true;
        }
        if(a[0]=="AnimTask_BlendNonAttackerPalettes")
        {
            if(a.Length!=6||Number(a[1]) is <0 or >255||Number(a[2]) is <0 or >32767||Number(a[3]) is <0 or >16||Number(a[4]) is <0 or >16||PaletteNumber(a[5]) is <0 or >32767)Fail(command,"Non-attacker blend source operands differ.");return true;
        }
        return false;
    }
    private bool TryCreateSlidingEffectTask(SourceAnimationCommand command)
    {
        if(command.Args[0]=="AnimTask_ShakeTargetInPattern")
        {
            var task=new PatternShakeTask(_cue.Target.Value,_animationArgs[0],_animationArgs[2]!=0,_animationArgs[3]);task.Step(this);if(!task.Finished)_tasks.Add(task);return true;
        }
        if(command.Args[0]=="AnimTask_BlendNonAttackerPalettes")
        {
            uint mask=0xF0000u&~(1u<<(16+_cue.Actor.Value));for(int j=5;j>0;j--)_animationArgs[j]=_animationArgs[j-1];
            var task=new BlendPaletteTask(mask,_animationArgs[1],_animationArgs[2],_animationArgs[3],unchecked((ushort)_animationArgs[4]));task.Step(this);if(!task.Finished)_tasks.Add(task);return true;
        }
        return false;
    }
    private sealed class PatternShakeTask(int slot,int duration,bool vertical,int pattern):VisualTask
    {
        // battle_anim_fire.c: only the first ten entries of each source table are indexed.
        private static readonly int[][] Patterns=[[-1,-1,0,1,1,0,0,-1,-1,1,1,0,0,-1,0,1],[-1,0,1,0,-1,1,0,-1,0,1,0,-1,0,1,0,1]];
        private int _step;
        public override void Step(SourceBattleAnimation owner)
        {
            int direction=Patterns[pattern][++_step%10],delta=owner._animationArgs[1]*direction;var p=owner._offsets[slot];
            owner._offsets[slot]=vertical?new(p.X,unchecked((short)Math.Abs(delta))):new(unchecked((short)delta),p.Y);
            if(_step==duration){owner._offsets[slot]=default;Finished=true;}
        }
    }
}
