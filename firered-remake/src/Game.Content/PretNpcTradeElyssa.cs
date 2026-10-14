using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

public sealed record NpcTradeItemsDocument(string SourceCommit, string Profile, int PreviousItemCount, int ItemCount,
    ImmutableArray<EarlyShopItemDefinition> Additions, string AdditionsSemanticSha256,
    ImmutableArray<EarlyShopStock> ShopsAdded, ImmutableArray<NpcTradeItemIcon> Icons);
public sealed record NpcTradeItemIcon(ItemId Item, int Width, int Height, string RgbaPath, string PngPath,
    string RgbaSha256, string PngSha256, ImmutableArray<string> SourcePaths, JsonElement BindingSource);

/// <summary>Original Vermilion trade room and held Stick; activation belongs to current composition.</summary>
public sealed class PretNpcTradeElyssa
{
    public const string ResourceId = "firered-npc-trade-elyssa-v1";
    public const string ManifestSha256 = "83af0f6324bcf6fc04c6451c869f4179c6b23b041ffc4d52adafe7adb7a56d18";
    public const string LockSha256 = "235a2a123d6c7c120f7c68d3dd4ff21250b06a89b6d6cdd2a1ad0995d5b2a09a";
    public const string FullSourceHash = "a58e254ea2faa4a6239cec987cb04b42f6e52ca883aa30c89dae0abe4e9f7137";
    public static MapId HouseMapId { get; } = new("map.vermilion-city-house2");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableArray<string> AdditionalSourceHashes { get; }
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
        IconFiles = items.Icons.SelectMany(i => new[] { i.RgbaPath, i.PngPath })
            .ToImmutableDictionary(p => p, p => source.Outputs[p], StringComparer.Ordinal);
        AdditionalSourceHashes = ["npc-trade-elyssa-manifest:" + ManifestSha256, "npc-trade-elyssa-lock:" + LockSha256,
            .. source.Files.Select(f => "npc-trade-elyssa:" + f.Path + ":" + f.Sha256)];
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
            Profile = NpcTradeShopCatalog.Profile, Items = predecessor.Catalog.Items.AddRange(Items.Additions).AddRange(mail.Items.Additions) });
    }
    public static PretNpcTradeElyssa Load(string projectRoot)
    {
        var source = new SilphSourcePackage(projectRoot, "npc-trade-elyssa", ResourceId,
            "scripts/npc-trade-elyssa.lock.json", LockSha256, 15127, ManifestSha256, 3652, 49, 16);
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("fullSourceHash").GetString() == FullSourceHash &&
            catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 1 &&
            JsonElement.DeepEquals(catalog.GetProperty("inputFiles"), source.Lock.GetProperty("files")),
            "Elyssa source/catalog identity differs.");
        foreach (var row in source.Document("npc-source.json").GetProperty("graphics").EnumerateArray())
        {
            Need(row.GetProperty("reuseExisting").GetBoolean(), "Elyssa actors must reuse authenticated existing graphics.");
            _ = SafariSource.Read(projectRoot, row.GetProperty("texturePath").GetString()!, row.GetProperty("sha256").GetString()!, row.GetProperty("byteLength").GetInt32());
        }
        var retained = source.Document("contracts.json").GetProperty("retainedCity");
        var city = SilphSourcePackage.Json(SafariSource.Read(projectRoot, retained.GetProperty("path").GetString()!,
            retained.GetProperty("sha256").GetString()!, retained.GetProperty("byteLength").GetInt32()));
        Need(JsonElement.DeepEquals(city.GetProperty("events"), SilphSourcePackage.Json(source.Raw["data/maps/VermilionCity/map.json"])),
            "Elyssa retained city source events differ.");
        Need(JsonElement.DeepEquals(city.GetProperty("events").GetProperty("warp_events")[6], retained.GetProperty("warp")),
            "Elyssa retained city door differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit, FullSourceHash,
            "Local source evidence; redistribution rights are not inferred.", "Original Vermilion trade room and Stick.", ResourceId, source.Files);
        var spec = catalog.GetProperty("maps")[0];
        string relative = spec.GetProperty("directory").GetString()!, path = spec.GetProperty("path").GetString()!;
        var bytes = source.Outputs[path];
        Need(spec.GetProperty("stableId").GetString() == HouseMapId.Value && path == relative + "/runtime-map.json" &&
            spec.GetProperty("sha256").GetString() == SafariSource.Hash(bytes) && spec.GetProperty("byteLength").GetInt32() == bytes.Length,
            "Elyssa compact resource binding differs.");
        var runtime = PretRuntimeMapData.Decode(bytes, new(HouseMapId.Value, path, bytes.Length, SafariSource.Hash(bytes),
            relative + "/world.json", spec.GetProperty("sourceWorldByteLength").GetInt32(), spec.GetProperty("sourceWorldSha256").GetString()!, FullSourceHash));
        PretRuntimeMapIntegration.ValidateSource(runtime, metadata, source.Raw.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal), spec);
        Need(runtime.Width == 11 && runtime.Height == 9 && MapIdFromSourceSymbol(runtime.SourceRef.Symbol) == HouseMapId,
            "Elyssa map identity/dimensions differ.");
        foreach (var artifact in runtime.Artifacts)
            Need(SafariSource.Hash(source.Outputs[relative + "/" + artifact.Path]) == artifact.Sha256, "Elyssa image binding differs.");
        var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
            PretRuntimeMapValidation.SafeFile(projectRoot, "content/generated/silph-region/npc/red-normal.png"), MapIdFromSourceSymbol);
        Need(map.FieldWarps.Length == 3 && map.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            map.FieldWarps.All(w => w.DestinationMapId == new MapId("map.vermilion-city") && w.DestinationWarpId == 6),
            "Original Elyssa actor/exit cohort differs.");
        var items = source.Parse<NpcTradeItemsDocument>("items.json");
        Need(items.SourceCommit == PretWorldMap.SourceCommit && items.Profile == NpcTradeShopCatalog.Profile &&
            items.PreviousItemCount == 125 && items.ItemCount == 126 && items.Additions.Length == 1 &&
            items.Additions[0].Id == NpcTradeShopCatalog.StickId && items.AdditionsSemanticSha256 == NpcTradeShopCatalog.AdditionalItemsSourceSha256 &&
            items.ShopsAdded.IsEmpty && items.Icons.Length == 1, "Elyssa Stick inventory scope differs.");
        source.Reference(items.Additions[0].Source);
        Need(items.Additions[0].SourceMetadata is { HoldEffect: "HOLD_EFFECT_STICK", CanGive: true, CanSell: true, CanToss: true },
            "Original Stick held metadata differs.");
        var icon = items.Icons[0];
        Need(icon.Item == NpcTradeShopCatalog.StickId && icon.Width == 24 && icon.Height == 24 &&
            source.Outputs[icon.RgbaPath].Length == 24 * 24 * 4 && SafariSource.Hash(source.Outputs[icon.RgbaPath]) == icon.RgbaSha256 &&
            SafariSource.Hash(source.Outputs[icon.PngPath]) == icon.PngSha256 &&
            icon.SourcePaths.All(source.Raw.ContainsKey), "Original Stick icon differs.");
        return new(source, map, items);
    }
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
