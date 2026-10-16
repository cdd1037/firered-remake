namespace Game.Presentation.BattleAnimations;

/// <summary>GBA ObjAffineSet integer matrix. The low angle byte is discarded.</summary>
public readonly record struct SourceAffineMatrix(int A,int B,int C,int D)
{
    // Exact Q1.14 quarter-wave constants. No platform floating-point calculation.
    private static readonly int[] QuarterSine = [0,402,803,1205,1605,2005,2404,2801,3196,3589,3980,4369,4756,5139,5519,5896,6269,6639,7005,7366,7723,8075,8423,8765,9102,9434,9759,10079,10393,10701,11002,11297,11585,11866,12139,12406,12665,12916,13159,13395,13622,13842,14053,14255,14449,14634,14810,14978,15136,15286,15426,15557,15678,15790,15892,15985,16069,16142,16206,16260,16305,16339,16364,16379,16384];
    public static int Sine(int angle)
    {
        angle&=255;int quadrant=angle>>6,index=angle&63;
        int value=QuarterSine[(quadrant&1)==0?index:64-index];
        return quadrant<2?value:-value;
    }
    public static SourceAffineMatrix FromScaleRotation(int xScale,int yScale,int rotation)
    {
        int angle=((ushort)rotation)>>8,sine=Sine(angle),cosine=Sine(angle+64);
        // BIOS negates b AFTER the multiply/arithmetic shift. Negating before the
        // shift gives an incorrect one-unit asymmetry at non-cardinal angles.
        return new((short)(((short)xScale*cosine)>>14),(short)-(((short)xScale*sine)>>14),
            (short)(((short)yScale*sine)>>14),(short)(((short)yScale*cosine)>>14));
    }
}
public sealed partial class SourceBattleAnimation
{
    private readonly bool[] _restoredObject=new bool[4],_restoredObjectOam=new bool[4],_blendedObject=new bool[4],_blendedObjectOam=new bool[4];
    private void PrepareBattlerRotScale(int slot,int objectMode)
    {
        if(objectMode is <0 or >1)throw new InvalidDataException("Unsupported battler object mode.");
        _restoredObject[slot]=_battlers[slot].Visible;_blendedObject[slot]=objectMode==1;_affineMode[slot]=true;
    }
    private void ResetBattlerRotScale(int slot)
    {SetBattlerRotScale(slot,256,256,0);_affineMode[slot]=false;_blendedObject[slot]=false;}
    private void SetBattlerRotScale(int slot,int xScale,int yScale,int rotation)
    {
        var m=SourceAffineMatrix.FromScaleRotation(xScale,yScale,rotation);
        _xScale[slot]=m.A;_matrixB[slot]=m.B;_matrixC[slot]=m.C;_yScale[slot]=m.D;
    }
    private sealed class WithdrawTask:VisualTask
    {
        private readonly int _slot;private int _rotation,_phase,_hold;
        public WithdrawTask(SourceBattleAnimation owner){_slot=owner._cue.Actor.Value;owner.PrepareBattlerRotScale(_slot,0);}
        public override void Step(SourceBattleAnimation owner)
        {
            owner.SetBattlerRotScale(_slot,256,256,(_slot&1)==0?-_rotation:_rotation);
            if(_phase==0)_rotation+=176;
            else if(_phase==1){if(++_hold==30)_phase=2;return;}
            else _rotation-=176;
            owner._offsets[_slot]=new(0,Math.Abs(owner._matrixC[_slot])>>3);
            if(_rotation is not (3872 or 0))return;
            if(_phase==2){owner.SetBattlerRotScale(_slot,256,256,0);owner.ResetBattlerRotScale(_slot);Finished=true;}
            else _phase++;
        }
    }

    private void SetBattlerYOffsetFromScale(int slot)
    {
        int height=64-_battlers[slot].YOffset*2;
        int scaled=_yScale[slot]==0?0:(height<<8)/_yScale[slot];
        _offsets[slot]=new(0,(height-Math.Min(128,scaled))/2);
    }
    private sealed class MinimizeTask:VisualTask
    {
        private readonly int _slot;private int _phase,_counter,_copies,_scale=256,_passes;
        public int LiveCopies;
        public MinimizeTask(SourceBattleAnimation owner){_slot=owner._cue.Actor.Value;owner.PrepareBattlerRotScale(_slot,0);}
        public override void Step(SourceBattleAnimation owner)
        {
            switch(_phase)
            {
                case 0:
                    if(_counter is 0 or 3 or 6)
                    {
                        int slot=Enumerable.Range(0,64).First(i=>(!owner._battlers.TryGetValue(i,out var b)||!b.Visible)&&owner._sprites.All(s=>s.Slot!=i));
                        owner._sprites.Add(new MinimizeClone(slot,owner._battlers[_slot].Subpriority-_copies++,owner._battlers[_slot],owner._doubles,owner._offsets[_slot],_scale,this));LiveCopies++;
                    }
                    _counter++;_scale+=40;owner.SetBattlerRotScale(_slot,_scale,_scale,0);owner.SetBattlerYOffsetFromScale(_slot);
                    if(_counter==32){_passes++;_phase++;}break;
                case 1:
                    if(LiveCopies!=0)break;
                    _counter=0;
                    if(_passes==3)_phase=3;
                    else{_copies=0;_scale=256;owner.SetBattlerRotScale(_slot,256,256,0);owner.SetBattlerYOffsetFromScale(_slot);_phase=2;}break;
                case 2:_phase=0;break;
                case 3:if(++_counter>32){_counter=0;_phase++;}break;
                case 4:
                    _counter+=2;_scale-=80;owner.SetBattlerRotScale(_slot,_scale,_scale,0);owner.SetBattlerYOffsetFromScale(_slot);
                    if(_counter==32){_counter=0;_phase++;}break;
                case 5:
                    owner.SetBattlerRotScale(_slot,256,256,0);owner.ResetBattlerRotScale(_slot);
                    // The source's uninitialized data[15] is zero. Scale restoration
                    // has already put the actor's y2 at zero on the previous tick.
                    owner._offsets[0]=new(owner._offsets[0].X,0);Finished=true;break;
            }
        }
    }
    private sealed class MinimizeClone(int slot,int priority,AnimationBattler battler,bool doubles,PixelPoint offset,int scale,MinimizeTask task):VisualSprite(slot,priority)
    {
        private int _life=16;private bool _visible;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"ClonedMinizeSprite_Step",battler.DisplayCenter??battler.Center(doubles),offset,0,_visible,false,true,scale,battler.Asset,Subpriority){BattlerCopySlot=battler.Slot.Value};
        public override void Step(SourceBattleAnimation owner){_visible=true;if(--_life==0){task.LiveCopies--;Finished=true;}}
    }
}
