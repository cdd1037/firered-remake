using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretBillSourceItem(PewterMachineItem Metadata, ImmutableArray<int> EffectBytes,
    OpeningItemSourceRef? EffectSource);
public sealed record PretBillDexView(SpeciesId Species, int SourceSpeciesId, int NationalDex, string Name, int X, int Y, int Width, int Height,
    bool MarksSeen, bool MarksOwned, string PngPath, OpeningItemSourceRef Source,
    OpeningItemSourceRef PaletteSource, OpeningItemSourceRef ScriptSource, OpeningItemSourceRef NameSource);
public sealed record PretBillItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<PretBillSourceItem> Items, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<PretMtMoonPickup> Pickups, ImmutableArray<PretMtMoonFossil> Rewards,
    ImmutableArray<PretBillDexView> DexViews, ImmutableArray<string> Limitations);

/// <summary>Authenticates northern-route items, exact pickups and Bill's four seen-only species pictures.</summary>
public static class PretBillItems
{
    public const string Profile = "firered-bill-items-v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretBillItemsDocument Load(string json)
    {
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretBillItemsDocument>(json, options);
        Require(document.SchemaVersion == 1 && document.Items.Length == 9 && document.Icons.Length == 9 &&
            document.Pickups.Length == 7 && document.Rewards.Length == 3 && document.DexViews.Length == 4 &&
            !document.Limitations.IsDefaultOrEmpty,
            "Bill item scope differs.");
        string[] items = ["NUGGET", "FAME_CHECKER", "SS_TICKET", "ELIXIR", "ETHER",
            "PECHA_BERRY", "ORAN_BERRY", "BLUK_BERRY", "BERRY_POUCH"];
        for (int n = 0; n < document.Items.Length; n++)
        {
            var item = document.Items[n]; var icon = document.Icons[n];
            Require(item.Metadata.Item.Id.Value == "pret:ITEM_" + items[n] &&
                icon.Item == item.Metadata.Item.Id, "Bill item order or icon differs.");
            PretPewterMachines.ValidateIcon(icon);
        }
        foreach (var pickup in document.Pickups)
        {
            Require(pickup.Quantity == 1 && !pickup.Underfoot && pickup.Elevation == 3 &&
                (pickup.Map is "Route24" or "Route25") && pickup.X >= 0 && pickup.Y >= 0 &&
                (pickup.Kind == "visible" ? pickup.LocalObjectId > 0 && pickup.Script is not null && pickup.ScriptSource is not null :
                pickup.Kind == "hidden" && pickup.LocalObjectId is null && pickup.Script is null && pickup.ScriptSource is null),
                "Bill pickup identity differs.");
        }
        Require(document.Pickups.Count(p => p.Kind == "hidden") == 5, "Bill hidden-item scope differs.");
        string[] species = ["EEVEE", "FLAREON", "JOLTEON", "VAPOREON"];
        int[] national = [133, 136, 135, 134];
        for (int n = 0; n < document.DexViews.Length; n++)
        {
            var view = document.DexViews[n];
            Require(view.Species.Value == "pret:SPECIES_" + species[n] && view.Name == species[n] &&
                view.SourceSpeciesId == national[n] && view.NationalDex == national[n] &&
                view.X == 10 && view.Y == 3 && view.MarksSeen && !view.MarksOwned,
                "Bill seen-only species view differs.");
            Require(view.Width == 64 && view.Height == 64, "Bill species picture dimensions differ.");
        }
        return document;
    }

    public static PretBillItemsDocument LoadProject(string root)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/bill-items/bill-items.json")));
        foreach (var icon in document.Icons)
            _ = PretPewterMachines.TexturePage(icon, Read(root, icon.PngPath));
        foreach (var view in document.DexViews)
            _ = DexPng(view, Read(root, "content/generated/bill-items/" + view.PngPath));
        return document;
    }

    public static EarlyShopCatalog LoadShopProject(string root)
    {
        var document = LoadProject(root);
        var old = PretCeruleanItems.LoadShopProject(root);
        var result = old with
        {
            Profile = BillItemRules.Profile,Items = old.Items.AddRange(document.Items.Take(3).Select(Definition))
        };
        EarlyShopInventoryRules.ValidateCatalog(result);
        return result;
    }

    private static EarlyShopItemDefinition Definition(PretBillSourceItem value)
    {
        var item = value.Metadata.Item;
        return new(item.Id, item.SourceItemSymbol, item.SourceItemId, item.Name, value.Metadata.Description,
            item.Price, item.Pocket, item.Importance, item.Type, item.FieldUseFunction, item.BattleUsage,
            item.BattleUseFunction, item.HoldEffectParam, 0, 0, value.EffectBytes, EarlyShopUseSupport.NoFieldUse,
            item.Source, value.EffectSource) { SourceMetadata = item };
    }

    public static byte[] DexPng(PretBillDexView view, byte[] png)
    {
        ArgumentNullException.ThrowIfNull(view);
        Require(view.Width == 64 && view.Height == 64, "Bill species picture dimensions differ.");
        return png;
    }
    private static byte[] Read(string root,string path)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes , "Bill item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Bill item path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("Bill item directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Bill item symlink rejected.");
        }
        return result;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
