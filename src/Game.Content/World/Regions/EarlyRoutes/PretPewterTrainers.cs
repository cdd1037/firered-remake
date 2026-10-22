using System.Collections.Immutable;
using Game.Domain.Trainers;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PewterTrainerBattleDefinition(TrainerId Trainer, int Mode, uint BattleTypeFlags,
    string ScriptEntry, string IntroText, string DefeatText, string PostBattleText,
    string? PostBattleScript, string? MusicOption, string EntryScript, string Callback, TrainerSourceRef Source);

public sealed record PewterTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<PewterTrainerBattleDefinition> Battles);

/// <summary>Loads compiled trainer parties and battle entries without reading source inventories.</summary>
public static class PretPewterTrainers
{
    public const string RelativePath = "content/generated/data/trainer-battles/pewter.json";

    public static OrdinaryTrainerCatalog Load(string root)
    {
        var document = ContentJson.ParseStrict<PewterTrainerDocument>(
            File.ReadAllText(Path.Combine(root, RelativePath)), SharedTrainerCatalog.Options(root));
        if (document.SchemaVersion != 1) throw new InvalidDataException("Unsupported Pewter trainer document schema.");
        var battles = document.Battles.Select(b => new CeruleanTrainerBattleDefinition(
            b.Trainer, b.Mode, 0, b.BattleTypeFlags, b.ScriptEntry, b.IntroText, b.DefeatText, "",
            b.PostBattleText, b.EntryScript, b.Callback, b.Source)
        {
            PostBattleScript = b.PostBattleScript,
            MusicOption = b.MusicOption
        }).ToImmutableArray();
        var catalog = new OrdinaryTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), battles, "pewter-trainer");
        return catalog;
    }

}
