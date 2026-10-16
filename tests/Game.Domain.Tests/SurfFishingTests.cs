using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Randomness;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class SurfFishingTests
{
    [TestMethod]
    [DataRow(0x10, true)] [DataRow(0x11, true)] [DataRow(0x12, true)]
    [DataRow(0x13, false)] [DataRow(0x15, true)] [DataRow(0x1a, true)]
    [DataRow(0x1b, true)] [DataRow(0x50, true)] [DataRow(0x51, true)]
    [DataRow(0x52, true)] [DataRow(0x53, true)] [DataRow(0, false)]
    [DataRow(0x17, false)] [DataRow(0x70, false)]
    public void SurfUsesSourceBehaviorAndRawCollisionRatherThanShoreTerrain(int behavior, bool allowed)
    {
        var context = new OldRodCastContext(new("map.vermilion-city"), true, 1, 0, behavior, 4, IsSurfing: true)
            { FrontMapCollision = 0 };
        Assert.AreEqual(allowed, OldRodFishingRules.CanCast(context));
        Assert.IsFalse(OldRodFishingRules.CanCast(context with { FrontMapCollision = 1 }));
        Assert.IsFalse(OldRodFishingRules.CanCast(context with { FrontMapCollision = null }));
        Assert.IsFalse(OldRodFishingRules.CanCast(context with { IsUnderwater = true }));
        Assert.IsFalse(OldRodFishingRules.CanCast(context with { OwnsOldRod = false }));
    }

    [TestMethod]
    [DataRow(FishingRod.Old, false)] [DataRow(FishingRod.Good, false)] [DataRow(FishingRod.Super, false)]
    [DataRow(FishingRod.Old, true)] [DataRow(FishingRod.Good, true)] [DataRow(FishingRod.Super, true)]
    public void SurfAndShoreRunIdenticalTaskAndRandomDraws(FishingRod rod, bool populated)
    {
        var map = new Game.Domain.Ids.MapId("map.test-fishing");
        int start = rod == FishingRod.Old ? 0 : rod == FishingRod.Good ? 2 : 5;
        int[] weights = rod == FishingRod.Old ? [70, 30] : rod == FishingRod.Good ? [60, 20, 20] : [40, 40, 15, 4, 1];
        var table = new OldRodEncounterTable(new("map.populated"), 20, weights.Select((w, i) =>
            new OldRodEncounterSlot(start + i, new("pret:SPECIES_MAGIKARP"), 5, 5, w)).ToImmutableArray());
        var profile = FishingTaskProfile.ForRod("test-surf-fishing", rod, [table], [map]);
        var shore = new OldRodCastContext(populated ? table.Map : map, true, 3, 2, 0x10, 3)
            { Rod = rod, OwnsGoodRod = true, OwnsSuperRod = true, FrontMapCollision = 0 };
        var surf = shore with { IsSurfing = true, PlayerElevation = 1, FrontCollision = 0 };
        var a = profile.Start(shore, new RngState(123)); var b = profile.Start(surf, new RngState(123));
        for (int i = 0; i < 600 && a.State.Status == OldRodFishingStatus.Running; i++)
        {
            Assert.AreEqual(a.State, b.State); Assert.AreEqual(a.GlobalRng, b.GlobalRng);
            CollectionAssert.AreEqual(a.RandomTrace.ToArray(), b.RandomTrace.ToArray());
            a = profile.Advance(a.State, new(MessageFinished: true, NoCatchAnimationFinished: true), a.GlobalRng);
            b = profile.Advance(b.State, new(MessageFinished: true, NoCatchAnimationFinished: true), b.GlobalRng);
        }
        Assert.AreNotEqual(OldRodFishingStatus.Running, a.State.Status);
        if (!populated) Assert.AreEqual(OldRodFishingStatus.NotEvenANibble, a.State.Status);
        Assert.AreEqual(a.State, b.State); Assert.AreEqual(a.GlobalRng, b.GlobalRng);
    }
}
