using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Loads Vs. Seeker rematches, trainer bindings and progress rules.</summary>
public sealed class PretVsSeeker
{
    public const string Profile = "firered-vs-seeker-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public VsSeekerCatalog Catalog { get; }
    private PretVsSeeker(VsSeekerCatalog catalog)
    { Catalog = catalog; }

    public static PretVsSeeker Load(string projectRoot) => LoadPackage(
        Path.Combine(projectRoot, "content/generated/data/services/vs-seeker.json"));

    public static PretVsSeeker LoadPackage(string catalogPath)
    {
        var bytes = File.ReadAllBytes(catalogPath);
        var doc = JsonSerializer.Deserialize<Document>(bytes, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new InvalidDataException("vs_seeker_catalog_empty");
        if (doc.SchemaVersion != 1 || doc.Rematches.IsDefaultOrEmpty || doc.ActorBindings.IsDefaultOrEmpty ||
            doc.ProgressFlags.IsDefaultOrEmpty || doc.ExcludedMaps.IsDefault ||
            doc.Acquisition is null || doc.RunningMovementByGraphics is null ||
            !doc.Rematches.Select(r => r.Index).SequenceEqual(Enumerable.Range(0, doc.Rematches.Length)) ||
            doc.Rematches.Any(r => r.Trainers.IsDefault || r.Trainers.Length != 6 ||
                r.TrainerSourceIds.IsDefault || r.TrainerSourceIds.Length != 6))
            throw new InvalidDataException("vs_seeker_catalog_shape");
        var catalog = new VsSeekerCatalog(doc.Rematches, doc.ActorBindings, doc.Acquisition, doc.RunningMovementByGraphics,
            doc.DefaultRunningMovement, doc.ProgressFlags, doc.ExcludedMaps);
        return new(catalog);
    }

    private sealed record Document(int SchemaVersion, ImmutableArray<VsSeekerRematch> Rematches,
        ImmutableArray<VsSeekerActorBinding> ActorBindings, VsSeekerAcquisition Acquisition,
        ImmutableDictionary<int, int> RunningMovementByGraphics, int DefaultRunningMovement,
        ImmutableArray<string> ProgressFlags,
        ImmutableArray<string> ExcludedMaps);
}
