using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Game.Content;

/// <summary>Exact source Surf Yes/No and fast-current scripts/text, using existing field opcodes.</summary>
public sealed class PretSurfScripts
{
    public const string LockSha256="ed42e3c477cd43a2ef3b5ba6bb23378624d33785f9ad9a3a690889fbadceefb2";
    public const int LockByteLength=767;
    public PretSourceScriptFile Script { get; }
    public string Text { get; }
    public ImmutableDictionary<string,int> Constants { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretSurfScripts(PretSourceScriptFile script,string text,ImmutableArray<string> hashes)
    { Script=script;Text=text;Constants=ImmutableDictionary<string,int>.Empty.Add("FLDEFF_USE_SURF",9);AdditionalSourceHashes=hashes; }
    public static PretSurfScripts Load(string root)
    {
        using var sourceLock=JsonDocument.Parse(SafariSource.Read(root,"scripts/surf-scripts.lock.json",LockSha256,LockByteLength));
        var hashes=ImmutableArray.CreateBuilder<string>();hashes.Add("surf-scripts-lock:"+LockSha256);var texts=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var row in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            string path=row.GetProperty("path").GetString()!,hash=row.GetProperty("sha256").GetString()!;
            var bytes=SafariSource.Read(root,"content/raw/surf-scripts/"+path,hash,row.GetProperty("byteLength").GetInt32());
            SafariSource.Require(SafariSource.GitBlob(bytes)==row.GetProperty("gitBlobSha1").GetString(),"Surf scripts Git blob differs.");
            texts.Add(path,new UTF8Encoding(false,true).GetString(bytes));hashes.Add("surf-script-source:"+path+":"+hash);
        }
        SafariSource.Require(Regex.IsMatch(texts["include/constants/field_effects.h"],@"^#define FLDEFF_USE_SURF\s+9\s*$",RegexOptions.Multiline),"Surf source field effect differs.");
        return new(new("data/scripts/surf.inc",texts["data/scripts/surf.inc"],new(SourceKind.Pret,"data/scripts/surf.inc","EventScript_UseSurf","pret/pokefirered",PretWorldMap.SourceCommit,"bf9b395e88ddf6dff7149d27f789ee34176372d59eadd6df4788466b4f44aea8")),texts["data/text/surf.inc"],hashes.ToImmutable());
    }
}
