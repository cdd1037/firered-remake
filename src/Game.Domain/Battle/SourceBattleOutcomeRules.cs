using Game.Domain.Roaming;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Publishes one outcome and its encounter-specific source receipt.</summary>
internal static class SourceBattleOutcomeRules
{
    internal static SourceBattleFrame SetOutcome(SourceBattleFrame frame, WildBattleOutcome outcome,
        SourceWildBattleOutcome? sourceOutcome = null)
    {
        var next = frame with { Outcome = outcome,
            TrainerEncounter = outcome != WildBattleOutcome.Ongoing && frame.TrainerEncounter is { } trainer
                ? trainer with { StaleCurrentMove = Game.Domain.Trainers.SourceTrainerEncounterPolicy.NoMove, PlayerFaintedThisTurn = false }
                : frame.TrainerEncounter };
        if (next.WildEncounter is not { } encounter) return next;
        var mapped = sourceOutcome ?? outcome switch {
            WildBattleOutcome.Won => SourceWildBattleOutcome.Won, WildBattleOutcome.Lost => SourceWildBattleOutcome.Lost,
            WildBattleOutcome.Draw => SourceWildBattleOutcome.Drew, WildBattleOutcome.Caught => SourceWildBattleOutcome.Caught,
            WildBattleOutcome.Escaped => SourceWildBattleOutcome.Ran, _ => (SourceWildBattleOutcome?)null };
        next = next with { WildEncounter = encounter with { SourceOutcome = mapped } };
        return encounter.CelioRoamer is not { } origin ? next : next with {
            WildEncounter = next.WildEncounter with { CelioRoamer = origin with {
                SourceOutcome = mapped is { } result ? (RoamerBattleOutcome)(int)result : null } } };
    }
}
