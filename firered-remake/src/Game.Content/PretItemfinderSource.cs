using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.World;
namespace Game.Content;

public sealed record PretItemfinderIcon(string Item, string File);
public sealed record PretItemfinderDocument(int SchemaVersion, int ItemfinderSourceId, int RequiredCaught,
    ImmutableArray<ItemfinderUnderfoot> Underfoot, ImmutableDictionary<string, string> Texts,
    string ItemfinderDescription, ImmutableArray<PretItemfinderIcon> Icons);
public static class PretItemfinderSource
{
    public static PretItemfinderDocument Load(string root)
    {
        var directory=Path.Combine(root,"content/generated/data/items/itemfinder-source");
        var bytes=File.ReadAllBytes(Path.Combine(directory,"itemfinder-source.json"));
        var result=ContentJson.ParseStrict<PretItemfinderDocument>(System.Text.Encoding.UTF8.GetString(bytes),ContentJson.CreateOptions());
        if(result.SchemaVersion!=1||result.ItemfinderSourceId!=261||result.RequiredCaught!=30||result.Underfoot.Length!=5||result.Icons.Length!=4)throw new InvalidDataException("itemfinder_source_contract");
        var icon=File.ReadAllBytes(Path.Combine(directory,"itemfinder.rgba"));
        if(icon.Length!=24*24*4)throw new InvalidDataException("itemfinder_icon_hash");
        foreach(var entry in result.Icons)
        {
            if(entry.File.Contains('/')||entry.File.Contains('\\')||!entry.File.EndsWith(".rgba",StringComparison.Ordinal))throw new InvalidDataException("itemfinder_icon_path");
            var pixels=File.ReadAllBytes(Path.Combine(directory,entry.File));
            if(pixels.Length == 0)throw new InvalidDataException("itemfinder_item_icon_hash");
        }
        return result;
    }
}
public sealed partial class PretCampaignCatalog:IItemfinderSourceCatalog
{
    public PretItemfinderDocument? ItemfinderDocument {get;internal set;}
    public ItemfinderSourceContract? ItemfinderSource=>ItemfinderDocument is {} source?new(source.Underfoot.AddRange(CapeBrinkUnderfootItems),source.Texts):null;
}
