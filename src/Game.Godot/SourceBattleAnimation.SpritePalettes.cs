namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private readonly Dictionary<int,ushort[]> _spritePalettes=[];
    private readonly Dictionary<int,SourceBattlerBackgroundCopy> _staticBackgroundCopies=[];
    private void LoadSpriteGraphics(string symbol)
    {
        if(!_loaded.Add(symbol))return;
        if(_script.SpriteTags.TryGetValue(symbol,out int tag)&&_script.SpritePalettes.TryGetValue(tag,out var palette))
            _spritePalettes[tag]=(ushort[])palette.Clone();
    }
    private void RotateSpritePalette(int tag,int first,int count)
    {
        if(!_spritePalettes.TryGetValue(tag,out var palette)||first<1||count<1||first+count>16)throw new InvalidDataException("Source sprite palette rotation has no loaded bank.");
        ushort last=palette[first+count-1];Array.Copy(palette,first,palette,first+1,count-1);palette[first]=last;
    }
    private void MoveBattlerToBackground(int slot,bool follow)
    {
        if(!_battlers[slot].Visible)return;
        _backgrounds.Add(slot);_restoredObject[slot]=false;
        if(follow)_staticBackgroundCopies.Remove(slot);
        // MoveBattlerSpriteToBG copies the unfaded source palette. Only its
        // optional updater follows later faded OBJ palette changes.
        else _staticBackgroundCopies[slot]=new(slot,_offsets[slot],null,false,false);
    }
    private void RestoreBattlerObject(int slot)=>_restoredObject[slot]=_battlers[slot].Visible;
    private void ClearBattlerBackgrounds(){_backgrounds.Clear();_staticBackgroundCopies.Clear();}
}
