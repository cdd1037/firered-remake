using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Encounters;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Route17 and the north gate upstairs extend the source map catalog; existing map owners remain unchanged.</summary>
public sealed class PretCyclingRoadRegion
{
    public static MapId Route17MapId { get; } = new("map.route-17");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public CyclingRoadTrainerCatalog Trainers { get; }
    public ImmutableArray<EarlyLandEncounterTable> LandEncounters { get; }
    public string NpcSourcePath { get; }
    private readonly ImmutableDictionary<string, int> _constants;

    private PretCyclingRoadRegion(ImmutableDictionary<MapId, PretWorldMap> maps, CyclingRoadTrainerCatalog trainers, string directory,
        ImmutableDictionary<string, int> constants, ImmutableArray<EarlyLandEncounterTable> landEncounters)
    {
        NewMaps = maps;
        Trainers = trainers;
        LandEncounters = landEncounters;
        NpcSourcePath = Path.Combine(directory, "npc-source.json");
        _constants = constants;
    }

    public static bool IsRoadOrGate(MapId map) =>
        map.Value is "map.route-16" or "map.route-17" or "map.route-18" or
            "map.route-16-north-entrance-1f" or "map.route-18-east-entrance-1f";

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE16" => new("map.route-16"),
        "MAP_ROUTE17" => Route17MapId,
        "MAP_ROUTE18" => new("map.route-18"),
        _ => throw new InvalidDataException("cycling_road_map_symbol:" + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map) => new(map.Id, map.Width, map.Height,
        map.CreateConnectedCells(), map.Events.GetProperty("connections").ValueKind == JsonValueKind.Null ? [] : map.Events.GetProperty("connections").EnumerateArray().Select(c =>
            new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Direction.North,
                "down" => Direction.South,
                _ => throw new InvalidDataException("cycling_road_connection_direction")
            }, c.GetProperty("offset").GetInt32(),
                MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray());

    public static PretCyclingRoadRegion Load(string root)
    {
        var source = new SilphSourcePackage(root, "maps/regions/cycling-road");
        var catalog = source.Document("catalog.json");
        var symbols = catalog.GetProperty("mapSymbols").EnumerateObject()
            .ToImmutableDictionary(p => p.Name, p => new MapId(p.Value.GetString()!), StringComparer.Ordinal);
        MapId Resolve(string symbol) => symbols.TryGetValue(symbol, out var id) ? id :
            throw new InvalidDataException("cycling_road_map_symbol:" + symbol);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,
            "Local source evidence; redistribution rights are not inferred.", "Original Cycling Road maps and gate interiors.",
            "firered-cycling-road-v1");
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var entry in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = entry.GetProperty("stableId").GetString()!;
            string relative = entry.GetProperty("mapPath").GetString()!;
            var runtime = PretRuntimeMapData.Decode(source.Read(relative), id, Resolve);
            var map = PretWorldMap.FromRuntime(runtime, metadata,
                PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretOverworldTextures.Resolve(source.Directory, "sprites/overworld/red-normal.png"), Resolve);
            if (Resolve(runtime.SourceMapSymbol) != map.Id)
                throw new InvalidDataException("cycling_road_map_identity");
            maps.Add(map.Id, map);
        }
        if (maps.Count == 0) throw new InvalidDataException("cycling_road_maps_empty");
        var doc = JsonSerializer.Deserialize<FiveIslandTrainerDocument>(source.Read("trainers.json"),
            SharedTrainerCatalog.Options(root)) ?? throw new InvalidDataException("cycling_road_trainers_missing");
        var trainers = new CyclingRoadTrainerCatalog(doc.Trainers.ToImmutableDictionary(t => t.Id),
            doc.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        CyclingRoadTrainerPartyFactory.ValidateCatalog(trainers);
        var land = ContentJson.ParseStrict<ImmutableArray<EarlyLandEncounterTable>>(
            System.Text.Encoding.UTF8.GetString(source.Read("land-encounters.json")), ContentJson.CreateOptions());
        if (land.IsDefaultOrEmpty ||
            !land.Select(t => t.Map.Value).ToHashSet().SetEquals(["map.route-17", "map.route-18"]))
            throw new InvalidDataException("cycling_road_land_maps");
        foreach (var table in land) EarlyLandEncounterRules.ValidateTableDefinition(table);
        return new(maps.ToImmutable(), trainers, source.Directory, PretSharedConstants.ForPackage(source.Directory), land);
    }

    public ImmutableArray<FieldPickupDefinition> HiddenPickups(EarlyShopCatalog shop) =>
        NewMaps[Route17MapId].Events.GetProperty("bg_events").EnumerateArray()
            .Where(e => e.GetProperty("type").GetString() == "hidden_item").Select(e =>
            {
                string symbol = e.GetProperty("item").GetString()!;
                string flag = e.GetProperty("flag").GetString()!;
                var item = shop.Items.Single(i => i.Id.Value == "pret:" + symbol);
                return new FieldPickupDefinition("cycling-road:" + flag, "MAP_ROUTE17", true, null,
                    e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
                    "EventScript_HiddenItemScript", symbol, item.SourceItemId, item.Id, item.Name, item.Pocket,
                    e.GetProperty("quantity").GetInt32(), flag, _constants[flag]);
            }).ToImmutableArray();
}
