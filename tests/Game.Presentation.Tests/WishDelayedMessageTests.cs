using System.Text.Json;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class WishDelayedMessageTests
{
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceTrainerEventDisplay? Describe(WildBattleCalledDelayedEvent receipt, WildBattleEvent? outcome = null,
        Func<WildBattleSide,int,string?>? prefixedPartyName = null) =>
        SourceTrainerPresentationModel.Describe(outcome is null ? [receipt] : [receipt,outcome], 0,
            s => s.ToString(), m => m.Value, (side, member) => $"{side} party {member}", prefixedPartyName:prefixedPartyName);

    [TestMethod]
    public void WishSetupIsSilentForBothSinglesAdaptersAndEveryDoublesPosition()
    {
        for (int slot = 0; slot < 4; slot++)
        {
            var receipt = new SourceCalledDelayedEvent(4, 1, new(slot), Id("WISH"), 0, 2, "scheduled", slot / 2);
            string original = JsonSerializer.Serialize(receipt);
            // The doubles formatter uses this same pure rule before selecting text.
            Assert.IsTrue(SourceTrainerPresentationModel.IsSilentDelayedReceipt(receipt.Move, receipt.Reason));
            if (slot < 2)
            {
                var single = (WildBattleCalledDelayedEvent)SourceBattleEventAdapter.Export(receipt);
                var display = Describe(single);
                Assert.IsNotNull(display); Assert.IsTrue(display.Silent);
                Assert.IsFalse(display.Unsupported); Assert.AreEqual(0, display.Texts.Length);
            }
            Assert.AreEqual(original, JsonSerializer.Serialize(receipt));
        }
    }

    [TestMethod]
    public void CountdownRemainsSilentWhileOtherDelayedMoveSetupKeepsItsSourceText()
    {
        foreach (string move in new[] { "WISH", "FUTURE_SIGHT", "DOOM_DESIRE" })
        foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
        {
            var countdown = new WildBattleCalledDelayedEvent(10, 2, side, Id(move), 2, 1, "countdown", 0);
            Assert.IsTrue(SourceTrainerPresentationModel.IsSilentDelayedReceipt(countdown.Move, countdown.Reason));
            Assert.IsTrue(Describe(countdown)!.Silent);
            if (move == "WISH") continue;
            var scheduled = countdown with { Sequence = 4, Turn = 1, Before = 0, After = 3, Reason = "scheduled" };
            Assert.IsFalse(SourceTrainerPresentationModel.IsSilentDelayedReceipt(scheduled.Move, scheduled.Reason));
            var display = Describe(scheduled); Assert.IsNotNull(display);
            Assert.IsFalse(display.Silent); Assert.IsFalse(display.Unsupported);
            Assert.AreEqual(move == "FUTURE_SIGHT" ? "sText_PkmnForesawAttack" : "sText_PkmnChoseXAsDestiny",
                display.Texts.Single().Symbol);
        }
    }

    [TestMethod]
    public void WishResolutionKeepsTheOriginalCasterPartyNameAndIsNotSetupSilence()
    {
        foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
        {
            var resolved = new WildBattleCalledDelayedEvent(20, 3, side, Id("WISH"), 1, 0, "resolved", 2);
            Assert.IsFalse(SourceTrainerPresentationModel.IsSilentDelayedReceipt(resolved.Move, resolved.Reason));
            var recovery = new WildBattleRecoveredEvent(21, 3, side, Id("WISH"), 7, 27, 20);
            var display = Describe(resolved,recovery); Assert.IsNotNull(display);
            Assert.IsFalse(display.Silent); Assert.IsFalse(display.Unsupported);
            var text = display.Texts.Single(); Assert.AreEqual("sText_PkmnWishCameTrue", text.Symbol);
            Assert.AreEqual($"{side} party 2", text.Values.Single(x => x.Key == "B_BUFF1").Value);
            Assert.IsTrue(Describe(resolved)!.Silent, "Zero-HP expiry has no source WishComesTrue script.");
            var full = new WildBattleMoveFailedEvent(21,3,side,side,Id("WISH"),"already-full-hp");
            Assert.AreEqual("sText_PkmnWishCameTrue",Describe(resolved,full)!.Texts.Single().Symbol);
            var missingCaster = Describe(resolved with { SourcePartyIndex = null },recovery);
            Assert.IsNotNull(missingCaster); Assert.IsTrue(missingCaster.Unsupported);
        }
    }

    [TestMethod]
    public void OriginalCasterUsesTheSuppliedPlayerTrainerOrWildPrefixAndUnavailableMemberFailsClosed()
    {
        foreach (var (side,prefix) in new[] { (WildBattleSide.Player,""), (WildBattleSide.Opponent,"Foe "), (WildBattleSide.Opponent,"Wild ") })
        {
            var resolved=new WildBattleCalledDelayedEvent(20,3,side,Id("WISH"),1,0,"resolved",2);
            var recovered=new WildBattleRecoveredEvent(21,3,side,Id("WISH"),1,51,50);
            var display=Describe(resolved,recovered,(s,member)=>member==2?prefix+"ORIGINAL":null);
            Assert.IsNotNull(display);Assert.IsFalse(display.Unsupported);
            Assert.AreEqual(prefix+"ORIGINAL",display.Texts.Single().Values.Single(v=>v.Key=="B_BUFF1").Value);
            var missing=Describe(resolved,recovered,(_,_)=>null);Assert.IsNotNull(missing);
            Assert.IsTrue(missing.Unsupported);Assert.IsFalse(missing.Silent);Assert.AreEqual(0,missing.Texts.Length);
        }
    }

    [TestMethod]
    public void MalformedOrUnmatchedPositiveSuffixDoesNotPrintWishCameTrue()
    {
        var resolved=new WildBattleCalledDelayedEvent(20,3,WildBattleSide.Player,Id("WISH"),1,0,"resolved",0);
        foreach (var recovered in new[] {
            new WildBattleRecoveredEvent(21,3,WildBattleSide.Opponent,Id("WISH"),7,27,20),
            new WildBattleRecoveredEvent(22,3,WildBattleSide.Player,Id("WISH"),7,27,20),
            new WildBattleRecoveredEvent(21,3,WildBattleSide.Player,Id("WISH"),0,20,20),
            new WildBattleRecoveredEvent(21,3,WildBattleSide.Player,Id("RECOVER"),7,27,20) })
        {
            var display=Describe(resolved,recovered);Assert.IsNotNull(display);Assert.IsTrue(display.Unsupported);
        }
        var other=resolved with {Move=Id("FUTURE_SIGHT")};
        Assert.AreEqual("sText_PkmnTookAttack",Describe(other)!.Texts.Single().Symbol);
    }

    [TestMethod]
    public void UnknownDelayedReasonsDoNotBecomeSilent()
    {
        foreach (string reason in new[] { "resolved", "failed", "unknown", "Scheduled" })
        {
            var receipt = new WildBattleCalledDelayedEvent(4, 1, WildBattleSide.Player, Id("WISH"), 0, 2, reason);
            Assert.IsFalse(SourceTrainerPresentationModel.IsSilentDelayedReceipt(receipt.Move, receipt.Reason));
            var display = Describe(receipt); Assert.IsNotNull(display);
            Assert.IsFalse(display.Silent); Assert.IsTrue(display.Unsupported);
        }
        Assert.IsFalse(SourceTrainerPresentationModel.IsSilentDelayedReceipt(Id("TACKLE"), "scheduled"));
    }
}
