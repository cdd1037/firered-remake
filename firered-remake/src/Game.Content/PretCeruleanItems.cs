using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;

namespace Game.Content;

public sealed record PretCeruleanItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    PretMtMoonItem Item, EarlyShopStock Shop, PretPewterMachineIcon Icon, ImmutableArray<string> Limitations);

/// <summary>Authenticates original source, the generated data and palette-derived Super Potion art.</summary>
public static class PretCeruleanItems
{
    public const string Profile = "firered-cerulean-items-v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretCeruleanItemsDocument Load(string json)
    {
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretCeruleanItemsDocument>(json, options);
        Require(document.SchemaVersion == 1 &&
            !document.RulesSources.IsDefaultOrEmpty && !document.Limitations.IsDefaultOrEmpty,
            "Cerulean item scope differs.");
        Require(document.Item.EffectSource is not null, "Cerulean item effect source missing.");
        CeruleanItemRules.ValidateSuperPotion(Definition(document.Item));
        CeruleanItemRules.ValidateStock(document.Shop);
        Require(document.Icon.Item == CeruleanItemRules.SuperPotionId, "Cerulean item icon identity differs.");
        PretPewterMachines.ValidateIcon(document.Icon);
        return document;
    }

    public static PretCeruleanItemsDocument LoadProject(string root)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/cerulean-items/cerulean-items.json")));
        return document;
    }

    public static EarlyShopCatalog LoadShopProject(string root)
    {
        var document = LoadProject(root);
        var old = PretMtMoonItems.LoadShopProject(root);
        var result = old with
        {
            Profile = CeruleanItemRules.Profile,Items = old.Items.Add(Definition(document.Item)),Shops = old.Shops.Add(document.Shop)
        };
        EarlyShopInventoryRules.ValidateCatalog(result);
        return result;
    }

    private static EarlyShopItemDefinition Definition(PretMtMoonItem value)
    {
        var item = value.Metadata.Item;
        return new(item.Id, item.SourceItemSymbol, item.SourceItemId, item.Name, value.Metadata.Description,
            item.Price, item.Pocket, item.Importance, item.Type, item.FieldUseFunction, item.BattleUsage,
            item.BattleUseFunction, item.HoldEffectParam, 50, 0, value.EffectBytes, value.UseSupport,
            item.Source, value.EffectSource) { SourceMetadata = item };
    }
    private static byte[] Read(string root,string path)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes , "Cerulean item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Cerulean item path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("Cerulean item directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Cerulean item symlink rejected.");
        }
        return result;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
