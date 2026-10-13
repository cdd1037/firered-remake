using Game.Domain.Roaming;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Publishes one outcome and its encounter-specific source receipt.</summary>
internal static class SourceBattleOutcomeRules
{
    internal static SourceBattleFrame SetOutcome(SourceBattleFrame frame, WildBattleOutcome outcome,
        RoamerBattleOutcome? sourceOutcome = null)
    {
        var next = frame with { Outcome = outcome,
            TrainerEncounter = outcome != WildBattleOutcome.Ongoing && frame.TrainerEncounter is { } trainer
                ? trainer with { StaleCurrentMove = Game.Domain.Trainers.ForestTrainerCombatRules.NoMove, PlayerFaintedThisTurn = false }
                : frame.TrainerEncounter };
        if (next.WildEncounter?.CelioRoamer is not { } origin) return next;
        var mapped = sourceOutcome ?? outcome switch {
            WildBattleOutcome.Won => RoamerBattleOutcome.Won, WildBattleOutcome.Lost => RoamerBattleOutcome.Lost,
            WildBattleOutcome.Draw => RoamerBattleOutcome.Drew, WildBattleOutcome.Caught => RoamerBattleOutcome.Caught,
            WildBattleOutcome.Escaped => RoamerBattleOutcome.Ran, _ => (RoamerBattleOutcome?)null };
        return next with { WildEncounter = next.WildEncounter with { CelioRoamer = origin with { SourceOutcome = mapped, OpponentRunSelected = false } } };
    }
}
