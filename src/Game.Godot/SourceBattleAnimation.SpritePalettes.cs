namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    // The renderer owns the animation pool 4..15; battler palettes reserve 0..3.
    // One slot bank is authoritative. Tag views are derived, including duplicate-tag first-match semantics.
    private readonly int[] _spritePaletteTags=Enumerable.Repeat(65535,16).ToArray();
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
    private IReadOnlyDictionary<int,ushort[]> SpritePaletteTagSnapshot()
    {
        Dictionary<int,ushort[]> result=[];
        for(int i=4;i<16;i++)if(_spritePaletteTags[i]!=65535)result.TryAdd(_spritePaletteTags[i],(ushort[])_spritePaletteBanks[i].Clone());
        return result;
    }
    private void FreeLoadedSpritePalettes()
    {foreach(string symbol in _loaded)if(_script.SpriteTags.TryGetValue(symbol,out int tag))FreeSpritePalette(tag);_loaded.Clear();}
    private void ResetSpritePalettes(){Array.Fill(_spritePaletteTags,65535);foreach(var bank in _spritePaletteBanks)Array.Clear(bank);}
    private void LoadSpriteGraphics(string symbol)
    {
        if(!_loaded.Add(symbol))return;
        if(!_script.SpriteTags.TryGetValue(symbol,out int tag))return;
        int slot=FindSpritePalette(tag);if(slot!=255)return;
        slot=AllocateSpritePalette(tag);if(slot==255)return;
        if(_script.SpritePalettes.TryGetValue(tag,out var palette))WriteFadedSpritePalette(slot,0,palette);
    }
    private void RotateSpritePalette(int tag,int first,int count)
    {
        int slot=FindSpritePalette(tag);
        if(slot==255||first<1||count<1||first+count>16)throw new InvalidDataException("Source sprite palette rotation has no loaded bank.");
        var palette=_spritePaletteBanks[slot];ushort last=palette[first+count-1];Array.Copy(palette,first,palette,first+1,count-1);palette[first]=last;
    }
    private void MoveBattlerToBackground(int slot,bool follow)
    {
        if(!_battlers[slot].Visible)return;
        SetBackgroundPriority(_battlers[slot].BackgroundRank,2);
        _backgrounds.Add(slot);_restoredObject[slot]=false;
        if(follow)_staticBackgroundCopies.Remove(slot);
        // MoveBattlerSpriteToBG copies the unfaded source palette. Only its
        // optional updater follows later faded OBJ palette changes.
        else _staticBackgroundCopies[slot]=new(slot,_offsets[slot],null,false,false);
    }
    private void RestoreBattlerObject(int slot)=>_restoredObject[slot]=_battlers[slot].Visible;
    private void ClearBattlerBackgrounds(){_backgrounds.Clear();_staticBackgroundCopies.Clear();}
}
