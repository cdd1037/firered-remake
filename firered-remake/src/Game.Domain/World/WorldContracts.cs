using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.World;

public enum Direction { North, East, South, West }
public enum TileBehavior { Floor, Grass, Ledge, Warp, Unsupported, Sign, ArrowWarp, StairWarp }
public sealed record TileCell(bool Blocked, int Elevation = 0, TileBehavior Behavior = TileBehavior.Floor);
public sealed record TilePosition(MapId MapId, int X, int Y, int Elevation, Direction Facing);
public interface IWorldMap
{
    MapId Id { get; }
    int Width { get; }
    int Height { get; }
    TileCell? GetTile(int x, int y);
}
public sealed record WorldSnapshot(string RulesVersion, long Revision, long StepCount, TilePosition Player, RngState Rng);
public sealed record MoveIntent(CommandId CommandId, long ExpectedRevision, Direction Direction);
public enum WorldEventKind { Moved, Blocked }
public sealed record WorldEvent(CommandId CausedByCommandId, WorldEventKind Kind, TilePosition Before, TilePosition After, string Reason);
public sealed record WorldTransition(TransitionStatus Status, WorldSnapshot State, ImmutableArray<WorldEvent> Events, Diagnostic? Diagnostic);

public sealed class GridMap : IWorldMap
{
    public MapId Id { get; }
    public int Width { get; }
    public int Height { get; }
    public ImmutableArray<TileCell> Cells { get; }
    public GridMap(MapId id, int width, int height, ImmutableArray<TileCell> cells)
    {
        if (!StableId.IsValid(id.Value)) throw new ArgumentException("Invalid map ID.", nameof(id));
        if (width <= 0 || height <= 0 || cells.IsDefault || (long)width * height != cells.Length || cells.Any(c => c is null))
            throw new ArgumentException("Grid dimensions must match a non-null immutable cell array.");
        Id = id; Width = width; Height = height; Cells = cells;
    }
    public TileCell? GetTile(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? null : Cells[y * Width + x];
}
