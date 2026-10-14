using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Domain.Events;

namespace Game.Content;

public sealed record FameCheckerCollectionCoverage(FameCheckerCollectionEvent Source, bool InstructionImported,
    bool AnySourceMapMounted, bool StaticallyReferencedFromMountedMap, ImmutableArray<string> UnsupportedOpcodesInBlock);

/// <summary>Loads the compact, source-authenticated data package. This does not mount maps or admit conversations.</summary>
public sealed class PretFameChecker
{
    public FameCheckerCatalog Catalog { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretFameChecker(FameCheckerCatalog catalog, ImmutableArray<PretWorldSourceFile> files)
    {
        Catalog = catalog; SourceFiles = files;
        AdditionalSourceHashes = ["fame-checker-lock:" + FameCheckerCatalog.SourceLockSha256,
            "fame-checker-catalog:" + FameCheckerCatalog.CatalogSha256,
            .. files.Select(f => "fame-checker:" + f.Path + ":" + f.Sha256)];
    }

    public static PretFameChecker Load(string projectRoot) => LoadPackage(
        Path.Combine(projectRoot, "scripts/fame-checker.lock.json"),
        Path.Combine(projectRoot, "content/generated/fame-checker/catalog.json"));

    /// <summary>The importer authenticates Git blobs against the pinned tree. Runtime authenticates its
    /// exact reviewed output and immutable source lock; it does not require duplicated raw source files.</summary>
    public static PretFameChecker LoadPackage(string lockPath, string catalogPath)
    {
        static byte[] Read(string path, int maximum, string hash)
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length <= 0 || stream.Length > maximum) throw new InvalidDataException("fame_checker_file_size");
            var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes);
            if (stream.ReadByte() != -1 || Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
                throw new InvalidDataException("fame_checker_file_hash");
            return bytes;
        }
        using var sourceLock = JsonDocument.Parse(Read(lockPath, 65536, FameCheckerCatalog.SourceLockSha256));
        var root = sourceLock.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1 || root.GetProperty("profile").GetString() != FameCheckerCatalog.Profile ||
            root.GetProperty("repository").GetString() != "pret/pokefirered" || root.GetProperty("commit").GetString() != FameCheckerCatalog.SourceCommit ||
            root.GetProperty("tree").GetString() != FameCheckerCatalog.SourceTree || root.GetProperty("gameEdition").GetString() != "FIRERED" ||
            root.GetProperty("romRevision").GetInt32() != 0)
            throw new InvalidDataException("fame_checker_source_identity");
        var files = root.GetProperty("files").EnumerateArray().Select(r => new PretWorldSourceFile(
            r.GetProperty("path").GetString()!, r.GetProperty("gitBlobSha1").GetString()!, r.GetProperty("byteLength").GetInt32(),
            r.GetProperty("sha256").GetString()!, "https://raw.githubusercontent.com/pret/pokefirered/" + FameCheckerCatalog.SourceCommit + "/" + r.GetProperty("path").GetString())).ToImmutableArray();
        if (files.Length != 113 || files.Select(f => f.Path).Distinct(StringComparer.Ordinal).Count() != files.Length)
            throw new InvalidDataException("fame_checker_source_inventory");
        var catalog = FameCheckerCatalog.Authenticate(Read(catalogPath, FameCheckerCatalog.CatalogByteLength, FameCheckerCatalog.CatalogSha256));
        return new(catalog, files);
    }

    /// <summary>Separates imported opcodes, mounted map context and a static reference path from map entrypoints.
    /// None is proof that script conditions are satisfiable, its dialogue executes, or the player has collected a fact.</summary>
    public ImmutableArray<FameCheckerCollectionCoverage> AuditCollectionEvents(PretCampaignCatalog campaign)
    {
        var scripts = campaign.ScriptCompilation;
        var mounted = campaign.SourceMaps.Values.Select(m => m.SourceMapSymbol).ToHashSet(StringComparer.Ordinal);
        var pending = new Queue<string>();
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        void Add(string? label) { if (!string.IsNullOrEmpty(label) && referenced.Add(label)) pending.Enqueue(label); }
        foreach (var id in campaign.Maps.Keys)
        {
            var map = campaign.MapDefinition(id);
            foreach (var actor in map.Actors) Add(actor.ScriptId);
            foreach (var trigger in map.CoordTriggers) Add(trigger.ScriptId);
            foreach (var trigger in map.BackgroundTriggers) Add(trigger.ScriptId);
            Add(map.Programs.OnTransition); Add(map.Programs.OnWarpTable); Add(map.Programs.OnFrameTable);
            Add(map.Programs.OnLoad); Add(map.Programs.OnResume); Add(map.Programs.OnReturnToField);
        }
        while (pending.TryDequeue(out var label))
        {
            if (scripts.MapHeaders.TryGetValue(label, out var headers)) foreach (var header in headers) Add(header.Target);
            if (scripts.MapConditions.TryGetValue(label, out var conditions)) foreach (var condition in conditions) Add(condition.Target);
            if (scripts.FindProgram(label) is { } program)
                foreach (string operand in program.Instructions.SelectMany(i => i.Args))
                    if (scripts.Programs.ContainsKey(operand) || scripts.MapHeaders.ContainsKey(operand) || scripts.MapConditions.ContainsKey(operand)) Add(operand);
        }
        return Catalog.CollectionEvents.Select(e =>
        {
            var program = scripts.FindProgram(e.ScriptLabel);
            bool imported = program?.Instructions.Any(i => i.Op == StoryOp.FameChecker && i.Source.Path == e.SourcePath &&
                i.Source.Line == e.SourceLine && i.Args.SequenceEqual(e.Arguments)) == true;
            return new FameCheckerCollectionCoverage(e, imported, e.SourceMaps.Any(mounted.Contains), referenced.Contains(e.ScriptLabel),
                program?.Instructions.Where(i => i.Op == StoryOp.Unsupported).Select(i => i.Opcode).Distinct(StringComparer.Ordinal).ToImmutableArray() ?? []);
        }).ToImmutableArray();
    }
}
