using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal static class SourceProtectionRules
{
    internal static string? Validate(WildBattleCombatant body) =>
        body.ProtectConsecutiveUses is < 0 or > 255 || body.Protected && body.Enduring ||
        body.Pokemon.CurrentHp == 0 && (body.Protected || body.Enduring || body.ProtectConsecutiveUses != 0)
            ? "source_kernel_protection_state" : null;
}

internal abstract partial class SourceRecipeContext
{
    internal static bool HasProtectionRecipe(string effect) => effect is "EFFECT_PROTECT" or "EFFECT_ENDURE";

    // Cmd_setprotectlike. Both owners provide their retained previous move and actual action
    // position. The approved fifth-and-later clamp is the same public singles rule.
    internal void ApplyProtection(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move,
        string purpose, MoveId? previousMove, bool notLastAction)
    {
        var body = Get(actor);
        int before = WildBattleRules.ProtectFamily(previousMove) ? body.ProtectConsecutiveUses : 0;
        int threshold = WildBattleRules.ProtectSuccessThreshold(before);
        int roll = Draw($"{purpose}.protect-success"); // Last-action failure still consumes Random().
        bool success = roll <= threshold && notLastAction;
        int after = success ? Math.Min(255, before + 1) : 0;
        Set(actor, body with { ProtectConsecutiveUses = after,
            Protected = success && move.Effect == "EFFECT_PROTECT", Enduring = success && move.Effect == "EFFECT_ENDURE" });
        Add(n => new SourceProtectAttemptEvent(n, Turn, actor, move.Id, before, after, threshold, roll, notLastAction, success));
        if (!success) Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, "protect-failed"));
        else Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor,
            move.Effect == "EFFECT_ENDURE" ? "endure" : "protect", 0, 1, "move"));
    }
}
