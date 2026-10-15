using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

public sealed record NpcTradeItemsDocument(string SourceCommit, string Profile, int PreviousItemCount, int ItemCount,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<EarlyShopStock> ShopsAdded,
    ImmutableArray<NpcTradeItemIcon> Icons);
public sealed record NpcTradeItemIcon(ItemId Item, int Width, int Height, string TexturePath, ImmutableArray<string> SourcePaths,
    JsonElement BindingSource);

/// <summary>Original Vermilion trade room and held Stick; activation belongs to current composition.</summary>
public sealed class PretNpcTradeElyssa
{
    public const string ResourceId = "firered-npc-trade-elyssa-v1";
    public static MapId HouseMapId { get; } = new("map.vermilion-city-house2");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement NpcSources { get; }
    public NpcTradeItemsDocument Items { get; }
    public ImmutableDictionary<string, byte[]> IconFiles { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretNpcTradeElyssa(SilphSourcePackage source, PretWorldMap map, NpcTradeItemsDocument items)
    {
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(map.Id, map);
        SourceFiles = source.Files; GeneratedDirectory = source.Directory; Items = items;
        Catalog = source.Document("catalog.json"); Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        IconFiles = items.Icons.Select(i => i.TexturePath)
            .ToImmutableDictionary(p => p, p => source.Outputs[p], StringComparer.Ordinal);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_VERMILION_CITY_HOUSE2" => HouseMapId,
        "MAP_VERMILION_CITY" => new("map.vermilion-city"),
        _ => throw new InvalidDataException("Unknown original Elyssa room destination: " + symbol)
    };
    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == HouseMapId && map.Width == 11 && map.Height == 9 &&
            map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null, "Original Elyssa room geometry differs.");
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), []);
    }
    public NpcTradeShopCatalog ExtendShop(FossilLabShopCatalog predecessor, PretNpcTradeMail mail)
    {
        ArgumentNullException.ThrowIfNull(predecessor); ArgumentNullException.ThrowIfNull(mail);
        return NpcTradeShopCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = NpcTradeShopCatalog.Profile,Items = predecessor.Catalog.Items.AddRange(Items.Additions).AddRange(mail.Items.Additions) });
    }
    public static PretNpcTradeElyssa Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot,"npc-trade-elyssa",            "scripts/npc-trade-elyssa.lock.json");
        var catalog = source.Document("catalog.json");
        Need(            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 1 ,
            "Elyssa source/catalog identity differs.");
        foreach (var row in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            Need(row.GetProperty("reuseExisting").GetBoolean(), "Elyssa actors must reuse authenticated existing graphics.");
        }
        var retained = source.Document("contracts.json").GetProperty("retainedCity");
        var city = SilphSourcePackage.Json(SafariSource.Read(projectRoot,retained.GetProperty("path").GetString()!));
        Need(JsonElement.DeepEquals(city.GetProperty("events").GetProperty("warp_events")[6], retained.GetProperty("warp")),
            "Elyssa retained city door differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.", "Original Vermilion trade room and Stick.", ResourceId, source.Files);
        var spec = catalog.GetProperty("maps")[0];
        string relative = spec.GetProperty("directory").GetString()!, path = spec.GetProperty("path").GetString()!;
        var bytes = source.Outputs[path];
        Need(spec.GetProperty("stableId").GetString() == HouseMapId.Value && path == relative + "/runtime-map.json" ,
            "Elyssa compact resource binding differs.");
        var runtime = PretRuntimeMapData.Decode(bytes, HouseMapId.Value, MapIdFromSourceSymbol);
        Need(runtime.Width == 11 && runtime.Height == 9 && MapIdFromSourceSymbol(runtime.SourceRef.Symbol) == HouseMapId,
            "Elyssa map identity/dimensions differ.");
        var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(projectRoot, "content/generated/silph-region/npc/red-normal.png"), MapIdFromSourceSymbol);
        Need(map.FieldWarps.Length == 3 && map.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            map.FieldWarps.All(w => w.DestinationMapId == new MapId("map.vermilion-city") && w.DestinationWarpId == 6),
            "Original Elyssa actor/exit cohort differs.");
        var items = source.Parse<NpcTradeItemsDocument>("items.json");
        Need(            items.PreviousItemCount == 125 && items.ItemCount == 126 && items.Additions.Length == 1 &&
            items.Additions[0].Id == NpcTradeShopCatalog.StickId &&
            items.ShopsAdded.IsEmpty && items.Icons.Length == 1, "Elyssa Stick inventory scope differs.");
        Need(items.Additions[0].SourceMetadata is { HoldEffect: "HOLD_EFFECT_STICK", CanGive: true, CanSell: true, CanToss: true },
            "Original Stick held metadata differs.");
        var icon = items.Icons[0];
        Need(icon.Item == NpcTradeShopCatalog.StickId && icon.Width == 24 && icon.Height == 24 &&
            PngRgba8.Decode(source.Outputs[icon.TexturePath], icon.Width, icon.Height).Length == 24 * 24 * 4 &&
            icon.SourcePaths.All(source.Raw.ContainsKey), "Original Stick icon differs.");
        return new(source, map, items);
    }
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
