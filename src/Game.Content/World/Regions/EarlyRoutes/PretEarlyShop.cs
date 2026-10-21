using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretEarlyShopIcon(ItemId Item, int Width, int Height, string PngPath, ImageRegion? Region = null);
public sealed record PretEarlyShopText(string Symbol, string Text, OpeningItemSourceRef Source);
public sealed record PretEarlyShopDocument(int SchemaVersion, EarlyShopCatalog Catalog, ImmutableArray<PretEarlyShopIcon> Icons,
    ImmutableArray<PretEarlyShopText> Texts);

/// <summary>Authenticates the complete pinned catalog, source texts, effect bytes and icon RGBA.
/// Loading stock metadata never enables an unimplemented item action or changes the campaign profile.</summary>
public static class PretEarlyShop
{

    public static PretEarlyShopDocument LoadPresentation(string root, EarlyShopCatalog catalog)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/generated/data/services/early-shop/early-shop.json")));
        var icons = ContentJson.ParseStrict<ImmutableArray<PretEarlyShopIcon>>(json.RootElement.GetProperty("icons").GetRawText(), ContentJson.CreateOptions());
        using var current = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "content/generated/data/items/current-icons.json")));
        icons = icons.AddRange(ContentJson.ParseStrict<ImmutableArray<PretEarlyShopIcon>>(
            current.RootElement.GetProperty("icons").GetRawText(), ContentJson.CreateOptions()));
        if (icons.Select(i => i.Item).Distinct().Count() != icons.Length ||
            icons.Any(i => catalog.Items.All(item => item.Id != i.Item)))
            throw new InvalidDataException("current_item_icon_admission");
        foreach (var icon in icons) ValidateIcon(icon);
        return new(1, catalog, icons, []);
    }

    public static PretEarlyShopDocument Load(string json, SharedItemCatalog items)
    {
        var options = items.CreateJsonOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        var document = ContentJson.ParseStrict<PretEarlyShopDocument>(json, options);
        if (document.SchemaVersion != 1 ||
            document.Icons.IsDefault || document.Icons.Length != 8 ||
            document.Texts.IsDefault || document.Texts.Length != 18)
            throw ContentJson.Error("early_shop_profile", "$", "Incomplete early-shop document.");
        try { EarlyShopInventoryRules.ValidateCatalog(document.Catalog); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        { throw ContentJson.Error("early_shop_catalog", "$.catalog", ex.Message); }
        for (int n = 0; n < document.Icons.Length; n++)
        {
            var icon = document.Icons[n];
            if (icon.Item != document.Catalog.Items[n].Id || icon.Width != 24 || icon.Height != 24)
                throw ContentJson.Error("early_shop_icon", "$.icons", "Item icon identity or dimensions differ.");
            ValidateIcon(icon);
        }
        return document;
    }

    public static void ValidateIcon(PretEarlyShopIcon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        if (icon.Width != 24 || icon.Height != 24 || string.IsNullOrWhiteSpace(icon.PngPath) ||
            Path.IsPathRooted(icon.PngPath) || icon.PngPath.Contains('\\') ||
            icon.PngPath.Split('/').Any(part => part is "" or "." or "..") || !icon.PngPath.EndsWith(".png", StringComparison.Ordinal))
            throw new InvalidDataException("Item icon metadata differs.");
        if (icon.Region is {} region && (region.X < 0 || region.Y < 0 || region.Width != icon.Width || region.Height != icon.Height))
            throw new InvalidDataException("Item icon atlas region differs.");
    }

    public static byte[] TexturePage(PretEarlyShopIcon icon, byte[] png)
    {
        ValidateIcon(icon);
        return png;
    }
}
