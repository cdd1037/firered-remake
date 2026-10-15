using System.Text.Json;

internal sealed record PokemonLeaguePresentationData(JsonElement Manifest,Dictionary<string,byte[]> Files);
internal static class PokemonLeaguePresentationAssets
{
    internal const string Base="content/generated/sprites/pokemon/pokemon-league/";
    internal const string SourceCommit="037335f4c725d7c9aecdac87066f2002b4bd7e14";

    internal static PokemonLeaguePresentationData Load(string root)
    {
        byte[] Read(string path)
        {
            if(Path.IsPathRooted(path)||path.Contains('\\')||path.Contains(':')||path.Split('/').Any(p=>p is "" or "." or ".."))
                throw new InvalidDataException("Unsafe Pokemon League asset.");
            var file=new FileInfo(Path.Combine(root,path));
            if(file.LinkTarget is not null)throw new InvalidDataException("Pokemon League asset size/symlink differs.");
            for(var dir=file.Directory;dir is not null;dir=dir.Parent)if(dir.LinkTarget is not null)throw new InvalidDataException("Pokemon League directory symlink rejected.");
            var bytes=File.ReadAllBytes(file.FullName);return bytes;
        }
        using var document=JsonDocument.Parse(Read(Base+"manifest.json"));var m=document.RootElement;
        if(m.GetProperty("schemaVersion").GetInt32()!=1)throw new InvalidDataException("Pokemon League presentation identity differs.");
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var r in m.GetProperty("outputs").EnumerateArray())files.Add(r.GetProperty("path").GetString()!,Read(Base+r.GetProperty("path").GetString()));
        return new(m.Clone(),files);
    }
}
