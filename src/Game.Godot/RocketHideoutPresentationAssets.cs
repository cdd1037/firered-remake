using System.Text.Json;

/// <summary>Bounded source pixels and presentation metadata. No upstream code or recorded audio.</summary>
internal static class RocketHideoutPresentationAssets
{

    internal sealed record Package(JsonElement Manifest,Dictionary<string,byte[]> Files);
    private static byte[] Read(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains('\\')||relative.Split('/').Any(p=>p is "" or "." or ".."))throw new InvalidDataException("Unsafe Rocket presentation path.");
        var file=new FileInfo(Path.Combine(root,relative));
        if(file.LinkTarget is not null)throw new InvalidDataException("Rocket presentation symlink rejected.");
        for(var parent=file.Directory;parent is not null;parent=parent.Parent)if(parent.LinkTarget is not null)throw new InvalidDataException("Rocket presentation directory symlink rejected.");
        byte[] data=File.ReadAllBytes(file.FullName);
        return data;
    }
    internal static Package Load(string root,bool portraits=false)
    {
        string stem=portraits?"rocket-hideout-art":"rocket-hideout-presentation";
        using var manifest=JsonDocument.Parse(Read(root,"content/generated/"+(portraits?"sprites/trainers/rocket-hideout":"maps/objects/rocket-hideout-presentation")+"/manifest.json"));
        var m=manifest.RootElement;        if(m.GetProperty("schemaVersion").GetInt32()!=1)throw new InvalidDataException("Rocket presentation provenance differs.");
        var result=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var row in m.GetProperty("outputs").EnumerateArray())
        {
            string name=row.GetProperty("path").GetString()!;
            result.Add(name,Read(root,"content/generated/"+(portraits?"sprites/trainers/rocket-hideout":"maps/objects/rocket-hideout-presentation")+"/"+name));
        }
        return new(m.Clone(),result);
    }
    internal static byte[] LoadTerrain(string root)
    {
        using var manifest=JsonDocument.Parse(Read(root,"content/generated/battle/terrain/rocket-hideout/manifest.json"));
        var m=manifest.RootElement;        if(m.GetProperty("terrain").GetString()!="BATTLE_TERRAIN_INDOOR_2")throw new InvalidDataException("Rocket source terrain provenance differs.");
        return Read(root,"content/generated/battle/terrain/rocket-hideout/indoor2-scene.png");
    }
}
