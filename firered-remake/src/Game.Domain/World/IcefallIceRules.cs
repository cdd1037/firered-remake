using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.World;

public enum IcefallCallbackPhase { Initialize, ObserveDestination, CrackDelay, BreakDelay }
public sealed record IcefallIceCell(int TemporaryFlag, int X, int Y);
public sealed record IcefallMetatileMutation(int X, int Y, int MetatileId, int SourceBehavior, int Collision, int Elevation);
/// <summary>Stable saves store only VisitedMask after CanSave; callback phases/hole are transient.</summary>
public sealed record IcefallIceState(MapId MapId, long Revision, int VisitedMask, int CrackedMask,
    IcefallCallbackPhase Phase, int PreviousX, int PreviousY, int IceX = -1, int IceY = -1, int Delay = 0, int HoleIndex = -1)
{
    public bool FallRequested => HoleIndex >= 0;
    public bool CanSave => HoleIndex < 0 && Phase is IcefallCallbackPhase.Initialize or IcefallCallbackPhase.ObserveDestination && VisitedMask == CrackedMask;
}
public sealed record IcefallCallbackIntent(CommandId CommandId, long ExpectedRevision, int DestinationX, int DestinationY);
public sealed record IcefallIceEvent(CommandId CausedByCommandId, string Kind, IcefallMetatileMutation? Mutation = null, int? TemporaryFlag = null);
public sealed record IcefallCallbackTransition(TransitionStatus Status, IcefallIceState State,
    ImmutableArray<IcefallIceEvent> Events, Diagnostic? Diagnostic);
public sealed record IcefallFallState(WorldSnapshot AwaitingWorld, CommandId PendingPresentationId, IcefallIceState AwaitingIce)
{
    public bool ControlsLocked => true;
    public bool CanSave => false;
    public int BeforeInvisibleDelay => 20;
    public int BeforeWarpDelay => 60;
}
public sealed record IcefallFallTransition(TransitionStatus Status, WorldSnapshot State, IcefallIceState Ice,
    IcefallFallState? ActiveFall, ImmutableArray<FourIslandFieldEvent> Events, Diagnostic? Diagnostic);

