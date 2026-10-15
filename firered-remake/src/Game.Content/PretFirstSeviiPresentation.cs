using System.Collections.Immutable;
using System.Text;
using System.Text.Json;

namespace Game.Content;

public sealed record FirstSeviiPresentationAsset(string Id, int Width, int Height, string TexturePath);
public sealed record FirstSeviiPresentationItem(string Id,string Symbol,string Name,string Description,string FieldUseFunction,string Pocket,string AssetId);
public sealed record FirstSeviiPresentationPortrait(string AssetId,string TrainerPic,int SourceTrainerPicId);
public sealed record FirstSeviiPresentationTrainer(string Id,string Name,string TrainerPic,string TrainerClass,string ClassName);
public sealed record FirstSeviiPresentationDocument(int SchemaVersion,ImmutableArray<FirstSeviiPresentationAsset> Assets,
    ImmutableArray<FirstSeviiPresentationItem> Items,ImmutableArray<FirstSeviiPresentationPortrait> Portraits,ImmutableArray<FirstSeviiPresentationTrainer> Trainers);

/// <summary>Original ferry pixels, ten trainer bindings, eight item icons and three ID-only audio cues.
/// All runtime inputs are authenticated; no reference checkout/build is required to start.</summary>
public sealed class PretFirstSeviiPresentation
{
    public const string Profile="firered-first-sevii-presentation-v1";
    public const string GeneratedDirectory="content/generated/ui/first-sevii-presentation/";
    public FirstSeviiPresentationDocument Document {get;}
    private readonly ImmutableDictionary<string,byte[]> _outputs;
    private PretFirstSeviiPresentation(FirstSeviiPresentationDocument document,ImmutableDictionary<string,byte[]> outputs)
    {Document=document;_outputs=outputs;}
    public static PretFirstSeviiPresentation LoadProject(string root,string? audioRoot=null)
    {
        var manifest=SafariSource.Parse<JsonElement>(SafariSource.Read(root,GeneratedDirectory+"manifest.json"));
        void Need(bool ok,string message){if(!ok)throw new InvalidDataException(message);}

        Need(manifest.GetProperty("schemaVersion").GetInt32()==1,"First-Sevii presentation manifest scope differs.");
        var outputs=ImmutableDictionary.CreateBuilder<string,byte[]>(StringComparer.Ordinal);
        foreach(var row in manifest.GetProperty("outputs").EnumerateArray())
        {string p=row.GetProperty("path").GetString()!;outputs.Add(p,SafariSource.Read(root,GeneratedDirectory+p));}
        var document=SafariSource.Parse<FirstSeviiPresentationDocument>(outputs["presentation.json"]);
        Need(document.SchemaVersion==1&&document.Assets.Length==18&&document.Items.Length==8&&document.Portraits.Length==6,"First-Sevii presentation inventory differs.");
        foreach(var asset in document.Assets)
            Need(asset.Width>0&&asset.Height>0&&asset.Width<=256&&asset.Height<=256&&(outputs[asset.TexturePath]).Length > 0,"First-Sevii image identity differs.");
        Need(document.Items.Select(i=>i.Symbol).SequenceEqual(new[]{"ITEM_METEORITE","ITEM_TRI_PASS","ITEM_IAPAPA_BERRY","ITEM_MAX_ELIXIR","ITEM_RAZZ_BERRY","ITEM_PINAP_BERRY","ITEM_FRESH_WATER","ITEM_SODA_POP"})&&
            document.Trainers.All(t=>document.Portraits.Any(p=>p.TrainerPic==t.TrainerPic)),"First-Sevii item/trainer bindings differ.");

        return new(document,outputs.ToImmutable());
    }
    public FirstSeviiPresentationAsset Asset(string id)=>Document.Assets.Single(a=>a.Id==id);
    public byte[] ReadPng(string id)=>_outputs[Asset(id).TexturePath];
}
