using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Authenticated additional original pixels; no runtime decoder or upstream execution.</summary>
internal static class CeladonGymPresentationAssets
{
    internal const string ManifestSha256="e4b97820623cd2b32dab8fe42640f1e72bca74bf2b3765cb236907e5ffca37a3";
    internal const string LockSha256="cebc2c7d891ca0188731eb84beaddc7f01c6ebac6d3527be1ef19611c634c849";
    internal static Dictionary<string,byte[]> Load(string root)
    {
        byte[] Read(string relative,string hash,int? expected=null)
        {
            if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("Unsafe CeladonGym art path.");
            var file=new FileInfo(Path.Combine(root,relative));
            if(file.LinkTarget is not null)throw new InvalidDataException("CeladonGym art symlink rejected.");
            for(var parent=file.Directory;parent is not null;parent=parent.Parent)if(parent.LinkTarget is not null)throw new InvalidDataException("CeladonGym art directory symlink rejected.");
            if(file.Length is <=0 or >2000000||expected is {} length&&file.Length!=length)throw new InvalidDataException("CeladonGym art size differs.");
            byte[] data=File.ReadAllBytes(file.FullName);
            if(Convert.ToHexStringLower(SHA256.HashData(data))!=hash)throw new InvalidDataException("CeladonGym art bytes differ: "+relative);
            return data;
        }
        using var locked=JsonDocument.Parse(Read("scripts/celadon-gym-art.lock.json",LockSha256));
        using var manifest=JsonDocument.Parse(Read("content/generated/celadon-gym-art/manifest.json",ManifestSha256));
        var m=manifest.RootElement;var l=locked.RootElement;
        if(m.GetProperty("schemaVersion").GetInt32()!=1||m.GetProperty("sourceCommit").GetString()!=CeladonGymSynthManifestIdentity.SourceCommit||
            m.GetProperty("profile").GetString()!="firered-celadon-gym-art-v1"||m.GetProperty("lockSha256").GetString()!=LockSha256||
            !JsonElement.DeepEquals(m.GetProperty("inputs"),l.GetProperty("files")))throw new InvalidDataException("CeladonGym art provenance differs.");
        foreach(var row in l.GetProperty("files").EnumerateArray())Read("content/raw/celadon-gym-art/"+row.GetProperty("path").GetString(),row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32());
        var result=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var row in m.GetProperty("outputs").EnumerateArray())
        {
            string name=row.GetProperty("path").GetString()!;
            result.Add(name,Read("content/generated/celadon-gym-art/"+name,row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32()));
        }
        if(result.Count!=6||new[]{"beauty-front","cooltrainer-f-front","erika-front"}.Any(id=>result[id+".rgba"].Length!=64*64*4))throw new InvalidDataException("CeladonGym art inventory differs.");
        return result;
    }
}
