using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasGroundSpreadRecipe(string effect) => effect is "EFFECT_EARTHQUAKE" or "EFFECT_MAGNITUDE";

    // BattleScript_HitsAllWithUndergroundBonusLoop. The action owner calls this once;
    // the script owns numeric all-other traversal, while the existing move-end/faint
    // owner retains target receipts, rewards and deferred replacement scheduling.
    internal void ExecuteGroundSpread(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot initialTarget,
        int slot, PokemonMoveDefinition move, string purpose)
    {
        if (!HasGroundSpreadRecipe(move.Effect) || !MetronomeSourceMoveDefinition.Matches(move))
            throw new SourceRecipeUnsupported("source_ground_spread_definition");
        SpendEarlyPartyPp(actor, initialTarget, slot, move);
        int? power = null;
        if (move.Effect == "EFFECT_MAGNITUDE")
        {
            var strength = MtMoonBattleData.Magnitude(Draw($"{purpose}.magnitude", 100) % 100);
            power = strength.Power;
            Add(n => new SourceMagnitudeEvent(n, Turn, actor, strength.Magnitude, strength.Power));
        }
        foreach (var target in SourceNumericTargets(actor, initialTarget))
        {
            BeginScriptTarget(target);
            // MoveValuesCleanUp resets result flags, not dynamic power. Dig's bonus
            // is recomputed for this target. Neither move uses seteffectwithchance.
            if (!FixedDamageAccuracy(actor, target, move, purpose))
            {
                var defender = Get(target);
                var types = WildBattleEffective.Types(catalog, defender);
                bool immune = WildBattleEffective.Ability(defender) == "ABILITY_LEVITATE" ||
                    types.Type1 == "TYPE_FLYING" || types.Type2 == "TYPE_FLYING";
                GroundSpreadNoEffect();
                // The miss label still executes typecalc; Ground immunity takes
                // precedence in resultmessage after that command updates its flags.
                if (immune) GroundSpreadReportType(catalog, actor, target, move, missed: true);
                else SourceGenericMiss(actor, target, move);
                CompleteScriptTarget();
                continue;
            }
            bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            // CalculateBaseDamage halves only MOVE_TARGET_BOTH in original Gen III.
            // FOES_AND_ALLY retains full damage, including the attacker's partner.
            int rawDamage = SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent, power);
            int damage = FixedDamageAdjust(target, move, rawDamage, purpose);
            int lost = SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
            if (critical && lost == 0 && rawDamage > 0 && !FixedDamageZeroCriticalPrinted(target))
                Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
            GroundSpreadReportType(catalog, actor, target, move, missed: false);
            if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
            if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
            SourceGenericContact(catalog, actor, target, move, purpose, lost);
            CompleteScriptTarget();
        }
        ForestOutcome(true);
    }

    internal virtual void GroundSpreadNoEffect() { }
    internal virtual void GroundSpreadReportType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        PokemonMoveDefinition move, bool missed)
    {
        if (!missed) SourceDamageResultMessage(actor, target, move);
        if (WildBattleEffective.Ability(Get(target)) == "ABILITY_LEVITATE") RecordAbility(target, "ABILITY_LEVITATE");
        ReportPewterEffectiveness(catalog, actor, target, move.Id);
    }
}
