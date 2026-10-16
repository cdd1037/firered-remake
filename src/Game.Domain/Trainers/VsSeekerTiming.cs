namespace Game.Domain.Trainers;

/// <summary>Application-owned presentation gates; source-local sprite frames remain presentation-owned.
/// Original scan: 84 walking/surfing or 88 bicycle animation ticks plus five task/movement completion ticks.
/// Original response movement: three DELAY_16 actions, plus the parent-task observation after STEP_END.
/// No wall clock or sound completion changes rules.</summary>
public static class VsSeekerTiming
{
    public const int NormalScanTicks = 89;
    public const int CyclingScanTicks = 93;
    public const int ResponseMovementTicks = 48;
    public const int WaitTicks = 49;
}
