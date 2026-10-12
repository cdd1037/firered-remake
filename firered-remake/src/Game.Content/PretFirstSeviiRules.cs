using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Trainers;
namespace Game.Content;
public sealed record FirstSeviiTrainerDocument(string SourceCommit,string Profile,bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles,JsonElement ResolvedMoves);
public static class PretFirstSeviiRules
{
    public const string TrainersSha256="94033333b452c605dfc434f1ef4d6f8012f4ee706d147495db8c96bfd61e10e8";
    public const string ItemsSha256="da5442330491c3c280ed6ef7d06b0c86dd3c36dacf85e326fbae7aa601684440";
    private static T Read<T>(string root,string file,string hash)
    {
        var bytes=File.ReadAllBytes(Path.Combine(root,"content/generated/first-sevii-rules",file));
        if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=hash)throw new InvalidDataException("First Sevii source bytes differ: "+file);
        return JsonSerializer.Deserialize<T>(bytes,new JsonSerializerOptions {PropertyNamingPolicy=JsonNamingPolicy.CamelCase})
            ??throw new InvalidDataException("Missing First Sevii source document.");
    }
    public static FirstSeviiTrainerCatalog LoadTrainers(string root)
    {
        var d=Read<FirstSeviiTrainerDocument>(root,"trainers.json",TrainersSha256);
        var result=new FirstSeviiTrainerCatalog(d.SourceCommit,d.Profile,d.CompleteSourceRoster,
            d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(FirstSeviiBattleKey.For));
        FirstSeviiTrainerPartyFactory.ValidateCatalog(result);return result;
    }
    public static FirstSeviiItemCatalog LoadItems(string root)
    { var result=Read<FirstSeviiItemCatalog>(root,"items.json",ItemsSha256);result.Validate();return result; }
}
