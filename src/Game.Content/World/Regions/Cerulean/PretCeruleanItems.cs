using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretCeruleanItemsDocument(int SchemaVersion, PretMtMoonItem Item, EarlyShopStock Shop, ItemIcon Icon);

/// <summary>Authenticates original source, the generated data and palette-derived Super Potion art.</summary>
public static class PretCeruleanItems
{
    public const string Profile = "firered-cerulean-items-v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretCeruleanItemsDocument Load(string json, SharedMachineCatalog machines)
    {
        var options = machines.CreateJsonOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretCeruleanItemsDocument>(json, options);
        Require(document.SchemaVersion == 1,
            "Cerulean item scope differs.");
        CeruleanItemRules.ValidateSuperPotion(Definition(document.Item));
        CeruleanItemRules.ValidateStock(document.Shop);
        Require(document.Icon.Item == CeruleanItemRules.SuperPotionId, "Cerulean item icon identity differs.");
        ItemIconAssets.Validate(document.Icon);
        return document;
    }

    public static PretCeruleanItemsDocument LoadProject(string root, SharedMachineCatalog? shared = null)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/data/items/cerulean/cerulean-items.json")), (shared ?? SharedMachineCatalog.LoadProject(root)));
        return document;
    }

    private static EarlyShopItemDefinition Definition(PretMtMoonItem value)
    {
        var item = value.Metadata.Item;
        return new(item, value.Metadata.Description, 50, 0, value.EffectBytes, value.UseSupport);
    }
    private static byte[] Read(string root,string path)
    {
        var p = GeneratedResourceFile.SafePath(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes , "Cerulean item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        return bytes;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
