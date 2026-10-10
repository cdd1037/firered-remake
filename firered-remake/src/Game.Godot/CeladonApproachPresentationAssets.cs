using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Authenticated additional original pixels; no runtime decoder or upstream execution.</summary>
internal static class CeladonApproachPresentationAssets
{
    internal const string ManifestSha256="74642b08fad4f1d23b6d753df0b1dab9670600ecf564dbadd0e4a7c4cb41ac41";
    internal const string LockSha256="c77e3512b325d8126e34f2096dfb0a2ae56afff7c73f25f2edc599c1a4233e78";
    internal static Dictionary<string,byte[]> Load(string root)
    {
        byte[] Read(string relative,string hash,int? expected=null)
        {
            if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("Unsafe CeladonApproach art path.");
            var file=new FileInfo(Path.Combine(root,relative));
            if(file.LinkTarget is not null)throw new InvalidDataException("CeladonApproach art symlink rejected.");
            for(var parent=file.Directory;parent is not null;parent=parent.Parent)if(parent.LinkTarget is not null)throw new InvalidDataException("CeladonApproach art directory symlink rejected.");
            if(file.Length is <=0 or >2000000||expected is {} length&&file.Length!=length)throw new InvalidDataException("CeladonApproach art size differs.");
            byte[] data=File.ReadAllBytes(file.FullName);
            if(Convert.ToHexStringLower(SHA256.HashData(data))!=hash)throw new InvalidDataException("CeladonApproach art bytes differ: "+relative);
            return data;
        }
        using var locked=JsonDocument.Parse(Read("scripts/celadon-approach-art.lock.json",LockSha256));
        using var manifest=JsonDocument.Parse(Read("content/generated/celadon-approach-art/manifest.json",ManifestSha256));
        var m=manifest.RootElement;var l=locked.RootElement;
        if(m.GetProperty("schemaVersion").GetInt32()!=1||m.GetProperty("sourceCommit").GetString()!=CeladonApproachSynthManifestIdentity.SourceCommit||
            m.GetProperty("profile").GetString()!="firered-celadon-approach-art-v1"||m.GetProperty("lockSha256").GetString()!=LockSha256||
            !JsonElement.DeepEquals(m.GetProperty("inputs"),l.GetProperty("files")))throw new InvalidDataException("CeladonApproach art provenance differs.");
        foreach(var row in l.GetProperty("files").EnumerateArray())Read("content/raw/celadon-approach-art/"+row.GetProperty("path").GetString(),row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32());
        var result=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var row in m.GetProperty("outputs").EnumerateArray())
        {
            string name=row.GetProperty("path").GetString()!;
            result.Add(name,Read("content/generated/celadon-approach-art/"+name,row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32()));
        }
        if(result.Count!=2||result["biker-front.rgba"].Length!=64*64*4)throw new InvalidDataException("CeladonApproach art inventory differs.");
        return result;
    }
}
