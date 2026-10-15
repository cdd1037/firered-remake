using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

/// <summary>Original 4F Retro Mail definition and full shelf provenance; Mail composition and stone effects stay separate.</summary>
public static class PretDepartmentFourthFloor
{
    public const string Base = "content/generated/department-fourth-floor/";
    private static byte[] Read(string root,string relative)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, relative));
        return bytes;
    }
    public static NpcTradeItemsDocument LoadDocument(string root)
    {
        var doc = JsonSerializer.Deserialize<NpcTradeItemsDocument>(Read(root,Base + "items.json"),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("department_fourth_floor_document_missing");
        if (            doc.Additions.IsDefault || doc.Additions.Length != 1 ||
            doc.Additions[0].Id != OrdinaryItemInventoryCatalog.RetroMailId ||
            doc.ShopsAdded.IsDefault || !doc.ShopsAdded.IsEmpty || doc.Icons.IsDefault || doc.Icons.Length != 1 ||
            doc.Icons[0].Item != OrdinaryItemInventoryCatalog.RetroMailId || doc.Icons[0].Width != 24 || doc.Icons[0].Height != 24)
            throw new InvalidDataException("department_fourth_floor_scope");
        return doc;
    }
    public static OrdinaryItemInventoryCatalog LoadShop(string root, EarlyShopCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var doc = LoadDocument(root);
        return OrdinaryItemInventoryCatalog.AppendRetroMail(predecessor, doc.Additions);
    }
    public static JsonElement LoadSourceContract(string root) =>
        JsonSerializer.Deserialize<JsonElement>(Read(root,Base + "source-contract.json"));
    public static byte[] LoadIconRgba(string root)
    {
        var icon = LoadDocument(root).Icons[0];
        return PngRgba8.Decode(Read(root,icon.TexturePath),icon.Width,icon.Height);
    }
}
