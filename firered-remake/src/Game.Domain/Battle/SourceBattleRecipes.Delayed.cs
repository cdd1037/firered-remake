using Game.Domain.Pokemon;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    // BattleScript_MonTookFutureAttack: shared accuracycheck and ApplyRandomDmgMultiplier.
    // Context-specific adjustment (Substitute/Endure/items), HP storage and callback ownership
    // stay with the established singles adapter or the positively bounded four-slot adapter.
    internal (bool Hit, int Damage, int Percent) PrepareFutureHit(BattlerSlot actor, BattlerSlot target,
        PokemonMoveDefinition move, int storedDamage, string purpose)
    {
        if (!SourceGenericAccuracy(actor, target, move, purpose))
        {
            Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, "future-accuracy"));
            return (false, 0, 0);
        }
        int percent = 100 - Draw(purpose + ".damage", 16) % 16;
        return (true, Math.Max(1, checked(storedDamage * percent) / 100), percent);
    }
}
