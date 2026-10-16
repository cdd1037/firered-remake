using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record FirstSeviiTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles);
public static class PretFirstSeviiRules
{
    private static T Read<T>(string root,string file, SharedMachineCatalog? shared = null)
    {
        var bytes=File.ReadAllBytes(Path.Combine(root,"content/generated/data/regions/first-sevii",file));
        return JsonSerializer.Deserialize<T>(bytes,SharedTrainerCatalog.Options(root, (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions(new JsonSerializerOptions {PropertyNamingPolicy=JsonNamingPolicy.CamelCase})))
            ??throw new InvalidDataException("Missing First Sevii source document.");
    }
    public static FirstSeviiTrainerCatalog LoadTrainers(string root, SharedMachineCatalog? shared = null)
    {
        var d=Read<FirstSeviiTrainerDocument>(root,"trainers.json", shared);
        var result=new FirstSeviiTrainerCatalog(d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(FirstSeviiBattleKey.For));
        FirstSeviiTrainerPartyFactory.ValidateCatalog(result);return result;
    }
    public static FirstSeviiItemCatalog LoadItems(string root, SharedMachineCatalog? shared = null)
    { var result=Read<FirstSeviiItemCatalog>(root,"items.json", shared);result.Validate();return result; }
}
