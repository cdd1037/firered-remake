using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretSsAnneItemsDocument(int SchemaVersion, ImmutableArray<PretBillSourceItem> Items, PretBillSourceItem BerryPouch, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<PretMtMoonPickup> Pickups);

/// <summary>Authenticates fourteen ship item declarations, their icons, twelve pickups and the Berry Pouch dependency.</summary>
public static class PretSsAnneItems
{
    public const string Profile = "firered-ssanne-items-v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretSsAnneItemsDocument Load(string json, SharedMachineCatalog machines)
    {
        var options = machines.CreateJsonOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretSsAnneItemsDocument>(json, options);
        Require(document.SchemaVersion == 1 && document.Items.Length == 14 && document.Icons.Length == 15 &&
            document.Pickups.Length == 12 && document.BerryPouch is not null,
            "S.S. Anne item scope differs.");
        string[] items = ["CHERI_BERRY", "CHESTO_BERRY", "ETHER", "GREAT_BALL", "HM01", "HYPER_POTION", "LAVA_COOKIE", "PECHA_BERRY", "SS_TICKET", "STARDUST", "SUPER_POTION", "TM31", "TM44", "X_ATTACK", "BERRY_POUCH"];
        var definitions = document.Items.Add(document.BerryPouch!);
        for (int n = 0; n < definitions.Length; n++)
        {
            var item = definitions[n]; var icon = document.Icons[n];
            Require(item.Metadata.Item.Id.Value == "pret:ITEM_" + items[n] && icon.Item == item.Metadata.Item.Id,
                "S.S. Anne item order or icon differs.");
            PretPewterMachines.ValidateIcon(icon);
        }
        foreach (var pickup in document.Pickups)
        {
            Require(pickup.Quantity == 1 && !pickup.Underfoot && pickup.Elevation is 0 or 3 &&
                pickup.Map.StartsWith("SSAnne_", StringComparison.Ordinal) && pickup.X >= 0 && pickup.Y >= 0 &&
                (pickup.Kind == "visible" ? pickup.LocalObjectId > 0 && pickup.Script is not null && pickup.ScriptSource is not null :
                pickup.Kind == "hidden" && pickup.LocalObjectId is null && pickup.Script is null && pickup.ScriptSource is null),
                "S.S. Anne pickup identity differs.");
        }
        Require(document.Pickups.Count(p => p.Kind == "hidden") == 5, "S.S. Anne hidden-item scope differs.");
        return document;
    }

    public static PretSsAnneItemsDocument LoadProject(string root, SharedMachineCatalog? shared = null)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/data/items/ssanne/ssanne-items.json")), (shared ?? SharedMachineCatalog.LoadProject(root)));
        return document;
    }

    /// <summary>Appends only ship additions to the authenticated four-Mart Vermilion catalog.</summary>

    public static EarlyShopItemDefinition? Definition(PretSsAnneItemsDocument document, ItemId id)
    {
        var source = document.Items.Add(document.BerryPouch!).FirstOrDefault(i => i.Metadata.Item.Id == id);
        return source is null ? null : Definition(source);
    }
    public static EarlyShopItemDefinition Definition(PretBillSourceItem value)
    {
        var item = value.Metadata.Item;
        var use = item.SourceItemSymbol switch
        {
            "ITEM_ETHER" => EarlyShopUseSupport.FieldEther,
            "ITEM_SUPER_POTION" or "ITEM_HYPER_POTION" => EarlyShopUseSupport.Potion,
            "ITEM_CHERI_BERRY" or "ITEM_PECHA_BERRY" => EarlyShopUseSupport.StatusCure,
            "ITEM_CHESTO_BERRY" => EarlyShopUseSupport.DeferredSleep,
            "ITEM_GREAT_BALL" => EarlyShopUseSupport.SeparateCapture,
            _ => EarlyShopUseSupport.NoFieldUse
        };
        int heal = use == EarlyShopUseSupport.Potion ? item.HoldEffectParam : 0;
        uint cure = item.SourceItemSymbol == "ITEM_CHERI_BERRY" ? 64u : item.SourceItemSymbol == "ITEM_PECHA_BERRY" ? 8u : 0u;
        return new(item, value.Metadata.Description, heal, cure, value.EffectBytes, use);
    }
    private static byte[] Read(string root,string path)
    {
        var p = GeneratedResourceFile.SafePath(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes , "S.S. Anne item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        return bytes;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
