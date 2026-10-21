using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal static bool HasResidualStatusRecipe(string effect) => effect is "EFFECT_LEECH_SEED" or "EFFECT_INGRAIN";
    internal bool LeechSeedAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose) =>
        !(Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED")) &&
        (CalledSureHit(actor, target) || !BillUnderground(actor, target, move) && EarlyPartyAccuracy(actor, target, move, purpose, false, LeechSeedSandstorm));

    // BattleScript_EffectLeechSeed: PP -> Substitute -> accuracy -> setseeded.
    // setseeded checks a previous seed before Grass; neither makes an accuracy draw disappear.
    internal void ExecuteLeechSeed(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move, string purpose)
    {
        SpendEarlyPartyPp(actor, target, slot, move);
        void Fail(string reason) => Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, reason));
        if (SourceCalledSecondaryBlocked(target)) { Fail("substitute"); return; }
        if (!LeechSeedAccuracy(actor, target, move, purpose)) { SourceGenericMiss(actor, target, move); return; }
        if (LeechSeedSource(target) is not null) { Fail("already-seeded"); return; }
        if (WildBattleEffective.HasType(catalog, Get(target), "TYPE_GRASS")) { Fail("grass-type-immunity"); return; }
        SetLeechSeedSource(target, actor);
        Add(n => new SourceLeechSeedChangedEvent(n, Turn, target, null, actor, "move"));
    }

    // BattleScript_EffectIngrain has no accuracy command, even on reapplication.
    internal void ExecuteIngrain(BattlerSlot actor, int slot, PokemonMoveDefinition move)
    {
        SpendEarlyPartyPp(actor, actor, slot, move);
        if (Get(actor).Rooted)
        { Add(n => new SourceMoveFailedEvent(n, Turn, actor, actor, move.Id, "already-rooted")); return; }
        Set(actor, Get(actor) with { Rooted = true });
        Add(n => new SourceRootsChangedEvent(n, Turn, actor, true, "ingrain"));
    }
}
