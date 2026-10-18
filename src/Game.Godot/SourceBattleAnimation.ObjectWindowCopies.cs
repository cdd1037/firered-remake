namespace Game.Presentation.BattleAnimations;

public sealed partial class SourceBattleAnimation
{
    private sealed class ObjectWindowCopy:VisualSprite
    {
        private readonly SourceVisualSpriteFrame _frame;
        public ObjectWindowCopy(int slot,SourceBattleAnimation o,int source):base(slot,o._battlers[source].Subpriority)
        {
            var b=o._battlers[source];SourcePixelAsset? material=null;o._spriteMaterials?.TryGetValue(b.Asset,out material);
            _frame=new(slot,"CreateInvisibleSpriteCopy",o.BattlerPosition(source),o._offsets[source],0,!o.SourceBattlerSpriteInvisible(source),false,false,256,b.Asset,Subpriority)
                {Width=64,Height=64,BattlerCopySlot=source,Material=material,ObjectWindow=true,OamPriority=0,Affine=true,DoubleSize=o._affineMode[source],MatrixA=o._xScale[source],MatrixB=o._matrixB[source],MatrixC=o._matrixC[source],MatrixD=o._yScale[source]};
            o._oamY[slot]=o._oamY[source];
        }
        public override bool Counted=>false;
        public override void Step(SourceBattleAnimation o){}
        public override SourceVisualSpriteFrame Frame=>_frame;
    }
}
