using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Original ordinary item definitions and pixels. Effect availability remains explicit.</summary>
public static class PretOrdinaryItemSupplement
{
    public const string Base = "content/generated/ordinary-item-supplement/";
    private static byte[] Read(string root, string relative)
    {
        return File.ReadAllBytes(Path.Combine(root, relative));
    }
    public static NpcTradeItemsDocument LoadDocument(string root, SharedMachineCatalog? shared = null)
    {
            var doc = JsonSerializer.Deserialize<NpcTradeItemsDocument>(Read(root,Base + "items.json"),
            (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions())
            ?? throw new InvalidDataException("ordinary_item_document_missing");
            if (            doc.PreviousItemCount != 129 || doc.ItemCount != 130 || doc.Additions.IsDefault || doc.Additions.Length != 1 ||
            doc.Additions[0].Id != ItemIds.PpMaxId ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 1 ||
            doc.Icons[0].Item != ItemIds.PpMaxId || doc.Icons[0].Width != 24 || doc.Icons[0].Height != 24)
            throw new InvalidDataException("ordinary_item_scope");
        return doc;
    }

    public static JsonElement LoadSourceContract(string root) =>
            JsonSerializer.Deserialize<JsonElement>(Read(root,Base + "source-contract.json"));
}
