using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

public sealed record NpcTradeHeldItemsDocument(ImmutableArray<EarlyShopItemDefinition> Additions,
    ImmutableArray<NpcTradeItemIcon> Icons);
public sealed record NpcTradeItemsDocument(int PreviousItemCount, int ItemCount,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<EarlyShopStock> ShopsAdded,
    ImmutableArray<NpcTradeItemIcon> Icons);
public sealed record NpcTradeItemIcon(ItemId Item, int Width, int Height, string TexturePath,
    JsonElement BindingSource, ImageRegion? Region = null);

/// <summary>Original Vermilion trade room and held Stick; activation belongs to current composition.</summary>
public sealed class PretNpcTradeElyssa
{
    public const string ResourceId = "firered-npc-trade-elyssa-v1";
    public static MapId HouseMapId { get; } = new("map.vermilion-city-house2");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public NpcTradeHeldItemsDocument Items { get; }
    public ImmutableDictionary<string, byte[]> IconFiles { get; }
    public string GeneratedDirectory { get; }

    private PretNpcTradeElyssa(SilphSourcePackage source, PretWorldMap map, NpcTradeHeldItemsDocument items)
    {
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(map.Id, map);
        GeneratedDirectory = source.Directory; Items = items;

        IconFiles = items.Icons.Select(i => i.TexturePath)
            .Distinct(StringComparer.Ordinal).ToImmutableDictionary(p => p, p => source.Read(p), StringComparer.Ordinal);
    }
    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_VERMILION_CITY_HOUSE2" => HouseMapId,
        "MAP_VERMILION_CITY" => new("map.vermilion-city"),
        _ => throw new InvalidDataException("Unknown original Elyssa room destination: " + symbol)
    };


    public static PretNpcTradeElyssa Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/npc-trade-elyssa");
        var map = world.Maps[HouseMapId];

        Need(map.FieldWarps.Length == 3 && map.Events.GetProperty("object_events").GetArrayLength() == 1 &&
            map.FieldWarps.All(w => w.DestinationMapId == new MapId("map.vermilion-city") && w.DestinationWarpId == 6),
            "Original Elyssa actor/exit cohort differs.");
        var items = source.Parse<NpcTradeHeldItemsDocument>("items.json");
        Need(            items.Additions.Length == 1 &&
            items.Additions[0].Id == ItemIds.StickId &&
            items.Icons.Length == 1, "Elyssa Stick inventory scope differs.");
        Need(items.Additions[0].Item is { HoldEffect: "HOLD_EFFECT_STICK", CanGive: true, CanSell: true, CanToss: true },
            "Original Stick held metadata differs.");
        var icon = items.Icons[0];
        Need(icon.Item == ItemIds.StickId && icon.Width == 24 && icon.Height == 24 &&
            (source.Read(icon.TexturePath)).Length > 0, "Original Stick icon differs.");
        return new(source, map, items);
    }
    private static void Need(bool value, string message) => SilphSourcePackage.Need(value, message);
}
