using System.Collections.Immutable;
using Game.Domain.Randomness;

namespace Game.Domain.Capture;

/// <summary>
/// Ordinary wild catch core from Cmd_handleballthrow, pinned FireRed src/battle_script_commands.c:9463-9615.
/// This is a deliberately bounded calculation, not a complete capture action or battle transaction.
/// </summary>
public static class CaptureRules
{
    public const string RulesVersion = "firered-ordinary-wild-capture-v1";
    public const string ConditionalBallsRulesVersion = "firered-ordinary-wild-capture-balls-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const int SuccessAnimationCode = 4;

    public static CaptureResult Resolve(CaptureInput input, RngState rng)
        => ResolveCore(input, rng, null, conditionalBalls: false);

    public static CaptureResult Resolve(CaptureInput input, RngState rng, CaptureBallContext? context)
        => ResolveCore(input, rng, context, conditionalBalls: true);

    private static CaptureResult ResolveCore(CaptureInput input, RngState rng, CaptureBallContext? context, bool conditionalBalls)
    {
        CaptureResult Failure(CaptureOutcome outcome, string code) => new(outcome, code, null, null, 0, null, rng, []);
        // Validate the full request before drawing; unsupported contexts cannot fall through to wild rules.
        if (input.MaximumHp is < 1 or > ushort.MaxValue || input.CurrentHp < 1 || input.CurrentHp > input.MaximumHp)
            return Failure(CaptureOutcome.InvalidInput, "invalid-hp");
        if (input.CatchRate is < 1 or > byte.MaxValue)
            return Failure(CaptureOutcome.InvalidInput, "invalid-catch-rate");
        if (input.Status is < CaptureStatus.None or > CaptureStatus.BadPoison)
            return Failure(CaptureOutcome.InvalidInput, "invalid-major-status");
        if (input.Ball is < CaptureBall.Master or > CaptureBall.Premier)
            return Failure(CaptureOutcome.InvalidInput, "invalid-ball");
        if (input.Encounter is < CaptureEncounterKind.OrdinaryWild or > CaptureEncounterKind.Story)
            return Failure(CaptureOutcome.InvalidInput, "invalid-encounter-kind");
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId)
            return Failure(CaptureOutcome.InvalidInput, "invalid-rng-algorithm");
        if (input.Encounter != CaptureEncounterKind.OrdinaryWild)
            return Failure(CaptureOutcome.Unsupported, "unsupported-encounter-kind");
        if (input.Ball == CaptureBall.Safari || !conditionalBalls && input.Ball >= CaptureBall.Safari)
            return Failure(CaptureOutcome.Unsupported, "unsupported-ball");
        if (context is not null && (context.TargetLevel is < 1 or > 100 || context.BattleTurnCounter is < 0 or > 255))
            return Failure(CaptureOutcome.InvalidInput, "invalid-ball-context");
        if (input.Ball is >= CaptureBall.Net and <= CaptureBall.Timer && context is null)
            return Failure(CaptureOutcome.Unsupported, "ball-context-required");
        // Original agbcc reads terrain[9] (TYPE_NORMAL == 0) at the byte preceding the ball table
        // for Master Ball. Use the pinned original-layout behavior, not a C# out-of-range read.
        // The original __udivsi3 returns 0 on division by 0. See the source chain in docs.
        int multiplier = input.Ball switch
        {
            CaptureBall.Master => 0,
            CaptureBall.Ultra => 20,
            CaptureBall.Great => 15,
            CaptureBall.Net => context!.TargetIsWaterOrBug ? 30 : 10,
            CaptureBall.Dive => context!.IsUnderwater ? 35 : 10,
            CaptureBall.Nest => Math.Max(10, 40 - context!.TargetLevel),
            CaptureBall.Repeat => context!.TargetSpeciesCaught ? 30 : 10,
            // The source assigns counter + 10 to u8 BEFORE clamping. 246..255 wrap to 0..9.
            CaptureBall.Timer => Math.Min(40, (int)unchecked((byte)(context!.BattleTurnCounter + 10))),
            _ => 10
        };
        uint ballAdjusted = (uint)(input.CatchRate * multiplier / 10);
        uint hpAdjusted = ballAdjusted * (uint)(input.MaximumHp * 3 - input.CurrentHp * 2) / (uint)(3 * input.MaximumHp);
        uint catchValue = input.Status switch
        {
            CaptureStatus.Sleep or CaptureStatus.Freeze => hpAdjusted * 2,
            CaptureStatus.Poison or CaptureStatus.Burn or CaptureStatus.Paralysis or CaptureStatus.BadPoison => hpAdjusted * 15 / 10,
            _ => hpAdjusted
        };
        var calculation = new CaptureCalculation(multiplier, ballAdjusted, hpAdjusted, catchValue, null);

        if (catchValue > 254)
            return new(CaptureOutcome.Caught, null, CaptureDecisionBranch.GuaranteedCatchValue, calculation, 0, SuccessAnimationCode, rng, []);
        uint threshold = catchValue == 0 ? 0 : 1048560 / IntegerSquareRoot(IntegerSquareRoot(16711680 / catchValue));
        calculation = calculation with { ShakeThreshold = threshold };
        RngState current = rng;
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        int checksPassed = 0;
        while (checksPassed < SuccessAnimationCode)
        {
            if (current.Cursor == ulong.MaxValue)
                return Failure(CaptureOutcome.InvalidInput, "rng-cursor-exhausted");
            RngDraw draw = Gen3Rng.Next(current, $"capture:shake-check-{checksPassed + 1}");
            trace.Add(draw.Trace);
            current = draw.State;
            if (draw.Value >= threshold) break; // The source condition is strictly Random() < odds.
            checksPassed++;
        }

        // This override must happen AFTER the random comparison, exactly as in Cmd_handleballthrow.
        bool master = input.Ball == CaptureBall.Master;
        int animationCode = master ? SuccessAnimationCode : checksPassed;
        return new(animationCode == SuccessAnimationCode ? CaptureOutcome.Caught : CaptureOutcome.Escaped,
            null, master ? CaptureDecisionBranch.MasterBallOverride : CaptureDecisionBranch.ShakeChecks,
            calculation, checksPassed, animationCode, current, trace.ToImmutable());
    }

    // Exact floor square root, no floating-point rounding or platform math dependency.
    private static uint IntegerSquareRoot(uint value)
    {
        uint low = 0, high = 65536;
        while (low + 1 < high)
        {
            uint middle = low + (high - low) / 2;
            if ((ulong)middle * middle <= value) low = middle;
            else high = middle;
        }
        return low;
    }
}
