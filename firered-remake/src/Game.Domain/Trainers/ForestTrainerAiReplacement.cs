using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>StaleCurrentMove is gCurrentMove at the replacement boundary, including MOVE_NONE after
/// residual/previous replacement cleanup. Target is the player's active after EXP and any forced replacement.</summary>
public sealed record ForestTrainerReplacementInput(ImmutableArray<WildBattleCombatant> Party, int FaintedPartyIndex,
    WildBattleCombatant FaintedActive, WildBattleCombatant Target, MoveId StaleCurrentMove);

public sealed record ForestTrainerReplacementDecision(TransitionStatus Status, int? PartyIndex,
    ImmutableArray<int> Scores, Diagnostic? Diagnostic)
{
    public bool Succeeded => Status == TransitionStatus.Accepted;
}

/// <summary>Bounded original GetMostSuitableMonToSwitchInto, including its stale-move and active-stat bugs.
/// This is opponent replacement after fainting, not voluntary switching. It never consumes RNG.</summary>
public static class ForestTrainerAiReplacement
{
    public static ForestTrainerReplacementDecision Choose(PokemonCatalog catalog, ForestTrainerReplacementInput input, bool pewterRules = false, bool earlyStarterRules = false, bool earlyPartyRules = false, bool route22Rules = false, bool route3Rules = false)
    {
        ForestTrainerReplacementDecision Fault(string code) => new(TransitionStatus.Fault, null, [],
            new(code, "Input is outside the supported forest trainer replacement profile."));
        route22Rules |= route3Rules;
        earlyPartyRules |= route22Rules;
        earlyStarterRules |= earlyPartyRules;
        string? error = Validate(catalog, input, pewterRules || earlyStarterRules, earlyStarterRules, earlyPartyRules, route22Rules, route3Rules);
        if (error is not null) return Fault(error);
        var active = input.FaintedActive.Pokemon;
        var a = catalog.Species[active.Species]; var d = catalog.Species[input.Target.Pokemon.Species];
        ElementType Type(string value) => Enum.Parse<ElementType>(value[5..], true);
        bool none = input.StaleCurrentMove.Value == "pret:MOVE_NONE";
        bool struggle = input.StaleCurrentMove == WildBattleRules.StruggleMove;
        var stale = none || struggle ? null : catalog.Moves[input.StaleCurrentMove];
        int power = none ? 0 : struggle ? 50 : stale!.Power;
        var staleType = none || struggle ? ElementType.Normal : Type(stale!.Type);
        bool physical = TypeRules.Category(staleType) == DamageCategory.Physical;
        // FaintClearSetData reset all active stages/status. Cmd_getexp case6 clears its ability
        // and item before any replacement. The source nevertheless uses that fainted mon's stats.
        int attack = physical ? active.Stats.Attack : active.Stats.SpecialAttack;
        int defense = physical ? input.Target.Pokemon.Stats.Defense : input.Target.Pokemon.Stats.SpecialDefense;
        if (physical) defense = Math.Max(1, WildBattleDamage.Stat(defense, input.Target.DefenseStage));
        int baseDamage = attack * power * (2 * active.Level / 5 + 2) / defense / 50;
        if (physical) baseDamage = Math.Max(1, baseDamage);
        baseDamage += 2;
        var scores = new int[input.Party.Length]; Array.Fill(scores, -1);
        int chosen = -1; byte bestDamage = 0;
        for (int i = 0; i < input.Party.Length; i++)
        {
            if (i == input.FaintedPartyIndex || input.Party[i].Pokemon.CurrentHp == 0) continue;
            scores[i] = 0;
            // No candidate's damaging move is super-effective against any of the admitted
            // targets. Source typing search therefore always exhausts, then reaches this loop.
            // Source does not check PP here, nor substitute candidate stats/current move.
            foreach (var slot in input.Party[i].Pokemon.Moves)
            {
                var move = catalog.Moves[slot.Move];
                int damage = baseDamage;
                var type = Type(move.Type);
                if (type == Type(a.Type1) || type == Type(a.Type2)) damage = damage * 15 / 10;
                foreach (var effect in TypeRules.OrderedEffects(type, Type(d.Type1), Type(d.Type2)))
                {
                    damage = damage * effect.MultiplierTenths / 10;
                    if (damage == 0 && effect.MultiplierTenths != 0) damage = 1;
                }
                scores[i] = Math.Max(scores[i], damage);
                if (bestDamage < damage) { bestDamage = unchecked((byte)damage); chosen = i; }
            }
        }
        // OpponentHandleChoosePokemon falls back to the first living, inactive party slot.
        if (chosen < 0) chosen = Enumerable.Range(0, input.Party.Length).First(i => i != input.FaintedPartyIndex && input.Party[i].Pokemon.CurrentHp > 0);
        return new(TransitionStatus.Accepted, chosen, [.. scores], null);
    }

