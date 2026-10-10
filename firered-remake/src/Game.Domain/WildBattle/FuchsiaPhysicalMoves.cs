using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

internal static class FuchsiaPhysicalMoveDefinition
{
    internal static bool Supports(MoveId move) => move.Value is "pret:MOVE_SWORDS_DANCE" or "pret:MOVE_FURY_CUTTER";

    internal static bool ValidDefinition(PokemonMoveDefinition move)
    {
        if (!Supports(move.Id) || move.Flags.IsDefault) return false;
        var expected = move.Id.Value == "pret:MOVE_SWORDS_DANCE"
            ? (14, 30, 0, 0, "TYPE_NORMAL", "EFFECT_ATTACK_UP_2", "MOVE_TARGET_USER", 0, 0)
            : (210, 20, 10, 95, "TYPE_BUG", "EFFECT_FURY_CUTTER", "MOVE_TARGET_SELECTED", 0, 0);
        string[] flags = move.Id.Value == "pret:MOVE_SWORDS_DANCE"
            ? ["FLAG_SNATCH_AFFECTED"]
            : ["FLAG_MAKES_CONTACT", "FLAG_PROTECT_AFFECTED", "FLAG_MIRROR_MOVE_AFFECTED", "FLAG_KINGS_ROCK_AFFECTED"];
        return (move.SourceMoveId, move.Pp, move.Power, move.Accuracy, move.Type, move.Effect,
            move.Target, move.SecondaryEffectChance, move.Priority) == expected &&
            move.Flags.Order(StringComparer.Ordinal).SequenceEqual(flags.Order(StringComparer.Ordinal));
    }
}

public static partial class WildBattleRules
{
    // Cmd_furycuttercalc increments BEFORE power calculation, with a five-hit cap.
    // This helper takes the stored counter before the next use, including at the cap.
    internal static int NextFuryCutterPower(int counter) => counter is >= 0 and <= 5
        ? 10 << Math.Min(counter, 4) : throw new ArgumentOutOfRangeException(nameof(counter));

    internal sealed partial class Execution
    {
        private void ChangeFuryCutterCounter(WildBattleSide side, int counter, string reason)
        {
            int before = Get(side).FuryCutterCounter;
            if (before == counter) return;
            Set(side, Get(side) with { FuryCutterCounter = counter });
            Add(n => new WildBattleMtMoonVolatileEvent(n, State.Turn, side, "fury-cutter", before, counter, reason));
        }

        // CancelMultiTurnMoves and ClearFuryCutterDestinyBondGrudge clear the counter.
        // FRLG deliberately omits the former call for full paralysis. Historical
        // profiles still cancel their existing Rollout state on that path.
        internal void FuchsiaCancelPhysicalChain(WildBattleSide side, string reason)
        {
            if (State.RulesVersion == FuchsiaRulesVersion && reason != "paralysis")
                ChangeFuryCutterCounter(side, 0, reason);
        }

        private bool FuchsiaPhysicalScript(PokemonCatalog catalog, WildBattleSide side,
            int slot, PokemonMoveDefinition move, string purpose)
        {
            if (move.Effect == "EFFECT_ATTACK_UP_2")
            {
                // BattleScript_EffectStatUp: no accuracy check, even at the cap.
                SpendEarlyPartyPp(side, side, slot, move);
                ChangeStage(side, side, WildBattleStat.Attack, 2, move.Id);
                return true;
            }
            if (move.Effect != "EFFECT_FURY_CUTTER") return false;

            var targetSide = Other(side);
            // BattleScript_EffectFuryCutter spends PP before accuracy. On a miss,
            // furycuttercalc jumps directly to MoveMissedPause: no damage RNG.
            SpendEarlyPartyPp(side, targetSide, slot, move);
            if (!EarlyPartyAccuracy(side, targetSide, move, purpose))
            {
                ChangeFuryCutterCounter(side, 0, "missed");
                return true;
            }

            var actor = Get(side); var target = Get(targetSide);
            int power = NextFuryCutterPower(actor.FuryCutterCounter);
            ChangeFuryCutterCounter(side, Math.Min(5, actor.FuryCutterCounter + 1), "hit");
            actor = Get(side);
            bool critical = CeruleanCritical(actor, target, move.Effect, purpose,
                EarlyPartyCriticalDenominator(actor, move.Effect));
            int Damage(int percent) => ForestBattleDamage.Calculate(catalog, actor, target, move.Id,
                critical, percent, true, true, true, true, route3Rules: true, dynamicPower: power,
                mtMoonRules: true, ceruleanRules: true, billRules: true, vermilionRules: true,
                ssAnneRules: true, vermilionGymRules: true, route11Rules: true,
                rain: SourceCalledRain, rockTunnelRules: true,
                reflect: RockTunnelReflects(targetSide), sourceDefinition: activeInvocation is null ? null : catalog.Moves[move.Id], sun: SourceCalledSun, lightScreen: SourceCalledLightScreen(targetSide), weatherSuppressed: SourceDamageWeatherSuppressed).Damage;

            // typecalc precedes adjustnormaldamage. Its no-effect jump revisits
            // furycuttercalc, clears the counter, and skips the damage/secondary draws.
            if (Damage(100) == 0)
            {
                ChangeFuryCutterCounter(side, 0, "no-effect");
                ReportPewterEffectiveness(catalog, side, targetSide, move.Id);
                return true;
            }
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            int damage = RockTunnelAdjustDamage(targetSide, Damage(percent));
            SourceGenericApplyDamage(side, targetSide, move.Id, damage, critical, percent);
            int after = Get(targetSide).Pokemon.CurrentHp;
            ReportPewterEffectiveness(catalog, side, targetSide, move.Id);
            // HitFromAtkAnimation always executes seteffectwithchance, including 0%.
            Draw($"{purpose}.secondary-effect", 100);
            if (after == 0) ForestFaint(targetSide);
            if (damage > 0) MtMoonRageOnDamage(side, targetSide, move.Id);
            if (damage > 0 && SourceCalledHitReachedPokemon(targetSide) && WildBattleEffective.Ability(target) == "ABILITY_STATIC" && Get(side).Pokemon.CurrentHp > 0 &&
                Draw($"{purpose}.static", 3) % 3 == 0)
            {
                RecordAbility?.Invoke(targetSide, "ABILITY_STATIC");
                InflictStatus(catalog, side, WildBattleStatus.Paralysis, true);
            }
            Route3ContactAbility(catalog, side, targetSide, move, purpose, damage);
            ForestOutcome(true);
            return true;
        }
    }
}
