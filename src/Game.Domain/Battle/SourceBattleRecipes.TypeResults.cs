using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal bool SourceTypeResultsEnabled => true;

    internal SourceTypeResult SourceTypeResult(PokemonCatalog catalog, BattlerSlot target, PokemonMoveDefinition move,
        string? type = null, SourceTypeCommand command = SourceTypeCommand.TypeCalc, bool apply = false)
    {
        var result = SourceTypeResultRules.Calculate(catalog, Get(target), move, type, command, !ChargeFirstTurn(move));
        if (SourceTypeResultsEnabled && apply && result.WonderGuard)
        {
            RecordAbility(target, "ABILITY_WONDER_GUARD");
            SourceTypeNoEffect(target, command != SourceTypeCommand.TypeCalc2);
        }
        return result;
    }

    // This is the move's type-result command, after base damage; the raw arithmetic
    // service stays ability-agnostic. MISSED suppresses body/Substitute HP updates,
    // contact/items, critical text and last-hit history even when computed damage > 0.
    private readonly Dictionary<BattlerSlot, (MoveId Move, int Damage)> typeMissDamage = [];
    internal int SourceTypeDamage(PokemonCatalog catalog, BattlerSlot target, PokemonMoveDefinition move,
        int damage, string? type = null, bool suppressTypeMiss = true, SourceTypeCommand command = SourceTypeCommand.TypeCalc)
    {
        typeMissDamage.Remove(target);
        if (!SourceTypeResultsEnabled || !SourceTypeResult(catalog, target, move, type, command, apply: true).WonderGuard || !suppressTypeMiss)
            return damage;
        typeMissDamage[target] = (move.Id, damage);
        return 0;
    }
    internal int TypeCommandDamage(BattlerSlot target, MoveId move, int damage, out bool missed)
    {
        missed = damage == 0 && typeMissDamage.TryGetValue(target, out var held) && held.Move == move;
        return missed ? typeMissDamage[target].Damage : damage;
    }

    internal bool SourceTypeReport(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        PokemonMoveDefinition move, string? type = null, SourceTypeCommand command = SourceTypeCommand.TypeCalc)
    {
        if (!SourceTypeResultsEnabled || !SourceTypeResult(catalog, target, move, type, command, apply: true).WonderGuard) return false;
        // B_MSG_AVOIDED_DMG has priority even over DOESNT_AFFECT_FOE. Do not
        // also emit effectiveness/no-effect text or the ordinary miss message.
        Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move.Id, "ABILITY_WONDER_GUARD"));
        return true;
    }

    internal bool SourceTypeAccuracyMiss(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        if (!SourceTypeResultsEnabled || move.Power == 0 || move.Id == WildBattleRules.StruggleMove ||
            Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED") ||
            BillUnderground(actor, target, move) || ChargeFirstTurn(move)) return false;
        return SourceTypeReport(TypeResultCatalog, actor, target, move, DynamicHitType(move), SourceTypeCommand.AccuracyMiss);
    }
}
