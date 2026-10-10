using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretEarlyShopIcon(ItemId Item, int Width, int Height, string RgbaBase64, string RgbaSha256,
    string PngPath, string PngSha256, OpeningItemSourceRef Source, OpeningItemSourceRef PaletteSource,
    OpeningItemSourceRef BindingSource);
public sealed record PretEarlyShopText(string Symbol, string Text, OpeningItemSourceRef Source);
public sealed record PretEarlyShopDocument(int SchemaVersion, ImmutableArray<PretOpeningItemInput> Inputs,
    ImmutableArray<OpeningItemSourceRef> RulesSources, EarlyShopCatalog Catalog, ImmutableArray<PretEarlyShopIcon> Icons,
    ImmutableArray<PretEarlyShopText> Texts, ImmutableArray<string> Limitations);

/// <summary>Authenticates the complete pinned catalog, source texts, effect bytes and icon RGBA.
/// Loading stock metadata never enables an unimplemented item action or changes the campaign profile.</summary>
public static class PretEarlyShop
{
    public const string GeneratedSha256 = "ebf9ebc60ba10937f318213528872e2ad9f46d095a7ecf18555d5b64498a46c7";
    public const string LockSha256 = "710db0686102fe5cbeb256876cac3b5d3b74320bbafb364478872f64da5642a3";

    public static PretEarlyShopDocument Load(string json)
    {
        if (Hash(Encoding.UTF8.GetBytes(json)) != GeneratedSha256)
            throw ContentJson.Error("early_shop_generated_hash", "$", "The complete reviewed early-shop bytes are required.");
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        var document = ContentJson.ParseStrict<PretEarlyShopDocument>(json, options);
        if (document.SchemaVersion != 1 || document.Inputs.IsDefault || document.Inputs.Length != 35 ||
            document.RulesSources.IsDefaultOrEmpty || document.Icons.IsDefault || document.Icons.Length != 8 ||
            document.Texts.IsDefault || document.Texts.Length != 18 || document.Limitations.IsDefaultOrEmpty)
            throw ContentJson.Error("early_shop_profile", "$", "Incomplete early-shop document.");
        try { EarlyShopInventoryRules.ValidateCatalog(document.Catalog); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        { throw ContentJson.Error("early_shop_catalog", "$.catalog", ex.Message); }
        var inputs = document.Inputs.ToDictionary(i => i.Path, StringComparer.Ordinal);
        void Source(OpeningItemSourceRef source)
        {
            if (source is null || string.IsNullOrWhiteSpace(source.Symbol) || source.StartLine < 1 ||
                source.EndLine < source.StartLine || !inputs.TryGetValue(source.Path, out var input) ||
                input.Sha256 != source.Sha256)
                throw ContentJson.Error("early_shop_source_ref", "$", "Source reference differs.");
        }
        foreach (var source in document.RulesSources) Source(source);
        foreach (var item in document.Catalog.Items)
        {
            Source(item.Source);
            if (item.EffectSource is not null) Source(item.EffectSource);
        }
        foreach (var shop in document.Catalog.Shops) Source(shop.Source);
        foreach (var text in document.Texts) Source(text.Source);
        for (int n = 0; n < document.Icons.Length; n++)
        {
            var icon = document.Icons[n];
            if (icon.Item != document.Catalog.Items[n].Id || icon.Width != 24 || icon.Height != 24)
                throw ContentJson.Error("early_shop_icon", "$.icons", "Item icon identity or dimensions differ.");
            _ = IconRgba(icon);
            Source(icon.Source); Source(icon.PaletteSource); Source(icon.BindingSource);
        }
        return document;
    }

    public static byte[] IconRgba(PretEarlyShopIcon icon)
    {
        ArgumentNullException.ThrowIfNull(icon);
        byte[] rgba;
        try { rgba = Convert.FromBase64String(icon.RgbaBase64); }
        catch (FormatException) { throw ContentJson.Error("early_shop_icon_rgba", "$.icons", "Malformed RGBA encoding."); }
        if (icon.Width != 24 || icon.Height != 24 || rgba.Length != 24 * 24 * 4 || Hash(rgba) != icon.RgbaSha256)
            throw ContentJson.Error("early_shop_icon_rgba", "$.icons", "Item icon RGBA differs.");
        return rgba;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
