namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    public sealed partial class SpriteScene
    {
        // Raw source helpers own these tags through their invisible parent, not
        // through the animation script's loadspritegfx cleanup list. No refcount:
        // the native parent's free-by-tag is destructive to competing owners.
        private readonly Dictionary<int,SourceBattleAnimation> _dynamicSpriteGraphics=[];
        internal bool HasDynamicSpriteGraphics(int tag)=>_dynamicSpriteGraphics.ContainsKey(tag);
        internal void AcquireDynamicSpriteGraphics(int tag,SourceBattleAnimation owner)
        {
            if(!_dynamicSpriteGraphics.TryAdd(tag,owner))throw new InvalidDataException("A raw source sprite graphics tag already has a live owner.");
        }
        internal bool ReleaseDynamicSpriteGraphics(int tag,SourceBattleAnimation owner)
        {
            if(!_dynamicSpriteGraphics.TryGetValue(tag,out var current)||!ReferenceEquals(current,owner))return false;
            _dynamicSpriteGraphics.Remove(tag);return true;
        }
        internal int[] OwnedDynamicSpriteGraphics(SourceBattleAnimation owner)=>_dynamicSpriteGraphics.Where(p=>ReferenceEquals(p.Value,owner)).Select(p=>p.Key).ToArray();
    }
    private void AcquireDynamicSpriteGraphics(int tag)
    {
        if(!_script.SpritePalettes.TryGetValue(tag,out var colors)||colors.Length!=16||colors.Any(v=>v>32767))throw new InvalidDataException("Raw sprite graphics require their indexed source palette.");
        if(_spriteScene.HasDynamicSpriteGraphics(tag)||FindSpritePalette(tag)!=255)throw new InvalidDataException("Raw sprite graphics cannot overlap another source tag owner.");
        int slot=FindSpritePalette(65535);if(slot==255)throw new InvalidDataException("Raw source sprite graphics exhausted palette banks.");
        _spriteScene.AcquireDynamicSpriteGraphics(tag,this);
        slot=AllocateSpritePalette(tag);ResetPaletteTransform(16+slot);LoadSpritePaletteRange(slot,0,colors);
    }
    private void ReleaseDynamicSpriteGraphics(int tag)
    {
        if(_spriteScene.ReleaseDynamicSpriteGraphics(tag,this))FreeSpritePalette(tag);
    }
    private void ReleaseOwnedDynamicSpriteGraphics()
    {
        foreach(int tag in _spriteScene.OwnedDynamicSpriteGraphics(this))ReleaseDynamicSpriteGraphics(tag);
    }
}
