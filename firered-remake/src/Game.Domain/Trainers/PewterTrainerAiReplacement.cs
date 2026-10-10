using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

public sealed record PewterTrainerReplacementDecision(TransitionStatus Status, int? PartyIndex, Diagnostic? Diagnostic)
{
    public bool Succeeded => Status == TransitionStatus.Accepted;
}

/// <summary>Only the two-member source gym parties: the single living reserve is the unique possible result
/// of GetMostSuitableMonToSwitchInto and OpponentHandleChoosePokemon's fallback. No prediction scores
/// are claimed, no RNG is consumed, and the stale-current-move source quirk is neither repaired nor generalized.</summary>
public static class PewterTrainerAiReplacement
{
    public static PewterTrainerReplacementDecision Choose(PokemonCatalog catalog, ForestTrainerReplacementInput input, bool earlyStarterRules = false, bool route22Rules = false, bool route3Rules = false)
    {
        PewterTrainerReplacementDecision Fault(string code) => new(TransitionStatus.Fault, null,
            new(code, "Input is outside the supported two-member Pewter replacement profile."));
        route22Rules |= route3Rules;
        earlyStarterRules |= route22Rules;
        if (route22Rules && catalog?.Species.Count != (route3Rules ? 40 : 32)) return Fault("trainer_replacement_catalog_scope");
        if (input is null || input.Party.IsDefault || input.Party.Length != 2 || input.FaintedPartyIndex is < 0 or > 1 ||
            input.FaintedActive is null || input.Target is null) return Fault("pewter_trainer_replacement_input");
        foreach (var mon in input.Party.Append(input.FaintedActive).Append(input.Target))
        {
            if (mon is null) return Fault("pewter_trainer_replacement_combatant");
            string? invalid = PokemonStateValidator.Validate(catalog, mon.Pokemon);
            if (invalid is not null) return Fault("pewter_trainer_replacement_" + invalid);
            if (!(route3Rules ? Route3TrainerAi.ValidSpecies(catalog!.Species[mon.Pokemon.Species]) : route22Rules ? Route22TrainerAi.ValidSpecies(catalog.Species[mon.Pokemon.Species]) : PewterTrainerAi.ValidSpecies(catalog.Species[mon.Pokemon.Species]))) return Fault("pewter_trainer_replacement_species");
            if (route22Rules && Route22TrainerAi.ValidateCombatant(catalog, mon, route3Rules) is not null) return Fault("pewter_trainer_replacement_route22_combatant");
            if (!route3Rules && !WildBattleRules.Route3Neutral(mon)) return Fault("trainer_replacement_route3_volatile_unsupported");
            if ((mon.HeldItem != "ITEM_NONE" && !(route3Rules && ReferenceEquals(mon, input.Target) && mon.HeldItem == "ITEM_ORAN_BERRY")) || !WildBattleStatus.IsSupported(mon.Status, earlyStarterRules)) return Fault("pewter_trainer_replacement_item_status");
            if (mon.AttackStage is < 0 or > 12 || mon.DefenseStage is < 0 or > 12 || mon.SpeedStage is < 0 or > 12 ||
                mon.AccuracyStage is < 0 or > 12 || mon.EvasionStage is < 0 or > 12) return Fault("pewter_trainer_replacement_stage");
        }
        bool liam = input.Party[0].Pokemon.SourceSpeciesId == 74 && input.Party[0].Pokemon.Level == 10;
        uint flags = liam ? 1u : 7u;
        if (!PewterTrainerAi.ValidUser(input.Party[0].Pokemon, flags) || !PewterTrainerAi.ValidUser(input.Party[1].Pokemon, flags) ||
            input.Party[0].Pokemon.SourceSpeciesId != 74 || input.Party[1].Pokemon.SourceSpeciesId != (liam ? 27 : 95))
            return Fault("pewter_trainer_replacement_party_scope");
        foreach (var mon in input.Party)
            if (mon.Pokemon.Ivs != SixStats.Zero || mon.Pokemon.Evs != SixStats.Zero || mon.Pokemon.Moves.Any(m =>
                m.PpUps != 0 || !PewterTrainerAi.ValidMove(catalog.Moves[m.Move]))) return Fault("pewter_trainer_replacement_party_moves");
        if (input.FaintedActive.Pokemon.CurrentHp != 0 || input.Target.Pokemon.CurrentHp == 0)
            return Fault("pewter_trainer_replacement_faint_state");
        var partyActive = input.Party[input.FaintedPartyIndex].Pokemon;
        if (partyActive with { Moves = input.FaintedActive.Pokemon.Moves } != input.FaintedActive.Pokemon ||
            !partyActive.Moves.SequenceEqual(input.FaintedActive.Pokemon.Moves)) return Fault("pewter_trainer_replacement_active_identity");
        int reserve = 1 - input.FaintedPartyIndex;
        if (input.Party[reserve].Pokemon.CurrentHp == 0) return Fault("pewter_trainer_replacement_no_reserve");
        if (input.StaleCurrentMove.Value is not ("pret:MOVE_NONE" or "pret:MOVE_STRUGGLE") &&
            !catalog.Moves.ContainsKey(input.StaleCurrentMove)) return Fault("pewter_trainer_replacement_stale_move");
        return new(TransitionStatus.Accepted, reserve, null);
    }
}
