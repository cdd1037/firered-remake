using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed class PretNameRaterContract
{
    public NameRaterSourceContent Source { get; }
    private PretNameRaterContract(NameRaterSourceContent source)
    {Source=source;}
    public static PretNameRaterContract Load(string root)
    {
        byte[] Read(string path)
        {
var bytes = File.ReadAllBytes(path);
return bytes;
        }
        using var document=JsonDocument.Parse(Read(Path.Combine(root,"scripts/name-rater.lock.json")));
        var doc=document.RootElement;
string? strings=null;
        var paths=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in doc.GetProperty("files").EnumerateArray())
        {
            string path=row.GetProperty("path").GetString()!;
            if(Path.IsPathRooted(path)||path.Split('/').Any(p=>p is "" or "." or "..")||!paths.Add(path))
                throw new InvalidDataException("name_rater_source_path");
            var bytes=Read(Path.Combine(root,PretSourcePaths.FireRedRawDirectory,path));
            if(path=="src/strings.c")strings=Encoding.UTF8.GetString(bytes);
        }
        string Text(string symbol)
        {
            var match=Regex.Match(strings??"",@"const u8 "+Regex.Escape(symbol)+"\\[\\] = _\\(\"([^\"]*)\"\\);",RegexOptions.CultureInvariant);
            if(!match.Success||match.Groups[1].Value.Contains('\\'))throw new InvalidDataException("name_rater_prompt_source");
            return match.Groups[1].Value;
        }
        return new(new(Text("gText_ChoosePokemon"),Text("gText_ChoosePokemonCancel")));
    }
}
