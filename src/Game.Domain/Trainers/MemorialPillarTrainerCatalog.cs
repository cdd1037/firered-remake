using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>The three original first-visit singles at Memorial Pillar, with six source party members.</summary>
public sealed record MemorialPillarTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<string, CeruleanTrainerBattleDefinition> BattlesByScript);

public static class MemorialPillarTrainerPartyFactory
{
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
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new ArgumentException("memorial_pillar_trainer_script_source");
    }

    public static TrainerPartyCreationResult Create(MemorialPillarTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer) || trainer.Party.Any(p =>
                !opponents.Catalog.Species.ContainsKey(p.Species)))
            throw new ArgumentException("memorial_pillar_trainer_opponent_catalog");
        return TrainerPartyFactory.CreateValidated(trainer, opponents.Mechanics.Catalog, rng, opponents.Mechanics);
    }
}
