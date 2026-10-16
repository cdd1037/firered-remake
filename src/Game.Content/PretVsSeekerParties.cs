using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record VsSeekerBattleOccurrenceDocument(ImmutableArray<VsSeekerBattleOccurrence> Occurrences);

/// <summary>Loads Vs. Seeker trainer parties and battle occurrences.</summary>
public static class PretVsSeekerParties
{
    public const string CatalogRelativePath = "content/generated/data/services/vs-seeker-parties/catalog.json";
    public const string OccurrencesRelativePath = "content/generated/data/services/vs-seeker-parties/battle-occurrences.json";

    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,RespectNullableAnnotations = true,RespectRequiredConstructorParameters = true
    };
    public static VsSeekerTrainerCatalog Load(string root) => VsSeekerTrainerCatalog.Authenticate(
        JsonSerializer.Deserialize<VsSeekerTrainerDocument>(Read(root,CatalogRelativePath), SharedTrainerCatalog.Options(root, Options))
        ?? throw new InvalidDataException("vs_seeker_trainer_document"));

    public static ImmutableArray<VsSeekerBattleOccurrence> LoadOccurrences(string root)
    {
        var document = JsonSerializer.Deserialize<VsSeekerBattleOccurrenceDocument>(
            Read(root,OccurrencesRelativePath), Options)
            ?? throw new InvalidDataException("vs_seeker_occurrence_document");
        if (document.Occurrences.IsDefaultOrEmpty)
            throw new InvalidDataException("vs_seeker_occurrence_scope");
        return document.Occurrences;
    }

    private static byte[] Read(string root,string relative)
    {
        string fullRoot = Path.GetFullPath(root), path = Path.GetFullPath(Path.Combine(fullRoot, relative));
        if (!path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException("vs_seeker_source_path");
        byte[] bytes = File.ReadAllBytes(path);
        return bytes;
    }
}
