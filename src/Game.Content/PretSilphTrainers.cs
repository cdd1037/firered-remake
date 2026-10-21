using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;
public sealed record SilphTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    ImmutableArray<PokemonMoveDefinition> Moves, ImmutableArray<PretRoute1MoveName> MoveNames);
public static class PretSilphTrainers
{
    private static SilphTrainerDocument ReadDocument(PretCanonicalPokemonCatalog canonical, string root)
    {
        var source = SilphSourcePackage.Catalogs(root); var d = source.Parse<SilphTrainerDocument>("trainers.json", canonical);
        if (d.SchemaVersion != 1) throw new InvalidDataException("Unsupported Silph trainer document schema.");
        foreach (var move in d.Moves)
            if (move.BattleEffectImplemented) throw new InvalidDataException("Trainer metadata cannot enable battle effects.");
        return d;
    }
    public static SilphTrainerDocument LoadDocument(PretCanonicalPokemonCatalog canonical, string root)
    {
        var document = ReadDocument(canonical, root);
        _ = CreateCatalog(document);
        return document;
    }

    public static OrdinaryTrainerCatalog Load(PretCanonicalPokemonCatalog canonical, string root) =>
        CreateCatalog(ReadDocument(canonical, root));

    private static OrdinaryTrainerCatalog CreateCatalog(SilphTrainerDocument document) =>
        new(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles, "silph-trainer");
}
