using Game.Domain.WildBattle;

namespace Game.Domain.Items;

/// <summary>PokemonUseItemEffects ITEM3 permanent-status bits, shared by field and battle effects.</summary>
internal static class SourceItemStatusCure
{
    internal static uint Mask(byte effect)
    {
        uint mask = 0;
        if ((effect & 0x20) != 0) mask |= 7;
        if ((effect & 0x10) != 0) mask |= 0xf88;
        if ((effect & 8) != 0) mask |= WildBattleStatus.Burn;
        if ((effect & 4) != 0) mask |= WildBattleStatus.Freeze;
        if ((effect & 2) != 0) mask |= WildBattleStatus.Paralysis;
        return mask;
    }
}
