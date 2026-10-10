using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Game.Content;

internal sealed class PretCeladonApproachSource
{
    internal const string Commit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string Tree = "7515cd4067ea55f8cdbc73624478afd2736c58b9";
    internal ImmutableArray<PretWorldSourceFile> Files { get; }
    internal ImmutableArray<string> Hashes { get; }
    private PretCeladonApproachSource(ImmutableArray<PretWorldSourceFile> files, ImmutableArray<string> hashes)
    { Files=files; Hashes=hashes; }
    internal static PretCeladonApproachSource Load(string root, string stem, string profile, string lockHash, int count)
    {
        using var document=JsonDocument.Parse(Read(root,"scripts/"+stem+".lock.json",lockHash));
        var s=document.RootElement;
        Require(s.GetProperty("schemaVersion").GetInt32()==1 && s.GetProperty("repository").GetString()=="pret/pokefirered" &&
            s.GetProperty("commit").GetString()==Commit && s.GetProperty("tree").GetString()==Tree && s.GetProperty("profile").GetString()==profile &&
            s.GetProperty("gameEdition").GetString()=="FIRERED" && s.GetProperty("romRevision").GetInt32()==0 && s.GetProperty("files").GetArrayLength()==count,"Celadon approach source identity differs.");
        var files=ImmutableArray.CreateBuilder<PretWorldSourceFile>();var hashes=ImmutableArray.CreateBuilder<string>();
        hashes.Add(stem+"-lock:"+lockHash);var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var f in s.GetProperty("files").EnumerateArray())
        {
            var p=f.GetProperty("path").GetString()!;var h=f.GetProperty("sha256").GetString()!;
            int length=f.GetProperty("byteLength").GetInt32();var blob=f.GetProperty("gitBlobSha1").GetString()!;
            var b=Read(root,"content/raw/"+stem+"/"+p,h,length);
            Require(seen.Add(p) && Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {length}\0"),..b]))==blob,"Celadon approach source Git blob differs.");
            files.Add(new(p,blob,length,h,$"https://raw.githubusercontent.com/pret/pokefirered/{Commit}/{p}"));hashes.Add(stem+":"+p+":"+h);
        }
        return new(files.ToImmutable(),hashes.ToImmutable());
    }
    internal void Reference(string path,string hash,int start,int end,string symbol)
        => Require(Files.Any(f=>f.Path==path&&f.Sha256==hash)&&start>0&&end>=start&&!string.IsNullOrWhiteSpace(symbol),"Celadon approach source reference differs.");
    internal static ImmutableDictionary<string,byte[]> Outputs(string root,string stem,string profile,string manifestHash,string lockHash,int sourceCount)
    {
        using var manifest=JsonDocument.Parse(Read(root,"content/generated/"+stem+"/manifest.json",manifestHash));
        var m=manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32()==1 && m.GetProperty("profile").GetString()==profile && m.GetProperty("sourceCommit").GetString()==Commit &&
            m.GetProperty("tree").GetString()==Tree && m.GetProperty("lockSha256").GetString()==lockHash && m.GetProperty("inputCount").GetInt32()==sourceCount,"Celadon approach output manifest identity differs.");
        var result=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var o in m.GetProperty("outputs").EnumerateArray())
        {
            var p=o.GetProperty("path").GetString()!;
            Require(!p.EndsWith(".wav",StringComparison.OrdinalIgnoreCase)&&!p.EndsWith(".ogg",StringComparison.OrdinalIgnoreCase)&&!p.EndsWith(".qoa",StringComparison.OrdinalIgnoreCase),"Unexpected imported audio.");
            result.Add(p,Read(root,"content/generated/"+stem+"/"+p,o.GetProperty("sha256").GetString()!,o.GetProperty("byteLength").GetInt32()));
        }
        return result.ToImmutable();
    }
    internal static byte[] Read(string root,string relative,string hash,int? length=null)
    {
        Require(!string.IsNullOrWhiteSpace(relative)&&!Path.IsPathRooted(relative)&&!relative.Contains('\\')&&!relative.Split('/').Any(x=>x is "" or "." or ".."),"Unsafe Celadon approach path.");
        var full=Path.GetFullPath(Path.Combine(root,relative));var file=new FileInfo(full);
        Require(file.LinkTarget is null,"Celadon approach file symlink rejected.");
        for(var parent=file.Directory;parent is not null;parent=parent.Parent)Require(parent.LinkTarget is null,"Celadon approach parent symlink rejected.");
        var bytes=PretRuntimeMapValidation.ReadBounded(full,length??ContentJson.MaximumJsonBytes);
        Require((length is null||bytes.Length==length)&&Hash(bytes)==hash,"Celadon approach pinned bytes differ: "+relative);
        return bytes;
    }
    internal static string Hash(byte[] b)=>Convert.ToHexStringLower(SHA256.HashData(b));
    internal static void Require(bool condition,string error){if(!condition)throw new InvalidDataException(error);}
}
