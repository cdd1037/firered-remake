using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>The three original first-visit singles at Memorial Pillar, with six source party members.</summary>
public sealed record MemorialPillarTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<string, CeruleanTrainerBattleDefinition> BattlesByScript);

public static class MemorialPillarTrainerPartyFactory
{
    public const string Profile = "firered-original-tm-acquisition-v1-trainers";
    public const string Map = "map.five-island-memorial-pillar";
    public const int VictoryMoney = 1176;
    public static ImmutableArray<TrainerId> AllTrainers { get; } =
        [new("pret:TRAINER_BIRD_KEEPER_MILO"), new("pret:TRAINER_BIRD_KEEPER_CHAZ"), new("pret:TRAINER_BIRD_KEEPER_HAROLD")];
    private static readonly ImmutableHashSet<TrainerId> TrainerIds = ImmutableHashSet.CreateRange<TrainerId>([
        new("pret:TRAINER_BIRD_KEEPER_MILO"),
        new("pret:TRAINER_BIRD_KEEPER_CHAZ"),
        new("pret:TRAINER_BIRD_KEEPER_HAROLD")]);
    private static readonly ImmutableHashSet<string> BattleScripts = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "FiveIsland_MemorialPillar_EventScript_Milo",
        "FiveIsland_MemorialPillar_EventScript_Chaz",
        "FiveIsland_MemorialPillar_EventScript_Harold");

    public static void ValidateCatalog(MemorialPillarTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != TrainerPartyFactory.SourceCommit || catalog.Profile != Profile || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.Trainers.Count != 3 || catalog.BattlesByScript is null || catalog.BattlesByScript.Count != 3)
            throw new ArgumentException("memorial_pillar_trainer_scope");
        foreach (var id in TrainerIds)
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null)
                throw new ArgumentException("memorial_pillar_trainer_source");
        foreach (var script in BattleScripts)
            if (!catalog.BattlesByScript.TryGetValue(script, out var battle) || battle is null)
                throw new ArgumentException("memorial_pillar_trainer_script_source");
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new ArgumentException("memorial_pillar_trainer_script_source");
    }

    public static TrainerPartyCreationResult Create(MemorialPillarTrainerCatalog trainers,
        MemorialPillarOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !ReferenceEquals(MemorialPillarOpponentPokemonCatalog.For(opponents.Catalog), opponents) ||
            !ReferenceEquals(MemorialPillarOpponentPokemonCatalog.For(opponents.Owned), opponents) ||
            !trainers.Trainers.TryGetValue(id, out var trainer) || trainer.Party.Any(p =>
                !opponents.Owned.SharedBattle.AdmitsTrainerOpponent(p.Species, MemorialPillarOpponentPokemonCatalog.Profile)))
            throw new ArgumentException("memorial_pillar_trainer_opponent_catalog");
        return TrainerPartyFactory.CreateValidated(trainer, opponents.Mechanics.Catalog, rng, opponents.Mechanics);
    }
}
