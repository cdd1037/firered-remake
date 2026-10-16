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

    private PretCyclingRoadRegion(ImmutableDictionary<MapId, PretWorldMap> maps, CyclingRoadTrainerCatalog trainers, string directory,
        ImmutableArray<EarlyLandEncounterTable> landEncounters)
    {
        NewMaps = maps;
        Trainers = trainers;
        LandEncounters = landEncounters;
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



    public static PretCyclingRoadRegion Load(PretCurrentWorld world)
    {
        var root = world.ProjectRoot;
        var source = new SilphSourcePackage(root, "maps/regions/cycling-road");
        var maps = world.MapsInDirectory(source.Directory);
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
        return new(maps, trainers, source.Directory, land);
    }

}
