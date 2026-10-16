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
    public static BillTrainerCatalog Load(string root)
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
        var catalog=new BillTrainerCatalog(doc.Trainers.ToImmutableDictionary(t=>t.Id),doc.Battles.ToImmutableDictionary(t=>t.Trainer))
        { AdditionalOccurrences=doc.AdditionalOccurrences };
        BillTrainerPartyFactory.ValidateCatalog(catalog);return catalog;
    }
}
