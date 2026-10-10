using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record PretWorldMapSpecification(string StableId, string SourceName, string SourceMapSymbol, string Directory);
public sealed record PretWorldWarp(int Index, int X, int Y, int Elevation, MapId DestinationMapId, int DestinationWarpId);

/// <summary>A closed, source-verified collection. Warp indices are original zero-based map.json array indices.</summary>
public static class PretWorldCatalog
{
    public static ImmutableArray<PretWorldMapSpecification> Specifications { get; } =
    [
        new("map.pallet-town", "PalletTown", "MAP_PALLET_TOWN", "pallet"),
        new("map.players-house-1f", "PalletTown_PlayersHouse_1F", "MAP_PALLET_TOWN_PLAYERS_HOUSE_1F", "players-house-1f"),
        new("map.players-house-2f", "PalletTown_PlayersHouse_2F", "MAP_PALLET_TOWN_PLAYERS_HOUSE_2F", "players-house-2f"),
        new("map.rivals-house", "PalletTown_RivalsHouse", "MAP_PALLET_TOWN_RIVALS_HOUSE", "rivals-house"),
        new("map.professor-oaks-lab", "PalletTown_ProfessorOaksLab", "MAP_PALLET_TOWN_PROFESSOR_OAKS_LAB", "professor-oaks-lab")
    ];
    internal static string TilesetBase(string symbol) => "data/tilesets/" + (symbol switch
    {
        "gTileset_General" => "primary/general", "gTileset_Building" => "primary/building",
        "gTileset_PalletTown" => "secondary/pallet_town", "gTileset_GenericBuilding1" => "secondary/generic_building_1",
        "gTileset_GenericBuilding2" => "secondary/generic_building_2", "gTileset_Lab" => "secondary/lab",
        _ => throw new InvalidDataException("Tileset is outside bounded source collection.")
    });
    // These are reviewed deterministic render outputs, separate from the upstream source hashes.
    // Updating them requires original-byte/render-oracle tests, never accepting the loaded document's own hash.
    internal static string ArtifactSetHash(string stableId) => stableId switch
    {
        "map.pallet-town" => "c9c74100dae4912cc6c82a12a259e45dfa98c698022ac56b6799ff6c75bcbfec",
        "map.players-house-1f" => "6db99902380adc44d702080b09a71da0aea9eab52b1ead8e28a7c71da71bea5c",
        "map.players-house-2f" => "914a918037eaf8ccdf2c93f21fb9096a82091a01de0afa0dfff799de8105ed19",
        "map.rivals-house" => "cfa668fe152056744859db8611de9c48276a951808bace4039db8e7d9b8ca82c",
        "map.professor-oaks-lab" => "a0d939a3cc8e47447e11cc3eb489f2649e697e889d98bb7b7e4948406c9d429a",
        _ => throw new InvalidDataException("Map is outside bounded source collection.")
    };
    public static ImmutableDictionary<MapId, PretWorldMap> Load(string collectionRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var maps = Specifications.Select(s => runtimeMaps is null ? PretWorldMap.Load(Path.Combine(collectionRoot, s.Directory)) : PretRuntimeMapIntegration.LoadBase(runtimeMaps,s,collectionRoot)).ToImmutableDictionary(m => m.Id);
        foreach (var map in maps.Values)
        foreach (var warp in map.Warps)
        {
            if (!maps.TryGetValue(warp.DestinationMapId, out var target) || warp.DestinationWarpId >= target.Warps.Length)
                throw new InvalidDataException("Warp destination map/index is missing.");
            var arrival = target.Warps[warp.DestinationWarpId];
            if (arrival.DestinationMapId != map.Id)
                throw new InvalidDataException("Warp destination does not return to originating source map.");
            // Do not require every event coordinate to be an ordinary floor: the source contains
            // collision-bearing doors, directional stairs and auxiliary edge warp entries.
            if (target.GetSourceCell(arrival.X, arrival.Y) is null)
                throw new InvalidDataException("Warp destination is outside source layout.");
        }
        return maps;
    }
}
