using System.Collections.Immutable;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>KnownTargetAbility is source battle-history knowledge, not the target's hidden actual ability.
/// Null means unrevealed. Source history is side-indexed and may be stale after a player switch.
/// Only the ordinary single-battle forest context is represented.</summary>
public sealed record ForestTrainerAiInput(WildBattleCombatant User, WildBattleCombatant Target,
    string? KnownTargetAbility = null, uint AiFlags = 1);

public sealed record ForestTrainerAbilityRead(int MoveSlot, string Reason, string Ability, bool Guessed);

/// <summary>Faults preserve the input RNG and expose no partial scores, reads or draws.</summary>
public sealed record ForestTrainerAiDecision(TransitionStatus Status, int? MoveSlot, ImmutableArray<int> Scores,
    ImmutableArray<int> SimulatedDamagePercent, RngState Rng, ImmutableArray<RandomTrace> Trace, Diagnostic? Diagnostic)
{
    public bool Succeeded => Status == TransitionStatus.Accepted;
    public ImmutableArray<ForestTrainerAbilityRead> AbilityReads { get; init; } = [];
}
