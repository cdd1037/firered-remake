using System.Collections.Immutable;
using System.Text;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record PokemonTowerTrainerDocument(int SchemaVersion,ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    ImmutableArray<PokemonMoveDefinition> Moves,ImmutableArray<PretRoute1MoveName> MoveNames);
/// <summary>Exact source teams and move metadata; no campaign activation or battle execution.</summary>
public static class PretPokemonTowerTrainers
{
    private static PokemonTowerTrainerDocument ReadDocument(PretCanonicalPokemonCatalog canonical, string root, string? dependencyProjectRoot)
    {
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/data/trainer-battles/pokemon-tower/trainers.json");
        var d=ContentJson.ParseStrict<PokemonTowerTrainerDocument>(Encoding.UTF8.GetString(bytes),SharedTrainerCatalog.Options(dependencyProjectRoot ?? root, canonical.ReferenceOptions()));
        if (d.SchemaVersion != 1) throw new InvalidDataException("Unsupported PokemonTower trainer document schema.");
        foreach (var move in d.Moves)
            if (move.BattleEffectImplemented) throw new InvalidDataException("Trainer metadata cannot enable battle effects.");
        return d;
    }
    public static PokemonTowerTrainerDocument LoadDocument(PretCanonicalPokemonCatalog canonical, string root, string? dependencyProjectRoot = null)
    {
        var document = ReadDocument(canonical, root, dependencyProjectRoot);
        _ = CreateCatalog(document);
        return document;
    }

    public static OrdinaryTrainerCatalog Load(PretCanonicalPokemonCatalog canonical, string root, string? dependencyProjectRoot = null) =>
        CreateCatalog(ReadDocument(canonical, root, dependencyProjectRoot));

    private static OrdinaryTrainerCatalog CreateCatalog(PokemonTowerTrainerDocument document)
    {
        var catalog = new OrdinaryTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id),
            document.Battles, "pokemon-tower-trainer");
        OrdinaryTrainerSpecialSourceValidation.Validate(catalog);
        return catalog;
    }
}
