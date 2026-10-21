using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>Authenticated Fuchsia Gym source map, trainers, invisible walls and badge/TM06 script contracts.</summary>
public sealed class PretFuchsiaGymRegion
{
    public const string Profile = "firered-fuchsia-gym-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }

    public JsonElement ExternalFuchsiaEvents { get; }
    public string GeneratedDirectory { get; }

    private PretFuchsiaGymRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        JsonElement externalFuchsiaEvents)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants;
         ExternalFuchsiaEvents = externalFuchsiaEvents;
    }

    public static PretFuchsiaGymRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/fuchsia-gym");

        var maps = world.MapsInDirectory(directory);

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_BADGE05_GET"] == 0x824 && constants["FLAG_DEFEATED_KOGA"] == 0x4B4 &&
            constants["FLAG_GOT_TM06_FROM_KOGA"] == 0x259 && constants["ITEM_TM06"] == 294,
            "Fuchsia Gym badge/reward constants differ.");
        using var cityEvents = JsonDocument.Parse(world.ReadEvents("data/maps/FuchsiaCity/map.json"));
        var gymEvents = maps[new MapId("map.fuchsia-city-gym")].Events;
        var cityEntry = cityEvents.RootElement.GetProperty("warp_events")[4];
        Require(cityEntry.GetProperty("dest_map").GetString() == "MAP_FUCHSIA_CITY_GYM" && cityEntry.GetProperty("dest_warp_id").GetString() == "1" &&
            gymEvents.GetProperty("warp_events")[1].GetProperty("dest_map").GetString() == "MAP_FUCHSIA_CITY" &&
            gymEvents.GetProperty("warp_events")[1].GetProperty("dest_warp_id").GetString() == "4",
            "Fuchsia Gym city entry differs from original source.");
        return new(directory, maps, constants.ToImmutable(), cityEvents.RootElement.Clone());
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
}
