using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>Original Grunt45 no-item/default-move party through the shared source factory.</summary>
public static class FourIslandTrainerPartyFactory
{
    public const string SourceCommit = TrainerPartyFactory.SourceCommit;
    public const string RulesVersion = "firered-four-island-trainers-v1";
    public const string BattleScript = "FourIsland_IcefallCave_Back_EventScript_LoreleiRocketsScene";
    public static TrainerId Grunt45 { get; } = new("pret:TRAINER_TEAM_ROCKET_GRUNT_45");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [Grunt45];
    public static ImmutableArray<TrainerId> SupportedTrainers => AllTrainers;
    internal sealed record ExpectedMember(string Species, int SourceSpecies, string Name, int Level, int Iv, int[] Encoded, string[] Moves, string HeldItem);
    internal sealed record ExpectedTrainer(string Name, int Gender, uint AiFlags, bool DoubleBattle, int[] Encoded, string[] Items, ExpectedMember[] Party, int BasePrize);
    internal static readonly ImmutableDictionary<TrainerId, ExpectedTrainer> Expected = new Dictionary<TrainerId, ExpectedTrainer>
    {
        [Grunt45] = new("GRUNT", 6, 1u, false, [193, 204, 207, 200, 206], ["ITEM_NONE", "ITEM_NONE", "ITEM_NONE", "ITEM_NONE"], [new("pret:SPECIES_ZUBAT",41,"ZUBAT",38,0,[212, 207, 188, 187, 206],["pret:MOVE_WING_ATTACK", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_AIR_CUTTER", "pret:MOVE_MEAN_LOOK"],"ITEM_NONE"), new("pret:SPECIES_ZUBAT",41,"ZUBAT",38,0,[212, 207, 188, 187, 206],["pret:MOVE_WING_ATTACK", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_AIR_CUTTER", "pret:MOVE_MEAN_LOOK"],"ITEM_NONE"), new("pret:SPECIES_GOLBAT",42,"GOLBAT",38,0,[193, 201, 198, 188, 187, 206],["pret:MOVE_BITE", "pret:MOVE_WING_ATTACK", "pret:MOVE_CONFUSE_RAY", "pret:MOVE_AIR_CUTTER"],"ITEM_NONE")], 1216)
    }.ToImmutableDictionary();
    public static int BasePrize(FourIslandTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (id != Grunt45) throw new NotSupportedException("four_island_trainer_id");
        var trainer = trainers.Trainers[id];
        return checked(4 * trainer.BaseMoney * trainer.Party[^1].Level);
    }
    public static TrainerPartyCreationResult Create(FourIslandTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("four_island_trainer_catalog");
        if (trainer.Party.Any(member => !opponents.Catalog.Species.ContainsKey(member.Species)))
            throw new NotSupportedException("four_island_trainer_species");
        return TrainerPartyFactory.CreateValidated(trainer, opponents.Mechanics.Catalog, rng, opponents.Mechanics);
    }
    public static void ValidateCatalog(FourIslandTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != SourceCommit || catalog.Profile != RulesVersion || catalog.CompleteSourceRoster ||
            catalog.Trainers is null || catalog.BattlesByScript is null || catalog.Trainers.Count != 1 || catalog.BattlesByScript.Count != 1)
            throw new NotSupportedException("four_island_trainer_scope");
        if (!catalog.Trainers.TryGetValue(Grunt45, out var trainer) || trainer is null || trainer.Id != Grunt45 ||
            trainer.Party.IsDefaultOrEmpty || trainer.Party.Any(m => m is null || m.CustomMoves.IsDefault || m.EncodedSpeciesName.IsDefaultOrEmpty) ||
            trainer.Items.IsDefault || trainer.EncodedTrainerName.IsDefaultOrEmpty || trainer.AiFlagSymbols.IsDefault)
            throw new NotSupportedException("four_island_trainer_source");
        if (!catalog.BattlesByScript.TryGetValue(BattleScript, out var battle))
            throw new NotSupportedException("four_island_trainer_battle_source");
        ValidateBattle(battle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || battle.ScriptEntry != BattleScript)
            throw new NotSupportedException("four_island_trainer_battle_source");
    }
}
