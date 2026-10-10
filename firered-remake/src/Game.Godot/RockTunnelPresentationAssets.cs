using System.Security.Cryptography;
using System.Text.Json;
internal static class RockTunnelPresentationAssets
{
    internal const string ManifestSha256="0ccb57312402bfbd2236f253dbce400dc129c16b7152357f620ffbd4f0fead06";
    internal const string LockSha256="f3d680414ab57473344167d8ac168b9ec7a22af5b6d991a8a42df86027ec8c9a";
    internal static Dictionary<string,byte[]> Load(string root)
    {
        byte[] Read(string relative,string hash,int? expected=null)
        {
            if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("Unsafe RockTunnel art path.");
            var file=new FileInfo(Path.Combine(root,relative));
            if(file.LinkTarget is not null)throw new InvalidDataException("RockTunnel art symlink rejected.");
            for(var parent=file.Directory;parent is not null;parent=parent.Parent)if(parent.LinkTarget is not null)throw new InvalidDataException("RockTunnel art directory symlink rejected.");
            if(file.Length is <=0 or >2000000||expected is {} length&&file.Length!=length)throw new InvalidDataException("RockTunnel art size differs.");
            var data=File.ReadAllBytes(file.FullName);if(Convert.ToHexStringLower(SHA256.HashData(data))!=hash)throw new InvalidDataException("RockTunnel art hash differs: "+relative);return data;
        }
        using var locked=JsonDocument.Parse(Read("scripts/rocktunnel-art.lock.json",LockSha256));
        using var manifest=JsonDocument.Parse(Read("content/generated/rocktunnel-art/manifest.json",ManifestSha256));
        var m=manifest.RootElement;var l=locked.RootElement;
        if(m.GetProperty("schemaVersion").GetInt32()!=1||m.GetProperty("sourceCommit").GetString()!=RockTunnelSynthManifestIdentity.SourceCommit||m.GetProperty("profile").GetString()!="firered-rocktunnel-art-v1"||m.GetProperty("lockSha256").GetString()!=LockSha256||!JsonElement.DeepEquals(m.GetProperty("inputs"),l.GetProperty("files")))throw new InvalidDataException("RockTunnel art provenance differs.");
        foreach(var row in l.GetProperty("files").EnumerateArray())Read("content/raw/rocktunnel-art/"+row.GetProperty("path").GetString(),row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32());
        var result=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var row in m.GetProperty("outputs").EnumerateArray()){string p=row.GetProperty("path").GetString()!;result.Add(p,Read("content/generated/rocktunnel-art/"+p,row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32()));}
        if(result.Count!=4||result["pokemaniac-front.rgba"].Length!=16384||result["indoor-streaks.rgba"].Length!=81920)throw new InvalidDataException("RockTunnel art inventory differs.");return result;
    }
}
