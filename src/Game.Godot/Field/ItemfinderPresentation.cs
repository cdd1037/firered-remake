using Game.Domain.World;

/// <summary>Read-only projection of itemfinder.c's 25-frame pulses and 8.8 sprite motion.</summary>
public static class ItemfinderPresentation
{
    public const int PulseFrames = 25;
    public static int MessageFrame(ItemfinderResponse response) => response.Dings * PulseFrames;

    public static ItemfinderSprite? Sprite(ItemfinderResponse response, Direction facing, int tick, int pulse)
    {
        if (response.Target is null || pulse < 0 || pulse >= response.Dings) return null;
        int age = tick - pulse * PulseFrames;
        if (age < 0) return null;
        bool star = response.Target.Underfoot;
        var direction = star ? Direction.North : response.Direction ?? facing;
        int dx = direction == Direction.West ? -100 : direction == Direction.East ? 100 : 0;
        int dy = direction == Direction.North ? -100 : direction == Direction.South ? 100 : 0;
        // AnimateSprites runs after the task creates the sprite. Signed right shift is
        // important: the first negative displacement is -1, not truncation toward zero.
        int steps = age + 1;
        int x = 120 + ((dx * steps) >> 8), y = 76 + ((dy * steps) >> 8);
        // The boundary-crossing frame is drawn; the following callback destroys it.
        int previousX = 120 + ((dx * age) >> 8), previousY = 76 + ((dy * age) >> 8);
        if (previousX <= 104 || previousX > 132 || previousY <= 60 || previousY > 88) return null;
        int rotation = star ? 0 : direction switch
        {
            Direction.North => 90, Direction.East => 180, Direction.South => -90, _ => 0
        };
        return new(star ? 4 : pulse, x, y, rotation);
    }
}

public readonly record struct ItemfinderSprite(int Frame, int X, int Y, int RotationDegrees);
