using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;

namespace FireRed.Tests.Application.WildBattles;

[TestClass]
public sealed class MoveAnimationVisibilityTests
{
    private static readonly MoveId Voice = new("pret:MOVE_HYPER_VOICE");
    private static readonly MoveId Fly = new("pret:MOVE_FLY");
    private static readonly MoveId Metronome = new("pret:MOVE_METRONOME");
    private static readonly MoveId Thrash = new("pret:MOVE_THRASH");
    private static readonly MoveId Bide = new("pret:MOVE_BIDE");
    private static SourcePpCommandEvent Pp(long sequence, int actor = 0, MoveId? move = null) =>
        new(sequence, 7, new(actor), new(actor ^ 1), 0, move ?? Voice, move ?? Voice, 10, 9, 1, 0);
    private static CampaignMoveAnimationDispatch Cue(long pp = 10) =>
        new("visibility", 9, 9, pp, 11, 304, new(0), new(1), false);
    private static SourceMtMoonVolatileEvent Charge(long sequence, int slot, int before, int after) =>
        new(sequence, 7, new(slot), "called-charge", before, after, "move");
    // Invocation authentication is exercised by the caller/locked-pair kernel
    // suites. Here the typed receipts isolate each admitted no-PP snapshot seam.
    private static (SourceBattleEvent Anchor, CampaignMoveAnimationDispatch Dispatch) NoPpAnchor(int move, bool substitute)
    {
        if (move is 118 or 37)
        {
            var id = move == 118 ? Metronome : Thrash;
            var attack = new SourceAttackStringEvent(10, 7, new(0), id, id);
            return (attack, new("visibility", 10, 10, null, move == 118 ? 10 : 12, move, new(0), new(move == 118 ? 0 : 1), false)
            { Boundary = move == 118 ? CampaignMoveAnimationBoundary.AfterCallerText : CampaignMoveAnimationBoundary.AfterRetainedAttackText });
        }
        Assert.AreEqual(117, move);
        SourceBattleEvent hit = substitute
            ? new SourceSubstituteDamagedEvent(10, 7, new(0), new(1), Bide, 100, 80, 20, false, 100)
            : new SourceHpChangedEvent(10, 7, new(0), new(1), Bide, 100, 80, 20, false, 100);
        return (hit, new("visibility", 10, null, null, 10, 117, new(0), new(1), substitute)
        { Boundary = CampaignMoveAnimationBoundary.BeforeHitResult, MoveTurn = 1 });
    }

    [TestMethod]
    public void InvocationCapturesKnownHiddenPartnersAndDetachesReturnedFlags()
    {
        MoveId?[] entry = [null, null, null, Fly];
        SourceBattleEvent[] events = [Pp(10)];
        var visible = CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(events, Cue(), entry);
        Assert.IsNotNull(visible);
        CollectionAssert.AreEqual(new[] { true, true, true, false }, visible);
        entry[3] = null;
        Assert.IsFalse(visible[3]);
        visible[0] = false;
        Assert.IsTrue(CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(events, Cue(), entry)![0]);
    }

    [TestMethod]
    public void SameBatchChargeAndReleaseUseTheirReceiptOrder()
    {
        MoveId?[] entry = new MoveId?[4];
        SourceBattleEvent[] charging = [Pp(1, 3, Fly), Charge(2, 3, 0, 1), Pp(10)];
        var hidden = CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(charging, Cue(), entry);
        Assert.IsNotNull(hidden); Assert.IsFalse(hidden[3]);
        SourceBattleEvent[] released = [Pp(1, 3, Fly), Charge(2, 3, 0, 1), Charge(3, 3, 1, 0), Pp(10)];
        var shown = CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(released, Cue(), entry);
        Assert.IsNotNull(shown); Assert.IsTrue(shown[3]);
    }

    [TestMethod]
    public void UnknownPartnerChargeFailsClosedEvenWhenActorIsVisible()
    {
        SourceBattleEvent[] events = [Charge(1, 3, 0, 1), Pp(10)];
        Assert.IsNull(CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(events, Cue(), new MoveId?[4]));
    }

    [TestMethod]
    public void MissingDuplicateWrongKindOrForeignPpAndInvalidEntryShapeFailClosed()
    {
        MoveId?[] entry = new MoveId?[4];
        SourceBattleEvent[][] malformed = [[], [Pp(9)], [Pp(10), Pp(10)], [Charge(10, 0, 1, 0)], [Pp(10, 2)]];
        foreach (var events in malformed)
            Assert.IsNull(CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(events, Cue(), entry));
        Assert.IsNull(CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation([Pp(10)], Cue(), new MoveId?[3]));
    }

