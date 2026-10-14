using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>Exact original Route19/20 singles and Lia/Luc doubles through the existing trainer construction kernel.</summary>
public static partial class SeafoamTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-seafoam-trainers-v1";
    public static ImmutableArray<TrainerId> AllTrainers => TrainerHashes.Keys.OrderBy(k => k.Value, StringComparer.Ordinal).ToImmutableArray();
    public static ImmutableArray<TrainerId> SupportedTrainers => AllTrainers;
    public static int BasePrize(SeafoamTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("seafoam_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }
    public static TrainerPartyCreationResult Create(SeafoamTrainerCatalog trainers,
        SeafoamOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(SeafoamOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("seafoam_trainer_catalog");
        foreach (var member in trainer.Party)
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("seafoam_trainer_species");
        var source = opponents.Mechanics;
        return TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
    }

    public static void ValidateCatalog(SeafoamTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesByScript is null || catalog.Trainers.Count != 21 || catalog.BattlesByScript.Count != 22)
            throw new NotSupportedException("seafoam_trainer_scope");
        foreach (var (id, hash) in TrainerHashes)
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null || trainer.Party.IsDefault ||
                trainer.Party.Any(p => p is null || p.CustomMoves.IsDefault || p.EncodedSpeciesName.IsDefault) ||
                trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefault || trainer.AiFlagSymbols.IsDefault ||
                CampaignSourceFingerprint.Compute(trainer) != hash) throw new NotSupportedException("seafoam_trainer_source");
        foreach (var (script, hash) in BattleHashes)
            if (!catalog.BattlesByScript.TryGetValue(script, out var battle) || battle is null || battle.ScriptEntry != script ||
                CampaignSourceFingerprint.Compute(battle) != hash) throw new NotSupportedException("seafoam_trainer_battle_source");
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleHashes.TryGetValue(battle.ScriptEntry, out var hash) || CampaignSourceFingerprint.Compute(battle) != hash)
            throw new NotSupportedException("seafoam_trainer_battle_source");
    }
}
