using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Original Water Stone metadata and icon. Original evolution effect is retained; actual use remains explicitly unsupported.</summary>
public static class PretSeafoamItems
{
    public const string Base = "content/generated/data/items/seafoam.json";
    private static byte[] Read(string root, string relative)
    {
        return File.ReadAllBytes(Path.Combine(root, relative));
    }
    public static NpcTradeItemsDocument LoadDocument(string root, SharedMachineCatalog? shared = null)
    {
            var doc = JsonSerializer.Deserialize<NpcTradeItemsDocument>(Read(root,Base),
            (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions())
            ?? throw new InvalidDataException("seafoam_item_document_missing");
            if (            doc.PreviousItemCount != 127 || doc.ItemCount != 128 || doc.Additions.IsDefault || doc.Additions.Length != 1 ||
            doc.Additions[0].Id != ItemIds.WaterStoneId || doc.Additions[0].UseSupport != EarlyShopUseSupport.NoFieldUse ||
                doc.Additions[0].FieldUseFunction != "FieldUseFunc_EvoItem" || !doc.Additions[0].EffectBytes.SequenceEqual([0,0,0,0,128,0]) || doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty ||
            doc.Icons.IsDefault || doc.Icons.Length != 1 || doc.Icons[0].Item != ItemIds.WaterStoneId || doc.Icons[0].Width != 24 || doc.Icons[0].Height != 24)
            throw new InvalidDataException("seafoam_item_scope");
        return doc;
    }

}
