using Game.Domain.Pokemon;

/// <summary>Source single-party geometry and cursor semantics, independent of engine state.</summary>
internal enum NativePartyLayout { Single, Double }

internal static class PartyPresentationAssets
{
    internal readonly record struct Box(int X,int Y,int Width,int Height)
    {
        internal bool Contains(double x,double y)=>x>=X&&x<X+Width&&y>=Y&&y<Y+Height;
    }
    internal static Box Slot(int index, NativePartyLayout layout=NativePartyLayout.Single)=>index is <0 or >5?throw new ArgumentOutOfRangeException(nameof(index)):
        layout==NativePartyLayout.Double ? index<2?new(8,8+index*56,80,56):new(96,8+(index-2)*32,144,24) :
        index==0?new(8,24,80,56):new(96,8+(index-1)*24,144,24);
    // Only visual body/status are replaced. Owned identity, mail and acquisition data stay intact.
    internal static PartyPokemon Displayed(PartyPokemon owned,PokemonInstance pokemon,uint status)
        => owned with { Pokemon=pokemon,Status=status };
    internal static Box Cancel=>new(184,136,56,16);
    internal static int Move(int cursor,int count,bool cancel,int dx,int dy,ref int lastRight, NativePartyLayout layout=NativePartyLayout.Single)
    {
        if(count is <1 or >6)throw new ArgumentOutOfRangeException(nameof(count));
        int rows=count+(cancel?1:0);
        if(dy!=0)return (cursor+rows+Math.Sign(dy))%rows;
        if(layout==NativePartyLayout.Double)
        {
            if(dx<0&&cursor>=2&&cursor<count){lastRight=cursor;return cursor<4?0:1;}
            if(dx>0&&cursor<2){int target=cursor==0?(lastRight==3?3:2):(lastRight==5?5:4);return target<count?target:cursor;}
            return cursor;
        }
        if(dx<0&&cursor>0&&cursor<count){lastRight=cursor;return 0;}
        if(dx>0&&cursor==0&&count>1)return Math.Clamp(lastRight,1,count-1);
        return cursor;
    }
}
