using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record LeagueRivalTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves);

/// <summary>Exact late Route22 source teams. Existing owned/opponent species and move metadata remain unchanged.</summary>
public static class PretLeagueRivalRules
{
    public const string TrainersSha256 = "f2f127caac8297beed4ffc108501a55433a5fe6fc42b6bb61dae694a50feaf9d";
    public const string SourceInputsSha256 = "dc17f397cb5790523a0ca766c2ecb05882f83a0e2927daf7669f1d64c705d76c";

    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/league-rival-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("League rival source bytes differ: " + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("Missing League rival source document.");
    }

    public static LeagueRivalTrainerDocument LoadDocument(string root)
    {
        var document = Read<LeagueRivalTrainerDocument>(root, "trainers.json", TrainersSha256);
        _ = LoadTrainers(document);
        return document;
    }

    public static LeagueRivalTrainerCatalog LoadTrainers(string root) =>
        LoadTrainers(Read<LeagueRivalTrainerDocument>(root, "trainers.json", TrainersSha256));

    private static LeagueRivalTrainerCatalog LoadTrainers(LeagueRivalTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty)
            throw new InvalidDataException("Missing League rival trainer or battle records.");
        var result = new LeagueRivalTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        LeagueRivalTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }

    public static void ValidateSourceEvidence(string root) =>
        _ = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
}
