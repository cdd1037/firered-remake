using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Battle;
using Game.Domain.Randomness;

namespace Game.Domain.WildBattle;

public enum WildBattleAttackCancellerPhase { Start, ConditionsPassed }
[Flags]
public enum WildBattleInvocationFlags
{
    None = 0, AttackStringPrinted = 1, NoPpDeduct = 2, NoAttackString = 4,
    AllowNoPp = 8, Obeys = 16, PpNotAffectedByPressure = 32, DisobedientMove = 64
}

/// <summary>Action-local source registers. CurrentMove need not occupy any learned slot.</summary>
public sealed record WildBattleMoveInvocation(WildBattleSide Actor, int ChosenSlot, int PpSlot,
    MoveId OriginalChosenMove, MoveId ChosenMove, MoveId CurrentMove, MoveId? CalledMove,
    WildBattleSide Target, WildBattleAttackCancellerPhase CancellerPhase, WildBattleInvocationFlags Flags)
{
    /// <summary>The originally selected actor owns PP even when Snatch changes the script attacker.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? PpOwnerOverride { get; init; }
    [JsonIgnore] public WildBattleSide PpOwner
    {
        get => PpOwnerOverride ?? Actor;
        init => PpOwnerOverride = value == Actor ? null : value;
    }
}

/// <summary>The history registers not representable by the historical chosen/landed fields.</summary>
public sealed record WildBattleSourceMoveHistory
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    public MoveId? LastPrintedMove { get; init; }
    public MoveId? LastResultingMove { get; init; }
    public string? LastHitByType { get; init; }
    public MoveId? LastTakenMove { get; init; }
    public MoveId? LastTakenFromPlayer { get; init; }
    public MoveId? LastTakenFromOpponent { get; init; }
}

public sealed record WildBattleTargetResolution(WildBattleSide Target, RngState Rng, ImmutableArray<RandomTrace> Trace);
public sealed record WildBattlePpReduction(int Before, int After, int CommandCost, WildBattleInvocationFlags FlagsAfter);
public sealed record WildBattleCalledMoveTransitionEvent(long Sequence, int Turn, WildBattleMoveInvocation Invocation) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleMoveTargetResolvedEvent(long Sequence, int Turn, WildBattleSide Actor, MoveId CurrentMove,
    WildBattleSide PriorTarget, WildBattleSide Target, SourceMoveTargetResolutionReason Reason) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattleAttackStringEvent(long Sequence, int Turn, WildBattleSide Actor, MoveId ChosenMove, MoveId CurrentMove) : WildBattleEvent(Sequence, Turn);
public sealed record WildBattlePpCommandEvent(long Sequence, int Turn, WildBattleSide Actor, WildBattleSide Target,
    int PpSlot, MoveId ChosenMove, MoveId CurrentMove, int Before, int After, int CommandCost, ulong RngCursor) : WildBattleEvent(Sequence, Turn)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public WildBattleSide? PpOwnerOverride { get; init; }
    [JsonIgnore] public WildBattleSide PpOwner
    {
        get => PpOwnerOverride ?? Actor;
        init => PpOwnerOverride = value == Actor ? null : value;
    }
}

/// <summary>Source commands shared by called-action dispatch. These never register move effects.</summary>
public static class WildBattleSourceCommands
{
    public static bool IsSoundMove(MoveId move) => move.Value is "pret:MOVE_GROWL" or "pret:MOVE_ROAR" or
        "pret:MOVE_SING" or "pret:MOVE_SUPERSONIC" or "pret:MOVE_SCREECH" or "pret:MOVE_SNORE" or
        "pret:MOVE_UPROAR" or "pret:MOVE_METAL_SOUND" or "pret:MOVE_GRASS_WHISTLE" or "pret:MOVE_HYPER_VOICE";
    public static WildBattleTargetResolution ResolveSinglesTarget(WildBattleSide actor, string targetMode,
        RngState rng, string purpose = "wild-battle.called-target")
    {
        if (!Enum.IsDefined(actor)) throw new ArgumentOutOfRangeException(nameof(actor));
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        var target = ResolveSinglesTarget(actor, targetMode, () => {
            var draw = Gen3Rng.Next(rng, purpose, 2); rng = draw.State; trace.Add(draw.Trace); return draw.Value;
        });
        return new(target, rng, trace.ToImmutable());
    }
    internal static WildBattleSide ResolveSinglesTarget(WildBattleSide actor, string targetMode, Func<ushort> draw)
    {
        if (targetMode == "MOVE_TARGET_SELECTED")
        {
            int target; do { target = draw() % 2; } while (target == (int)actor);
            return (WildBattleSide)target;
        }
        return targetMode switch {
            "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" => actor,
            "MOVE_TARGET_DEPENDS" or "MOVE_TARGET_BOTH" or "MOVE_TARGET_FOES_AND_ALLY" or
            "MOVE_TARGET_OPPONENTS_FIELD" or "MOVE_TARGET_RANDOM" => actor == WildBattleSide.Player ? WildBattleSide.Opponent : WildBattleSide.Player,
            _ => throw new ArgumentOutOfRangeException(nameof(targetMode))
        };
    }

    /// <summary>One Cmd_ppreduce. NO_PPDEDUCT is cleared after each command, even when no PP changed.</summary>
    public static WildBattlePpReduction ReducePp(int pp, string targetMode, WildBattleSide actor,
        WildBattleSide target, string targetAbility, int otherSidePressureCount, int otherBattlerPressureCount,
        WildBattleInvocationFlags flags = WildBattleInvocationFlags.None)
    {
        if (!Enum.IsDefined(actor) || !Enum.IsDefined(target)) throw new ArgumentOutOfRangeException(nameof(actor));
        return ReducePp(pp, targetMode, new BattlerSlot((int)actor), new BattlerSlot((int)target), targetAbility,
            otherSidePressureCount, otherBattlerPressureCount, flags);
    }

    public static WildBattlePpReduction ReducePp(int pp, string targetMode, BattlerSlot actor,
        BattlerSlot target, string targetAbility, int otherSidePressureCount, int otherBattlerPressureCount,
        WildBattleInvocationFlags flags = WildBattleInvocationFlags.None)
    {
        if (pp is < 0 or > 255 || otherSidePressureCount is < 0 or > 2 || otherBattlerPressureCount is < 0 or > 3)
            throw new ArgumentOutOfRangeException(nameof(pp));
        int cost = 1;
        if (!flags.HasFlag(WildBattleInvocationFlags.PpNotAffectedByPressure))
            cost += targetMode switch {
                "MOVE_TARGET_FOES_AND_ALLY" => otherBattlerPressureCount,
                "MOVE_TARGET_BOTH" or "MOVE_TARGET_OPPONENTS_FIELD" => otherSidePressureCount,
                "MOVE_TARGET_SELECTED" or "MOVE_TARGET_DEPENDS" or "MOVE_TARGET_RANDOM" or "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" =>
                    actor != target && targetAbility == "ABILITY_PRESSURE" ? 1 : 0,
                _ => throw new ArgumentOutOfRangeException(nameof(targetMode))
            };
        bool suppressed = (flags & (WildBattleInvocationFlags.NoPpDeduct | WildBattleInvocationFlags.NoAttackString)) != 0;
        return new(pp, suppressed ? pp : Math.Max(0, pp - cost), cost, flags & ~WildBattleInvocationFlags.NoPpDeduct);
    }
}
