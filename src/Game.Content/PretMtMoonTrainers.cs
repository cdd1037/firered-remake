using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record MtMoonTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<ForestTrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretMtMoonTrainers
{
    public const string RelativePath = "content/generated/data/trainer-battles/mtmoon.json";

    public static OrdinaryTrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<MtMoonTrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), SharedTrainerCatalog.Options(root));
        if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported MtMoon trainer document schema.");
        var battles = document.Battles.Select(b => new CeruleanTrainerBattleDefinition(
            b.Trainer, b.Mode, 0, b.BattleTypeFlags, b.ScriptEntry, b.IntroText, b.DefeatText, "",
            b.PostBattleText, b.EntryScript, b.Callback, b.Source)).ToImmutableArray();
        var catalog = new OrdinaryTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), battles, "mtmoon-trainer");
        OrdinaryTrainerSpecialSourceValidation.Validate(catalog);
        return catalog;
    }

}
