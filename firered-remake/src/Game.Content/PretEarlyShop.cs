using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretEarlyShopIcon(ItemId Item, int Width, int Height, string PngPath, OpeningItemSourceRef Source,
    OpeningItemSourceRef PaletteSource, OpeningItemSourceRef BindingSource);
public sealed record PretEarlyShopText(string Symbol, string Text, OpeningItemSourceRef Source);
public sealed record PretEarlyShopDocument(int SchemaVersion, ImmutableArray<PretOpeningItemInput> Inputs,
    ImmutableArray<OpeningItemSourceRef> RulesSources, EarlyShopCatalog Catalog, ImmutableArray<PretEarlyShopIcon> Icons,
    ImmutableArray<PretEarlyShopText> Texts, ImmutableArray<string> Limitations);

/// <summary>Authenticates the complete pinned catalog, source texts, effect bytes and icon RGBA.
/// Loading stock metadata never enables an unimplemented item action or changes the campaign profile.</summary>
public static class PretEarlyShop
{

    public static PretEarlyShopDocument Load(string json)
    {
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        var document = ContentJson.ParseStrict<PretEarlyShopDocument>(json, options);
        if (document.SchemaVersion != 1 || document.Inputs.IsDefault ||
            document.RulesSources.IsDefaultOrEmpty || document.Icons.IsDefault || document.Icons.Length != 8 ||
            document.Texts.IsDefault || document.Texts.Length != 18 || document.Limitations.IsDefaultOrEmpty)
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
    }

    public static byte[] IconRgba(PretEarlyShopIcon icon, byte[] png)
    {
        ValidateIcon(icon);
        return PngRgba8.Decode(png, icon.Width, icon.Height);
    }
}
