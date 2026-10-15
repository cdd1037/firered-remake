using Game.Content;
using System.Text.Json;

/// <summary>Additional original pixels; no runtime decoder or upstream execution.</summary>
internal static class CeladonGymPresentationAssets
{

    internal static Dictionary<string,byte[]> Load(string root)
    {
        byte[] Read(string relative)
        {
            if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("Unsafe CeladonGym art path.");
            var file=new FileInfo(Path.Combine(root,relative));
            if(file.LinkTarget is not null)throw new InvalidDataException("CeladonGym art symlink rejected.");
            for(var parent=file.Directory;parent is not null;parent=parent.Parent)if(parent.LinkTarget is not null)throw new InvalidDataException("CeladonGym art directory symlink rejected.");
            byte[] data=File.ReadAllBytes(file.FullName);
            return data;
        }
        using var manifest=JsonDocument.Parse(Read("content/generated/celadon-gym-art/manifest.json"));
        var m=manifest.RootElement;        if(m.GetProperty("schemaVersion").GetInt32()!=1||
            m.GetProperty("profile").GetString()!="firered-celadon-gym-art-v1")throw new InvalidDataException("CeladonGym art provenance differs.");
        var result=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var row in m.GetProperty("outputs").EnumerateArray())
        {
            string name=row.GetProperty("path").GetString()!;
            result.Add(name,Read("content/generated/celadon-gym-art/"+name));
        }
        if(new[]{"beauty-front","cooltrainer-f-front","erika-front"}.Any(id=>!NativeImages.HasSize(result[id+".png"], 64, 64)))throw new InvalidDataException("CeladonGym art inventory differs.");
        return result;
    }
}
