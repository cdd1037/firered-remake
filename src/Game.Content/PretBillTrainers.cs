using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record BillTrainerDocument(int SchemaVersion, ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    ImmutableArray<CeruleanTrainerBattleDefinition> AdditionalOccurrences);

/// <summary>Loads trainer tables and their initial battle scripts.</summary>
public static class PretBillTrainers
{
    public static OrdinaryTrainerCatalog Load(string root) => Load(root, out _);

    public static OrdinaryTrainerCatalog Load(string root, out ImmutableArray<CeruleanTrainerBattleDefinition> additionalOccurrences)
    {
        byte[] Read(string path)
        {
            var bytes=File.ReadAllBytes(GeneratedResourceFile.SafePath(root,path));
            return bytes;
        }
        var doc=ContentJson.ParseStrict<BillTrainerDocument>(Encoding.UTF8.GetString(Read("content/generated/data/trainer-battles/bill.json")),SharedTrainerCatalog.Options(root));
        if(doc.SchemaVersion!=1 || doc.AdditionalOccurrences.IsDefault || doc.AdditionalOccurrences.Any(b=>b is null))
            throw new InvalidDataException("Bill trainer document identity differs.");
        var shared=SharedTrainerCatalog.LoadProject(root);
        foreach(var occurrence in doc.AdditionalOccurrences) _=shared.Definition(occurrence.Trainer);
        additionalOccurrences = doc.AdditionalOccurrences;
        foreach (var battle in doc.Battles.Where(b => b.Mode != 0))
        {
            string expected = battle.Trainer.Value == "pret:TRAINER_TEAM_ROCKET_GRUNT_6"
                ? "Route24_Text_YoudBecomeTopRocketLeader" : "";
            if (battle.IntroText != "" || battle.PostBattleText != expected)
                throw new InvalidDataException("bill_special_dialogue_binding");
        }
        return new(doc.Trainers.ToImmutableDictionary(t => t.Id), doc.Battles, "bill-trainer");
    }
}
