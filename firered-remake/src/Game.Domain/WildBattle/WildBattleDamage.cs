using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

public static class WildBattleDamage
{
    public static DamageResult Calculate(PokemonCatalog catalog, WildBattleCombatant user,
        WildBattleCombatant target, MoveId moveId, bool critical, int randomPercent, string rulesVersion, int? dynamicPower = null, bool rain = false, bool reflect = false) =>
        rulesVersion switch
        {
            WildBattleRules.FuchsiaRulesVersion when catalog.Species.Count == 107 => ForestBattleDamage.Calculate(catalog,user,target,moveId,critical,randomPercent,true,true,true,true,route3Rules:true,dynamicPower:dynamicPower,mtMoonRules:true,ceruleanRules:true,billRules:true,vermilionRules:true,ssAnneRules:true,vermilionGymRules:true,route11Rules:true,rain:rain,rockTunnelRules:true,reflect:reflect),
            WildBattleRules.SnorlaxRulesVersion when Game.Domain.WildBattle.EeveeBattleScope.MechanicsCount(catalog) == 102 => ForestBattleDamage.Calculate(catalog,user,target,moveId,critical,randomPercent,true,true,true,true,route3Rules:true,dynamicPower:dynamicPower,mtMoonRules:true,ceruleanRules:true,billRules:true,vermilionRules:true,ssAnneRules:true,vermilionGymRules:true,route11Rules:true,rain:rain,rockTunnelRules:true,reflect:reflect),
            WildBattleRules.TowerRulesVersion when catalog.Species.Count == 101 => ForestBattleDamage.Calculate(catalog,user,target,moveId,critical,randomPercent,true,true,true,true,route3Rules:true,dynamicPower:dynamicPower,mtMoonRules:true,ceruleanRules:true,billRules:true,vermilionRules:true,ssAnneRules:true,vermilionGymRules:true,route11Rules:true,rain:rain,rockTunnelRules:true,reflect:reflect),
            WildBattleRules.RocketHideoutRulesVersion when catalog.Species.Count == 99 => ForestBattleDamage.Calculate(catalog,user,target,moveId,critical,randomPercent,true,true,true,true,route3Rules:true,dynamicPower:dynamicPower,mtMoonRules:true,ceruleanRules:true,billRules:true,vermilionRules:true,ssAnneRules:true,vermilionGymRules:true,route11Rules:true,rain:rain,rockTunnelRules:true,reflect:reflect),
            WildBattleRules.CeladonGymRulesVersion when catalog.Species.Count == 98 => ForestBattleDamage.Calculate(catalog,user,target,moveId,critical,randomPercent,true,true,true,true,route3Rules:true,dynamicPower:dynamicPower,mtMoonRules:true,ceruleanRules:true,billRules:true,vermilionRules:true,ssAnneRules:true,vermilionGymRules:true,route11Rules:true,rain:rain,rockTunnelRules:true,reflect:reflect),
            WildBattleRules.RockTunnelRulesVersion or WildBattleRules.CeladonApproachRulesVersion when catalog.Species.Count == (rulesVersion == WildBattleRules.CeladonApproachRulesVersion ? 94 : 93) => ForestBattleDamage.Calculate(catalog,user,target,moveId,critical,randomPercent,true,true,true,true,route3Rules:true,dynamicPower:dynamicPower,mtMoonRules:true,ceruleanRules:true,billRules:true,vermilionRules:true,ssAnneRules:true,vermilionGymRules:true,route11Rules:true,rain:rain,rockTunnelRules:true,reflect:reflect),
            WildBattleRules.Route11RulesVersion => ForestBattleDamage.Calculate(catalog,user,target,moveId,critical,randomPercent,true,true,true,true,route3Rules:true,dynamicPower:dynamicPower,mtMoonRules:true,ceruleanRules:true,billRules:true,vermilionRules:true,ssAnneRules:true,vermilionGymRules:true,route11Rules:true,rain:rain),
            WildBattleRules.VermilionGymRulesVersion => ForestBattleDamage.Calculate(catalog,user,target,moveId,critical,randomPercent,true,true,true,true,route3Rules:true,dynamicPower:dynamicPower,mtMoonRules:true,ceruleanRules:true,billRules:true,vermilionRules:true,ssAnneRules:true,vermilionGymRules:true),
            WildBattleRules.SsAnneRulesVersion => ForestBattleDamage.Calculate(catalog,user,target,moveId,critical,randomPercent,true,true,true,true,route3Rules:true,dynamicPower:dynamicPower,mtMoonRules:true,ceruleanRules:true,billRules:true,vermilionRules:true,ssAnneRules:true),
            WildBattleRules.VermilionRulesVersion => ForestBattleDamage.Calculate(catalog,user,target,moveId,critical,randomPercent,true,true,true,true,route3Rules:true,dynamicPower:dynamicPower,mtMoonRules:true,ceruleanRules:true,billRules:true,vermilionRules:true),
            WildBattleRules.BillRulesVersion => ForestBattleDamage.Calculate(catalog,user,target,moveId,critical,randomPercent,true,true,true,true,route3Rules:true,dynamicPower:dynamicPower,mtMoonRules:true,ceruleanRules:true,billRules:true),
            WildBattleRules.CeruleanRulesVersion => ForestBattleDamage.Calculate(catalog, user, target, moveId, critical, randomPercent, true, true, true, true, route3Rules: true, dynamicPower: dynamicPower, mtMoonRules: true, ceruleanRules: true),
            WildBattleRules.MtMoonRulesVersion => ForestBattleDamage.Calculate(catalog, user, target, moveId, critical, randomPercent, true, true, true, true, route3Rules: true, dynamicPower: dynamicPower, mtMoonRules: true),
            WildBattleRules.Route3RulesVersion => ForestBattleDamage.Calculate(catalog, user, target, moveId, critical, randomPercent, true, true, true, true, route3Rules: true),
            WildBattleRules.Route22RulesVersion => ForestBattleDamage.Calculate(catalog, user, target, moveId, critical, randomPercent, true, true, true, true),
            WildBattleRules.EarlyPartyRulesVersion => ForestBattleDamage.Calculate(catalog, user, target, moveId, critical, randomPercent, true, true, true),
            WildBattleRules.EarlyStarterRulesVersion => ForestBattleDamage.Calculate(catalog, user, target, moveId, critical, randomPercent, true, true),
            WildBattleRules.PewterRulesVersion => ForestBattleDamage.Calculate(catalog, user, target, moveId, critical, randomPercent, true),
            WildBattleRules.ForestRulesVersion => ForestBattleDamage.Calculate(catalog, user, target, moveId, critical, randomPercent),
            WildBattleRules.RulesVersion => Calculate(catalog, user, target, moveId, critical, randomPercent),
            _ => throw new NotSupportedException("wild_battle_rules_version")
        };

