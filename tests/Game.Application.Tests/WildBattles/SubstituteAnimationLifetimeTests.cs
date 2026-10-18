using Game.Application;
using Game.Domain.Battle;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class SubstituteAnimationLifetimeTests
{
    [TestMethod]
    public void RevealedOccupantSurvivesResultPagesUntilExplicitControllerRestore()
    {
        var lifetime=new CampaignSubstituteAnimationLifetime();var member=new PartyMemberKey(BattleSide.Player,2);
        lifetime.Reveal(0,member);
        // Per-hit ACKs and an early KO do not replace the source flag_x8 owner.
        for(int page=0;page<5;page++){lifetime.Observe(0,member,true);Assert.IsTrue(lifetime.IsRevealed(0,member));}
        lifetime.Restore(0);Assert.IsFalse(lifetime.IsRevealed(0,member));
    }
    [TestMethod]
    public void ReplacementOrBrokenDollDropsOnlyItsOwnRevealedState()
    {
        var lifetime=new CampaignSubstituteAnimationLifetime();var left=new PartyMemberKey(BattleSide.Player,0);
        var right=new PartyMemberKey(BattleSide.Player,1);var incoming=new PartyMemberKey(BattleSide.Player,2);
        lifetime.Reveal(0,left);lifetime.Reveal(2,right);
        lifetime.Observe(0,incoming,true);Assert.IsFalse(lifetime.IsRevealed(0,left));Assert.IsFalse(lifetime.IsRevealed(0,incoming));
        Assert.IsTrue(lifetime.IsRevealed(2,right));
        lifetime.Observe(2,right,false);Assert.IsFalse(lifetime.IsRevealed(2,right));
        lifetime.Observe(2,right,true);Assert.IsFalse(lifetime.IsRevealed(2,right));
    }
    [TestMethod]
    public void CancellationAndNewBattleClearAllOwnersAndCrossSideOwnerIsRejected()
    {
        var lifetime=new CampaignSubstituteAnimationLifetime();var player=new PartyMemberKey(BattleSide.Player,0);
        var opponent=new PartyMemberKey(BattleSide.Opponent,0);
        Assert.ThrowsExactly<ArgumentException>(()=>lifetime.Reveal(1,player));
        lifetime.Reveal(0,player);lifetime.Reveal(1,opponent);lifetime.Clear();
        Assert.IsFalse(lifetime.IsRevealed(0,player));Assert.IsFalse(lifetime.IsRevealed(1,opponent));
    }
}
