namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    // Raw battler-copy geometry and body material share the scene's existing
    // offset, affine and RGB555 authorities. No additional compositor is needed.
    private SourceVisualSpriteFrame CaptureBattlerCopyFrame(int destination,int source,string callback)
    {
        var body=_battlers[source];
        if(_spriteMaterials is null||!_spriteMaterials.TryGetValue(body.Asset,out var pixels)||pixels is not {Width:64,Height:64,Pixels.Length:4096})
            throw new InvalidOperationException("A raw battler copy requires its current actor-owned body material.");
        var material=pixels with{Pixels=(ushort[])pixels.Pixels.Clone(),PaletteIndices=(byte[])pixels.PaletteIndices.Clone(),Palette=(ushort[])pixels.Palette.Clone()};
        _oamY[destination]=_oamY[source];
        return new(destination,callback,BattlerPosition(source),_offsets[source],0,true,false,true,256,body.Asset,body.Subpriority)
        {Width=64,Height=64,BattlerCopySlot=source,Material=material,OamPriority=_battlerObjectPriorities[source],
            Affine=_affineMode[source],DoubleSize=_affineMode[source],MatrixA=_xScale[source],MatrixB=_matrixB[source],MatrixC=_matrixC[source],MatrixD=_yScale[source]};
    }
}
