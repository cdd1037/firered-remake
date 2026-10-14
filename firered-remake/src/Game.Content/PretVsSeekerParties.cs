using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record VsSeekerBattleOccurrenceDocument(string SourceCommit, string Profile,
    ImmutableArray<VsSeekerBattleOccurrence> Occurrences);

/// <summary>Fixed artifact authentication precedes deserialization; raw Git blobs are evidence,
/// never executable inputs. The existing canonical mechanics loader is reused by callers.</summary>
public static class PretVsSeekerParties
{
    public const string CatalogRelativePath = "content/generated/vs-seeker-parties/catalog.json";
    public const string OccurrencesRelativePath = "content/generated/vs-seeker-parties/battle-occurrences.json";
    public const string LockRelativePath = "content/sources/vs-seeker-parties/source-lock.json";
    public const string CatalogSha256 = "8ead46783ef3860e167a57b8b833271efb95cddf20164753f21c1e4fb16f392e";
    public const string OccurrencesSha256 = "f1e98c69dbb055744ef297fd297830abffe59db4a8268c01295ad23c697ff1c7";
    public const string LockSha256 = "9e824bd409d03beaaf45ae59564a00e0969ebb1a37dbbca1c2482f12de489ec7";
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true, RespectRequiredConstructorParameters = true
    };
    public static VsSeekerTrainerCatalog Load(string root) => VsSeekerTrainerCatalog.Authenticate(
        JsonSerializer.Deserialize<VsSeekerTrainerDocument>(Read(root, CatalogRelativePath, CatalogSha256, 938554), Options)
        ?? throw new InvalidDataException("vs_seeker_trainer_document"));

    public static ImmutableArray<VsSeekerBattleOccurrence> LoadOccurrences(string root)
    {
        var document = JsonSerializer.Deserialize<VsSeekerBattleOccurrenceDocument>(
            Read(root, OccurrencesRelativePath, OccurrencesSha256, 243473), Options)
            ?? throw new InvalidDataException("vs_seeker_occurrence_document");
        if (document.SourceCommit != TrainerPartyFactory.SourceCommit || document.Profile != VsSeekerTrainerCatalog.Profile ||
            document.Occurrences.IsDefault || document.Occurrences.Length != 468)
            throw new InvalidDataException("vs_seeker_occurrence_scope");
        return document.Occurrences;
    }

    public static void ValidateSourceEvidence(string root)
    {
        byte[] bytes = Read(root, LockRelativePath, LockSha256);
        using var sourceLock = JsonDocument.Parse(bytes);
        var document = sourceLock.RootElement;
        if (document.GetProperty("commit").GetString() != TrainerPartyFactory.SourceCommit ||
            document.GetProperty("tree").GetString() != "7515cd4067ea55f8cdbc73624478afd2736c58b9" ||
            document.GetProperty("profile").GetString() != VsSeekerTrainerCatalog.Profile)
            throw new InvalidDataException("vs_seeker_source_lock");
        foreach (var input in document.GetProperty("files").EnumerateArray())
        {
            string path = input.GetProperty("path").GetString()!;
            var data = Read(root, "content/raw/vs-seeker-parties/" + path, input.GetProperty("sha256").GetString()!,
                input.GetProperty("byteLength").GetInt32());
            string blob = Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {data.Length}\0"), .. data]));
            if (blob != input.GetProperty("gitBlobSha1").GetString()) throw new InvalidDataException("vs_seeker_source_blob:" + path);
        }
    }

    private static byte[] Read(string root, string relative, string expected, int? length = null)
    {
        string fullRoot = Path.GetFullPath(root), path = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("vs_seeker_source_path");
        if (length is { } size && new FileInfo(path).Length != size) throw new InvalidDataException("vs_seeker_source_length:" + relative);
        byte[] bytes = File.ReadAllBytes(path);
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected) throw new InvalidDataException("vs_seeker_source_hash:" + relative);
        return bytes;
    }
}
