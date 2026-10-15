using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>The eleven original first-visit singles, including Selphy's no-intro continuation.</summary>
public sealed record LostCaveTrainerCatalog(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<string, CeruleanTrainerBattleDefinition> BattlesByScript, VsSeekerTrainerCatalog InheritedTrainers);

public static class LostCaveTrainerPartyFactory
{
    public const string Profile = "firered-lost-cave-trainers-v1";
    public static ImmutableArray<TrainerId> AllTrainers { get; } = LostCaveTrainerSource.TrainerIds.OrderBy(t => t.Value, StringComparer.Ordinal).ToImmutableArray();
    public static LostCaveTrainerCatalog Authenticate(VsSeekerTrainerCatalog inherited)
    {
        ArgumentNullException.ThrowIfNull(inherited);
        var result = LostCaveTrainerSource.Create(inherited);
        ValidateCatalog(result); return result;
    }

    public static void ValidateCatalog(LostCaveTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != TrainerPartyFactory.SourceCommit || catalog.Profile != Profile || catalog.CompleteSourceRoster ||
            catalog.InheritedTrainers is null || catalog.Trainers is null || catalog.Trainers.Count != 11 || catalog.BattlesByScript is null || catalog.BattlesByScript.Count != 11)
            throw new ArgumentException("lost_cave_trainer_scope");
        foreach (var id in LostCaveTrainerSource.TrainerIds)
            if (!catalog.Trainers.TryGetValue(id, out var trainer) || trainer is null)
                throw new ArgumentException("lost_cave_trainer_source");
        foreach (var label in LostCaveTrainerSource.BattleScripts)
            if (!catalog.BattlesByScript.TryGetValue(label, out var battle) || battle is null)
                throw new ArgumentException("lost_cave_battle_source");
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !LostCaveTrainerSource.BattleScripts.Contains(battle.ScriptEntry))
            throw new ArgumentException("lost_cave_battle_source");
    }

    public static TrainerPartyCreationResult Create(LostCaveTrainerCatalog trainers,
        OwnedBattlePokemonCatalog owned, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (owned is null || !ReferenceEquals(OwnedBattlePokemonCatalog.For(owned.Catalog), owned) ||
            OwnedBattlePokemonCatalog.SourceAncestor(owned, LostCaveOwnedPokemonCatalog.For) is null ||
            !trainers.Trainers.TryGetValue(id, out var trainer) || trainer.Party.Any(p => !owned.AdmitsTrainerOpponent(p.Species, owned.TrainerOpponentProfile)))
            throw new ArgumentException("lost_cave_trainer_opponent_catalog");
        var source = owned.Mechanics;
        if (LostCaveTrainerSource.InheritedIds.Contains(id))
        {
            if (!ReferenceEquals(trainer, trainers.InheritedTrainers.Trainers[id]))
                throw new ArgumentException("lost_cave_inherited_trainer_identity");
            return VsSeekerTrainerPartyFactory.Create(trainers.InheritedTrainers, source, id, rng);
        }
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        if ((trainer.PartyFlags & 1) == 0) return made;
        return made with { Party = made.Party.Select((member, index) => member with { Pokemon = member.Pokemon with {
            Moves = trainer.Party[index].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
}
