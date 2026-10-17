using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;

namespace FireRed.Tests.Application.WildBattles;

[TestClass]
public sealed class MoveAnimationVisibilityTests
{
    private static readonly MoveId Voice = new("pret:MOVE_HYPER_VOICE");
    private static readonly MoveId Fly = new("pret:MOVE_FLY");
    private static SourcePpCommandEvent Pp(long sequence, int actor = 0, MoveId? move = null) =>
        new(sequence, 7, new(actor), new(actor ^ 1), 0, move ?? Voice, move ?? Voice, 10, 9, 1, 0);
    private static CampaignMoveAnimationDispatch Cue(long pp = 10) =>
        new("visibility", 9, 9, pp, 11, 304, new(0), new(1), false);
    private static SourceMtMoonVolatileEvent Charge(long sequence, int slot, int before, int after) =>
        new(sequence, 7, new(slot), "called-charge", before, after, "move");

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
}
