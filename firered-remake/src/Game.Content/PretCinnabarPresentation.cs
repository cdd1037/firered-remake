using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;
public sealed record CinnabarFieldTile(string Family,string Symbol,int MetatileId,uint RawAttributes,int X,int Y,int Width,int Height);
public sealed record CinnabarFieldLayer(string Family,string Layer,string TexturePath,string RgbaPath,int Width,int Height,string Sha256,string RgbaSha256);
public sealed record CinnabarFieldArtDocument(int SchemaVersion,string Profile,string SourceCommit,string LockSha256,
    ImmutableArray<CinnabarFieldTile> Tiles,ImmutableArray<CinnabarFieldLayer> Layers,ImmutableArray<string> Limits,ImmutableArray<SnorlaxArtOutput> Outputs);

/// <summary>Authenticated trainer portraits/classes and family-specific statue/barrier/quiz door pixels.</summary>
public sealed class PretCinnabarPresentation
{
    public const string TrainerManifestSha256="df6d1970c156851879116b6cd942432620c010e68256f9e2cb7274d129496dc2";
    public const string FieldManifestSha256="6198df81066b133d774c1ac3234f2cad4415a9a9cb5fb0c4c18770c1e68577fd";
    public JsonElement TrainerManifest {get;}
    public ImmutableDictionary<string,byte[]> TrainerFiles {get;}
    public ImmutableDictionary<string,string> TrainerClasses {get;}
    public CinnabarFieldArtDocument Field {get;}
    public ImmutableDictionary<string,byte[]> FieldFiles {get;}
    public ImmutableArray<string> AdditionalSourceHashes {get;}
    private PretCinnabarPresentation(JsonElement trainers,ImmutableDictionary<string,byte[]> trainerFiles,CinnabarFieldArtDocument field,ImmutableDictionary<string,byte[]> fieldFiles)
    {
        TrainerManifest=trainers;TrainerFiles=trainerFiles;Field=field;FieldFiles=fieldFiles;
        TrainerClasses=trainers.GetProperty("trainerClasses").EnumerateObject().ToImmutableDictionary(p=>p.Name,p=>p.Value.GetString()!,StringComparer.Ordinal);
        AdditionalSourceHashes=["cinnabar-trainer-art:"+TrainerManifestSha256,"cinnabar-field-art:"+FieldManifestSha256];
    }
    public static PretCinnabarPresentation Load(string root)
    {
        using var locked=JsonDocument.Parse(SafariSource.Read(root,"scripts/cinnabar-presentation.lock.json",PretCinnabarArt.SourceLockSha256,24855));
        var entries=locked.RootElement.GetProperty("files");
        Need(locked.RootElement.GetProperty("commit").GetString()==PretWorldMap.SourceCommit&&entries.GetArrayLength()==122,"Cinnabar presentation source scope differs.");
        foreach(var row in entries.EnumerateArray())
        {
            var b=SafariSource.Read(root,"content/raw/cinnabar-presentation/"+row.GetProperty("path").GetString(),row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32());
            Need(Convert.ToHexStringLower(SHA1.HashData([..Encoding.ASCII.GetBytes($"blob {b.Length}\0"),..b]))==row.GetProperty("gitBlobSha1").GetString(),"Cinnabar source Git blob differs.");
        }
        using var trainers=JsonDocument.Parse(SafariSource.Read(root,"content/generated/cinnabar-trainer-art/manifest.json",TrainerManifestSha256,31876));
        var tr=trainers.RootElement;
        Need(tr.GetProperty("schemaVersion").GetInt32()==1&&tr.GetProperty("profile").GetString()=="firered-cinnabar-trainer-art-v1"&&tr.GetProperty("sourceCommit").GetString()==PretWorldMap.SourceCommit&&tr.GetProperty("lockSha256").GetString()==PretCinnabarArt.SourceLockSha256&&JsonElement.DeepEquals(tr.GetProperty("inputs"),entries)&&tr.GetProperty("portraits").GetArrayLength()==8&&tr.GetProperty("trainerClasses").EnumerateObject().Count()==8,"Cinnabar trainer presentation differs.");
        var trainerFiles=ReadOutputs(root,"content/generated/cinnabar-trainer-art/",SafariSource.Parse<ImmutableArray<SnorlaxArtOutput>>(Encoding.UTF8.GetBytes(tr.GetProperty("outputs").GetRawText())));
        var pics=new HashSet<string>(StringComparer.Ordinal);
        foreach(var p in tr.GetProperty("portraits").EnumerateArray())
        {
            Need(pics.Add(p.GetProperty("trainerPic").GetString()!)&&p.GetProperty("width").GetInt32()==64&&p.GetProperty("height").GetInt32()==64&&p.GetProperty("sourceFrame").GetInt32()==0&&p.GetProperty("x").GetInt32()==144&&p.GetProperty("y").GetInt32()==8,"Cinnabar trainer portrait shape differs.");
            var rgba=trainerFiles[p.GetProperty("rgbaPath").GetString()!];Need(rgba.Length==64*64*4&&SafariSource.Hash(rgba)==p.GetProperty("rgbaSha256").GetString(),"Cinnabar trainer pixels differ.");
        }
        Need(pics.SetEquals(new[]{"TRAINER_PIC_BURGLAR","TRAINER_PIC_FISHERMAN","TRAINER_PIC_LEADER_BLAINE","TRAINER_PIC_SCIENTIST","TRAINER_PIC_SIS_AND_BRO","TRAINER_PIC_SUPER_NERD","TRAINER_PIC_SWIMMER_M","TRAINER_PIC_YOUNGSTER"}),"Cinnabar trainer inventory differs.");
        var field=SafariSource.Parse<CinnabarFieldArtDocument>(SafariSource.Read(root,"content/generated/cinnabar-field-art/manifest.json",FieldManifestSha256,9349));
        Need(field.SchemaVersion==1&&field.Profile=="firered-cinnabar-field-art-v1"&&field.SourceCommit==PretWorldMap.SourceCommit&&field.LockSha256==PretCinnabarArt.SourceLockSha256&&field.Tiles.Length==37&&field.Layers.Length==6&&field.Outputs.Length==12&&!field.Limits.IsDefaultOrEmpty,"Cinnabar field presentation differs.");
        var fieldFiles=ReadOutputs(root,"content/generated/cinnabar-field-art/",field.Outputs);
        foreach(var family in new[]{"mansion","gym"})
        {
            int count=family=="mansion"?29:8;var tiles=field.Tiles.Where(t=>t.Family==family).ToArray();var layers=field.Layers.Where(l=>l.Family==family).ToArray();
            Need(tiles.Length==count&&tiles.Select(t=>t.MetatileId).Distinct().Count()==count&&tiles.Select(t=>t.X).SequenceEqual(Enumerable.Range(0,count).Select(i=>i*16))&&tiles.All(t=>t.Y==0&&t.Width==16&&t.Height==16)&&layers.Select(l=>l.Layer).SequenceEqual(new[]{"bottom","middle","top"}),"Cinnabar map-family atlas differs.");
            foreach(var layer in layers)Need(layer.Width==count*16&&layer.Height==16&&fieldFiles[layer.RgbaPath].Length==count*16*16*4&&SafariSource.Hash(fieldFiles[layer.RgbaPath])==layer.RgbaSha256&&SafariSource.Hash(fieldFiles[layer.TexturePath])==layer.Sha256,"Cinnabar map-family pixels differ.");
        }
        return new(tr.Clone(),trainerFiles,field,fieldFiles);
    }
    private static ImmutableDictionary<string,byte[]> ReadOutputs(string root,string prefix,ImmutableArray<SnorlaxArtOutput> outputs)=>outputs.ToImmutableDictionary(o=>o.Path,o=>SafariSource.Read(root,prefix+o.Path,o.Sha256,o.ByteLength),StringComparer.Ordinal);
    private static void Need(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
}
