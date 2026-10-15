using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Encounters;

namespace Game.Content;

public sealed record SeafoamTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);

/// <summary>Thin source-authenticated Articuno ownership, machines and original Route19/20 trainer teams.</summary>
public static class PretSeafoamRules
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/seafoam-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("seafoam_source_missing");
    }

    public static SeafoamTrainerDocument LoadDocument(string root)
    {
        var document = Read<SeafoamTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document); return document;
    }
    public static SeafoamTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static SeafoamTrainerCatalog LoadTrainers(SeafoamTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty || document.Trainers.Sum(t => t.Party.Length) != 57)
            throw new InvalidDataException("seafoam_trainers_missing");
        var result = new SeafoamTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        SeafoamTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root,"rules.json");
    public static SeafoamWaterEncounterCatalog LoadWater(SevenIslandWaterEncounterCatalog predecessor) =>
        SeafoamWaterEncounterCatalog.Authenticate(predecessor, SeafoamEncounterTables.Water);
}
