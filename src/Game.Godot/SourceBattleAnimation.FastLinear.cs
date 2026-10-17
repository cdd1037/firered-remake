namespace Game.Presentation.BattleAnimations;

// battle_anim_mons.c: InitAnimFastLinearTranslationWithSpeed and
// AnimFastTranslateLinear. The low velocity bit carries the sign and still
// contributes to the unsigned accumulator; this is not an 8.8 translation.
public sealed class SourceFastLinear
{
    private readonly ushort _vx,_vy;
    private ushort _x,_y;
    private PixelPoint _offset;
    public short Remaining{get;private set;}
    public SourceFastLinear(int deltaX,int deltaY,int speed)
    {
        if(speed is <1 or >short.MaxValue)throw new InvalidDataException("Source fast-linear speed differs.");
        Remaining=unchecked((short)((Math.Abs(deltaX)<<4)/speed));
        if(Remaining<=0)throw new InvalidDataException("Source fast-linear duration is not positive.");
        _vx=(ushort)(((unchecked((ushort)(Math.Abs(deltaX)<<4))/Remaining)&~1)|(deltaX<0?1:0));
        _vy=(ushort)(((unchecked((ushort)(Math.Abs(deltaY)<<4))/Remaining)&~1)|(deltaY<0?1:0));
    }
    private SourceFastLinear(ushort vx,ushort vy,short remaining){_vx=vx;_vy=vy;Remaining=remaining;}
    public SourceFastLinear ReverseCopy()=>new((ushort)(_vx^1),(ushort)(_vy^1),Remaining);
    public void ResetAccumulators(){_x=0;_y=0;_offset=default;}
    public bool Step(out PixelPoint offset)
    {
        if(Remaining==0){offset=_offset;return true;}
        _x=unchecked((ushort)(_x+_vx));_y=unchecked((ushort)(_y+_vy));
        _offset=new((_vx&1)!=0?-(_x>>4):_x>>4,(_vy&1)!=0?-(_y>>4):_y>>4);
        Remaining=unchecked((short)(Remaining-1));offset=_offset;return false;
    }
    public void StepContinuing(out PixelPoint offset){Remaining=1;Step(out offset);}
}
