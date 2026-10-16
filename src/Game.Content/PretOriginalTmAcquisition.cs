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
    public JsonElement ServiceBindings { get; }
    public FiveIslandTrainerDocument MemorialTrainers { get; }
    public JsonElement MemorialEncounters { get; }
    public string GeneratedDirectory { get; }

    private PretOriginalTmAcquisition(SilphSourcePackage source, ImmutableDictionary<MapId, PretWorldMap> maps,
        ImmutableDictionary<string, int> constants)
    {
        NewMaps = maps; Constants = constants;
        GeneratedDirectory = source.Directory;
        ServiceBindings = source.Document("services.json");

        MemorialTrainers = source.Parse<FiveIslandTrainerDocument>("memorial-trainers.json");
        MemorialEncounters = source.Document("memorial-encounters.json");
        _ = PretMemorialPillarTrainers.Load(MemorialTrainers);
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



    /// <summary>Authenticate typed shared-service bindings against the actual imported event and source label inventory.</summary>
    public ImmutableArray<FieldItemServiceDefinition> ServiceDefinitions()
    {
        var results = PretFieldItemServices.Decode(ServiceBindings, NewMaps, Constants);
        Need(results.Length == 4 && results.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() == 4 &&
            results.Select(r => (r.Map, r.ScriptId)).Distinct().Count() == 4, "Original TM service identity collection differs.");
        return results;
    }

    public ImmutableArray<FieldPickupDefinition> MemorialPickups(EarlyShopCatalog shop) =>
        _pickups.Memorial.Where(p => !p.Hidden).Select(p => {
            var item = shop.Items.SingleOrDefault(i => i.Id.Value == "pret:" + p.ItemSymbol)
                ?? throw new InvalidDataException("Memorial item is not admitted: " + p.ItemSymbol);
            return BindPickup(p, item.SourceItemId, item.Name, item.Pocket);
        }).ToImmutableArray();

    public ImmutableArray<FieldPickupDefinition> MachineBallPickups(MachineCatalog machines) =>
        _pickups.Machines.Select(p => {
            var item = (MachineRules.Definition(machines, new ItemId("pret:" + p.ItemSymbol))
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

    public static PretOriginalTmAcquisition Load(PretCurrentWorld world, string? dependencyRoot = null)
    {
        var projectRoot = world.ProjectRoot;
        dependencyRoot ??= projectRoot;
        var source = new SilphSourcePackage(projectRoot,"maps/regions/original-tm-acquisition");
        var maps = world.MapsInDirectory(source.Directory).ToBuilder();
        Need(maps.Keys.ToHashSet().SetEquals([ReturnRoomMapId, PsychicHouseMapId, MemorialMapId]), "Original TM map collection differs.");
        var constants = PretSharedConstants.ForPackage(source.Directory);
        return new(source, maps.ToImmutable(), constants);
    }

    private static void Need(bool condition, string message) => SilphSourcePackage.Need(condition, message);
}
