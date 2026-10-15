using System.Text;
using System.Text.Json;
using Game.Domain.World;

/// <summary>Discrete source task callbacks, independent of Godot draw and authoritative rules.</summary>
internal static class SilphSourcePresentation
{
    internal const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    internal readonly record struct TeleportPose(Direction Facing, int OffsetY, bool Complete);
    internal static Direction Rotate(Direction facing) => facing switch
    { Direction.South => Direction.West, Direction.West => Direction.North, Direction.North => Direction.East, _ => Direction.South };

    // Source avatar origin is screen y80. The first task callback occurs on frame1.
    internal static TeleportPose TeleportOut(Direction initial, int frames)
    {
        if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames));
        int y = 80 * 16, dy = 1, timer = 0; bool offscreen = false; Direction facing = initial;
        for (int frame = 0; frame < frames; frame++)
        {
            if (offscreen) return new(facing, (y >> 4) - 80, true);
            if (++timer == 8) { timer = 0; facing = Rotate(facing); }
            y -= dy; dy += 3; offscreen = (y >> 4) < -32;
        }
        return new(facing, (y >> 4) - 80, false);
    }
    internal static TeleportPose TeleportIn(Direction initial, int frames)
    {
        if (frames < 0) throw new ArgumentOutOfRangeException(nameof(frames));
        int y = -32 * 16, dy = 116, timer = 0, state = 1, delay = 0; Direction facing = Rotate(initial);
        for (int frame = 0; frame < frames; frame++)
        {
            Direction returned = facing;
            if (++timer == 8) { timer = 0; facing = Rotate(facing); returned = Rotate(facing); }
            if (state == 1)
            {
                y += dy; dy = Math.Max(4, dy - 3);
                if ((y >> 4) >= 80) { y = 80 * 16; state = 2; }
            }
            else if (state == 2) { if (++delay > 8) state = 3; }
            else if (returned == initial) return new(initial, 0, true);
        }
        return new(facing, (y >> 4) - 80, false);
    }
    internal static int ElevatorShakeFrames(int from, int to) => 3 * new[] { 8, 16, 24, 32, 38, 46, 53, 56, 57 }[Math.Min(8, Math.Abs(from - to))];
    internal static int ElevatorWindowCycles(int distance) => 3 * (Math.Min(8, Math.Abs(distance)) + 1);
    internal static (int Scroll, int Row) MenuDefault(int index)
    {
        if (index is < 0 or > 10) throw new ArgumentOutOfRangeException(nameof(index));
        return index <= 4 ? (0, index) : index <= 9 ? (index - 4, 4) : (5, 5);
    }
    internal static (int Scroll, int Row) MoveMenu(int scroll, int row, bool down)
    {
        if (scroll is < 0 or > 5 || row is < 0 or > 6) throw new ArgumentOutOfRangeException(nameof(scroll));
        if (down) return scroll == 5 ? (scroll, Math.Min(6, row + 1)) : row < 4 ? (scroll, row + 1) : (scroll + 1, 4);
        return scroll == 0 ? (0, Math.Max(0, row - 1)) : row > 2 ? (scroll, row - 1) : (scroll - 1, 2);
    }
}
