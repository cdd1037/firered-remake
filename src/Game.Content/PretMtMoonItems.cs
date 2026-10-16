using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
namespace Game.Content;

public sealed record PretMtMoonItem(MachineItem Metadata, ImmutableArray<int> EffectBytes, EarlyShopUseSupport UseSupport);
public sealed record PretMtMoonPickup(string Map, string Kind, ItemId Item, int Quantity, int? LocalObjectId,
    int X, int Y, int Elevation, string Flag, bool Underfoot, string? Script, OpeningItemSourceRef Source,
    OpeningItemSourceRef? ScriptSource);
public sealed record PretMtMoonFossil(ItemId Item, string Script, OpeningItemSourceRef Source);
public sealed record PretMtMoonItemsDocument(int SchemaVersion, ImmutableArray<PretMtMoonItem> Items, ImmutableArray<MachineDefinition> Machines,
    ImmutableArray<MachineCompatibility> Tm39Compatibility, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<PretMtMoonPickup> Pickups, ImmutableArray<PretMtMoonFossil> Fossils);

/// <summary>All reviewed input, generated and icon bytes are authenticated before bounded composition.</summary>
public static class PretMtMoonItems
{
    public const string Profile = "firered-mtmoon-items-v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretMtMoonItemsDocument Load(string json, SharedMachineCatalog machines)
    {
        var options = machines.CreateJsonOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var d = ContentJson.ParseStrict<PretMtMoonItemsDocument>(json, options);
        Require(d.SchemaVersion == 1 && d.Items.Length == 12 && d.Machines.Length == 3 && d.Tm39Compatibility.Length == 56 &&
            d.Icons.Length == 12 && d.Pickups.Length == 19 && d.Fossils.Length == 2, "MtMoon item scope differs.");
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

    public static PretMtMoonItemsDocument LoadProject(string root, SharedMachineCatalog? shared = null)
    {
        var d = Load(Utf8.GetString(Read(root,"content/generated/data/items/mtmoon/mtmoon-items.json")), (shared ?? SharedMachineCatalog.LoadProject(root)));
        return d;
    }

    private static byte[] Read(string root,string path)
    {
        var p = GeneratedResourceFile.SafePath(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes , "MtMoon item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        return bytes;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
