namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    // Source x/y are independent of x2/y2. In particular, task-affine foot
    // anchoring must not overwrite Thrash's simultaneous raw-position motion.
    private readonly PixelPoint[] _battlerPositionOffsets=new PixelPoint[4];
    private PixelPoint BattlerPosition(int slot)
    {var b=_battlers[slot];var p=b.DisplayCenter??b.Center(_doubles);var d=_battlerPositionOffsets[slot];return new(unchecked((short)(p.X+d.X)),unchecked((short)(p.Y+d.Y)));}
    private void SetBattlerPosition(int slot,PixelPoint position)
    {var b=_battlers[slot];var p=b.DisplayCenter??b.Center(_doubles);_battlerPositionOffsets[slot]=new(unchecked((short)position.X)-p.X,unchecked((short)position.Y)-p.Y);}
    private PixelPoint BattlerBackgroundOffset(int slot)
    {var a=_offsets[slot];var b=_battlerPositionOffsets[slot];return new(a.X+b.X,a.Y+b.Y);}
    private PixelPoint BattlerObjectOffset(int slot)
    {var p=BattlerBackgroundOffset(slot);var g=BattlerSpriteCoordOffset(slot);return new(p.X+g.X,p.Y+g.Y);}
}
