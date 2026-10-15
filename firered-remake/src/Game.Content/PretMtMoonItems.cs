using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;

public sealed record PretMtMoonItem(PewterMachineItem Metadata, ImmutableArray<int> EffectBytes,
    OpeningItemSourceRef? EffectSource, EarlyShopUseSupport UseSupport);
public sealed record PretMtMoonPickup(string Map, string Kind, ItemId Item, int Quantity, int? LocalObjectId,
    int X, int Y, int Elevation, string Flag, bool Underfoot, string? Script, OpeningItemSourceRef Source,
    OpeningItemSourceRef? ScriptSource);
public sealed record PretMtMoonFossil(ItemId Item, string Script, OpeningItemSourceRef Source);
public sealed record PretMtMoonItemsDocument(int SchemaVersion, string Profile, string SourceCommit,
    ImmutableArray<PretPewterMachineInput> Inputs, ImmutableArray<OpeningItemSourceRef> RulesSources,
    ImmutableArray<PretMtMoonItem> Items, ImmutableArray<PewterAdditionalMachine> Machines,
    ImmutableArray<PewterMachineCompatibility> Tm39Compatibility, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<PretMtMoonPickup> Pickups, ImmutableArray<PretMtMoonFossil> Fossils, ImmutableArray<string> Limitations);

/// <summary>All reviewed input, generated and icon bytes are authenticated before bounded composition.</summary>
public static class PretMtMoonItems
{
    public const string Profile = "firered-mtmoon-items-v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretMtMoonItemsDocument Load(string json)
    {
        var options = ContentJson.CreateOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var d = ContentJson.ParseStrict<PretMtMoonItemsDocument>(json, options);
        Require(d.SchemaVersion == 1 && d.Items.Length == 12 && d.Machines.Length == 3 && d.Tm39Compatibility.Length == 56 &&
            d.Icons.Length == 12 && d.Pickups.Length == 19 && d.Fossils.Length == 2 && !d.Limitations.IsDefaultOrEmpty &&
            !d.RulesSources.IsDefaultOrEmpty, "MtMoon item scope differs.");
        foreach (var i in d.Icons)
        {
            PretPewterMachines.ValidateIcon(i);
        }
        foreach (var p in d.Pickups)
        {
            Require(p.Quantity == 1 && !p.Underfoot && p.X >= 0 && p.Y >= 0 &&
                (p.Kind == "visible" ? p.LocalObjectId > 0 && p.Script is not null && p.ScriptSource is not null :
                p.Kind == "hidden" && p.LocalObjectId is null && p.Script is null && p.ScriptSource is null), "MtMoon pickup identity differs.");
        }
        Require(d.Pickups.Count(p => p.Map != "Route4") == 18 && d.Pickups.Count(p => p.Kind == "hidden") == 8 &&
            d.Pickups.Single(p => p.Map == "Route4").Item == new ItemId("pret:ITEM_TM05"), "MtMoon pickup coverage differs.");
        return d;
    }

    public static PretMtMoonItemsDocument LoadProject(string root)
    {
        var d = Load(Utf8.GetString(Read(root,"content/generated/mtmoon-items/mtmoon-items.json")));
        return d;
    }

    public static EarlyShopCatalog LoadShopProject(string root)
    {
        var d = LoadProject(root);
        var old = PretEarlyShop.Load(Utf8.GetString(Read(root,"content/generated/early-shop/early-shop.json")));
        var items = d.Items.Take(9).Select(value =>
        {
            var i = value.Metadata.Item;
            return new EarlyShopItemDefinition(i.Id, i.SourceItemSymbol, i.SourceItemId, i.Name, value.Metadata.Description,
                i.Price, i.Pocket, i.Importance, i.Type, i.FieldUseFunction, i.BattleUsage, i.BattleUseFunction,
                i.HoldEffectParam, 0, 0, value.EffectBytes, value.UseSupport, i.Source, value.EffectSource) { SourceMetadata = i };
        });
        var result = old.Catalog with { Profile = EarlyShopInventoryRules.MtMoonProfile,Items = old.Catalog.Items.AddRange(items) };
        EarlyShopInventoryRules.ValidateCatalog(result);
        return result;
    }
    private static byte[] Read(string root,string path)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes , "MtMoon item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe MtMoon item path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("MtMoon item directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("MtMoon item symlink rejected.");
        }
        return result;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
