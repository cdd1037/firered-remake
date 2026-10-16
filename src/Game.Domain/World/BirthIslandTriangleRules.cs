using System.Collections.Immutable;

namespace Game.Domain.World;

public sealed record BirthIslandTriangleState(int InteractionNumber, int Steps, bool Awakened);
public sealed record BirthIslandTriangleInteraction(BirthIslandTriangleState State, int Result, int X, int Y, int MovementTicks, string? Sound);

/// <summary>Literal field_specials.c triangle state machine; no RNG or invented route history.</summary>
public static class BirthIslandTriangleRules
{
    public const string Map = "map.birth-island-exterior";
    public const string InteractionVariable = "VAR_DEOXYS_INTERACTION_NUM";
    public const string StepVariable = "VAR_DEOXYS_INTERACTION_STEP_COUNTER";
    public const string AwakenedFlag = "FLAG_SYS_DEOXYS_AWAKENED";
    public const string RockActor = "LOCALID_BIRTH_ISLAND_EXTERIOR_ROCK";
    public const string DeoxysActor = "LOCALID_BIRTH_ISLAND_DEOXYS";
    public const string RockHideFlag = "FLAG_HIDE_BIRTH_ISLAND_METEORITE";
    public const int PaletteSlot = 10;
    public static readonly ImmutableArray<(int X, int Y)> Positions =
        [(15,12),(11,14),(15,8),(19,14),(12,11),(18,11),(15,14),(11,14),(19,14),(15,15),(15,10)];
    public static readonly ImmutableArray<int> StepCaps = [4,8,8,8,4,4,4,6,3,3];
    // Exact JASC source palettes converted by the original RGB5 channel truncation.
    public static readonly ImmutableArray<ImmutableArray<ushort>> Palettes =
    [
        [0x626d, 0x1084, 0x294a, 0x4631, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000],
        [0x626d, 0x1085, 0x294a, 0x4631, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000],
        [0x0000, 0x1086, 0x294b, 0x4652, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000],
        [0x0000, 0x1088, 0x294e, 0x4653, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000],
        [0x0000, 0x1089, 0x294f, 0x4674, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000],
        [0x0000, 0x108c, 0x2951, 0x4675, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000],
        [0x0000, 0x108c, 0x2952, 0x4696, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000],
        [0x0000, 0x108d, 0x2953, 0x4697, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000],
        [0x0000, 0x108f, 0x2955, 0x4ab8, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000],
        [0x0000, 0x1090, 0x2956, 0x4ab9, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000],
        [0x626d, 0x1099, 0x295f, 0x4f3f, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000, 0x0000],
    ];
    public static string? Validate(BirthIslandTriangleState state) => state.InteractionNumber is < 0 or > 10
        ? "birth_triangle_interaction_range" : state.Steps is < 0 or > 99 ? "birth_triangle_step_range" : null;
    public static BirthIslandTriangleState CountStep(BirthIslandTriangleState state)
    {
        Require(state);
        return state with { Steps = state.Steps == 99 ? 0 : state.Steps + 1 };
    }
    public static BirthIslandTriangleInteraction Interact(BirthIslandTriangleState state)
    {
        Require(state);
        if (state.Awakened) return Result(state, 3, 0, null);
        int previousSteps = state.Steps;
        state = state with { Steps = 0 };
        if (state.InteractionNumber != 0 && previousSteps > StepCaps[state.InteractionNumber - 1])
            return Result(state with { InteractionNumber = 0 }, 0, 60, "SE_M_CONFUSE_RAY");
        if (state.InteractionNumber == 10) return Result(state with { Awakened = true }, 2, 0, null);
        return Result(state with { InteractionNumber = state.InteractionNumber + 1 }, 1, 5, "SE_DEOXYS_MOVE");
    }
    private static BirthIslandTriangleInteraction Result(BirthIslandTriangleState state, int result, int ticks, string? sound)
    {
        var position = Positions[state.InteractionNumber];
        return new(state, result, position.X, position.Y, ticks, sound);
    }
    private static void Require(BirthIslandTriangleState state)
    {
        if (Validate(state) is {} error) throw new ArgumentException(error, nameof(state));
    }
}
