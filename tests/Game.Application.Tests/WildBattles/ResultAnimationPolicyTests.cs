using Game.Application;
using Game.Domain.Options;
namespace FireRed.Tests.Application.WildBattles;
[TestClass,TestCategory("Game")]
public sealed class ResultAnimationPolicyTests
{
    [TestMethod]
    [DataRow(CampaignResultAnimationKind.StatChange,SourceBattleScene.On,true)]
    [DataRow(CampaignResultAnimationKind.StatChange,SourceBattleScene.Off,true)]
    [DataRow(CampaignResultAnimationKind.HeldItem,SourceBattleScene.On,true)]
    [DataRow(CampaignResultAnimationKind.HeldItem,SourceBattleScene.Off,false)]
    public void SourceResultEffectsRetainTheirDistinctBattleScenePolicy(CampaignResultAnimationKind kind,SourceBattleScene scene,bool expected)
        =>Assert.AreEqual(expected,CampaignResultAnimationPolicy.Allows(kind,scene));
    [TestMethod]
    public void UnknownResultKindDoesNotInheritAnOptionExemption()
        =>Assert.IsFalse(CampaignResultAnimationPolicy.Allows((CampaignResultAnimationKind)99,SourceBattleScene.Off));
}
