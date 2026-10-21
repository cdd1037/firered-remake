namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private readonly Dictionary<int,AnimationBattler> _battlerReplacements=[];
    private readonly Dictionary<int,AnimationBattler> _initialBattlers=[];
    private readonly bool[] _battlerBackgroundMosaic=new bool[2];
    private readonly bool[] _battlerBackgroundEnabled=[true,true];
    private void SetBattlerBackgroundEnabled(int slot,bool enabled)
        =>_battlerBackgroundEnabled[_battlers[slot].BackgroundRank-1]=enabled;
    private int _backgroundMosaicHorizontal,_backgroundMosaicVertical;

    private void SetBackgroundMosaic(int horizontal,int vertical)
    {
        if(horizontal is <0 or >15||vertical is <0 or >15)throw new ArgumentOutOfRangeException(nameof(horizontal));
        _backgroundMosaicHorizontal=horizontal;_backgroundMosaicVertical=vertical;
    }
    private void SetBattlerBackgroundMosaic(int slot,bool enabled)
        =>_battlerBackgroundMosaic[_battlers[slot].BackgroundRank-1]=enabled;
    private void SwapBattlerMaterial(int slot)
    {
        if(!_battlerReplacements.TryGetValue(slot,out var replacement))throw new InvalidOperationException("Source battler replacement is missing.");
        _battlers[slot]=replacement;
        ResetPaletteTransform(16+slot);SetBattlerGreyscaleOrOriginal(slot,true);
        // Live monbg follows the selected sprite material and its new default Y.
        _staticBackgroundCopies.Remove(slot);
    }
    private void ResetBattlerMaterials()
    {
        foreach(var (slot,battler) in _initialBattlers)_battlers[slot]=battler;
        Array.Clear(_battlerBackgroundMosaic);Array.Fill(_battlerBackgroundEnabled,true);SetBackgroundMosaic(0,0);
    }
}
