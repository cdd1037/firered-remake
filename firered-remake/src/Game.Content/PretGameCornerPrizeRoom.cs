using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>One original room from the already authenticated regional package; no campaign or exchange admission.</summary>
public sealed class PretGameCornerPrizeRoom
{
    public static readonly MapId MapId = new("map.celadon-city-game-corner-prize-room");
    public const string ScriptPath = "data/maps/CeladonCity_GameCorner_PrizeRoom/scripts.inc";
    public const string ContentIdentity = "game-corner-prize-room:original-map-firered-source-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public PretWorldMap Map => NewMaps[MapId];

    private PretGameCornerPrizeRoom(PretWorldMap map) =>
        NewMaps = ImmutableDictionary<MapId, PretWorldMap>.Empty.Add(MapId, map);

    public static PretGameCornerPrizeRoom Load(PretRegionalServiceMaps regional,
        IReadOnlyDictionary<MapId, PretWorldMap> existing)
    {
        ArgumentNullException.ThrowIfNull(regional);
        ArgumentNullException.ThrowIfNull(existing);
        if (existing.ContainsKey(MapId) || !existing.TryGetValue(new("map.celadon-city"), out var city) ||
            !regional.NewMaps.TryGetValue(MapId, out var map))
            throw new InvalidDataException("game_corner_prize_room_predecessor");
        if (map.Id != MapId || map.SourceRef.Path != "data/maps/CeladonCity_GameCorner_PrizeRoom/map.json" ||
            city.SourceRef.Path != "data/maps/CeladonCity/map.json" ||
            map.Width != 9 || map.Height != 10 || map.Events.GetProperty("object_events").GetArrayLength() != 5 ||
            map.Events.GetProperty("connections").ValueKind != System.Text.Json.JsonValueKind.Null)
            throw new InvalidDataException("game_corner_prize_room_source");
        var door = city.FieldWarps.Single(w => w.Index == 5);
        if (door.X != 39 || door.Y != 20 || door.DestinationMapId != MapId || door.DestinationWarpId != 1 ||
            !map.FieldWarps.Select(w => (w.Index, w.X, w.Y, w.Elevation, w.DestinationWarpId))
                .SequenceEqual([(0, 3, 9, 0, 5), (1, 4, 8, 3, 5), (2, 5, 9, 0, 5)]) ||
            map.FieldWarps.Any(w => w.DestinationMapId != city.Id))
            throw new InvalidDataException("game_corner_prize_room_original_doors");
        return new(map);
    }

    internal static bool IsOriginalScript(PretSourceScriptFile file) =>
        file.Path == ScriptPath;
}
