using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated Celadon Gym source map, trainers, Cut trees and badge/TM19 script contracts.</summary>
public sealed class PretCeladonGymRegion
{
    public const string Profile = "firered-celadon-gym-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }

    public JsonElement ExternalCeladonEvents { get; }
    public string GeneratedDirectory { get; }

    private PretCeladonGymRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        JsonElement externalCeladonEvents)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants;
         ExternalCeladonEvents = externalCeladonEvents;
    }

    public static PretCeladonGymRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/celadon-gym");

        var maps = world.MapsInDirectory(directory);

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_TEMP_12"] == 18 && constants["FLAG_TEMP_13"] == 19 && constants["FLAG_TEMP_14"] == 20 &&
            constants["FLAG_BADGE04_GET"] == 0x823 && constants["FLAG_DEFEATED_ERIKA"] == 0x4B3 &&
            constants["FLAG_GOT_TM19_FROM_ERIKA"] == 0x293 && constants["ITEM_TM19"] == 307,
            "Celadon Gym badge/Cut/reward constants differ.");
        using var cityEvents = JsonDocument.Parse(world.ReadEvents("data/maps/CeladonCity/map.json"));
        var gymEvents = maps[new MapId("map.celadon-city-gym")].Events;
        var cityEntry = cityEvents.RootElement.GetProperty("warp_events")[6];
        Require(cityEntry.GetProperty("dest_map").GetString() == "MAP_CELADON_CITY_GYM" && cityEntry.GetProperty("dest_warp_id").GetString() == "1" &&
            gymEvents.GetProperty("warp_events")[1].GetProperty("dest_map").GetString() == "MAP_CELADON_CITY" &&
            gymEvents.GetProperty("warp_events")[1].GetProperty("dest_warp_id").GetString() == "6",
            "Celadon Gym city entry differs from original source.");
        return new(directory, maps, constants.ToImmutable(), cityEvents.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
}
