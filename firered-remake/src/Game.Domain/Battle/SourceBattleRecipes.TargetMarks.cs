using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasTargetMarkRecipe(string effect) => effect is "EFFECT_LOCK_ON" or "EFFECT_FORESIGHT";
    internal abstract void SetSureHit(BattlerSlot target, BattlerSlot actor);
    internal virtual bool TargetMarkSandstorm => SandstormTurnsRemaining > 0;
    internal virtual bool TargetMarkAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose) =>
        !(Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED")) &&
        EarlyPartyAccuracy(actor, target, move, purpose, false, TargetMarkSandstorm);

    // Both source definitions have accuracy 100. Their actual accuracy command still observes
    // stages, Protect, source-position sure hit, semi-invulnerability and abilities.
    // Evasion-item admission remains outside this family migration.
    internal void ExecuteTargetMark(BattlerSlot actor, BattlerSlot target, int slot, PokemonMoveDefinition move, string purpose)
    {
        if (move.Effect == "EFFECT_LOCK_ON")
        {
            SpendEarlyPartyPp(actor, target, slot, move);
            if (SourceCalledSecondaryBlocked(target))
            {
                if (Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED")) SourceGenericMiss(actor, target, move);
                else Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, "substitute"));
                return;
            }
            if (!TargetMarkAccuracy(actor, target, move, purpose)) { SourceGenericMiss(actor, target, move); return; }
            SetSureHit(target, actor);
            Add(n => new SourceMtMoonVolatileEvent(n, Turn, target, "sure-hit", 0, 2, "move"));
        }
        else
        {
            // Foresight checks accuracy before attackstring/ppreduce; it bypasses Substitute.
            bool hit = TargetMarkAccuracy(actor, target, move, purpose);
            SpendEarlyPartyPp(actor, target, slot, move);
            if (!hit) { SourceGenericMiss(actor, target, move); return; }
            var body = Get(target); Set(target, body with { Identified = true });
            Add(n => new SourceMtMoonVolatileEvent(n, Turn, target, "identified", body.Identified ? 1 : 0, 1, "move"));
        }
    }
}
