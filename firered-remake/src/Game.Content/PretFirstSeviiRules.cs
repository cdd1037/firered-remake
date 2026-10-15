using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record FirstSeviiTrainerDocument(string SourceCommit,string Profile,bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,JsonElement ResolvedMoves);
public static class PretFirstSeviiRules
{
    private static T Read<T>(string root,string file)
    {
        var bytes=File.ReadAllBytes(Path.Combine(root,"content/generated/first-sevii-rules",file));
        return JsonSerializer.Deserialize<T>(bytes,new JsonSerializerOptions {PropertyNamingPolicy=JsonNamingPolicy.CamelCase})
            ??throw new InvalidDataException("Missing First Sevii source document.");
    }
    public static FirstSeviiTrainerCatalog LoadTrainers(string root)
    {
        var d=Read<FirstSeviiTrainerDocument>(root,"trainers.json");
        var result=new FirstSeviiTrainerCatalog(d.SourceCommit,d.Profile,d.CompleteSourceRoster,
            d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(FirstSeviiBattleKey.For));
        FirstSeviiTrainerPartyFactory.ValidateCatalog(result);return result;
    }
    public static FirstSeviiItemCatalog LoadItems(string root)
    { var result=Read<FirstSeviiItemCatalog>(root,"items.json");result.Validate();return result; }
}
