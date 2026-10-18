namespace Game.Presentation.BattleAnimations;

// Palette operations preserve the original RGB555 channels. Bit 15 is this
// renderer's opacity bit, independent of the GBA palette's unused high bit.
public static class SourcePaletteMath
{
    public static ushort Apply(ushort original,SourcePaletteBlend? blend,bool inverted,bool greyscale=false)
    {if(greyscale){int average=((original&31)+((original>>5)&31)+((original>>10)&31))/3;original=(ushort)((original&0x8000)|average|(average<<5)|(average<<10));}ushort pixel=blend is null?original:Blend(original,blend.Coefficient,blend.Color);return inverted?(ushort)(pixel^0x7fff):pixel;}
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

public readonly record struct SourcePaletteEntry(int Index,ushort Color);

public sealed partial class SourceBattleAnimation
{
    // Direct source gPlttBufferFaded writes retain palette indices. Whole-bank
    // loads/fades replace these entries; snapshots detach their current values.
    private readonly Dictionary<int,ushort> _fadedBackgroundEntries=[];
    private void WriteBackgroundPaletteRange(int bank,int start,ReadOnlySpan<ushort> colors)
    {
        if(bank is <0 or >15||start is <0 or >15||start+colors.Length>16)
            throw new ArgumentOutOfRangeException(nameof(start));
        for(int i=0;i<colors.Length;i++)
        {
            ushort color=(ushort)(colors[i]&0x7fff);_fadedBackgroundEntries[bank*16+start+i]=color;
            if(bank==2&&_fadedBackgroundPalette is {} loaded)loaded[start+i]=color;
        }
    }
    private void ClearBackgroundPaletteEntries(int bank)
    {for(int i=0;i<16;i++)_fadedBackgroundEntries.Remove(bank*16+i);}
    private readonly HashSet<int> _invertedPalettes=[];
    private void InvertSelectedPalettes(int background,int actor,int target)
    {
        uint mask=(background&256)!=0?0xeu:0;
        if((actor&256)!=0)mask|=1u<<(16+_cue.Actor.Value);
        if((target&256)!=0)mask|=1u<<(16+_cue.Target.Value);
        for(int bank=0;bank<32;bank++)if((mask&(1u<<bank))!=0)
        {for(int i=0;i<16;i++)if(_fadedBackgroundEntries.TryGetValue(bank*16+i,out var entry))_fadedBackgroundEntries[bank*16+i]=(ushort)(entry^0x7fff);if(!_invertedPalettes.Add(bank))_invertedPalettes.Remove(bank);if(bank==2&&_fadedBackgroundPalette is {} colors)for(int i=0;i<16;i++)colors[i]^=0x7fff;}
    }
    private readonly Dictionary<int,SourcePaletteBlend> _paletteBlends=[];
    private readonly List<SoundTask> _soundTasks=[];
    private void SetBattlerGreyscaleOrOriginal(int slot,bool original)
    {
        _paletteBlends.Remove(16+slot);_invertedPalettes.Remove(16+slot);
        if(original)_greyscale.Remove(slot);else _greyscale.Add(slot);
    }
    private void BlendPalettes(uint mask,int coefficient,ushort color)
    {
        for(int i=0;i<32;i++)if((mask&(1u<<i))!=0)
        {if(i<16)ClearBackgroundPaletteEntries(i);if(i>=20){int slot=i-16;for(int index=0;index<16;index++)_spritePaletteBanks[slot][index]=SourcePaletteMath.Blend(_spritePaletteUnfadedBanks[slot][index],coefficient,color);continue;}if(i==2)BlendBackgroundPalette(coefficient,color);_invertedPalettes.Remove(i);if(i>=16)_greyscale.Remove(i-16);if(coefficient==0)_paletteBlends.Remove(i);else _paletteBlends[i]=new(i,coefficient,color);}
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
    private abstract class SoundTask:SourceTask
    {
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
