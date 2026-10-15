using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretRockTunnelItemsDocument(int SchemaVersion, ImmutableArray<PretBillSourceItem> Items, ImmutableArray<PretPewterMachineIcon> Icons,
    ImmutableArray<PretMtMoonPickup> Pickups);

/// <summary>Authenticates seven RockTunnel item declarations, icons and fifteen source pickups.</summary>
public static class PretRockTunnelItems
{
    public const string Profile = "firered-rocktunnel-items-v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static PretRockTunnelItemsDocument Load(string json, SharedMachineCatalog machines)
    {
        var options = machines.CreateJsonOptions();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, false));
        var document = ContentJson.ParseStrict<PretRockTunnelItemsDocument>(json, options);
        Require(document.SchemaVersion == 1 && document.Items.Length == 7 && document.Icons.Length == 7 &&
            document.Pickups.Length == 15,
            "RockTunnel item scope differs.");
        string[] items = ["MAX_ETHER", "PERSIM_BERRY", "NANAB_BERRY", "REVIVE", "EVERSTONE", "PEARL", "HARD_STONE"];
        var definitions = document.Items;
        for (int n = 0; n < definitions.Length; n++)
        {
            var item = definitions[n]; var icon = document.Icons[n];
            Require(item.Metadata.Item.Id.Value == "pret:ITEM_" + items[n] && icon.Item == item.Metadata.Item.Id,
                "RockTunnel item order or icon differs.");
            PretPewterMachines.ValidateIcon(icon);
        }
        foreach (var pickup in document.Pickups)
        {
            Require(pickup.Quantity == 1 && !pickup.Underfoot && pickup.Elevation is 0 or 3 &&
                (pickup.Map is "Route9" or "Route10" or "RockTunnel_1F" or "RockTunnel_B1F") && pickup.X >= 0 && pickup.Y >= 0 &&
                (pickup.Kind == "visible" ? pickup.LocalObjectId > 0 && pickup.Script is not null && pickup.ScriptSource is not null :
                pickup.Kind == "hidden" && pickup.LocalObjectId is null && pickup.Script is null && pickup.ScriptSource is null),
                "RockTunnel pickup identity differs.");
        }
        Require(document.Pickups.Count(p => p.Kind == "hidden") == 8, "RockTunnel hidden-item scope differs.");
        return document;
    }

    public static PretRockTunnelItemsDocument LoadProject(string root, SharedMachineCatalog? shared = null)
    {
        var document = Load(Utf8.GetString(Read(root,            "content/generated/data/items/rocktunnel/rocktunnel-items.json")), (shared ?? SharedMachineCatalog.LoadProject(root)));
        return document;
    }

    /// <summary>Preserves all32 inherited definitions, including Revive; adds six new unique IDs.</summary>

    public static EarlyShopItemDefinition? Definition(PretRockTunnelItemsDocument document, ItemId id)
    {
        var source = document.Items.SingleOrDefault(i => i.Metadata.Item.Id == id);
        if (source is null) return null;
        var definition = PretSsAnneItems.Definition(source);
        return id == RockTunnelItemRules.ReviveId ? definition with { UseSupport = EarlyShopUseSupport.FieldRevive } : definition;
    }
    private static byte[] Read(string root,string path)
    {
        var p = SafeFile(root, path);
        var info = new FileInfo(p);
        Require(info.Length <= ContentJson.MaximumJsonBytes , "RockTunnel item file length differs: " + path);
        var bytes = File.ReadAllBytes(p);
        return bytes;
    }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe RockTunnel item path.");
        for (var d = new DirectoryInfo(root); d is not null; d = d.Parent)
            if (d.LinkTarget is not null) throw new InvalidDataException("RockTunnel item directory symlink rejected.");
        var result = root;
        foreach (var part in path.Split('/'))
        {
            result = Path.Combine(result, part);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("RockTunnel item symlink rejected.");
        }
        return result;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
}