    public static int Stat(int basis, int stage)
    {
        if (basis is < 1 or > 999 || stage is < 0 or > 12) throw new ArgumentOutOfRangeException(nameof(stage));
        return stage < 6 ? basis * 10 / (10 + (6 - stage) * 5) : basis * (10 + (stage - 6) * 5) / 10;
    }

    public static int Accuracy(int baseAccuracy, int accuracyStage, int evasionStage)
    {
        if (baseAccuracy is < 1 or > 100 || accuracyStage is < 0 or > 12 || evasionStage is < 0 or > 12)
            throw new ArgumentOutOfRangeException(nameof(accuracyStage));
        (int numerator, int denominator) = Math.Clamp(accuracyStage + 6 - evasionStage, 0, 12) switch
        {
            0 => (33, 100), 1 => (36, 100), 2 => (43, 100), 3 => (50, 100), 4 => (60, 100),
            5 => (75, 100), 6 => (1, 1), 7 => (133, 100), 8 => (166, 100), 9 => (2, 1),
            10 => (233, 100), 11 => (133, 50), _ => (3, 1)
        };
        return baseAccuracy * numerator / denominator;
    }

    public static DamageResult Calculate(PokemonCatalog catalog, WildBattleCombatant user,
        WildBattleCombatant target, MoveId moveId, bool critical, int randomPercent, bool forestRules = false)
    {
        if (forestRules) return ForestBattleDamage.Calculate(catalog, user, target, moveId, critical, randomPercent);
        int attack = Math.Max(1, Stat(user.Pokemon.Stats.Attack, critical && user.AttackStage <= 6 ? 6 : user.AttackStage));
        int defense = Math.Max(1, Stat(target.Pokemon.Stats.Defense, critical && target.DefenseStage >= 6 ? 6 : target.DefenseStage));
        if (moveId == WildBattleRules.StruggleMove)
            return DamageRules.Calculate(new(user.Pokemon.Level, 50, ElementType.Normal, attack, defense,
                ElementType.Mystery, null, ElementType.Mystery, null, critical, randomPercent));
        var move = catalog.Moves[moveId];
        if (move.Id.Value is not ("pret:MOVE_TACKLE" or "pret:MOVE_SCRATCH")) throw new NotSupportedException("wild_battle_damage_move");
        var a = WildBattleEffective.Types(catalog, user); var d = WildBattleEffective.Types(catalog, target);
        ElementType Type(string symbol) => Enum.Parse<ElementType>(symbol[5..], true);
        return DamageRules.Calculate(new(user.Pokemon.Level, move.Power, ElementType.Normal, attack, defense,
            Type(a.Type1), Type(a.Type2), Type(d.Type1), Type(d.Type2), critical, randomPercent));
    }
}
