using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
namespace Game.Domain.FirstBattle;
public sealed record FirstBattleAiInput(PokemonInstance User,PokemonInstance Target,int UserAttackStage=6,
    int UserDefenseStage=6,int TargetAttackStage=6,int TargetDefenseStage=6,uint AiFlags=7);
public sealed record FirstBattleAiDecision(TransitionStatus Status,int? MoveSlot,ImmutableArray<int> Scores,
    ImmutableArray<int> SimulatedDamagePercent,RngState Rng,ImmutableArray<RandomTrace> Trace,Diagnostic? Diagnostic);
