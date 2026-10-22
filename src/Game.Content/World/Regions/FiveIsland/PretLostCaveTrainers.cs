using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Trainers;

namespace Game.Content;

public static class PretLostCaveTrainers
{
    public static OrdinaryTrainerCatalog Load(string root)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(GeneratedResourceFile.SafePath(root,
            "content/generated/data/encounters/lost-cave.json")));
        var options = SharedTrainerCatalog.Options(root);
        var trainers = ContentJson.ParseStrict<ImmutableArray<TrainerDefinition>>(
            document.RootElement.GetProperty("trainers").GetRawText(), options);
        var battles = ContentJson.ParseStrict<ImmutableArray<CeruleanTrainerBattleDefinition>>(
            document.RootElement.GetProperty("battles").GetRawText(), options);
        return new(trainers.ToImmutableDictionary(t => t.Id), battles, "lost-cave-trainer");
    }
}
