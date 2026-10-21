using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record ForestTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<ForestTrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretForestTrainers
{
    public const string RelativePath = "content/generated/data/trainer-battles/forest.json";

    public static OrdinaryTrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<ForestTrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), SharedTrainerCatalog.Options(root));
        if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported Forest trainer document schema.");
        var battles = document.Battles.Select(b => new CeruleanTrainerBattleDefinition(
            b.Trainer, b.Mode, 0, b.BattleTypeFlags, b.ScriptEntry, b.IntroText, b.DefeatText, "",
            b.PostBattleText, b.EntryScript, b.Callback, b.Source)).ToImmutableArray();
        return new(document.Trainers.ToImmutableDictionary(t => t.Id), battles, "forest-trainer");
    }

}
