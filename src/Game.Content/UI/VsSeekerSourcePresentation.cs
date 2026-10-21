namespace Game.Content;

public enum VsSeekerIcon { Unfought, Accepted, Declined }
public readonly record struct VsSeekerTextureSlice(string AssetId, int Frame, int Width, int Height);
public readonly record struct VsSeekerIdlePose(int Frame, int XOffset, int YOffset);

/// <summary>Source-local presentation only. No world changes, outcome selection, RNG or save state.</summary>
public static class VsSeekerSourcePresentation
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const int ResponseWaitTicks = 48;
    public const int AcceptedStepTicks = 4;
    public const int IconAnimationTicks = 60;
    // AnimateSprites invokes SpriteCB_TrainerIcons before AnimateSprite. ANIMCMD_END is observed
    // by the next callback, so the final image survives the end-command frame.
    public const int IconTicks = 61;
    public const int FirstSignalTaskTick = 31;
    public const int SecondSignalTaskTick = 42;
    private static readonly int[] NormalFrames = [0,1,5,6,7,8,7,8,7,8,7,8,7,8,7,8,7,8,6,1,0];
    private static readonly int[] BikeFrames = [0,1,2,3,4,5,4,5,4,5,4,5,4,5,4,5,4,5,3,2,1,0];
    // Exact fixed-point arithmetic from gSineTable, src/trig.c; no floating-point approximation.
    private static readonly int[] JumpOffsets = [-1,-2,-4,-4,-5,-5,-5,-5,-4,-3,-2,-1,0];
    private static readonly int[] SwimOffsets = [0,0,0,0,0,0,1,1,1,1,1,1,1,1,1,1,2,1,1,1,1,1,1,1,1,1,1,0,0,0,0,0,0,-1,-1,-1,-1,-1,-2,-2,-2,-2,-2,-2,-2,-2,-2,-2,-2,-2,-2,-2,-2,-2,-2,-2,-2,-2,-2,-1,-1,-1,-1,-1];

    /// <summary>84 normal/surfing, 88 bicycle ticks. This excludes outer task and movement completion ticks.</summary>
    public static int ScannerAnimationTicks(bool cycling) => cycling ? 88 : 84;

    /// <summary>Frame in the original source sheet; -1 means the animation has ended.</summary>
    public static int ScannerFrame(bool cycling, int animationTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(animationTick);
        var frames = cycling ? BikeFrames : NormalFrames;
        return animationTick / 4 < frames.Length ? frames[animationTick / 4] : -1;
    }

    /// <summary>All source scanner frames are unflipped, regardless of the player's saved facing.</summary>
    public static VsSeekerTextureSlice? ScannerSlice(bool female, bool cycling, int animationTick)
    {
        int frame = ScannerFrame(cycling, animationTick);
        if (frame < 0) return null;
        string gender = female ? "green" : "red";
        if (cycling) return new(gender + "-scanner-bike", frame, 32, 32);
        return frame < 5 ? new(gender + "-field-move", frame, 16, 32)
            : new(gender + "-scanner-extra", frame - 5, 16, 32);
    }

    public static bool HasSignalAtTaskTick(int tick) => tick is FirstSignalTaskTick or SecondSignalTaskTick;

    /// <summary>Frame in reaction-icons.png, whose columns retain source image order (!, !!, X).</summary>
    public static int IconFrame(VsSeekerIcon icon, int iconTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(iconTick);
        if (!Enum.IsDefined(icon)) throw new ArgumentOutOfRangeException(nameof(icon));
        if (iconTick >= IconTicks) return -1;
        return (icon switch { VsSeekerIcon.Unfought => 0, VsSeekerIcon.Accepted => 3, _ => 6 }) +
            (iconTick < 4 ? 0 : iconTick < 8 ? 1 : 2);
    }

    /// <summary>Additional icon y2 after each source sprite callback. Icon center is actor center -16 plus this offset.</summary>
    public static int IconYOffset(int iconTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(iconTick);
        // src/trainer_see.c: velocity -5, then +1 each callback until offset returns to zero.
        int steps = Math.Min(iconTick + 1, 11);
        return -5 * steps + steps * (steps - 1) / 2;
    }

    /// <summary>Tick 0 is the first object callback after SetTrainerMovementType. Reuse existing NPC frames 0/9;
    /// draw these poses unflipped. Frame -1 means preserve the prior image on the first initialization callback.
    /// Freeze this clock whenever that source object is frozen or script-owned.
    /// The 126/66 periods include the two original movement-type/action initialization callbacks.</summary>
    public static VsSeekerIdlePose ReadyIdlePose(string movementType, long idleTick)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(idleTick);
        switch (movementType)
        {
            case "MOVEMENT_TYPE_RAISE_HAND_AND_STOP": return new(9, 0, 0);
            case "MOVEMENT_TYPE_RAISE_HAND_AND_JUMP":
                int tick = (int)(idleTick % 126);
                if (tick == 0) return new(idleTick == 0 ? -1 : 0, 0, 0);
                if (tick == 1 || tick == 31) return new(9, 0, 0);
                if (tick is >= 2 and <= 14) return new(tick == 14 ? 0 : 9, 0, JumpOffsets[tick - 2]);
                if (tick is >= 32 and <= 44) return new(tick == 44 ? 0 : 9, 0, JumpOffsets[tick - 32]);
                return new(0, 0, 0);
            case "MOVEMENT_TYPE_RAISE_HAND_AND_SWIM":
                int swimTick = (int)(idleTick % 66);
                if (idleTick == 0) return new(-1, 0, 0);
                return new(9, swimTick < 2 ? 0 : SwimOffsets[(swimTick - 1) % 64], 0);
            default: throw new ArgumentException("Unknown source rematch idle movement.", nameof(movementType));
        }
    }
}