    [TestMethod]
    public void LaterChargeAndReleaseCannotLeakBackIntoTheInvocation()
    {
        SourceBattleEvent[] laterCharge = [Pp(10), Pp(12, 3, Fly), Charge(13, 3, 0, 1)];
        var shown = CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(laterCharge, Cue(), new MoveId?[4]);
        Assert.IsNotNull(shown); Assert.IsTrue(shown[3]);
        MoveId?[] entry = [null, Fly];
        SourceBattleEvent[] laterRelease = [Pp(10), Charge(12, 1, 1, 0)];
        var hidden = CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(laterRelease, Cue(), entry);
        Assert.IsNotNull(hidden); Assert.IsFalse(hidden[1]);
    }

    [TestMethod, DataRow(118, false), DataRow(37, false), DataRow(117, false), DataRow(117, true)]
    public void NoPpAttackAndBideResultAnchorsPreserveEntryAndPrecedingChargeVisibility(int move, bool substitute)
    {
        var (anchor, dispatch) = NoPpAnchor(move, substitute);
        Assert.IsNull(dispatch.PpSequence); Assert.AreEqual(anchor.Sequence, dispatch.SnapshotSequence);
        Assert.AreEqual(move == 117 ? (long?)null : anchor.Sequence, dispatch.AttackSequence);
        MoveId?[] entry = [null, null, null, Fly];
        SourceBattleEvent[] precedingCharge = [Pp(1, 2, Fly), Charge(2, 2, 0, 1), anchor];
        var hidden = CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(precedingCharge, dispatch, entry);
        Assert.IsNotNull(hidden);
        CollectionAssert.AreEqual(new[] { true, true, false, false }, hidden);
        SourceBattleEvent[] precedingRelease = [Pp(1, 2, Fly), Charge(2, 2, 0, 1), Charge(3, 3, 1, 0), anchor];
        var released = CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(precedingRelease, dispatch, entry);
        Assert.IsNotNull(released);
        CollectionAssert.AreEqual(new[] { true, true, false, true }, released);
        CollectionAssert.AreEqual(new MoveId?[] { null, null, null, Fly }, entry, "Projection must not mutate entry charge state.");
    }

    [TestMethod, DataRow(118, false), DataRow(37, false), DataRow(117, false), DataRow(117, true)]
    public void NoPpAnchorsRejectMissingDuplicateWrongKindAndForeignReceipts(int move, bool substitute)
    {
        var (anchor, dispatch) = NoPpAnchor(move, substitute);
        SourceBattleEvent foreign = anchor switch
        {
            SourceAttackStringEvent a => a with { Actor = new(2) },
            SourceHpChangedEvent h => h with { Actor = new(2) },
            SourceSubstituteDamagedEvent s => s with { Actor = new(2) },
            _ => throw new InvalidOperationException("Unexpected no-PP anchor.")
        };
        SourceBattleEvent wrongKind = move == 117
            ? new SourceAttackStringEvent(10, 7, new(0), Bide, Bide)
            : new SourceHpChangedEvent(10, 7, new(0), new(1), Thrash, 100, 80, 20, false, 100);
        SourceBattleEvent[][] malformed = [[], [anchor with { Sequence = 9 }], [anchor, anchor], [anchor, Pp(10)], [Pp(10)], [wrongKind], [foreign]];
        foreach (var events in malformed)
            Assert.IsNull(CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(events, dispatch, new MoveId?[4]));
        Assert.IsNull(CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation([anchor], dispatch with { Sequence = 11 }, new MoveId?[4]));
        if (move == 117)
        {
            SourceBattleEvent wrongTarget = anchor is SourceHpChangedEvent hp
                ? hp with { Target = new(3) } : ((SourceSubstituteDamagedEvent)anchor) with { Target = new(3) };
            Assert.IsNull(CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation([wrongTarget], dispatch, new MoveId?[4]));
        }
    }

    [TestMethod, DataRow(118, false), DataRow(37, false), DataRow(117, false), DataRow(117, true)]
    public void NoPpAnchorsExcludeLaterChargesAndReleasesFromTheirSnapshot(int move, bool substitute)
    {
        var (anchor, dispatch) = NoPpAnchor(move, substitute);
        MoveId?[] entry = [null, null, null, Fly];
        SourceBattleEvent[] events = [Pp(1, 2, Fly), Charge(2, 2, 0, 1), anchor,
            Pp(11, 1, Fly), Charge(12, 1, 0, 1), Charge(13, 3, 1, 0), Charge(14, 2, 1, 0)];
        var visible = CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(events, dispatch, entry);
        Assert.IsNotNull(visible);
        CollectionAssert.AreEqual(new[] { true, true, false, false }, visible,
            "The later target charge and both entry/preceding partner releases must not leak across the invocation anchor.");
    }
}
