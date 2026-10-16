using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Game.Domain.Events;
namespace Game.Content;

public sealed record PretSourceScriptFile(string Path,string Text,SourceRef SourceRef);
public sealed record PretMapScriptHeader(string EventKind,string Target);
public sealed record PretMapScriptCondition(string Variable,string Expected,string Target);
public sealed record PretStoryDataDeclaration(string Directive, ImmutableArray<string> Operands);
public sealed record PretMovementCommand(string Opcode,int SourceLine);
public sealed record PretStoryCompilation(
    ImmutableDictionary<string,StoryProgram> Programs,
    ImmutableDictionary<string,ImmutableArray<PretMapScriptHeader>> MapHeaders,
    ImmutableDictionary<string,ImmutableArray<PretMapScriptCondition>> MapConditions,
    ImmutableDictionary<string,ImmutableArray<PretMovementCommand>> Movements,
    ImmutableArray<ContentDiagnostic> UnsupportedInstructions, ImmutableDictionary<string,int> Constants) : IStoryCatalog
{
    public ImmutableDictionary<string,ImmutableArray<PretStoryDataDeclaration>> AssemblyData { get; init; }=ImmutableDictionary<string,ImmutableArray<PretStoryDataDeclaration>>.Empty;
    public ImmutableDictionary<string,StoryMenuDefinition> Menus { get; init; }=ImmutableDictionary<string,StoryMenuDefinition>.Empty;
    public StoryMenuDefinition? FindMenu(string id)=>Menus.GetValueOrDefault(id);
    public StoryMenuDefinition? FindListMenu(int sourceId)=>Menus.Values.SingleOrDefault(m=>m.ListLayout is not null && m.SourceId==sourceId);
    public StoryProgram? FindProgram(string id)=>Programs.GetValueOrDefault(id);
    public bool TryConstant(string symbol,out int value)
    {
        var constants=new Dictionary<string,int>(StringComparer.Ordinal){{"FALSE",0},{"TRUE",1},{"DIR_SOUTH",1},{"DIR_NORTH",2},{"DIR_WEST",3},{"DIR_EAST",4},{"FCPICKSTATE_COLORED",2},{"FCPICKSTATE_SILHOUETTE",1},{"FCPICKSTATE_NO_DRAW",0},{"CRY_MODE_NORMAL",0}};
        return Constants.TryGetValue(symbol,out value) || constants.TryGetValue(symbol,out value);
    }
}
