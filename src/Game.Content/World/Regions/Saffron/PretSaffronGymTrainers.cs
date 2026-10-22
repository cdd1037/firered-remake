using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;
public sealed record SaffronGymTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1MoveName> MoveNames);
public static class PretSaffronGymTrainers
{
    public static SaffronGymTrainerDocument LoadDocument(PretCanonicalPokemonCatalog canonical, string root)
    {
        var source = SaffronGymSource.Catalogs(root); var d = source.Parse<SaffronGymTrainerDocument>("trainers.json", canonical);
        if (d.SchemaVersion != 1) throw new InvalidDataException("Unsupported SaffronGym trainer document schema.");
        TrainerCatalogValidation.Validate(d.Trainers.ToImmutableDictionary(t => t.Id), d.Battles, OrdinaryTrainerCatalog.ValidateBattle);
        foreach (var move in d.Moves)
            if (move.BattleEffectImplemented) throw new InvalidDataException("Trainer metadata cannot enable battle effects.");
        return d;
    }
    public static OrdinaryTrainerCatalog Load(PretCanonicalPokemonCatalog canonical, string root)
    { var d = LoadDocument(canonical, root); return new(d.Trainers.ToImmutableDictionary(t => t.Id), d.Battles, "saffron-gym-trainer"); }
}
