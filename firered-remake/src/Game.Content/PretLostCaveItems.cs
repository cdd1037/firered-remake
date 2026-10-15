using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Original Lost Cave/Selphy inventory closure; acquisition remains owned by the existing current commands.</summary>
public static class PretLostCaveItems
{
    public const string Base = "content/generated/lost-cave-items/";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static byte[] Read(string root,string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/').Any(p => p is "" or "." or ".."))
            throw new InvalidDataException("lost_cave_item_path");
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        return bytes;
    }
    public static NpcTradeItemsDocument LoadItemsDocument(string root)
    {
        var doc = JsonSerializer.Deserialize<NpcTradeItemsDocument>(Read(root,Base + "items.json"), Options)
            ?? throw new InvalidDataException("lost_cave_item_document_missing");
        if (            doc.Additions.IsDefault || doc.Additions.Length != 4 ||
            !doc.Additions.Select(i => i.Id).SequenceEqual(OrdinaryItemInventoryCatalog.LostCaveItems) ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 4 ||
            !doc.Icons.Select(i => i.Item).SequenceEqual(OrdinaryItemInventoryCatalog.LostCaveItems) ||
            doc.Icons.Any(i => i.Width != 24 || i.Height != 24))
            throw new InvalidDataException("lost_cave_item_scope");
        return doc;
    }
    public static OrdinaryItemInventoryCatalog LoadItems(string root, EarlyShopCatalog predecessor) =>
        OrdinaryItemInventoryCatalog.AppendLostCaveItems(predecessor, LoadItemsDocument(root).Additions);
    public static JsonElement LoadSourceContract(string root) =>
        JsonSerializer.Deserialize<JsonElement>(Read(root,Base + "source-contract.json"));
}
