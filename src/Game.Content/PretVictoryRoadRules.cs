using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
namespace Game.Content;

public sealed record VictoryRoadTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,ImmutableArray<CeruleanTrainerBattleDefinition> Battles);
/// <summary>Authenticated Victory Road rules without new ownership or an Application profile.</summary>
public static class PretVictoryRoadRules
{
    public const string SourceCommit="037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public static VictoryRoadTrainerDocument LoadTrainerDocument(string root)
    {
        var d=ContentJson.ParseStrict<VictoryRoadTrainerDocument>(Encoding.UTF8.GetString(Read(root,"content/generated/data/regions/victory-road/trainers.json")), SharedTrainerCatalog.Options(root));
        _=Catalog(d);return d;
    }
    public static VictoryRoadTrainerCatalog LoadTrainers(string root)=>Catalog(LoadTrainerDocument(root));
    private static VictoryRoadTrainerCatalog Catalog(VictoryRoadTrainerDocument d)
    {
        var c=new VictoryRoadTrainerCatalog(d.Trainers.ToImmutableDictionary(t=>t.Id),d.Battles.ToImmutableDictionary(b=>b.ScriptEntry,StringComparer.Ordinal));
        VictoryRoadTrainerPartyFactory.ValidateCatalog(c);return c;
    }
    private static byte[] Read(string root, string relative) => GeneratedResourceFile.Read(root, relative);
    private static void Need(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
}
