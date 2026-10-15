using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;
public sealed record SilphTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1MoveName> MoveNames);
public sealed record SilphTrainerCatalogContent(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers, ImmutableDictionary<TrainerId, CeruleanTrainerBattleDefinition> Battles);
public static class PretSilphTrainers
{
    public static SilphTrainerDocument LoadDocument(PretCanonicalPokemonCatalog canonical, string root)
    {
        var source = SilphSourcePackage.Catalogs(root); var d = source.Parse<SilphTrainerDocument>("trainers.json", canonical);
        if (d.SchemaVersion != 1) throw new InvalidDataException("Unsupported Silph trainer document schema.");
        SilphTrainerPartyFactory.ValidateCatalog(new(d.Trainers.ToImmutableDictionary(t => t.Id),
            d.Battles.ToImmutableDictionary(b => b.Trainer)));
        foreach (var move in d.Moves)
            if (move.BattleEffectImplemented) throw new InvalidDataException("Trainer metadata cannot enable battle effects.");
        return d;
    }
    public static SilphTrainerCatalogContent Load(PretCanonicalPokemonCatalog canonical, string root)
    { var d = LoadDocument(canonical, root); return new(d.Trainers.ToImmutableDictionary(t => t.Id), d.Battles.ToImmutableDictionary(b => b.Trainer)); }
}
