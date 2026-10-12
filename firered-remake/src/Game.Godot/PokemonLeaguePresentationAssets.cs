using System.Security.Cryptography;
using System.Text.Json;

internal sealed record PokemonLeaguePresentationData(JsonElement Manifest,Dictionary<string,byte[]> Files);
internal static class PokemonLeaguePresentationAssets
{
    internal const string Base="content/generated/pokemon-league-presentation/";
    internal const string SourceCommit="037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal const string ManifestSha256="211d6c3c6377699241beb9ac00645c4181d0aab08e46b96f5f02070b5fb76b1c";
    internal const string LockSha256="ecdba9b64157d7bf91a51145d077639d4be4f49de539c8926010c2486ab4dd7a";
    internal const string SynthSha256="fc6b8faf25cd3ec914e0667d0d01739af2cdde949f7eec4dc63dcae655c017aa";
    internal const int SynthBytes=6631;
    internal static PokemonLeaguePresentationData Load(string root)
    {
        byte[] Read(string path,string hash,int size)
        {
            if(Path.IsPathRooted(path)||path.Contains('\\')||path.Contains(':')||path.Split('/').Any(p=>p is "" or "." or "..")||size is <=0 or >4_000_000)
                throw new InvalidDataException("Unsafe Pokemon League asset.");
            var file=new FileInfo(Path.Combine(root,path));
            if(file.LinkTarget is not null||file.Length!=size)throw new InvalidDataException("Pokemon League asset size/symlink differs.");
            for(var dir=file.Directory;dir is not null;dir=dir.Parent)if(dir.LinkTarget is not null)throw new InvalidDataException("Pokemon League directory symlink rejected.");
            var bytes=File.ReadAllBytes(file.FullName);if(Convert.ToHexStringLower(SHA256.HashData(bytes))!=hash)throw new InvalidDataException("Pokemon League asset hash differs: "+path);return bytes;
        }
        using var locked=JsonDocument.Parse(Read("scripts/pokemon-league-presentation.lock.json",LockSha256,32102));
        using var document=JsonDocument.Parse(Read(Base+"manifest.json",ManifestSha256,106948));var m=document.RootElement;
        if(m.GetProperty("schemaVersion").GetInt32()!=1||m.GetProperty("sourceCommit").GetString()!=SourceCommit||m.GetProperty("lockSha256").GetString()!=LockSha256||
            !JsonElement.DeepEquals(m.GetProperty("inputs"),locked.RootElement.GetProperty("files")))throw new InvalidDataException("Pokemon League presentation identity differs.");
        foreach(var r in m.GetProperty("inputs").EnumerateArray())Read("content/raw/pokemon-league-presentation/"+r.GetProperty("path").GetString(),r.GetProperty("sha256").GetString()!,r.GetProperty("byteLength").GetInt32());
        var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
        foreach(var r in m.GetProperty("outputs").EnumerateArray())files.Add(r.GetProperty("path").GetString()!,Read(Base+r.GetProperty("path").GetString(),r.GetProperty("sha256").GetString()!,r.GetProperty("byteLength").GetInt32()));
        if(m.GetProperty("trainers").GetArrayLength()!=7||m.GetProperty("portraits").GetArrayLength()!=7||m.GetProperty("sprites").GetArrayLength()!=36||m.GetProperty("pokemonIcons").GetArrayLength()!=9||m.GetProperty("credits").GetProperty("commands").GetArrayLength()!=66)
            throw new InvalidDataException("Pokemon League presentation inventory differs.");
        return new(m.Clone(),files);
    }
}
