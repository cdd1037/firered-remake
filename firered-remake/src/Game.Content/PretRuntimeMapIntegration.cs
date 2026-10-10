using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Connect the pinned compact derivative to the unchanged source/profile identities.</summary>
internal static class PretRuntimeMapIntegration
{
    internal static bool Replaces(PretRuntimeMapCatalog runtime, string path, JsonElement originalOutput)
    {
        var entry=runtime.Entries.SingleOrDefault(e=>e.SourcePath==path);
        if(entry is null)return false;
        Need(entry.SourceWorldSha256==originalOutput.GetProperty("sha256").GetString() &&
            entry.SourceByteLength==originalOutput.GetProperty("byteLength").GetInt32(),"Runtime derivative does not replace this authenticated world output: "+path);
        return true;
    }
    internal static void ValidateSource(PretRuntimeMapData compact,PretWorldMetadata metadata,Dictionary<string,byte[]> raw,JsonElement spec)
    {
        Need(compact.FullSourceHash==metadata.FullSourceHash && compact.Id.Value==spec.GetProperty("stableId").GetString(),"Runtime profile/map identity differs.");
        var reference=compact.SourceRef;
        Need(reference.Repository==metadata.Repository && reference.Commit==metadata.Commit &&
            reference.Path=="data/maps/"+spec.GetProperty("sourceName").GetString()+"/map.json" &&
            reference.Symbol==spec.GetProperty("sourceMapSymbol").GetString(),"Runtime source identity differs.");
        var source=metadata.Files.Single(f=>f.Path==reference.Path);
        Need(source.Sha256==reference.Sha256 && raw.TryGetValue(reference.Path,out _),"Runtime source digest differs.");
        using var events=JsonDocument.Parse(raw[reference.Path]);
        Need(JsonElement.DeepEquals(events.RootElement,compact.Events),"Runtime events differ from authenticated source.");
        foreach(var script in compact.Scripts)
            Need(raw.TryGetValue(script.Path,out var bytes) && Encoding.UTF8.GetBytes(script.Text).AsSpan().SequenceEqual(bytes) &&
                metadata.Files.Single(f=>f.Path==script.Path).Sha256==script.Sha256,"Runtime script differs from authenticated source: "+script.Path);
    }
    internal static PretWorldMap LoadBase(PretRuntimeMapCatalog runtime,PretWorldMapSpecification spec,string generated)
    {
        var compact=runtime.Maps[new MapId(spec.StableId)];
        Need(compact.FullSourceHash==PretWorldMap.FullSourceHash && compact.SourceRef.Symbol==spec.SourceMapSymbol &&
            runtime.Entries.Single(e=>e.StableId==spec.StableId).SourcePath==spec.Directory+"/world.json","Runtime base map differs.");
        string directory=Path.Combine(generated,spec.Directory);
        var expected=new HashSet<string>(["bottom.png","middle.png","top.png","map.png","player.png"],StringComparer.Ordinal);
        foreach(var a in compact.Artifacts)
        {
            Need(expected.Remove(a.Path),"Runtime base artifact inventory differs.");
            var bytes=PretRuntimeMapValidation.ReadBounded(Path.Combine(directory,a.Path),16*1024*1024);
            Need(Convert.ToHexStringLower(SHA256.HashData(bytes))==a.Sha256 && bytes.Length>24 &&
                bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}) &&
                BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4))==a.Width &&
                BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4))==a.Height,"Runtime base PNG identity differs.");
        }
        Need(expected.Count==0,"Runtime base artifact missing.");
        return PretWorldMap.FromRuntime(compact,runtime.BaseMetadata,directory);
    }
    private static void Need(bool value,string message){if(!value)throw new InvalidDataException(message);}
}
