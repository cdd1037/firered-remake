using Game.Domain.Pokemon;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal static bool HasAllySupportRecipe(string effect) => effect is "EFFECT_FOLLOW_ME" or "EFFECT_HELPING_HAND";

    internal void ExecuteAllySupport(BattlerSlot actor, int slot, PokemonMoveDefinition move, ref bool handled)
    {
        if (!HasAllySupportRecipe(move.Effect)) return;
        handled = true;
        // Both scripts start as MOVE_TARGET_USER. trysethelpinghand changes the target only
        // after attackstring/ppreduce, so a Pressure partner never increases this PP cost.
        SpendEarlyPartyPp(actor, actor, slot, move);
        ApplyAllySupport(actor, actor, move);
    }

    internal void ApplyAllySupport(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        if (move.Effect == "EFFECT_FOLLOW_ME")
        {
            int before = SourceDoubleBattle ? FollowMeTimer(actor.Side) : 0;
            if (SourceDoubleBattle) SetFollowMe(actor);
            Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, "follow-me", before, 1,
                SourceDoubleBattle ? "move" : "single-battle-no-redirection"));
            return;
        }
        if (move.Effect != "EFFECT_HELPING_HAND") throw new SourceRecipeUnsupported("source_ally_support_recipe");
        // Preserve the historical singles representation and event exactly; its nonexistent
        // partner cannot be represented by the singles boundary adapter.
        if (!SourceDoubleBattle)
        {
            SourceCalledProtectionFailure(actor, target, move.Id, "no-ally-in-single-battle");
            return;
        }
        target = actor.Partner;
        SetAllySupportTarget(target);
        // Cmd_trysethelpinghand checks source absence and both protect flags. Neither HP,
        // deferred KO vacancy, nor whether the partner already acted changes this command.
        if (AllySupportAbsent(target) || HelpingHand(actor) || HelpingHand(target))
        {
            SourceCalledProtectionFailure(actor, target, move.Id, "helping-hand-unavailable");
            return;
        }
        SetHelpingHand(target);
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, target, "helping-hand", 0, 1, "move"));
    }
}
