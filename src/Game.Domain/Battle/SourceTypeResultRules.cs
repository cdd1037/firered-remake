using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal enum SourceTypeCommand { TypeCalc, TypeCalc2, AccuracyMiss, MissedTypeCalc }
internal readonly record struct SourceTypeResult(bool TypeImmune, bool Levitate, bool WonderGuard,
    bool SuperEffective, bool NotVeryEffective)
{
    internal bool Immune => TypeImmune || Levitate || WonderGuard;
    internal WildBattleEffectiveness? Effectiveness => TypeImmune || Levitate ? WildBattleEffectiveness.NoEffect :
        SuperEffective && !NotVeryEffective ? WildBattleEffectiveness.SuperEffective :
        NotVeryEffective && !SuperEffective ? WildBattleEffectiveness.NotVeryEffective : null;
}

// Original Cmd_typecalc, Cmd_typecalc2 and CheckWonderGuardAndLevitate have
// different result registers. In particular typecalc cancels opposite rows;
// typecalc2/miss retain both flags, and typecalc2 stops at the first immunity.
internal static class SourceTypeResultRules
{
    internal static SourceTypeResult Calculate(ElementType attack, ElementType first, ElementType second,
        string ability, bool identified, int power, bool struggle = false, bool attacksThisTurn = true,
        SourceTypeCommand command = SourceTypeCommand.TypeCalc)
    {
        if (struggle && command != SourceTypeCommand.TypeCalc2 || command == SourceTypeCommand.AccuracyMiss && power == 0)
            return new();
        bool levitate = ability == "ABILITY_LEVITATE" && attack == ElementType.Ground;
        bool immune = false, super = false, resisted = false;
        if (!levitate)
        foreach (var row in TypeRules.OrderedEffects(attack, first, second))
        {
            if (identified && row.Defense == ElementType.Ghost && attack is ElementType.Normal or ElementType.Fighting) continue;
            if (row.MultiplierTenths == 0)
            {
                immune = true;
                if (command is SourceTypeCommand.TypeCalc or SourceTypeCommand.MissedTypeCalc) { super = false; resisted = false; }
                if (command == SourceTypeCommand.TypeCalc2) break;
            }
            else if (command is SourceTypeCommand.TypeCalc or SourceTypeCommand.MissedTypeCalc)
            {
                if (power == 0 || immune || command == SourceTypeCommand.MissedTypeCalc) continue;
                if (row.MultiplierTenths == 20) { if (resisted) resisted = false; else super = true; }
                if (row.MultiplierTenths == 5) { if (super) super = false; else resisted = true; }
            }
            else
            {
                super |= row.MultiplierTenths == 20;
                resisted |= row.MultiplierTenths == 5;
            }
        }
        bool guard = ability == "ABILITY_WONDER_GUARD" && attacksThisTurn && power != 0 && (!super || resisted);
        return new(immune, levitate, guard, super, resisted);
    }

    internal static SourceTypeResult Calculate(PokemonCatalog catalog, WildBattleCombatant target,
        PokemonMoveDefinition move, string? effectiveType = null, SourceTypeCommand command = SourceTypeCommand.TypeCalc,
        bool attacksThisTurn = true)
    {
        static ElementType Type(string s) => Enum.Parse<ElementType>(s[5..], true);
        var types = WildBattleEffective.Types(catalog, target);
        return Calculate(Type(effectiveType ?? move.Type), Type(types.Type1), Type(types.Type2),
            WildBattleEffective.Ability(target), target.Identified, move.Power,
            move.Id == WildBattleRules.StruggleMove, attacksThisTurn, command);
    }
}
