using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.World;

namespace Game.Content;

/// <summary>Project final source geometry using the world's single symbol registry.</summary>
public static class PretWorldMapConnections
{
    public static ConnectedFieldMap Create(PretWorldMap map)
    {
        var source = map.Events.GetProperty("connections");
        var connections = source.ValueKind == JsonValueKind.Null ? ImmutableArray<ConnectedFieldConnection>.Empty :
            source.EnumerateArray().Select(c => new ConnectedFieldConnection(c.GetProperty("direction").GetString() switch
            {
                "up" => Direction.North, "down" => Direction.South,
                "left" => Direction.West, "right" => Direction.East,
                _ => throw new InvalidDataException("Unknown world connection direction.")
            }, c.GetProperty("offset").GetInt32(), map.ResolveMapSymbol(c.GetProperty("map").GetString()!))).ToImmutableArray();
        return new(map.Id, map.Width, map.Height, map.CreateConnectedCells(), connections);
    }
}
