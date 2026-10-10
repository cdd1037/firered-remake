using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Bounded source pixels and presentation metadata. No upstream code or recorded audio.</summary>
internal static class RocketHideoutPresentationAssets
{
    internal const string ArtManifestSha256="70816b98ea3965fc1f7c2c10b2d2571243ca45c93f1627e464cfaf1eb6166b27";
    internal const string ArtLockSha256="d2600202a1282d17f48d7e50e7c321c044a6caced765b2700ff6b7a46a84fe0b";
    internal const string ManifestSha256="42850f9c78d63fcdc3868d5550d2c80fceb976538b6d400dcfd3136ab355b66c";
    internal const string LockSha256="0800621564eec021214ffd5e088e22bb3272e8b781836f06d765cf6ed51435bd";
    internal const string TerrainManifestSha256="88bb8890282965fb1f9560f93db7863c26ce52dead768044df4b103b215e77af";
    internal const string TerrainLockSha256="033fb31fc6d0a3f6d9bb8dc82d261687e929c0e4017d882a78d36ad1dc5d72ec";
    internal const string TerrainPngSha256="ca49d22cfae2d035ad6cfeb18626cfe30fa8e696abe380a5830a7cfee82f5e3e";
    internal sealed record Package(JsonElement Manifest,Dictionary<string,byte[]> Files);
    private static byte[] Read(string root,string relative,string hash,int? expected=null)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("Unsafe Rocket presentation path.");
        var file=new FileInfo(Path.Combine(root,relative));
        if(file.LinkTarget is not null)throw new InvalidDataException("Rocket presentation symlink rejected.");
        for(var parent=file.Directory;parent is not null;parent=parent.Parent)if(parent.LinkTarget is not null)throw new InvalidDataException("Rocket presentation directory symlink rejected.");
        if(file.Length is <=0 or >3000000||expected is {} length&&file.Length!=length)throw new InvalidDataException("Rocket presentation size differs.");
        byte[] data=File.ReadAllBytes(file.FullName);
        if(Convert.ToHexStringLower(SHA256.HashData(data))!=hash)throw new InvalidDataException("Rocket presentation bytes differ: "+relative);
        return data;
    }
    internal static Package Load(string root,bool portraits=false)
    {
        string stem=portraits?"rocket-hideout-art":"rocket-hideout-presentation",lockHash=portraits?ArtLockSha256:LockSha256;
        using var locked=JsonDocument.Parse(Read(root,"scripts/"+stem+".lock.json",lockHash));
        using var manifest=JsonDocument.Parse(Read(root,"content/generated/"+stem+"/manifest.json",portraits?ArtManifestSha256:ManifestSha256));
        var m=manifest.RootElement;var l=locked.RootElement;
        if(m.GetProperty("schemaVersion").GetInt32()!=1||m.GetProperty("sourceCommit").GetString()!=RocketHideoutSynthManifestIdentity.SourceCommit||
            m.GetProperty("profile").GetString()!="firered-"+stem+"-v1"||m.GetProperty("lockSha256").GetString()!=lockHash||
            l.GetProperty("commit").GetString()!=RocketHideoutSynthManifestIdentity.SourceCommit)throw new InvalidDataException("Rocket presentation provenance differs.");
        foreach(var row in l.GetProperty("files").EnumerateArray())Read(root,"content/raw/"+stem+"/"+row.GetProperty("path").GetString(),row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32());
        foreach(var row in l.GetProperty("inputs").EnumerateArray())Read(root,row.GetProperty("path").GetString()!,row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32());
        var result=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var row in m.GetProperty("outputs").EnumerateArray())
        {
            string name=row.GetProperty("path").GetString()!;
            result.Add(name,Read(root,"content/generated/"+stem+"/"+name,row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32()));
        }
        if(result.Count!=(portraits?4:6)||!portraits&&m.GetProperty("tiles").GetArrayLength()!=22)throw new InvalidDataException("Rocket presentation scope differs.");
        return new(m.Clone(),result);
    }
    internal static byte[] LoadTerrain(string root)
    {
        const string stem="rocket-hideout-terrain";
        using var locked=JsonDocument.Parse(Read(root,"scripts/"+stem+".lock.json",TerrainLockSha256));
        using var manifest=JsonDocument.Parse(Read(root,"content/generated/"+stem+"/manifest.json",TerrainManifestSha256));
        var m=manifest.RootElement;var l=locked.RootElement;
        if(m.GetProperty("profile").GetString()!="firered-rocket-hideout-terrain-v1"||m.GetProperty("terrain").GetString()!="BATTLE_TERRAIN_INDOOR_2"||
            m.GetProperty("sourceCommit").GetString()!=RocketHideoutSynthManifestIdentity.SourceCommit||m.GetProperty("lockSha256").GetString()!=TerrainLockSha256||
            l.GetProperty("files").GetArrayLength()!=12||m.GetProperty("outputs").GetArrayLength()!=1)throw new InvalidDataException("Rocket source terrain provenance differs.");
        foreach(var row in l.GetProperty("files").EnumerateArray())Read(root,"content/raw/"+stem+"/"+row.GetProperty("path").GetString(),row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32());
        foreach(var row in l.GetProperty("inputs").EnumerateArray())Read(root,row.GetProperty("path").GetString()!,row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32());
        return Read(root,"content/generated/"+stem+"/indoor2-scene.png",TerrainPngSha256,107705);
    }
}
