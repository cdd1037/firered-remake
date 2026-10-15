using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Content;

public sealed record OriginalTmPickup(string MapSymbol, bool Hidden, int? SourceLocalId, int X, int Y,
    int Elevation, string ScriptId, string ItemSymbol, int Quantity, string Flag);
public sealed record OriginalTmPickups(ImmutableArray<OriginalTmPickup> Memorial, ImmutableArray<OriginalTmPickup> Machines);

/// <summary>Original TM27/TM29/TM42 maps and unchanged TM18/TM48 map anchors. Loading does not admit gameplay.</summary>
public sealed class PretOriginalTmAcquisition
{
    public const string ResourceId = "firered-original-tm-acquisition-v1";
    public static MapId ReturnRoomMapId { get; } = new("map.route-12-north-entrance-2f");
    public static MapId PsychicHouseMapId { get; } = new("map.saffron-city-mr-psychics-house");
    public static MapId MemorialMapId { get; } = new("map.five-island-memorial-pillar");
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    private readonly OriginalTmPickups _pickups;
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement Catalog { get; }
    public JsonElement Contracts { get; }
    public JsonElement ServiceBindings => Contracts.GetProperty("serviceBindings");
    public FiveIslandTrainerDocument MemorialTrainers { get; }
    public JsonElement MemorialEncounters { get; }
    public JsonElement NpcSources { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");

    private PretOriginalTmAcquisition(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory; Catalog = source.Document("catalog.json");
        Contracts = source.Document("contracts.json");
        NpcSources = source.Document("npc-source.json");
        MemorialTrainers = source.Parse<FiveIslandTrainerDocument>("memorial-trainers.json");
        MemorialEncounters = source.Document("memorial-encounters.json");
        Need(            !MemorialTrainers.CompleteSourceRoster && MemorialTrainers.Trainers.Length == 3 && MemorialTrainers.Battles.Length == 3 &&
            MemorialTrainers.Trainers.Sum(t => t.Party.Length) == 6 && MemorialTrainers.Trainers.All(t => !t.DoubleBattle && t.PartyFlags == 0),
            "Original Memorial trainer source scope differs.");
        _pickups = ContentJson.ParseStrict<OriginalTmPickups>(File.ReadAllText(Path.Combine(source.Directory, "pickups.json")), ContentJson.CreateOptions());
        Need(_pickups.Memorial.Length == 5 && _pickups.Memorial.Count(p => p.Hidden) == 4 && _pickups.Machines.Length == 2,
            "Original TM pickup collection differs.");
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_ROUTE12_NORTH_ENTRANCE_2F" => ReturnRoomMapId,
        "MAP_SAFFRON_CITY_MR_PSYCHICS_HOUSE" => PsychicHouseMapId,
        "MAP_FIVE_ISLAND_MEMORIAL_PILLAR" => MemorialMapId,
        "MAP_ROUTE12_NORTH_ENTRANCE_1F" => new("map.route-12-north-entrance-1f"),
        "MAP_SAFFRON_CITY" => new("map.saffron-city"),
        "MAP_FIVE_ISLAND_MEADOW" => new("map.five-island-meadow"),
        "MAP_ROUTE12" => new("map.route-12"),
        "MAP_ROUTE15" => new("map.route-15"),
        _ => throw new InvalidDataException("Unknown original TM acquisition map symbol: " + symbol)
    };

    public static ConnectedFieldMap ConnectedMap(PretWorldMap map)
    {
        Need(map.Id == ReturnRoomMapId || map.Id == PsychicHouseMapId || map.Id == MemorialMapId,
            "Unexpected original TM map adapter.");
        var connections = map.Events.GetProperty("connections");
        var edges = connections.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty :
            connections.EnumerateArray().Select(c => new ConnectedFieldConnection(
                c.GetProperty("direction").GetString() switch {
                    "left" => Direction.West, "right" => Direction.East, "up" => Direction.North, "down" => Direction.South,
                    _ => throw new InvalidDataException("Unknown original connection direction.") },
                c.GetProperty("offset").GetInt32(), MapIdFromSourceSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height,
            map.Cells.Select(c => new ConnectedFieldCell(c.BehaviorId, c.Collision, c.Elevation)).ToImmutableArray(), edges);
    }

    /// <summary>Append exactly three source instances; never replace existing Route12/15 or entrance geometry.</summary>
    public ImmutableDictionary<MapId, PretWorldMap> MountMaps(ImmutableDictionary<MapId, PretWorldMap> existing)
    {
        ValidateRetainedMaps(existing);
        Need(!NewMaps.Keys.Any(existing.ContainsKey), "Original TM acquisition maps were already mounted.");
        return existing.AddRange(NewMaps);
    }

    public void ValidateRetainedMaps(IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        foreach (var row in Contracts.GetProperty("retainedMaps").EnumerateArray())
            Need(existing.ContainsKey(new MapId(row.GetProperty("mapId").GetString()!)), "Original TM retained map is missing.");
    }

    /// <summary>Authenticate typed shared-service bindings against the actual imported event and source label inventory.</summary>
    public ImmutableArray<FieldItemServiceDefinition> ServiceDefinitions()
    {
        var results = ImmutableArray.CreateBuilder<FieldItemServiceDefinition>();
        foreach (var binding in ServiceBindings.EnumerateArray())
        {
            var mapId = new MapId(binding.GetProperty("mapId").GetString()!);
            string id = binding.GetProperty("id").GetString()!, script = binding.GetProperty("scriptId").GetString()!;
            string group = binding.GetProperty("eventGroup").GetString()!;
            int index = binding.GetProperty("eventIndex").GetInt32();
            Need(NewMaps.TryGetValue(mapId, out var map) && group is "object_events" or "bg_events", "Original TM service map/group differs.");
            var events = map!.Events.GetProperty(group);
            Need(index >= 0 && index < events.GetArrayLength(), "Original TM service event index differs.");
            var e = events[index];
            Need(JsonElement.DeepEquals(e, binding.GetProperty("sourceEvent")) && e.GetProperty("script").GetString() == script,
                "Original TM service event binding differs.");
            int? Optional(string name) => binding.GetProperty(name).ValueKind == JsonValueKind.Null ? null : binding.GetProperty(name).GetInt32();
            int? local = Optional("sourceLocalId"), x = Optional("backgroundX"), y = Optional("backgroundY");
            Need(group == "object_events" ? local == index + 1 && x is null && y is null :
                local is null && x == e.GetProperty("x").GetInt32() && y == e.GetProperty("y").GetInt32(), "Original TM service interaction position differs.");
            var programs = binding.GetProperty("programIds").EnumerateArray().Select(p => p.GetString()!).ToImmutableArray();
            var items = binding.GetProperty("itemSymbols").EnumerateArray().Select(p => p.GetString()!).ToImmutableArray();
            Need(programs.Contains(script) && programs.Distinct(StringComparer.Ordinal).Count() == programs.Length &&
                !items.IsEmpty && items.Distinct(StringComparer.Ordinal).Count() == items.Length && items.All(Constants.ContainsKey),
                "Original TM service call graph or item scope differs.");
            results.Add(new(id, mapId, script, local, x, y, programs, items));
        }
        Need(results.Count == 4 && results.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() == 4 &&
            results.Select(r => (r.Map, r.ScriptId)).Distinct().Count() == 4, "Original TM service identity collection differs.");
        return results.ToImmutable();
    }

    public ImmutableArray<FieldPickupDefinition> MemorialPickups(EarlyShopCatalog shop) =>
        _pickups.Memorial.Select(p => {
            var item = shop.Items.SingleOrDefault(i => i.Id.Value == "pret:" + p.ItemSymbol)
                ?? throw new InvalidDataException("Memorial item is not admitted: " + p.ItemSymbol);
            return BindPickup(p, item.SourceItemId, item.Name, item.Pocket);
        }).ToImmutableArray();

    public ImmutableArray<FieldPickupDefinition> MachineBallPickups(PewterMachineCatalog machines) =>
        _pickups.Machines.Select(p => {
            var item = (PewterMachineRules.Definition(machines, new ItemId("pret:" + p.ItemSymbol))
                ?? throw new InvalidDataException("Required original machine is not admitted: " + p.ItemSymbol)).Machine.Item;
            Need(item.Pocket == "POCKET_TM_CASE", "Original machine pocket differs.");
            return BindPickup(p, item.SourceItemId, item.Name, item.Pocket);
        }).ToImmutableArray();

    private FieldPickupDefinition BindPickup(OriginalTmPickup pickup, int sourceItemId, string name, string pocket)
    {
        Need(Constants[pickup.ItemSymbol] == sourceItemId && pickup.Quantity > 0 &&
            pickup.X >= 0 && pickup.Y >= 0 && pickup.Hidden == (pickup.SourceLocalId is null),
            "Original TM pickup data differs.");
        return new("original-tm-acquisition:" + pickup.Flag, pickup.MapSymbol, pickup.Hidden, pickup.SourceLocalId,
            pickup.X, pickup.Y, pickup.Elevation, pickup.ScriptId, pickup.ItemSymbol, sourceItemId,
            new ItemId("pret:" + pickup.ItemSymbol), name, pocket, pickup.Quantity, pickup.Flag, Constants[pickup.Flag]);
    }

    public static PretOriginalTmAcquisition Load(string projectRoot, string? dependencyRoot = null)
    {
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"original-tm-acquisition");
        var catalog = source.Document("catalog.json");
        Need(catalog.GetProperty("resourceId").GetString() == ResourceId && catalog.GetProperty("maps").GetArrayLength() == 3 , "Original TM catalog scope differs.");
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; redistribution rights are not inferred.",
            "Original TM acquisition maps; shared gameplay admission is separate.", ResourceId, []);
        var maps = ImmutableDictionary.CreateBuilder<MapId, PretWorldMap>();
        foreach (var spec in catalog.GetProperty("maps").EnumerateArray())
        {
            string id = spec.GetProperty("stableId").GetString()!, relative = spec.GetProperty("directory").GetString()!;
            string path = spec.GetProperty("path").GetString()!;
            Need(path == relative + "/runtime-map.json" ,
                "Original TM compact map binding differs.");
            var runtime = PretRuntimeMapData.Decode(source.Read(path), id, MapIdFromSourceSymbol);
            Need(MapIdFromSourceSymbol(runtime.SourceRef.Symbol).Value == id, "Original TM map symbol differs.");
            var map = PretWorldMap.FromRuntime(runtime, metadata, PretRuntimeMapValidation.SafeFile(source.Directory, relative),
                PretRuntimeMapValidation.SafeFile(dependencyRoot, "content/generated/shared-overworld/red-normal.png"), MapIdFromSourceSymbol);
            maps.Add(map.Id, map);
        }
        Need(maps.Keys.ToHashSet().SetEquals([ReturnRoomMapId, PsychicHouseMapId, MemorialMapId]), "Original TM map collection differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps.ToImmutable(), constants);
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
