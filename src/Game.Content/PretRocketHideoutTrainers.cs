using System.Collections.Immutable;
using System.Text;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record RocketHideoutTrainerDocument(int SchemaVersion,ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    ImmutableArray<PokemonMoveDefinition> Moves,ImmutableArray<PretRoute1MoveName> MoveNames);
/// <summary>Exact source teams and move metadata; no campaign activation or battle execution.</summary>
public static class PretRocketHideoutTrainers
{
    private static RocketHideoutTrainerDocument ReadDocument(PretCanonicalPokemonCatalog canonical, string root)
    {
        var bytes=PretCeladonApproachSource.Read(root,"content/generated/data/trainer-battles/rocket-hideout/trainers.json");
        var d=ContentJson.ParseStrict<RocketHideoutTrainerDocument>(Encoding.UTF8.GetString(bytes),SharedTrainerCatalog.Options(root, canonical.ReferenceOptions()));
        if (d.SchemaVersion != 1) throw new InvalidDataException("Unsupported RocketHideout trainer document schema.");
        foreach (var move in d.Moves)
            if (move.BattleEffectImplemented) throw new InvalidDataException("Trainer metadata cannot enable battle effects.");
        return d;
    }
    public static RocketHideoutTrainerDocument LoadDocument(PretCanonicalPokemonCatalog canonical, string root)
    {
        var document = ReadDocument(canonical, root);
        _ = CreateCatalog(document);
        return document;
    }

    public static OrdinaryTrainerCatalog Load(PretCanonicalPokemonCatalog canonical, string root) =>
        CreateCatalog(ReadDocument(canonical, root));

    private static OrdinaryTrainerCatalog CreateCatalog(RocketHideoutTrainerDocument document)
    {
        var catalog = new OrdinaryTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id),
            document.Battles, "rocket-hideout-trainer");
        OrdinaryTrainerSpecialSourceValidation.Validate(catalog);
        return catalog;
    }
}
