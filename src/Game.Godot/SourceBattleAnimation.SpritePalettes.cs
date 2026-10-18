namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    // The renderer owns the animation pool 4..15; battler palettes reserve 0..3.
    // One slot bank is authoritative. Tag views are derived, including duplicate-tag first-match semantics.
    private readonly int[] _spritePaletteTags=Enumerable.Repeat(65535,16).ToArray();
    private readonly ushort[][] _spritePaletteUnfadedBanks=Enumerable.Range(0,16).Select(_=>new ushort[16]).ToArray();
    private readonly ushort[][] _spritePaletteBanks=Enumerable.Range(0,16).Select(_=>new ushort[16]).ToArray();
    private readonly Dictionary<int,SourceBattlerBackgroundCopy> _staticBackgroundCopies=[];
    private int FindSpritePalette(int tag){for(int i=4;i<16;i++)if(_spritePaletteTags[i]==tag)return i;return 255;}
    private int AllocateSpritePalette(int tag)
    {int slot=FindSpritePalette(65535);if(slot!=255)_spritePaletteTags[slot]=tag;return slot;}
    private void FreeSpritePalette(int tag){int slot=FindSpritePalette(tag);if(slot!=255)_spritePaletteTags[slot]=65535;}
    private void WriteFadedSpritePalette(int slot,int first,IReadOnlyList<ushort> colors)
    {
        if(slot is <4 or >15||first<0||first+colors.Count>16)throw new InvalidDataException("Source sprite palette write is outside its bank.");
        for(int i=0;i<colors.Count;i++)_spritePaletteBanks[slot][first+i]=colors[i];
    }
    private void LoadSpritePaletteRange(int slot,int first,IReadOnlyList<ushort> colors)
    {
        if(slot is <4 or >15||first<0||first+colors.Count>16||colors.Any(c=>c>0x7fff))throw new InvalidDataException("Source palette load is outside its RGB555 bank.");
        for(int i=0;i<colors.Count;i++)_spritePaletteUnfadedBanks[slot][first+i]=colors[i];
        WriteFadedSpritePalette(slot,first,colors);
    }
    private void BlendSpritePaletteRange(int slot,int first,int count,int coefficient,ushort color)
    {
        if(slot is <4 or >15||first<0||count<0||first+count>16||coefficient is <0 or >16||color>0x7fff)throw new InvalidDataException("Source palette blend is outside its RGB555 bank.");
        ushort[] colors=new ushort[count];for(int i=0;i<count;i++)colors[i]=SourcePaletteMath.Blend(_spritePaletteUnfadedBanks[slot][first+i],coefficient,color);
        WriteFadedSpritePalette(slot,first,colors);
    }
    private void SetSpriteGreyscaleOrOriginal(int slot,bool original)
    {
        if(slot is <4 or >15)throw new InvalidDataException("Source grayscale palette has no owned sprite bank.");
        var normal=_spritePaletteUnfadedBanks[slot];
        WriteFadedSpritePalette(slot,0,original?normal:normal.Select(c=>SourcePaletteMath.Apply(c,null,false,true)).ToArray());
    }
    private IReadOnlyDictionary<int,ushort[]> SpritePaletteTagSnapshot()
    {
        Dictionary<int,ushort[]> result=[];
        for(int i=4;i<16;i++)if(_spritePaletteTags[i]!=65535)result.TryAdd(_spritePaletteTags[i],(ushort[])_spritePaletteBanks[i].Clone());
        return result;
    }
    private void FreeLoadedSpritePalettes()
    {foreach(string symbol in _loaded)if(_script.SpriteTags.TryGetValue(symbol,out int tag))FreeSpritePalette(tag);_loaded.Clear();}
    private void ResetSpritePalettes(){Array.Fill(_spritePaletteTags,65535);foreach(var bank in _spritePaletteBanks)Array.Clear(bank);foreach(var bank in _spritePaletteUnfadedBanks)Array.Clear(bank);}
    private void LoadSpriteGraphics(string symbol)
    {
        if(!_loaded.Add(symbol))return;
        if(!_script.SpriteTags.TryGetValue(symbol,out int tag))return;
        int slot=FindSpritePalette(tag);if(slot!=255)return;
        slot=AllocateSpritePalette(tag);if(slot==255)return;ResetPaletteTransform(16+slot);
        if(_script.SpritePalettes.TryGetValue(tag,out var palette)){LoadSpritePaletteRange(slot,0,palette);}
    }
    private void RotateSpritePalette(int tag,int first,int count)
    {
        int slot=FindSpritePalette(tag);
        if(slot==255||first<1||count<1||first+count>16)throw new InvalidDataException("Source sprite palette rotation has no loaded bank.");
        var palette=_spritePaletteBanks[slot];ushort last=palette[first+count-1];Array.Copy(palette,first,palette,first+1,count-1);palette[first]=last;
    }
    private void MoveBattlerToBackground(int slot,bool follow)
    {
        if(!IsSourceBattlerVisible(slot))return;
        SetBackgroundPriority(_battlers[slot].BackgroundRank,2);
        _backgrounds.Add(slot);_restoredObject[slot]=false;SetSourceBattlerSpriteInvisible(slot,true);
        if(follow)_staticBackgroundCopies.Remove(slot);
        // MoveBattlerSpriteToBG copies the unfaded source palette. Only its
        // optional updater follows later faded OBJ palette changes.
        else _staticBackgroundCopies[slot]=new(slot,BattlerBackgroundOffset(slot),UnfadedPaletteBlend(16+slot),false,false);
    }
    private void MoveBattlerToStaticBackground(int slot)
    {
        if(!IsSourceBattlerVisible(slot))return;
        MoveBattlerToBackground(slot,false);
        SetSourceBattlerSpriteInvisible(slot,false);
    }
    private sealed class ClearStaticBattlerBackgroundTask(int slot,int partner):VisualTask
    {
        private int _ticks;
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation owner)
        {
            if(++_ticks==1)return;
            foreach(int selected in new[]{slot,partner})
            {
                if(selected<0||!owner.IsSourceBattlerVisible(selected))continue;
                int rank=owner._battlers[selected].BackgroundRank;
                foreach(int copied in owner._backgrounds.Where(i=>owner._battlers[i].BackgroundRank==rank).ToArray())
                {owner._backgrounds.Remove(copied);owner._staticBackgroundCopies.Remove(copied);}
            }
            Finished=true;
        }
    }
    private void RestoreBattlerObject(int slot){_restoredObject[slot]=IsSourceBattlerVisible(slot);if(_restoredObject[slot])SetSourceBattlerSpriteInvisible(slot,false);}
    private void ClearBattlerBackgrounds(){_backgrounds.Clear();_staticBackgroundCopies.Clear();}
}
