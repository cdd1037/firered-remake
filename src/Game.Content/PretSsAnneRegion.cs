using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Ids;

namespace Game.Content;

public sealed class PretSsAnneRegion
{
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public string GeneratedDirectory { get; }
    public JsonElement ExternalVermilionEvents { get; }

    private PretSsAnneRegion(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        GeneratedDirectory = Path.Combine(project, "content/generated/maps/regions/ssanne");
        NewMaps = world.MapsInDirectory(GeneratedDirectory);
        Constants = PretSharedConstants.ForPackage(GeneratedDirectory);
        using var ExternalVermilionEventsDocument = JsonDocument.Parse(world.ReadEvents("data/maps/VermilionCity/map.json"));
        ExternalVermilionEvents = ExternalVermilionEventsDocument.RootElement.Clone();
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_SSANNE_1F_CORRIDOR" => new("map.ssanne-1f-corridor"),
        "MAP_SSANNE_1F_ROOM1" => new("map.ssanne-1f-room-1"),
        "MAP_SSANNE_1F_ROOM2" => new("map.ssanne-1f-room-2"),
        "MAP_SSANNE_1F_ROOM3" => new("map.ssanne-1f-room-3"),
        "MAP_SSANNE_1F_ROOM4" => new("map.ssanne-1f-room-4"),
        "MAP_SSANNE_1F_ROOM5" => new("map.ssanne-1f-room-5"),
        "MAP_SSANNE_1F_ROOM6" => new("map.ssanne-1f-room-6"),
        "MAP_SSANNE_1F_ROOM7" => new("map.ssanne-1f-room-7"),
        "MAP_SSANNE_2F_CORRIDOR" => new("map.ssanne-2f-corridor"),
        "MAP_SSANNE_2F_ROOM1" => new("map.ssanne-2f-room-1"),
        "MAP_SSANNE_2F_ROOM2" => new("map.ssanne-2f-room-2"),
        "MAP_SSANNE_2F_ROOM3" => new("map.ssanne-2f-room-3"),
        "MAP_SSANNE_2F_ROOM4" => new("map.ssanne-2f-room-4"),
        "MAP_SSANNE_2F_ROOM5" => new("map.ssanne-2f-room-5"),
        "MAP_SSANNE_2F_ROOM6" => new("map.ssanne-2f-room-6"),
        "MAP_SSANNE_3F_CORRIDOR" => new("map.ssanne-3f-corridor"),
        "MAP_SSANNE_B1F_CORRIDOR" => new("map.ssanne-b1f-corridor"),
        "MAP_SSANNE_B1F_ROOM1" => new("map.ssanne-b1f-room-1"),
        "MAP_SSANNE_B1F_ROOM2" => new("map.ssanne-b1f-room-2"),
        "MAP_SSANNE_B1F_ROOM3" => new("map.ssanne-b1f-room-3"),
        "MAP_SSANNE_B1F_ROOM4" => new("map.ssanne-b1f-room-4"),
        "MAP_SSANNE_B1F_ROOM5" => new("map.ssanne-b1f-room-5"),
        "MAP_SSANNE_CAPTAINS_OFFICE" => new("map.ssanne-captains-office"),
        "MAP_SSANNE_DECK" => new("map.ssanne-deck"),
        "MAP_SSANNE_EXTERIOR" => new("map.ssanne-exterior"),
        "MAP_SSANNE_KITCHEN" => new("map.ssanne-kitchen"),
        _ => PretVermilionRegion.MapIdFromSourceSymbol(symbol)
    };



    public static PretSsAnneRegion Load(PretCurrentWorld world) => new(world);
}
