using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasAbilityExchangeRecipe(string effect) => effect is "EFFECT_ROLE_PLAY" or "EFFECT_SKILL_SWAP";

    internal void ExecuteAbilityExchange(BattlerSlot actor, BattlerSlot target, int slot, PokemonMoveDefinition move)
    {
        // Both source scripts spend PP before NO_ACC_CALC_CHECK_LOCK_ON, ignore Substitute,
        // and finish through ordinary MOVEEND. Acquisition is never an ON_SWITCHIN call.
        SpendEarlyPartyPp(actor, target, slot, move);
        void Fail(string reason) => Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, reason));
        if (!CopyNoAccuracy(actor, target, move)) { Fail("semi-invulnerable"); return; }
        string own = WildBattleEffective.Ability(Get(actor)), theirs = WildBattleEffective.Ability(Get(target));
        bool swap = move.Effect == "EFFECT_SKILL_SWAP";
        if (theirs == "ABILITY_WONDER_GUARD" || (swap
            ? own == "ABILITY_WONDER_GUARD" || own == "ABILITY_NONE" && theirs == "ABILITY_NONE"
            : theirs == "ABILITY_NONE"))
        { Fail("uncopyable-ability"); return; }
        void Change(BattlerSlot recipient, string ability)
        {
            var body = Get(recipient); var before = body.CalledOverlay;
            var after = (before ?? new()) with { Ability = ability };
            Set(recipient, body with { CalledOverlay = after });
            Add(n => new SourceCalledOverlayChangedEvent(n, Turn, recipient, move.Id, before, after));
        }
        Change(actor, theirs);
        if (swap) Change(target, own);
    }
}
