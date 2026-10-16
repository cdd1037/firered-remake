namespace Game.Presentation.BattleAnimations;

// Palette operations preserve the original RGB555 channels. Bit 15 is this
// renderer's opacity bit, independent of the GBA palette's unused high bit.
public static class SourcePaletteMath
{
    public static ushort Blend(ushort original,int coefficient,ushort color)
    {
        if(coefficient is <0 or >16)throw new ArgumentOutOfRangeException(nameof(coefficient));
        int result=original&0x8000;
        for(int shift=0;shift<=10;shift+=5)
        {
            int channel=(original>>shift)&31,target=(color>>shift)&31;
            result|=(channel+(((target-channel)*coefficient)>>4))<<shift;
        }
        return (ushort)result;
    }
}

public sealed partial class SourceBattleAnimation
{
    private readonly Dictionary<int,SourcePaletteBlend> _paletteBlends=[];
    private readonly List<SoundTask> _soundTasks=[];
    private void BlendPalettes(uint mask,int coefficient,ushort color)
    {
        for(int i=0;i<32;i++)if((mask&(1u<<i))!=0)
        {if(coefficient==0)_paletteBlends.Remove(i);else _paletteBlends[i]=new(i,coefficient,color);}
    }
    // Source AnimTask_Flash / AnimTask_Flash_Step. Only BG palettes 1..3
    // and currently visible battler OBJ palettes are selected; HUD/text stay separate.
    private sealed class FlashTask:VisualTask
    {
        private readonly uint _mons;
        private int _state,_counter,_coefficient;
        public FlashTask(SourceBattleAnimation owner)
        {
            foreach(var b in owner._battlers.Values)if(b.Visible)_mons|=1u<<(16+b.Slot.Value);
            owner.BlendPalettes(_mons,16,0);owner.BlendPalettes(0xe,16,0xffff);
        }
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_state)
            {
                case 0:if(++_counter>6){_counter=0;_coefficient=16;_state++;}break;
                case 1:
                    if(++_counter>1)
                    {
                        _counter=0;--_coefficient;owner.BlendPalettes(0xe,_coefficient,0xffff);owner.BlendPalettes(_mons,_coefficient,0);
                        if(_coefficient==0)_state++;
                    }
                    break;
                case 2:Finished=true;break;
            }
        }
    }
    // Source priority-1 sound task; init runs once immediately, then once in
    // this frame's RunTasks. Visual waits ignore it; script end waits for it.
    private abstract class SoundTask
    {
        public bool Finished{get;protected set;}
        public abstract void Step(SourceBattleAnimation owner);
    }
    private sealed class DelayedSound(string sound,int pan,int frames):SoundTask
    {
        private int _remaining=frames;
        public override void Step(SourceBattleAnimation owner)
        {if(_remaining--<=0){owner._audio.Play(new(sound,pan,owner._frame));Finished=true;}}
    }
    private sealed class SoundLoop(string sound,int pan,int delay,int plays):SoundTask
    {
        private readonly int _delay=delay;
        private int _counter=delay,_remaining=plays;
        public override void Step(SourceBattleAnimation owner)
        {
            if(_counter++>=_delay)
            {
                _counter=0;owner._audio.Play(new(sound,pan,owner._frame));
                if(--_remaining==0)Finished=true;
            }
        }
    }
}
