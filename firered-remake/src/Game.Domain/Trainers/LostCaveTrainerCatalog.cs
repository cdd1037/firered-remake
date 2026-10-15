using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>The eleven original first-visit singles, including Selphy's no-intro continuation.</summary>
public sealed record LostCaveTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<string, CeruleanTrainerBattleDefinition> BattlesByScript, VsSeekerTrainerCatalog InheritedTrainers);

public static class LostCaveTrainerPartyFactory
{
    public static ImmutableArray<TrainerId> AllTrainers { get; } = LostCaveTrainerSource.TrainerIds.OrderBy(t => t.Value, StringComparer.Ordinal).ToImmutableArray();
    public static LostCaveTrainerCatalog Authenticate(VsSeekerTrainerCatalog inherited, ImmutableDictionary<TrainerId, TrainerDefinition> shared)
    {
        ArgumentNullException.ThrowIfNull(inherited);
        var result = LostCaveTrainerSource.Create(inherited, shared);
        ValidateCatalog(result); return result;
    }

    public static void ValidateCatalog(LostCaveTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
    }

    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !LostCaveTrainerSource.BattleScripts.Contains(battle.ScriptEntry))
            throw new ArgumentException("lost_cave_battle_source");
    }

    public static TrainerPartyCreationResult Create(LostCaveTrainerCatalog trainers,
        GamePokemonCatalog owned, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (owned is null || !trainers.Trainers.TryGetValue(id, out var trainer) || trainer.Party.Any(p => !owned.Opponents.Catalog.Species.ContainsKey(p.Species)))
            throw new ArgumentException("lost_cave_trainer_opponent_catalog");
        var source = owned.Mechanics;
        if (LostCaveTrainerSource.InheritedIds.Contains(id))
        {
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
