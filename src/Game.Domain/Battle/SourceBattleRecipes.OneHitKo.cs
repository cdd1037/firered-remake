using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal void ExecuteOneHitKo(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, int slot,
        PokemonMoveDefinition move, string purpose)
    {
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!OneHitNoAccuracy(actor, target, move))
        { Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, "ohko-unreachable")); return; }
        bool immune = FixedDamageType(catalog, target, move).Immune ||
            move.Type == "TYPE_GROUND" && WildBattleEffective.Ability(Get(target)) == "ABILITY_LEVITATE";
        if (immune)
        {
            OneHitReportType(catalog, actor, target, move);
            SourceGenericSecondary(catalog, actor, target, move, purpose, 0);
            return;
        }
        // Cmd_tryKO draws Focus Band before Sturdy or the level/chance test.
        RollFocusBand(target, purpose);
        if (WildBattleEffective.Ability(Get(target)) == "ABILITY_STURDY")
        {
            RecordAbility(target, "ABILITY_STURDY");
            Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move.Id, "ABILITY_STURDY"));
            Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, "ohko-sturdy"));
            return;
        }
        int userLevel = Get(actor).Pokemon.Level, targetLevel = Get(target).Pokemon.Level;
        int roll = CalledSureHit(actor, target) && userLevel >= targetLevel
            ? 0 : Draw($"{purpose}.ohko", 100) % 100 + 1;
        int chance = unchecked((ushort)(move.Accuracy + userLevel - targetLevel));
        if (roll >= chance || userLevel < targetLevel)
        { Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, userLevel < targetLevel ? "ohko-level" : "ohko-miss")); return; }
        // Unlike adjustnormaldamage, tryKO applies Endure/Focus even behind Substitute.
        int damage = AdjustHeldDamage(target, move.Id, Get(target).Pokemon.CurrentHp, purpose, ohko: true, roll: false);
        int lost = SourceGenericApplyDamage(actor, target, move, damage, false, 100);
        OneHitReportType(catalog, actor, target, move);
        SourceGenericSecondary(catalog, actor, target, move, purpose, damage);
        OneHitFinish(catalog, actor, target, move, purpose, lost);
    }
    internal bool OneHitNoAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        CalledSureHit(actor, target) || SemiInvulnerable(target) == WildBattleSemiInvulnerable.None &&
        !(Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED"));
    internal void OneHitReportType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        SourceGenericEffectiveness(catalog, actor, target, move);
    internal void OneHitFinish(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose, int lost)
    {
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, lost);
        ForestOutcome(true);
    }
}
