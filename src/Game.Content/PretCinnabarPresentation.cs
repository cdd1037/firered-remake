using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Game.Content;
public sealed record CinnabarFieldTile(string Family,string Symbol,int MetatileId,uint RawAttributes,int X,int Y,int Width,int Height);
public sealed record CinnabarFieldLayer(string Family, string Layer, string TexturePath, int Width, int Height);
public sealed record CinnabarFieldArtDocument(int SchemaVersion, ImmutableArray<CinnabarFieldTile> Tiles,
    ImmutableArray<CinnabarFieldLayer> Layers, ImmutableArray<string> Limits,
    ImmutableArray<SnorlaxArtOutput> Outputs);

/// <summary>Authenticated trainer portraits/classes and family-specific statue/barrier/quiz door pixels.</summary>
public sealed class PretCinnabarPresentation
{
    public JsonElement TrainerManifest {get;}
    public ImmutableDictionary<string,byte[]> TrainerFiles {get;}
    public ImmutableDictionary<string,string> TrainerClasses {get;}
    public CinnabarFieldArtDocument Field {get;}
    public ImmutableDictionary<string,byte[]> FieldFiles {get;}
    private PretCinnabarPresentation(JsonElement trainers,ImmutableDictionary<string,byte[]> trainerFiles,CinnabarFieldArtDocument field,ImmutableDictionary<string,byte[]> fieldFiles)
    {
        TrainerManifest=trainers;TrainerFiles=trainerFiles;Field=field;FieldFiles=fieldFiles;
        TrainerClasses=trainers.GetProperty("trainerClasses").EnumerateObject().ToImmutableDictionary(p=>p.Name,p=>p.Value.GetString()!,StringComparer.Ordinal);
    }
    public static PretCinnabarPresentation Load(string root)
    {
        using var trainers=JsonDocument.Parse(SafariSource.Read(root,"content/generated/sprites/trainers/cinnabar/manifest.json"));
        var tr=trainers.RootElement;
        Need(tr.GetProperty("schemaVersion").GetInt32()==1&&tr.GetProperty("portraits").GetArrayLength()==8&&tr.GetProperty("trainerClasses").EnumerateObject().Count()==8,"Cinnabar trainer presentation differs.");
        var trainerFiles=ReadOutputs(root,"content/generated/sprites/trainers/cinnabar/",SafariSource.Parse<ImmutableArray<SnorlaxArtOutput>>(Encoding.UTF8.GetBytes(tr.GetProperty("outputs").GetRawText())));
        var pics=new HashSet<string>(StringComparer.Ordinal);
        foreach(var p in tr.GetProperty("portraits").EnumerateArray())
        {
            Need(pics.Add(p.GetProperty("trainerPic").GetString()!)&&p.GetProperty("width").GetInt32()==64&&p.GetProperty("height").GetInt32()==64&&p.GetProperty("sourceFrame").GetInt32()==0&&p.GetProperty("x").GetInt32()==144&&p.GetProperty("y").GetInt32()==8,"Cinnabar trainer portrait shape differs.");
            var pngBytes=trainerFiles[p.GetProperty("texturePath").GetString()!];Need(pngBytes.Length > 0,"Cinnabar trainer pixels differ.");
        }
        Need(pics.SetEquals(new[]{"TRAINER_PIC_BURGLAR","TRAINER_PIC_FISHERMAN","TRAINER_PIC_LEADER_BLAINE","TRAINER_PIC_SCIENTIST","TRAINER_PIC_SIS_AND_BRO","TRAINER_PIC_SUPER_NERD","TRAINER_PIC_SWIMMER_M","TRAINER_PIC_YOUNGSTER"}),"Cinnabar trainer inventory differs.");
        var field=SafariSource.Parse<CinnabarFieldArtDocument>(SafariSource.Read(root,"content/generated/maps/objects/cinnabar-field-art/manifest.json"));
        Need(field.SchemaVersion==1&&field.Tiles.Length==37&&field.Layers.Length==6&&!field.Limits.IsDefaultOrEmpty,"Cinnabar field presentation differs.");
        var fieldFiles=ReadOutputs(root,"content/generated/maps/objects/cinnabar-field-art/",field.Outputs);
        foreach(var family in new[]{"mansion","gym"})
        {
            int count=family=="mansion"?29:8;var tiles=field.Tiles.Where(t=>t.Family==family).ToArray();var layers=field.Layers.Where(l=>l.Family==family).ToArray();
            Need(tiles.Length==count&&tiles.Select(t=>t.MetatileId).Distinct().Count()==count&&tiles.Select(t=>t.X).SequenceEqual(Enumerable.Range(0,count).Select(i=>i*16))&&tiles.All(t=>t.Y==0&&t.Width==16&&t.Height==16)&&layers.Select(l=>l.Layer).SequenceEqual(new[]{"bottom","middle","top"}),"Cinnabar map-family atlas differs.");
            foreach(var layer in layers)Need(layer.Width==count*16&&layer.Height==16&&(fieldFiles[layer.TexturePath]).Length > 0,"Cinnabar map-family pixels differ.");
        }
        return new(tr.Clone(),trainerFiles,field,fieldFiles);
    }
    private static ImmutableDictionary<string,byte[]> ReadOutputs(string root,string prefix,ImmutableArray<SnorlaxArtOutput> outputs)=>outputs.ToImmutableDictionary(o=>o.Path,o=>SafariSource.Read(root,prefix+o.Path),StringComparer.Ordinal);
    private static void Need(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
}
