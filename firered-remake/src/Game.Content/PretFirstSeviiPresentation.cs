using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record FirstSeviiPresentationAsset(string Id,int Width,int Height,string RgbaPath,string PngPath,string RgbaSha256,string PngSha256,ImmutableArray<string> SourcePaths);
public sealed record FirstSeviiPresentationItem(string Id,string Symbol,string Name,string Description,string FieldUseFunction,string Pocket,string AssetId);
public sealed record FirstSeviiPresentationPortrait(string AssetId,string TrainerPic,int SourceTrainerPicId);
public sealed record FirstSeviiPresentationTrainer(string Id,string Name,string TrainerPic,string TrainerClass,string ClassName);
public sealed record FirstSeviiPresentationDocument(int SchemaVersion,string Profile,ImmutableArray<FirstSeviiPresentationAsset> Assets,
    ImmutableArray<FirstSeviiPresentationItem> Items,ImmutableArray<FirstSeviiPresentationPortrait> Portraits,ImmutableArray<FirstSeviiPresentationTrainer> Trainers);

/// <summary>Original ferry pixels, ten trainer bindings, eight item icons and three ID-only audio cues.
/// All runtime inputs are authenticated; no reference checkout/build is required to start.</summary>
public sealed class PretFirstSeviiPresentation
{
    public const string Profile="firered-first-sevii-presentation-v1";
    public const string GeneratedDirectory="content/generated/first-sevii-presentation/";
    public FirstSeviiPresentationDocument Document {get;}
    public ImmutableArray<string> AdditionalSourceHashes {get;}
    private readonly ImmutableDictionary<string,byte[]> _outputs;
    private PretFirstSeviiPresentation(FirstSeviiPresentationDocument document,ImmutableDictionary<string,byte[]> outputs,ImmutableArray<string> hashes)
    {Document=document;_outputs=outputs;AdditionalSourceHashes=hashes;}
    public static PretFirstSeviiPresentation LoadProject(string root,string? audioRoot=null)
    {
        var source=SafariSource.Parse<JsonElement>(SafariSource.Read(root,"scripts/first-sevii-presentation.lock.json",FirstSeviiPresentationIdentity.LockSha256,FirstSeviiPresentationIdentity.LockBytes));
        var manifest=SafariSource.Parse<JsonElement>(SafariSource.Read(root,GeneratedDirectory+"manifest.json",FirstSeviiPresentationIdentity.ManifestSha256,FirstSeviiPresentationIdentity.ManifestBytes));
        void Need(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
        Need(source.GetProperty("schemaVersion").GetInt32()==1&&source.GetProperty("profile").GetString()==Profile&&source.GetProperty("repository").GetString()=="pret/pokefirered"&&
            source.GetProperty("commit").GetString()==FirstSeviiPresentationIdentity.SourceCommit&&source.GetProperty("tree").GetString()==SilphSourcePackage.SourceTree&&
            source.GetProperty("files").GetArrayLength()==FirstSeviiPresentationIdentity.InputCount,"First-Sevii presentation source scope differs.");
        Need(manifest.GetProperty("schemaVersion").GetInt32()==1&&manifest.GetProperty("profile").GetString()==Profile&&manifest.GetProperty("sourceCommit").GetString()==FirstSeviiPresentationIdentity.SourceCommit&&
            manifest.GetProperty("sourceTree").GetString()==SilphSourcePackage.SourceTree&&manifest.GetProperty("lockSha256").GetString()==FirstSeviiPresentationIdentity.LockSha256&&
            manifest.GetProperty("outputs").GetArrayLength()==FirstSeviiPresentationIdentity.OutputCount,"First-Sevii presentation manifest scope differs.");
        var hashes=ImmutableArray.CreateBuilder<string>();hashes.Add("first-sevii-presentation-lock:"+FirstSeviiPresentationIdentity.LockSha256);hashes.Add("first-sevii-presentation-manifest:"+FirstSeviiPresentationIdentity.ManifestSha256);
        var files=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach(var row in source.GetProperty("files").EnumerateArray())
        {
            string p=row.GetProperty("path").GetString()!,h=row.GetProperty("sha256").GetString()!;
            byte[] bytes=SafariSource.Read(root,"content/raw/first-sevii-presentation/"+p,h,row.GetProperty("byteLength").GetInt32());
            Need(SafariSource.GitBlob(bytes)==row.GetProperty("gitBlobSha1").GetString(),"Original first-Sevii source blob differs.");
            files.Add(p,h);hashes.Add("first-sevii-presentation-source:"+p+":"+h);
        }
        Need(SafariSource.Hash(Encoding.UTF8.GetBytes(string.Concat(files.Select(f=>f.Key+"\0"+f.Value+"\n"))))==manifest.GetProperty("fullSourceHash").GetString(),"First-Sevii source inventory differs.");
        var outputs=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var row in manifest.GetProperty("outputs").EnumerateArray())
        {string p=row.GetProperty("path").GetString()!;outputs.Add(p,SafariSource.Read(root,GeneratedDirectory+p,row.GetProperty("sha256").GetString()!,row.GetProperty("byteLength").GetInt32()));}
        var document=SafariSource.Parse<FirstSeviiPresentationDocument>(outputs["presentation.json"]);
        Need(document.SchemaVersion==1&&document.Profile==Profile&&document.Assets.Length==18&&document.Items.Length==8&&document.Portraits.Length==6&&document.Trainers.Length==10,"First-Sevii presentation inventory differs.");
        foreach(var asset in document.Assets)
            Need(asset.Width>0&&asset.Height>0&&asset.Width<=256&&asset.Height<=256&&outputs[asset.RgbaPath].Length==asset.Width*asset.Height*4&&
                SafariSource.Hash(outputs[asset.RgbaPath])==asset.RgbaSha256&&SafariSource.Hash(outputs[asset.PngPath])==asset.PngSha256&&asset.SourcePaths.All(files.ContainsKey),"First-Sevii image identity differs.");
        Need(document.Items.Select(i=>i.Symbol).SequenceEqual(new[]{"ITEM_METEORITE","ITEM_TRI_PASS","ITEM_IAPAPA_BERRY","ITEM_MAX_ELIXIR","ITEM_RAZZ_BERRY","ITEM_PINAP_BERRY","ITEM_FRESH_WATER","ITEM_SODA_POP"})&&
            document.Trainers.All(t=>document.Portraits.Any(p=>p.TrainerPic==t.TrainerPic)),"First-Sevii item/trainer bindings differ.");
        Need(outputs["synth-manifest.json"].Length==FirstSeviiPresentationIdentity.SynthBytes&&SafariSource.Hash(outputs["synth-manifest.json"])==FirstSeviiPresentationIdentity.SynthSha256,"First-Sevii audio cue identity differs.");
        // The unchanged actual pack is mandatory even when presentation audio is disabled.
        // A reference ROM/build hash is never a startup prerequisite.
        _=SafariSource.Read(audioRoot??root,"content/generated/synth-audio/firered.pack","00b3a8376e2a44f1919d4a2573a63cb2873a19b5662df496c4f292c16ad0b46d",0x086fbea4-0x08489968);
        return new(document,outputs.ToImmutable(),hashes.ToImmutable());
    }
    public FirstSeviiPresentationAsset Asset(string id)=>Document.Assets.Single(a=>a.Id==id);
    public byte[] ReadRgba(string id)=>[.._outputs[Asset(id).RgbaPath]];
}
