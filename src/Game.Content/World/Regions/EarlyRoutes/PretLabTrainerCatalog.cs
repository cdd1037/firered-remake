using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record PretLabTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<EarlyRivalBattleDefinition> Battles);

/// <summary>Loads lab battle entries and resolves their shared trainer definitions.</summary>
public static class PretLabTrainerCatalog
{

    public static TrainerCatalog Load(string generatedJson, SharedTrainerCatalog trainers)
    {
        ArgumentNullException.ThrowIfNull(trainers);
        var doc = ContentJson.ParseStrict<PretLabTrainerDocument>(generatedJson, trainers.CreateJsonOptions());
        if (doc.SchemaVersion != 1)
            throw ContentJson.Error("trainer_schema", "$", "Unsupported lab trainer document schema.");
        if (doc.Trainers.IsDefaultOrEmpty || doc.Battles.IsDefaultOrEmpty)
            throw ContentJson.Error("trainer_missing_data", "$", "Trainer selections and battle entries are required.");
        foreach (var battle in doc.Battles)
        {
            if (battle is null) throw ContentJson.Error("trainer_battle", "$.battles", "Null battle parameters.");
        }
        try
        {
            var catalog = new TrainerCatalog(doc.Trainers.ToImmutableDictionary(t => t.Id), doc.Battles.ToImmutableDictionary(b => b.Trainer));
            TrainerPartyFactory.ValidateCatalog(catalog);
            return catalog;
        }
        catch (ArgumentException ex) { throw ContentJson.Error("trainer_catalog", "$", ex.Message); }
        catch (NotSupportedException ex) { throw ContentJson.Error("trainer_catalog", "$", ex.Message); }
    }

}
