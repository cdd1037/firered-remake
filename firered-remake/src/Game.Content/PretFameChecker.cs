using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Events;

namespace Game.Content;

public sealed record FameCheckerCollectionCoverage(FameCheckerCollectionEvent Source, bool InstructionImported,
    bool AnySourceMapMounted, bool StaticallyReferencedFromMountedMap, ImmutableArray<string> UnsupportedOpcodesInBlock);

/// <summary>Loads Fame Checker facts and their source metadata.</summary>
public sealed class PretFameChecker
{
    public FameCheckerCatalog Catalog { get; }
    private PretFameChecker(FameCheckerCatalog catalog) { Catalog = catalog; }

    public static PretFameChecker Load(string projectRoot) => LoadPackage(
        Path.Combine(projectRoot, "content/generated/data/services/fame-checker.json"));

    public static PretFameChecker LoadPackage(string catalogPath) =>
        new(FameCheckerCatalog.Authenticate(File.ReadAllBytes(catalogPath)));

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
