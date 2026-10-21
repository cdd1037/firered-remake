namespace Game.Presentation.BattleAnimations;
public sealed partial class SourceBattleAnimation
{
    private bool ValidateSlidingBackgroundCommand(SourceAnimationCommand command)
    {
        var a=command.Args;
        if(command.Op!="createvisualtask")return false;
        if(a.FirstOrDefault()=="AnimTask_MoveSkyUppercutBg")
        {if(a.Length!=3||Number(a[1]) is <0 or >255||AnimationArgument(a[2]) is <short.MinValue or >short.MaxValue)Fail(command,"Sky Uppercut background source operands differ.");return true;}
        if(a.FirstOrDefault()!="AnimTask_StartSlidingBg")return false;
        if(a.Length!=6||Number(a[1]) is <0 or >255||a.Skip(2).Take(2).Any(v=>AnimationArgument(v) is <short.MinValue or >short.MaxValue)||AnimationArgument(a[5]) is <short.MinValue or >ushort.MaxValue||Number(a[4]) is <0 or >1)Fail(command,"Sliding background source operands differ.");
        return true;
    }
    private bool TryCreateSlidingBackgroundTask(SourceAnimationCommand command)
    {
        if(command.Args[0]=="AnimTask_MoveSkyUppercutBg")
        {var task=new SkyUppercutBackgroundTask(_animationArgs[0],_cue.Target.IsPlayer);BackgroundScreenSize=0;task.Initialize(this);if(!task.Finished)_tasks.Add(task);return true;}
        if(command.Args[0]!="AnimTask_StartSlidingBg")return false;
        BackgroundScreenSize=0;
        if(_animationArgs[2]!=0&&!_cue.Actor.IsPlayer)
        {_animationArgs[0]=unchecked((short)-_animationArgs[0]);_animationArgs[1]=unchecked((short)-_animationArgs[1]);}
        _tasks.Add(new SlidingBackgroundTask(_animationArgs[0],_animationArgs[1],_animationArgs[3]));return true;
    }
    private sealed class SkyUppercutBackgroundTask(short delay,bool targetPlayer):VisualTask
    {
        private short _delay=delay,_state=1,_fx,_fy;
        public void Initialize(SourceBattleAnimation o)=>Move(o);
        private void Move(SourceBattleAnimation o)
        {
            _fy=unchecked((short)(_fy+2816));
            o.BackgroundScrollX=unchecked((ushort)(o.BackgroundScrollX+(targetPlayer?1:-1)*(_fx>>8)));o.BackgroundScrollY=unchecked((ushort)(o.BackgroundScrollY+(_fy>>8)));
            _fx=(short)(_fx&255);_fy=(short)(_fy&255);
            if(o._animationArgs[7]==-1){o.BackgroundScrollX=o.BackgroundScrollY=0;o.BackgroundScreenSize=1;Finished=true;}
        }
        public override void Step(SourceBattleAnimation o)
        {if(_state==1){_delay=unchecked((short)(_delay-1));if(_delay==-1)_state++;}else _fx=unchecked((short)(_fx+1280));Move(o);}
    }
    private sealed class SlidingBackgroundTask:VisualTask
    {
        private readonly short _vx,_vy,_stop;private short _fx,_fy;
        public SlidingBackgroundTask(short vx,short vy,short stop){_vx=vx;_vy=vy;_stop=stop;Priority=5;}
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation owner)
        {
            _fx=unchecked((short)(_fx+_vx));_fy=unchecked((short)(_fy+_vy));
            owner.BackgroundScrollX=unchecked((ushort)(owner.BackgroundScrollX+(_fx>>8)));owner.BackgroundScrollY=unchecked((ushort)(owner.BackgroundScrollY+(_fy>>8)));
            _fx=(short)(_fx&255);_fy=(short)(_fy&255);
            if(owner._animationArgs[7]!=_stop)return;
            owner.BackgroundScrollX=owner.BackgroundScrollY=0;owner.BackgroundScreenSize=1;Finished=true;
        }
    }
}
