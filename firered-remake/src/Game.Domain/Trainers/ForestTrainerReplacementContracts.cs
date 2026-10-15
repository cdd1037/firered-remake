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
