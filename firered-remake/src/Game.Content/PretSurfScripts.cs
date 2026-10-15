using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Game.Content;

/// <summary>Exact source Surf Yes/No and fast-current scripts/text, using existing field opcodes.</summary>
public sealed class PretSurfScripts
{
    public PretSourceScriptFile Script { get; }
    public string Text { get; }
    public ImmutableDictionary<string,int> Constants { get; }
    private PretSurfScripts(PretSourceScriptFile script,string text)
    { Script=script;Text=text;Constants=ImmutableDictionary<string,int>.Empty.Add("FLDEFF_USE_SURF",9);}
    public static PretSurfScripts Load(string root)
    {
        using var sourceLock=JsonDocument.Parse(SafariSource.Read(root,"scripts/surf-scripts.lock.json"));
var texts=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var row in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            string path=row.GetProperty("path").GetString()!;
            var bytes=SafariSource.Read(root,PretSourcePaths.FireRedRawDirectory + "/"+path);
            texts.Add(path,new UTF8Encoding(false,true).GetString(bytes));        }
        SafariSource.Require(Regex.IsMatch(texts["include/constants/field_effects.h"],@"^#define FLDEFF_USE_SURF\s+9\s*$",RegexOptions.Multiline),"Surf source field effect differs.");
        return new(new("data/scripts/surf.inc",texts["data/scripts/surf.inc"],new(SourceKind.Pret,"data/scripts/surf.inc","EventScript_UseSurf","pret/pokefirered",PretWorldMap.SourceCommit)),texts["data/text/surf.inc"]);
    }
}
