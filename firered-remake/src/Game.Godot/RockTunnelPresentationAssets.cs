using Game.Content;
using System.Text.Json;
internal static class RockTunnelPresentationAssets
{

    internal static Dictionary<string,byte[]> Load(string root)
    {
        byte[] Read(string relative)
        {
            if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("Unsafe RockTunnel art path.");
            var file=new FileInfo(Path.Combine(root,relative));
            if(file.LinkTarget is not null)throw new InvalidDataException("RockTunnel art symlink rejected.");
            for(var parent=file.Directory;parent is not null;parent=parent.Parent)if(parent.LinkTarget is not null)throw new InvalidDataException("RockTunnel art directory symlink rejected.");
            var data=File.ReadAllBytes(file.FullName);return data;
        }
        using var manifest=JsonDocument.Parse(Read("content/generated/rocktunnel-art/manifest.json"));
        var m=manifest.RootElement;        if(m.GetProperty("schemaVersion").GetInt32()!=1||m.GetProperty("profile").GetString()!="firered-rocktunnel-art-v1")throw new InvalidDataException("RockTunnel art provenance differs.");
        var result=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var row in m.GetProperty("outputs").EnumerateArray()){string p=row.GetProperty("path").GetString()!;result.Add(p,Read("content/generated/rocktunnel-art/"+p));}
        if(PngRgba8.Decode(result["pokemaniac-front.png"]).Length!=16384||PngRgba8.Decode(result["indoor-streaks.png"]).Length!=81920)throw new InvalidDataException("RockTunnel art inventory differs.");return result;
    }
}
