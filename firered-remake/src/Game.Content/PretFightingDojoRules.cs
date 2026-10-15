using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;
namespace Game.Content;

public sealed record FightingDojoTrainerDocument(ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);
/// <summary>Bounded Dojo ownership, opponent, trainer and machine catalogs over authenticated Power Plant resources.</summary>
public static class PretFightingDojoRules
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/fighting-dojo", file));
        return JsonSerializer.Deserialize<T>(bytes, SharedTrainerCatalog.Options(root, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } })) ?? throw new InvalidDataException("fighting_dojo_source_missing");
    }
    public static FightingDojoTrainerDocument LoadDocument(string root)
    {
        var document = Read<FightingDojoTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document); return document;
    }
    public static FightingDojoTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static FightingDojoTrainerCatalog LoadTrainers(FightingDojoTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty) throw new InvalidDataException("fighting_dojo_trainers_missing");
        var result = new FightingDojoTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        FightingDojoTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root,"rules.json");
}
