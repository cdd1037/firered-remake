namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    // battle_anim_mons.c: InitAnimArcTranslation/TranslateAnimHorizontalArc.
    // Share only the motion words. Callers retain callback timing and lifetimes.
    private sealed class HorizontalArc(int dx,int dy,int duration,int amplitude)
    {
        private readonly PhysicalLinear _linear=new(dx,dy,duration);
        private readonly short _step=unchecked((short)(0x8000/duration));
        private short _angle;
        public ((int Remaining,int X,int Y) Linear,short Angle) Capture()=>(_linear.Capture(),_angle);
        public void Restore(((int Remaining,int X,int Y) Linear,short Angle) state)
        {_linear.Restore(state.Linear);_angle=state.Angle;}
        public bool Step(int[] sine,out PixelPoint offset)
        {
            if(_linear.Step(out var linear)){offset=default;return true;}
            _angle=unchecked((short)(_angle+_step));
            offset=new(linear.X,unchecked((short)(linear.Y+((amplitude*sine[(byte)(_angle>>8)])>>8))));return false;
        }
    }
}
