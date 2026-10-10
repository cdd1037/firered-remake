using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed class PretNameRaterContract
{
    public const string LockSha256="bba2d7d3d50d79593a60ab969478e5d9547450a340b01727d2b35af7bd8e369e";
    public NameRaterSourceContent Source { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
    private PretNameRaterContract(NameRaterSourceContent source,ImmutableArray<string> hashes)
    {Source=source;AdditionalSourceHashes=hashes;}
    public static PretNameRaterContract Load(string root)
    {
        byte[] Read(string path,int count,string hash)
        {
            if(count<0||count>1024*1024)throw new InvalidDataException("name_rater_source_limit");
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
            if(input.Length!=count)throw new InvalidDataException("name_rater_source_size");
            var bytes=new byte[count];input.ReadExactly(bytes);
            if(input.ReadByte()!=-1||input.Length!=count||Convert.ToHexStringLower(SHA256.HashData(bytes))!=hash)
                throw new InvalidDataException("name_rater_source_hash");
            return bytes;
        }
        using var document=JsonDocument.Parse(Read(Path.Combine(root,"scripts/name-rater.lock.json"),3963,LockSha256));
        var doc=document.RootElement;
        if(doc.GetProperty("commit").GetString()!=PretWorldMap.SourceCommit||doc.GetProperty("files").GetArrayLength()!=15)
            throw new InvalidDataException("name_rater_source_scope");
        var hashes=ImmutableArray.CreateBuilder<string>();hashes.Add("name-rater-lock:"+LockSha256);
        string? strings=null;
        var paths=new HashSet<string>(StringComparer.Ordinal);
        foreach(var row in doc.GetProperty("files").EnumerateArray())
        {
            string path=row.GetProperty("path").GetString()!;
            if(Path.IsPathRooted(path)||path.Split('/').Any(p=>p is "" or "." or "..")||!paths.Add(path))
                throw new InvalidDataException("name_rater_source_path");
            string hash=row.GetProperty("sha256").GetString()!;
            var bytes=Read(Path.Combine(root,"content/raw/name-rater",path),row.GetProperty("bytes").GetInt32(),hash);
            hashes.Add("name-rater-source:"+path+":"+hash);
            if(path=="src/strings.c")strings=Encoding.UTF8.GetString(bytes);
        }
        string Text(string symbol)
        {
            var match=Regex.Match(strings??"",@"const u8 "+Regex.Escape(symbol)+"\\[\\] = _\\(\"([^\"]*)\"\\);",RegexOptions.CultureInvariant);
            if(!match.Success||match.Groups[1].Value.Contains('\\'))throw new InvalidDataException("name_rater_prompt_source");
            return match.Groups[1].Value;
        }
        return new(new(Text("gText_ChoosePokemon"),Text("gText_ChoosePokemonCancel")),hashes.ToImmutable());
    }
}
