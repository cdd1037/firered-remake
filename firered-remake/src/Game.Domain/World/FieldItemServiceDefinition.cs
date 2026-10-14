using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.World;

/// <summary>Source-imported entry and call graph for an ordinary inventory service.</summary>
public sealed record FieldItemServiceDefinition(string Id, MapId Map, string ScriptId,
    int? SourceLocalId, int? X, int? Y, ImmutableArray<string> ProgramIds,
    ImmutableArray<string> ItemSymbols);
