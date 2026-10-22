using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Trainers;

/// <summary>Validated single-battle definitions and their distinct script occurrences.</summary>
public sealed class OrdinaryTrainerCatalog
{
    public ImmutableDictionary<TrainerId, TrainerDefinition> Trainers { get; }
    public ImmutableArray<CeruleanTrainerBattleDefinition> Battles { get; }
    public string TracePrefix { get; }

    public OrdinaryTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> trainers,
        ImmutableArray<CeruleanTrainerBattleDefinition> battles, string tracePrefix)
    {
        if (string.IsNullOrWhiteSpace(tracePrefix) || battles.IsDefaultOrEmpty)
            throw new ArgumentException("ordinary_trainer_catalog");
        TrainerCatalogValidation.Validate(trainers, battles, ValidateBattle);
        if (trainers.Values.Any(t => t.DoubleBattle))
            throw new ArgumentException("ordinary_trainer_battle_semantics");
        if (battles.Select(b => (b.Source.Path, b.Source.StartLine, b.ScriptEntry)).Distinct().Count() != battles.Length)
            throw new ArgumentException("ordinary_trainer_occurrence_identity");
        if (!trainers.Keys.ToHashSet().SetEquals(battles.Select(b => b.Trainer)))
            throw new ArgumentException("ordinary_trainer_membership");
        Trainers = trainers;
        Battles = battles;
        TracePrefix = tracePrefix;
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || battle.Mode is < 0 or > 3 || battle.RivalFlags != 0 || battle.BattleTypeFlags != 8 ||
            !string.IsNullOrEmpty(battle.VictoryText) || battle.Callback != "CB2_EndTrainerBattle" ||
            battle.EntryScript != (battle.Mode == 3 ? "EventScript_DoNoIntroTrainerBattle" : "EventScript_TryDoNormalTrainerBattle") ||
            string.IsNullOrWhiteSpace(battle.ScriptEntry) || string.IsNullOrWhiteSpace(battle.DefeatText) ||
            battle.Source is null || string.IsNullOrWhiteSpace(battle.Source.Path) ||
            battle.Source.StartLine <= 0 || battle.Source.EndLine < battle.Source.StartLine)
            throw new ArgumentException("ordinary_trainer_battle_semantics");
    }
}
