using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Items;
using Game.Domain.Ids;
namespace Game.Content;

public sealed record PretRocketHideoutItemsDocument(int SchemaVersion, ImmutableArray<PretBillSourceItem> Items, ImmutableArray<PretPewterMachineIcon> Icons);
public sealed record PretRocketHideoutPickup(string Map, string Kind, int? LocalId, int? EventIndex,
    int X, int Y, int Elevation, string Flag, int SourceFlagId, bool InitiallyHidden, bool Underfoot,
    ItemId Item, int Quantity, string? Script, string? ScriptCommand, OpeningItemSourceRef MapSource,
    OpeningItemSourceRef? ScriptSource);
public sealed record PretRocketHideoutPickupsDocument(int SchemaVersion, ImmutableArray<PretRocketHideoutPickup> Pickups);

public static class PretRocketHideoutItems
{
    public const string Profile = RocketHideoutItemRules.Profile;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static void Require(bool condition, string message) => PretCeladonApproachSource.Require(condition,message);
    public static PretRocketHideoutItemsDocument Load(string json, SharedMachineCatalog machines)
    {
        var options = machines.CreateJsonOptions(); options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase,false));
        var document = ContentJson.ParseStrict<PretRocketHideoutItemsDocument>(json,options);
        Require(document.SchemaVersion == 1 && document.Items.Length == 10 && document.Icons.Length == 10,"Rocket Hideout item scope differs.");
        foreach (var item in document.Items)
        {
            RocketHideoutItemRules.ValidateSourceItem(item.Metadata,item.EffectBytes);
        }
        Require(document.Items.Select(i => i.Metadata.Item.Id).Distinct().Count() == 10,"Duplicate Rocket Hideout source items.");
        foreach (var icon in document.Icons)
        {
            Require(document.Items.Any(i => i.Metadata.Item.Id == icon.Item) && icon.Region is not null,"Rocket Hideout icon identity differs.");
PretPewterMachines.ValidateIcon(icon);
        }
        Require(document.Icons.Select(i => i.Item).Distinct().Count() == 10,"Duplicate Rocket Hideout icons.");
        return document;
    }
    public static PretRocketHideoutItemsDocument LoadProject(string root, SharedMachineCatalog? shared = null)
    {
        var outputs = PretCeladonApproachSource.Outputs(root,"data/items/rocket-hideout");
        var document = Load(Utf8.GetString(outputs["rocket-hideout-items.json"]), (shared ?? SharedMachineCatalog.LoadProject(root)));
        return document;
    }

    private static EarlyShopItemDefinition Definition(PretBillSourceItem item)
    {
        var result = PretSsAnneItems.Definition(item);
        return result.Id == RocketHideoutItemRules.NetBallId || result.Id == RocketHideoutItemRules.NestBallId ?
            result with { UseSupport = EarlyShopUseSupport.SeparateCapture } : result;
    }
    public static EarlyShopItemDefinition? Definition(PretRocketHideoutItemsDocument document, ItemId id)
    {
        var item = document.Items.SingleOrDefault(i => i.Metadata.Item.Id == id); return item is null ? null : Definition(item);
    }
    public static PretRocketHideoutPickupsDocument LoadPickupsProject(string root)
    {
        var bytes = PretCeladonApproachSource.Read(root,"content/generated/data/items/rocket-hideout/rocket-hideout-pickups.json");
        var document = ContentJson.ParseStrict<PretRocketHideoutPickupsDocument>(Utf8.GetString(bytes),ContentJson.CreateOptions());
        Require(document.SchemaVersion == 1 &&
            document.Pickups.Length == 18 && document.Pickups.Count(p => p.Kind == "visible") == 14 && document.Pickups.Count(p => p.Kind == "hidden") == 4 &&
            document.Pickups.Count(p => p.InitiallyHidden) == 2 && document.Pickups.Select(p => p.Flag).Distinct().Count() == 18,"Rocket Hideout pickup scope differs.");
        foreach (var pickup in document.Pickups)
        {
            Require(pickup.Quantity == 1 && pickup.Map is "MAP_ROCKET_HIDEOUT_B1F" or "MAP_ROCKET_HIDEOUT_B2F" or "MAP_ROCKET_HIDEOUT_B3F" or "MAP_ROCKET_HIDEOUT_B4F" &&
                pickup.X >= 0 && pickup.Y >= 0 && pickup.Elevation is 0 or 3 && !pickup.Underfoot && pickup.SourceFlagId > 0,"Rocket Hideout pickup shape differs.");
            Require(pickup.Kind == "visible" ? pickup.LocalId is > 0 && pickup.EventIndex is null && pickup.Script is not null && pickup.ScriptCommand is "finditem" or "giveitem" && pickup.ScriptSource is not null :
                pickup.LocalId is null && pickup.EventIndex is >= 0 && pickup.Script is null && pickup.ScriptCommand is null && pickup.ScriptSource is null,"Rocket Hideout pickup event differs.");
            Require(pickup.InitiallyHidden == (pickup.Item == RocketHideoutItemRules.LiftKeyId || pickup.Item == RocketHideoutItemRules.SilphScopeId),"Rocket key visibility differs.");
        }
        return document;
    }
}
