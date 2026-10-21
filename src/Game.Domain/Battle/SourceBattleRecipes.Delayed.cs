using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal static bool HasDelayedRecipe(string effect) => effect is "EFFECT_WISH" or "EFFECT_FUTURE_SIGHT";

    // Cmd_trywish / Cmd_trysetfutureattack run after ppreduce, without an accuracy,
    // Protect or Substitute command. Both timers belong to positions, not occupants.
    internal void ExecuteDelayed(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move)
    {
        SpendEarlyPartyPp(actor, target, slot, move);
        if (move.Effect == "EFFECT_WISH")
        {
            if (DelayedWish(actor) is not null)
            { Add(n => new SourceMoveFailedEvent(n, Turn, actor, actor, move.Id, "wish-slot-occupied")); return; }
            var source = WildBattleTransformationRules.PersistentPokemon(Get(actor));
            int partyIndex = DelayedPartyIndex(actor);
            SetDelayedWish(actor, new(2, partyIndex, source.Species, source.OriginalTrainerId, source.Personality));
            Add(n => new SourceCalledDelayedEvent(n, Turn, actor, move.Id, 0, 2, "scheduled", partyIndex));
            return;
        }
        if (DelayedFuture(target) is not null)
        { Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, "future-slot-occupied")); return; }
        int damage = SourceBattleDamage.Calculate(catalog, Get(actor), Get(target), move, false, 100, rain: SourceCalledRain, reflect: RockTunnelReflects(target),
            sun: SourceCalledSun, lightScreen: SourceCalledLightScreen(target), weatherSuppressed: SourceDamageWeatherSuppressed,
            sourceDoubleBattle: SourceDoubleBattle, sourceDefenderCount: SourceDefenderCount(target),
            sourcePlusMinusBoost: SourcePlusMinusBoost(Get(actor))).Steps.Single(s => s.Operation == "plus-two").Value;
        // CalculateBaseDamage stops before STAB/type/random; this command adds Helping Hand.
        if (HelpingHand(actor)) damage = checked(damage * 15) / 10;
        SetDelayedFuture(target, new(3, move.Id, actor, damage));
        Add(n => new SourceCalledDelayedEvent(n, Turn, target, move.Id, 0, 3, "scheduled"));
    }

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

internal static class SourceDelayedRules
{
    internal static string? Validate(SourceBattleFrame frame, SourceBattleSlot cell)
    {
        if (cell.Position.Wish is not { } wish) return null;
        var source = frame.Party(cell.Slot.Side).Members[wish.SourcePartyIndex].Pokemon;
        return wish.SourceSpecies != source.Species || wish.SourceOriginalTrainerId != source.OriginalTrainerId ||
            wish.SourcePersonality != source.Personality ? "source_four_wish_attribution" : null;
    }
}
