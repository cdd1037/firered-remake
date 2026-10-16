namespace Game.Domain.World;

/// <summary>Global FireRed metatile semantics, independent of map admission and travel effects.</summary>
public static class FieldWaterRules
{
    // metatile_behavior.c sBehaviorSurfable. Waterfall is surfable here; individual
    // actions such as CanFish reject it explicitly. IsBridge is always false in FireRed.
    public static bool IsSurfableBehavior(int behavior) => behavior is
        >= 0x10 and <= 0x13 or 0x15 or 0x1a or 0x1b or >= 0x50 and <= 0x53;
}
