using System.Collections.Immutable;
using Game.Domain.Ids;
namespace Game.Domain.Events;
/// <summary>Supported read-only script contract. No flags, items, movement or hidden side effects.</summary>
public sealed record ReadOnlyDialogue(ScriptId ScriptId, TextId TextId, ImmutableArray<string> Pages);
public sealed record DialogueCursor(ScriptId ScriptId, int PageIndex);
