using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class TargetResolutionReceiptPresentationTests
{
    [TestMethod, DataRow("COUNTER"), DataRow("MIRROR_COAT")]
    public void TypedTargetResolutionIsSilentBookkeepingAndKeepsThePairedAttack(string move)
    {
        MoveId id = new("pret:MOVE_" + move);
        WildBattleEvent[] events = [
            new WildBattleMoveTargetResolvedEvent(1, 0, WildBattleSide.Player, id, WildBattleSide.Player,
                WildBattleSide.Opponent, SourceMoveTargetResolutionReason.Retaliation),
            new WildBattleAttackStringEvent(2, 0, WildBattleSide.Player, id, id),
            new WildBattleMoveUsedEvent(3, 0, WildBattleSide.Player, WildBattleSide.Opponent, 0, id, 20, 19)];
        var resolved = SourceTrainerPresentationModel.Describe(events, 0, side => side.ToString(), m => m.Value, (_, _) => "party");
        Assert.IsNotNull(resolved); Assert.IsTrue(resolved.Silent); Assert.IsFalse(resolved.Unsupported);
        var attack = SourceTrainerPresentationModel.Describe(events, 1, side => side.ToString(), m => m.Value, (_, _) => "party");
        Assert.IsNotNull(attack); Assert.IsTrue(attack.Silent);
        var used = SourceTrainerPresentationModel.Describe(events, 2, side => side.ToString(), m => m.Value, (_, _) => "party");
        Assert.IsNull(used, "Ordinary MoveUsed keeps the existing view-owned attack page.");
        Assert.IsTrue(SourceTrainerPresentationModel.PairedMoveUsed(events, 1, (WildBattleAttackStringEvent)events[1]));
    }
}
