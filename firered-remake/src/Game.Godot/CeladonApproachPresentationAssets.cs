using Game.Content;
using System.Text.Json;

/// <summary>Additional original pixels; no runtime decoder or upstream execution.</summary>
internal static class CeladonApproachPresentationAssets
{

    internal static Dictionary<string,byte[]> Load(string root)
    {
        byte[] Read(string relative)
        {
            if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("Unsafe CeladonApproach art path.");
            var file=new FileInfo(Path.Combine(root,relative));
            if(file.LinkTarget is not null)throw new InvalidDataException("CeladonApproach art symlink rejected.");
            for(var parent=file.Directory;parent is not null;parent=parent.Parent)if(parent.LinkTarget is not null)throw new InvalidDataException("CeladonApproach art directory symlink rejected.");
            byte[] data=File.ReadAllBytes(file.FullName);
            return data;
        }
        using var manifest=JsonDocument.Parse(Read("content/generated/celadon-approach-art/manifest.json"));
        var m=manifest.RootElement;        if(m.GetProperty("schemaVersion").GetInt32()!=1||
            m.GetProperty("profile").GetString()!="firered-celadon-approach-art-v1")throw new InvalidDataException("CeladonApproach art provenance differs.");
        var result=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var row in m.GetProperty("outputs").EnumerateArray())
        {
            string name=row.GetProperty("path").GetString()!;
            result.Add(name,Read("content/generated/celadon-approach-art/"+name));
        }
        if(PngRgba8.Decode(result["biker-front.png"]).Length!=64*64*4)throw new InvalidDataException("CeladonApproach art inventory differs.");
        return result;
    }
}
