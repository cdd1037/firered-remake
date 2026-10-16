using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record SeviiRubyTrainerDocument(ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles);

/// <summary>Source-authenticated initial trainer catalog and additive owned/opponent proofs; no runtime route admission.</summary>
public static class PretSeviiRubyRules
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/sevii-ruby", file));
        return JsonSerializer.Deserialize<T>(bytes, SharedTrainerCatalog.Options(root, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } })) ?? throw new InvalidDataException("Missing Sevii Ruby source document.");
    }
    public static SeviiRubyTrainerDocument LoadDocument(string root)
    {
        var document = Read<SeviiRubyTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document); return document;
    }
    public static SeviiRubyTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static SeviiRubyTrainerCatalog LoadTrainers(SeviiRubyTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty) throw new InvalidDataException("Missing Ruby trainer records.");
        var result = new SeviiRubyTrainerCatalog(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        SeviiRubyTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root,"rules.json");
}