/// <summary>Literal STEP_CB_ICE callback. Invoke once per source task callback, including while movement presents.</summary>
public static class IcefallIceRules
{
    public const int ThinIceMetatile = 0x359, CrackedIceMetatile = 0x35a, HoleMetatile = 0x35b, HoleBehavior = 0x66;
    public static ImmutableArray<IcefallIceCell> Cells { get; } =
        [new(1,8,3), new(2,10,5), new(3,15,5), new(4,8,9), new(5,9,9), new(6,16,9), new(7,8,10), new(8,9,10), new(9,8,14)];
    public static IcefallIceState NewMap(MapId map) => new(map, 0, 0, 0, IcefallCallbackPhase.Initialize, -1, -1);
    public static IcefallIceState RestoreStable(int visitedMask)
    {
        if (visitedMask is < 0 or > 511) throw new ArgumentOutOfRangeException(nameof(visitedMask));
        return new(FourIslandFieldSource.FirstFloor, 0, visitedMask, visitedMask, IcefallCallbackPhase.Initialize, -1, -1);
    }
    public static IcefallIceState ReloadStable(IcefallIceState state)
    {
        if (!Valid(state) || !state.CanSave) throw new ArgumentException("Transient ice cannot be a stable save/reload checkpoint.", nameof(state));
        return state with { CrackedMask = state.VisitedMask, Phase = IcefallCallbackPhase.Initialize, PreviousX = -1, PreviousY = -1, IceX = -1, IceY = -1, Delay = 0 };
    }
    public static ImmutableArray<IcefallMetatileMutation> StableCracks(int visitedMask)
    {
        if (visitedMask is < 0 or > 511) throw new ArgumentOutOfRangeException(nameof(visitedMask));
        return Cells.Where(c => (visitedMask & (1 << (c.TemporaryFlag - 1))) != 0)
            .Select(c => new IcefallMetatileMutation(c.X, c.Y, CrackedIceMetatile, FourIslandFieldSource.CrackedIce, 0, 3)).ToImmutableArray();
    }
    public static int EffectiveBehavior(IcefallIceState state, int x, int y, int sourceBehavior)
    {
        int index = Index(x, y);
        return index < 0 ? sourceBehavior : state.HoleIndex == index ? HoleBehavior :
            (state.CrackedMask & (1 << index)) != 0 ? FourIslandFieldSource.CrackedIce : sourceBehavior;
    }
    public static IcefallCallbackTransition Tick(IcefallIceState state, IcefallCallbackIntent intent, IFieldMap map)
    {
        IcefallCallbackTransition Fail(string c, TransitionStatus status = TransitionStatus.Fault) => new(status, state, [], new(c, c));
        if (!Valid(state) || state.MapId != FourIslandFieldSource.FirstFloor || map.Id != state.MapId || map.Width != 20 || map.Height != 20)
            return Fail("invalid-ice-callback-state");
        if (!StableId.IsValid(intent.CommandId.Value) || intent.ExpectedRevision != state.Revision)
            return Fail("stale-or-invalid-ice-callback", TransitionStatus.Rejected);
        if (state.FallRequested) return Fail("ice-fall-handler-required", TransitionStatus.Rejected);
        var destination = map.GetTile(intent.DestinationX, intent.DestinationY);
        if (destination is null || destination.Blocked || destination.Elevation is < 0 or > 15)
            return Fail("invalid-ice-destination");
        var next = state with { Revision = state.Revision + 1 };
        ImmutableArray<IcefallIceEvent> events = [];
        switch (state.Phase)
        {
            case IcefallCallbackPhase.Initialize:
                next = next with { Phase = IcefallCallbackPhase.ObserveDestination, PreviousX = intent.DestinationX, PreviousY = intent.DestinationY };
                break;
            case IcefallCallbackPhase.ObserveDestination:
                if ((intent.DestinationX, intent.DestinationY) == (state.PreviousX, state.PreviousY)) break;
                next = next with { PreviousX = intent.DestinationX, PreviousY = intent.DestinationY };
                int raw = map.SourceBehaviorAt(intent.DestinationX, intent.DestinationY) ?? -1;
                int behavior = EffectiveBehavior(state, intent.DestinationX, intent.DestinationY, raw);
                if (behavior is not (FourIslandFieldSource.ThinIce or FourIslandFieldSource.CrackedIce)) break;
                int index = Index(intent.DestinationX, intent.DestinationY);
                if (index < 0 || destination.Elevation != 3 || raw is not (FourIslandFieldSource.ThinIce or FourIslandFieldSource.CrackedIce))
                    return Fail("unsupported-ice-cell");
                bool thin = behavior == FourIslandFieldSource.ThinIce;
                if (!thin && (state.VisitedMask & (1 << index)) == 0) return Fail("ice-state-map-disagreement");
                next = next with { Phase = thin ? IcefallCallbackPhase.CrackDelay : IcefallCallbackPhase.BreakDelay,
                    IceX = intent.DestinationX, IceY = intent.DestinationY, Delay = 4, VisitedMask = thin ? state.VisitedMask | (1 << index) : state.VisitedMask };
                if (thin) events = [new(intent.CommandId, "temporary-flag-set", TemporaryFlag: index + 1)];
                break;
            case IcefallCallbackPhase.CrackDelay:
            case IcefallCallbackPhase.BreakDelay:
                if (state.Delay != 0) { next = next with { Delay = state.Delay - 1 }; break; }
                int changed = Index(state.IceX, state.IceY);
                bool breaking = state.Phase == IcefallCallbackPhase.BreakDelay;
                var mutation = new IcefallMetatileMutation(state.IceX, state.IceY, breaking ? HoleMetatile : CrackedIceMetatile,
                    breaking ? HoleBehavior : FourIslandFieldSource.CrackedIce, 0, 3);
                next = next with { Phase = IcefallCallbackPhase.ObserveDestination, CrackedMask = state.CrackedMask | (1 << changed),
                    HoleIndex = breaking ? changed : -1, IceX = -1, IceY = -1 };
                events = [new(intent.CommandId, breaking ? "ice-break-var-temp-1-set" : "ice-crack", mutation)];
                break;
        }
        return new(TransitionStatus.Accepted, next, events, null);
    }

