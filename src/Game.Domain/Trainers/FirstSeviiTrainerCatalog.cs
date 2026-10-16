using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Trainers;
/// <summary>The exact instruction location, including repeated trainer commands within one label.</summary>
public readonly record struct FirstSeviiBattleKey(string ScriptEntry, int SourceLine)
{
    public static FirstSeviiBattleKey For(CeruleanTrainerBattleDefinition battle) => new(battle.ScriptEntry, battle.Source.StartLine);
}
public sealed record FirstSeviiTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<FirstSeviiBattleKey, CeruleanTrainerBattleDefinition> BattlesBySource)
{
    public CeruleanTrainerBattleDefinition Battle(string scriptEntry, int sourceLine)
    {
        return BattlesBySource.TryGetValue(new(scriptEntry, sourceLine), out var battle) ? battle :
            throw new NotSupportedException("first_sevii_trainer_occurrence");
    }
    /// <summary>The four authenticated unconditional no-intro calls repeat after loss, regardless of fought flags.</summary>
    public bool ReplaysDefeatedTrainer(string scriptEntry, int sourceLine) => Battle(scriptEntry,sourceLine) is
        { ScriptEntry: "ThreeIsland_EventScript_BattleBikersScene", Mode: 3 };
}
