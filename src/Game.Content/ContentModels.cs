using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Game.Content;

// Schema 1 is a small engineering IR, not a representation of all FireRed mechanics.
public enum SourceKind { OriginalFixture, Pret }
public sealed record SourceRef(SourceKind Kind, string Path, string Symbol, string? Repository = null, string? Commit = null);
public interface IContentDefinition { string StableId { get; } SourceRef SourceRef { get; } }
public sealed record ContentBundle(int SchemaVersion, string StableId, SourceRef SourceRef,
    string RulesVersion, bool IsOriginalFixture,
    ImmutableArray<TileDefinition> Tiles, ImmutableArray<MapDefinition> Maps,
    ImmutableArray<ScriptDefinition> Scripts, ImmutableArray<TextDefinition> Texts,
    ImmutableArray<FlagDefinition> Flags, ImmutableArray<ItemDefinition> Items,
    ImmutableArray<SpeciesDefinition> Species, ImmutableArray<RewardDefinition> Rewards) : IContentDefinition;
public enum ContentTileKind { Floor, Wall, Grass, Door }
public sealed record TileDefinition(string StableId, SourceRef SourceRef, ContentTileKind Kind,
    bool Blocked, int Elevation) : IContentDefinition;
public sealed record MapDefinition(string StableId, SourceRef SourceRef, int Width, int Height,
    ImmutableArray<string> Tiles, ImmutableArray<WarpDefinition> Warps,
    ImmutableArray<NpcDefinition> Npcs) : IContentDefinition;
public sealed record WarpDefinition(string StableId, SourceRef SourceRef, int X, int Y,
    string DestinationMapId, int DestinationX, int DestinationY) : IContentDefinition;
public sealed record NpcDefinition(string StableId, SourceRef SourceRef, int X, int Y,
    string ScriptId) : IContentDefinition;
public sealed record TextDefinition(string StableId, SourceRef SourceRef, string Language, string Value) : IContentDefinition;
public sealed record FlagDefinition(string StableId, SourceRef SourceRef, bool InitialValue) : IContentDefinition;
public sealed record ItemDefinition(string StableId, SourceRef SourceRef, string NameTextId, int StackLimit) : IContentDefinition;
public sealed record SpeciesDefinition(string StableId, SourceRef SourceRef, string NameTextId,
    int BaseHp, int BaseAttack, int BaseDefense, int BaseSpeed, int BaseSpecialAttack,
    int BaseSpecialDefense) : IContentDefinition;
public sealed record ItemGrant(string ItemId, int Quantity);
public sealed record RewardDefinition(string StableId, SourceRef SourceRef, string ClaimFlagId,
    int Money, ImmutableArray<ItemGrant> Items) : IContentDefinition;
public sealed record ScriptDefinition(string StableId, SourceRef SourceRef,
    ImmutableArray<ScriptInstruction> Instructions) : IContentDefinition;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "op", IgnoreUnrecognizedTypeDiscriminators = false)]
[JsonDerivedType(typeof(SayInstruction), "say")]
[JsonDerivedType(typeof(GiveRewardInstruction), "giveReward")]
[JsonDerivedType(typeof(SetFlagInstruction), "setFlag")]
[JsonDerivedType(typeof(BranchFlagInstruction), "branchFlag")]
[JsonDerivedType(typeof(JumpInstruction), "jump")]
[JsonDerivedType(typeof(EndInstruction), "end")]
public abstract record ScriptInstruction(string Label);
public sealed record SayInstruction(string Label, string TextId) : ScriptInstruction(Label);
public sealed record GiveRewardInstruction(string Label, string RewardId) : ScriptInstruction(Label);
public sealed record SetFlagInstruction(string Label, string FlagId, bool Value) : ScriptInstruction(Label);
public sealed record BranchFlagInstruction(string Label, string FlagId, string IfTrue, string IfFalse) : ScriptInstruction(Label);
public sealed record JumpInstruction(string Label, string Target) : ScriptInstruction(Label);
public sealed record EndInstruction(string Label) : ScriptInstruction(Label);

public sealed record ContentDiagnostic(string Code, string Path, string Message);
public sealed class ContentValidationException : Exception
{
    public IReadOnlyList<ContentDiagnostic> Diagnostics { get; }
    public ContentValidationException(IEnumerable<ContentDiagnostic> diagnostics)
        : this(diagnostics.ToArray()) { }
    private ContentValidationException(ContentDiagnostic[] diagnostics)
        : base(string.Join(Environment.NewLine, diagnostics.Select(d => $"{d.Code} at {d.Path}: {d.Message}")))
        => Diagnostics = Array.AsReadOnly(diagnostics);
}
