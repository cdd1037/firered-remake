using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class AnimationInvocationNormalizationTests
{
    private static readonly MoveId Pound=new("pret:MOVE_POUND");
    private static ImmutableArray<SourceBattleEvent> Events => [
        new SourceAttackStringEvent(1,1,new(0),Pound,Pound),
        new SourceMoveUsedEvent(2,1,new(0),new(1),0,Pound,35,34),
        new SourcePpCommandEvent(3,1,new(0),new(1),0,Pound,Pound,35,34,1,0),
        new SourceHpChangedEvent(4,1,new(0),new(1),Pound,30,20,10,false,100)];
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events)=>
        new("normalized",new("test"),"test",new(),CampaignDoubleBattleStage.Combat,events,1,[],[],0,0,[],[],[],[],0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events)=>
        new("normalized",null!,new(),events,1,null!,null!,CampaignBattlePhase.Events,[],0,[]);

    [TestMethod]
    public void BothEnvelopesUseTheSameInvocationAndPreserveForeignPpOwner()
    {
        var events = Events; var single = Single(events); var doubles = Double(events);
        Assert.AreEqual(events, single.Events); Assert.AreEqual(events, doubles.Events);
        for (int i = 0; i < events.Length; i++) Assert.AreSame(single.Events[i], doubles.Events[i]);
        Assert.AreEqual(AnimationDispatchFixture.AtAttackText(doubles,SourceBattleScene.On),
            AnimationDispatchFixture.AtAttackText(single,true,SourceBattleScene.On));
        var foreign=events.SetItem(2,((SourcePpCommandEvent)events[2]) with{PpOwner=new(1)});
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(foreign),SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(foreign),true,SourceBattleScene.On).Dispatch);
    }

    [TestMethod]
    public void CatalogIdentityAndActualScriptIdentityMustBothMatch()
    {
        var moves=CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves;
        Assert.IsNotNull(CampaignMoveAnimationPresentation.AtAttackText(Double(Events),SourceBattleScene.On,moves,AnimationDispatchFixture.Supports).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(Events),SourceBattleScene.On,moves,(_,_)=>false).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(Events),SourceBattleScene.On,null,AnimationDispatchFixture.Supports).Dispatch);
        var wrong=moves.ToDictionary(p=>p.Key,p=>p.Value);
        wrong[Pound]=wrong[Pound] with{SourceMoveId=2};
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(Events),SourceBattleScene.On,wrong,AnimationDispatchFixture.Supports).Dispatch);
        Assert.IsFalse(AnimationDispatchFixture.Supports(1,"pret:MOVE_KARATE_CHOP"));
    }
}
