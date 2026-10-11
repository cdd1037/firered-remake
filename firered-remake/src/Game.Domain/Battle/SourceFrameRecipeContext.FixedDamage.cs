using Game.Domain.Pokemon;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    // Damage zero is independent of MOVE_RESULT_NO_EFFECT (level-one Psywave and
    // False Swipe/Endure at one HP). Retain that distinction through datahpupdate.
    private bool? fixedDamageNoEffect;
    internal override int FixedDamageApply(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move,
        int damage, bool critical, int percent, bool immune)
    {
        fixedDamageNoEffect = immune;
        try { return SourceGenericApplyDamage(actor, target, move, immune ? 0 : damage, critical, percent); }
        finally { fixedDamageNoEffect = null; }
    }
}