    private static string? Validate(PokemonCatalog? catalog, ForestTrainerReplacementInput? input, bool pewterRules, bool earlyStarterRules, bool earlyPartyRules, bool route22Rules, bool route3Rules)
    {
        if (route22Rules && catalog?.Species.Count != (route3Rules ? 40 : 32)) return "forest_trainer_replacement_catalog_scope";
        if (input is null || input.Party.IsDefaultOrEmpty || input.Party.Length > 3 ||
            input.FaintedPartyIndex < 0 || input.FaintedPartyIndex >= input.Party.Length ||
            input.FaintedActive is null || input.Target is null)
            return "forest_trainer_replacement_input";
        foreach (var mon in input.Party.Append(input.FaintedActive).Append(input.Target))
        {
            if (mon is null) return "forest_trainer_replacement_combatant";
            string? error = PokemonStateValidator.Validate(catalog, mon.Pokemon);
            if (error is not null) return "forest_trainer_replacement_" + error;
            if (!(route3Rules ? Route3TrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : route22Rules ? Route22TrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : pewterRules ? PewterTrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : ForestTrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]))) return "forest_trainer_replacement_species";
            if (route22Rules && Route22TrainerAi.ValidateCombatant(catalog, mon, route3Rules) is not null) return "forest_trainer_replacement_route22_combatant";
            if (!route3Rules && !WildBattleRules.Route3Neutral(mon)) return "trainer_replacement_route3_volatile_unsupported";
            if ((mon.HeldItem != "ITEM_NONE" && !(route3Rules && ReferenceEquals(mon, input.Target) && mon.HeldItem == "ITEM_ORAN_BERRY")) || !WildBattleStatus.IsSupported(mon.Status, earlyStarterRules)) return "forest_trainer_replacement_item_status";
            if (mon.AttackStage is < 0 or > 12 || mon.DefenseStage is < 0 or > 12 || mon.SpeedStage is < 0 or > 12 ||
                mon.AccuracyStage is < 0 or > 12 || mon.EvasionStage is < 0 or > 12) return "forest_trainer_replacement_stage";
        }
        if (input.FaintedActive.Pokemon.CurrentHp != 0 || input.Target.Pokemon.CurrentHp == 0)
            return "forest_trainer_replacement_faint_state";
        var partyActive = input.Party[input.FaintedPartyIndex].Pokemon;
        if (partyActive with { Moves = input.FaintedActive.Pokemon.Moves } != input.FaintedActive.Pokemon ||
            !partyActive.Moves.SequenceEqual(input.FaintedActive.Pokemon.Moves)) return "forest_trainer_replacement_active_identity";
        if (!input.Party.Where((_,i)=>i!=input.FaintedPartyIndex).Any(x=>x.Pokemon.CurrentHp>0))
            return "forest_trainer_replacement_no_reserve";
        foreach (var member in input.Party)
        {
            var mon=member.Pokemon;
            if (mon.SourceSpeciesId is not (10 or 11 or 13 or 14) || mon.Level is < 6 or > 9 || mon.Ivs != SixStats.Zero || mon.Evs != SixStats.Zero)
                return "forest_trainer_replacement_party_scope";
            var initial=PokemonMoveRules.InitialMoves(catalog!,catalog!.Species[mon.Species],mon.Level);
            if (!mon.Moves.Select(x=>x.Move).SequenceEqual(initial.Select(x=>x.Move)) || mon.Moves.Any(x=>x.PpUps!=0))
                return "forest_trainer_replacement_party_moves";
        }
        foreach(var mon in input.Party) foreach(var move in mon.Pokemon.Moves)
            if (!ForestBattleScope.ValidDefinition(catalog!.Moves[move.Move])) return "forest_trainer_replacement_move_definition";
        if (input.StaleCurrentMove.Value is not ("pret:MOVE_NONE" or "pret:MOVE_STRUGGLE") &&
            (!catalog!.Moves.TryGetValue(input.StaleCurrentMove,out var stale) || !(route3Rules ? Route3BattleScope.ValidDefinition(stale) : route22Rules ? Route22BattleScope.ValidDefinition(stale) : earlyPartyRules ? EarlyPartyBattleScope.ValidDefinition(stale) : earlyStarterRules ? EarlyStarterBattleScope.ValidDefinition(stale) : pewterRules ? PewterBattleScope.ValidDefinition(stale) : ForestBattleScope.ValidDefinition(stale))))
            return "forest_trainer_replacement_stale_move";
        return null;
    }
}
