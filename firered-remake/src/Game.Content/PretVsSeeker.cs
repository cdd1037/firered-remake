using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Trainers;

namespace Game.Content;

/// <summary>Authenticates both the immutable source contract and the exact regenerated small runtime catalog.
/// This loader does not admit new maps, trainer rosters or battle effects.</summary>
public sealed class PretVsSeeker
{
    public const string Profile = "firered-vs-seeker-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourceTree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    public const string SourceLockSha256 = "f5492743073af63221c23754e416574c5660bb3115dcdba906d80b8d156a5241";
    public const string CatalogSha256 = "fe86197c305da3991d16a3c9fc6574740e35ef59fa6ab27e0e2adc788abf4590";
    public const int CatalogByteLength = 191022;
    public VsSeekerCatalog Catalog { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretVsSeeker(VsSeekerCatalog catalog, ImmutableArray<string> hashes)
    { Catalog = catalog; AdditionalSourceHashes = hashes; }

    public static PretVsSeeker Load(string projectRoot) => LoadPackage(
        Path.Combine(projectRoot, "scripts/vs-seeker.lock.json"), Path.Combine(projectRoot, "content/generated/vs-seeker/catalog.json"));

    public static PretVsSeeker LoadPackage(string lockPath, string catalogPath)
    {
        var lockBytes = Read(lockPath, 65536, SourceLockSha256);
        using var sourceLock = JsonDocument.Parse(lockBytes);
        var root = sourceLock.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("profile").GetString() != Profile ||
            root.GetProperty("repository").GetString() != "pret/pokefirered" || root.GetProperty("commit").GetString() != SourceCommit ||
            root.GetProperty("tree").GetString() != SourceTree || root.GetProperty("gameEdition").GetString() != "FIRERED" ||
            root.GetProperty("romRevision").GetInt32() != 0 || root.GetProperty("files").GetArrayLength() != 61)
            throw new InvalidDataException("vs_seeker_lock_identity");
        var hashes = ImmutableArray.CreateBuilder<string>();
        hashes.Add("vs-seeker-lock:" + SourceLockSha256); hashes.Add("vs-seeker-catalog:" + CatalogSha256);
        foreach (var file in root.GetProperty("files").EnumerateArray())
            hashes.Add("vs-seeker:" + file.GetProperty("path").GetString() + ":" + file.GetProperty("sha256").GetString());
        var bytes = Read(catalogPath, CatalogByteLength, CatalogSha256);
        if (bytes.Length != CatalogByteLength) throw new InvalidDataException("vs_seeker_catalog_length");
        var doc = JsonSerializer.Deserialize<Document>(bytes, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        }) ?? throw new InvalidDataException("vs_seeker_catalog_empty");
        if (doc.SchemaVersion != 1 || doc.Profile != Profile || doc.SourceCommit != SourceCommit || doc.SourceTree != SourceTree ||
            doc.SourceLockSha256 != SourceLockSha256 || doc.Rematches.Length != 221 || doc.ActorBindings.Length != 234 ||
            doc.RosterRequirements.Length != 351 || doc.ProgressFlags.Length != 6 || doc.ExcludedMaps.Length != 4 ||
            !doc.Rematches.Select(r => r.Index).SequenceEqual(Enumerable.Range(0, 221)) ||
            doc.Rematches.Any(r => r.Trainers.Length != 6 || r.TrainerSourceIds.Length != 6))
            throw new InvalidDataException("vs_seeker_catalog_shape");
        var catalog = new VsSeekerCatalog(doc.Rematches, doc.ActorBindings, doc.Acquisition, doc.RunningMovementByGraphics,
            doc.DefaultRunningMovement, doc.RosterRequirements, doc.ProgressFlags, doc.ExcludedMaps);
        return new(catalog, hashes.ToImmutable());
    }

    private static byte[] Read(string path, int maxBytes, string expectedHash)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length <= 0 || stream.Length > maxBytes) throw new InvalidDataException("vs_seeker_file_size");
        var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != expectedHash)
            throw new InvalidDataException("vs_seeker_file_hash");
        return bytes;
    }

    private sealed record Document(int SchemaVersion, string Profile, string SourceCommit, string SourceTree, string SourceLockSha256,
        ImmutableArray<VsSeekerRematch> Rematches, ImmutableArray<VsSeekerActorBinding> ActorBindings, VsSeekerAcquisition Acquisition,
        ImmutableDictionary<int, int> RunningMovementByGraphics, int DefaultRunningMovement,
        ImmutableArray<VsSeekerRosterRequirement> RosterRequirements, ImmutableArray<string> ProgressFlags, ImmutableArray<string> ExcludedMaps);
}
