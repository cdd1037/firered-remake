using System.Collections.Immutable;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

public enum PokeFluteBattlePhase { NoEffect, Played, Fanfare, Awakened }
/// <summary>Source party-wide message/fanfare sequence. No individual wake messages are printed.</summary>
public sealed record WildBattlePokeFluteEvent(long Sequence, int Turn, PokeFluteBattlePhase Phase,
    ImmutableArray<int> PlayerPartyIndices, ImmutableArray<int> OpponentPartyIndices) : WildBattleEvent(Sequence, Turn);

public sealed record WildBattlePokeFluteResult(WildBattleResult Battle, ImmutableArray<PartyPokemon> Party)
{
    public bool Succeeded => Battle.Succeeded;
    public string? Fault => Battle.Fault;
}
