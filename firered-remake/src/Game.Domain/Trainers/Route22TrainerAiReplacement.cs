using Game.Domain.Pokemon;

namespace Game.Domain.Trainers;

public sealed record Route22TrainerReplacementDecision(TransitionStatus Status, int? PartyIndex, Diagnostic? Diagnostic)
{
    public bool Succeeded => Status == TransitionStatus.Accepted;
}

/// <summary>Faint replacement for the fixed two-member early-rival parties. With exactly one living
/// reserve, the original replacement passes/fallback can only choose it; no RNG or switch scores.</summary>
public static class Route22TrainerAiReplacement
{
    public static Route22TrainerReplacementDecision Choose(PokemonCatalog catalog, ForestTrainerReplacementInput input, bool route3Rules = false)
    {
        Route22TrainerReplacementDecision Fault(string code) => new(TransitionStatus.Fault, null,
            new(code, "Input is outside the bounded Route22 two-member faint replacement profile."));
        if (catalog?.Species.Count != (route3Rules ? 40 : 32)) return Fault("trainer_replacement_catalog_scope");
        if (input is null || input.Party.IsDefault || input.Party.Length != 2 || input.FaintedPartyIndex is < 0 or > 1 ||
            input.FaintedActive is null || input.Target is null) return Fault("route22_trainer_replacement_input");
        if (input.Party.Any(mon => mon is null || mon.HeldItem != "ITEM_NONE" || mon.AttackBadgeBoost) || input.FaintedActive.HeldItem != "ITEM_NONE") return Fault("route22_trainer_replacement_enemy_item");
        foreach (var mon in input.Party.Append(input.FaintedActive).Append(input.Target))
        {
            string? error = Route22TrainerAi.ValidateCombatant(catalog, mon, route3Rules);
            if (error is not null) return Fault("route22_trainer_replacement_" + error);
        }
        if (input.Party[0].Pokemon.SourceSpeciesId != 16 || input.Party[1].Pokemon.SourceSpeciesId is not (1 or 4 or 7))
            return Fault("route22_trainer_replacement_party_order");
        foreach (var mon in input.Party)
            if (!Route22TrainerAi.ValidUser(mon.Pokemon) || mon.Pokemon.Moves.Any(m => !Route22TrainerAi.ValidMove(catalog.Moves[m.Move])))
                return Fault("route22_trainer_replacement_party_roster");
        if (input.FaintedActive.Pokemon.CurrentHp != 0 || input.Target.Pokemon.CurrentHp == 0)
            return Fault("route22_trainer_replacement_faint_state");
        var active = input.Party[input.FaintedPartyIndex].Pokemon;
        if (active with { Moves = input.FaintedActive.Pokemon.Moves } != input.FaintedActive.Pokemon ||
            !active.Moves.SequenceEqual(input.FaintedActive.Pokemon.Moves)) return Fault("route22_trainer_replacement_active_identity");
        int reserve = 1 - input.FaintedPartyIndex;
        if (input.Party[reserve].Pokemon.CurrentHp == 0) return Fault("route22_trainer_replacement_no_reserve");
        if (input.StaleCurrentMove.Value is not ("pret:MOVE_NONE" or "pret:MOVE_STRUGGLE") &&
            !catalog.Moves.ContainsKey(input.StaleCurrentMove)) return Fault("route22_trainer_replacement_stale_move");
        return new(TransitionStatus.Accepted, reserve, null);
    }
}