    /// <summary>Begins lockall/delay20/invisible/SE_FALL/delay60. Application presents those original script operations.</summary>
    public static IcefallFallTransition StartFall(WorldSnapshot world, IcefallIceState ice, FourIslandFieldIntent intent, IFieldMap firstFloor, IcefallFallState? activeFall = null)
    {
        IcefallFallTransition Fail(string c) => new(TransitionStatus.Rejected, world, ice, activeFall, [], new(c, c));
        var diagnostic = FourIslandFieldSource.Validate(world, intent, firstFloor);
        if (diagnostic is not null) return Fail(diagnostic.Code);
        if (activeFall is not null) return Fail("ice-fall-already-active");
        if (!Valid(ice) || ice.MapId != world.Player.MapId || !ice.FallRequested || intent.CompletedPresentationId is not null)
            return Fail("ice-fall-not-ready");
        // warphole uses the avatar's CURRENT coordinates, never the stored hole coordinates or ladder index.
        if (Index(world.Player.X, world.Player.Y) != ice.HoleIndex) return Fail("unsupported-fall-away-from-hole");
        var next = world with { Revision = world.Revision + 1 };
        return new(TransitionStatus.Accepted, next, ice, new(next, intent.CommandId, ice), [], null);
    }
    public static IcefallFallTransition CompleteFall(WorldSnapshot world, IcefallIceState ice, IcefallFallState fall,
        FourIslandFieldIntent intent, IFieldMap basement)
    {
        IcefallFallTransition Fail(string c) => new(TransitionStatus.Rejected, world, ice, fall, [], new(c, c));
        if (world != fall.AwaitingWorld || ice != fall.AwaitingIce || intent.ExpectedRevision != world.Revision || !StableId.IsValid(intent.CommandId.Value) ||
            intent.CommandId == fall.PendingPresentationId || intent.CompletedPresentationId != fall.PendingPresentationId)
            return Fail("ice-fall-presentation-mismatch");
        if (!Valid(ice) || ice.MapId != FourIslandFieldSource.FirstFloor || !ice.FallRequested ||
            world.Player.MapId != ice.MapId || Index(world.Player.X, world.Player.Y) != ice.HoleIndex ||
            basement.Id != FourIslandFieldSource.Basement || basement.Width != 24 || basement.Height != 20 || world.Revision == long.MaxValue)
            return Fail("invalid-ice-fall-state");
        var target = basement.GetTile(world.Player.X, world.Player.Y);
        int? behavior = basement.SourceBehaviorAt(world.Player.X, world.Player.Y);
        if (target is not { Blocked: false, Elevation: 3 } || behavior is not (0x08 or FourIslandFieldSource.Ice))
            return Fail("invalid-ice-fall-landing");
        var p = world.Player with { MapId = basement.Id, Elevation = 3, Facing = Direction.South };
        var next = world with { Revision = world.Revision + 1, Player = p };
        return new(TransitionStatus.Accepted, next, NewMap(basement.Id), null,
            [new(intent.CommandId, FourIslandFieldEventKind.Fell, world.Player, p, "warphole-reset-on-foot-controllable-south")], null);
    }
    private static int Index(int x, int y)
    { for (int i = 0; i < Cells.Length; i++) if (Cells[i].X == x && Cells[i].Y == y) return i; return -1; }
    private static bool Valid(IcefallIceState s) => s is not null && StableId.IsValid(s.MapId.Value) && s.Revision is >= 0 and < long.MaxValue &&
        s.VisitedMask is >= 0 and <= 511 && s.CrackedMask is >= 0 and <= 511 && (s.CrackedMask & ~s.VisitedMask) == 0 &&
        Enum.IsDefined(s.Phase) && s.Delay is >= 0 and <= 4 && s.HoleIndex is >= -1 and < 9 &&
        (s.HoleIndex < 0 || s.Phase == IcefallCallbackPhase.ObserveDestination && (s.CrackedMask & (1 << s.HoleIndex)) != 0) &&
        (s.Phase == IcefallCallbackPhase.Initialize ? s.PreviousX == -1 && s.PreviousY == -1 : s.PreviousX is >=0 and <20 && s.PreviousY is >=0 and <20) &&
        (s.Phase is IcefallCallbackPhase.CrackDelay or IcefallCallbackPhase.BreakDelay ? Index(s.IceX, s.IceY) >= 0 && (s.VisitedMask & (1 << Index(s.IceX,s.IceY))) != 0 &&
            (s.Phase != IcefallCallbackPhase.BreakDelay || (s.CrackedMask & (1 << Index(s.IceX,s.IceY))) != 0) : s.Delay == 0);
}
