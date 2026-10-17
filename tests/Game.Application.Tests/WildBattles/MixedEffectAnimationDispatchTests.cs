using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Game.Domain.World;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class MixedEffectAnimationDispatchTests:WildBattleFixture
{
    [TestMethod]
    [DataRow("swagger-confused",207,true)][DataRow("swagger-own-tempo",207,true)][DataRow("swagger-safeguard",207,true)][DataRow("swagger-already-confused",207,true)][DataRow("swagger-capped-own-tempo",207,true)]
    [DataRow("swagger-malformed-cap-confused",207,false)][DataRow("swagger-precondition",207,false)][DataRow("swagger-miss",207,false)][DataRow("swagger-substitute",207,false)]
    [DataRow("tickle-stats",321,true)][DataRow("tickle-cap",321,true)][DataRow("tickle-mist",321,true)][DataRow("tickle-clear-body",321,true)][DataRow("tickle-white-smoke",321,true)][DataRow("tickle-hyper-cutter",321,true)]
    [DataRow("tickle-precondition",321,false)][DataRow("tickle-accuracy",321,false)][DataRow("tickle-malformed-caps",321,false)]
    public void MixedEffectDedicatedRootPrecedesPostAnimationProtection(string scenario,int id,bool expected)
    {
        var session=CampaignSession.RestoreCurrent(content,FieldFixture(),checkpoint.Generation);Send(session,new("turn",Direction:Direction.North));Drain(session);
        var wild=session.State.WildBattle!;
        var move=new MoveId(id==207?"pret:MOVE_SWAGGER":"pret:MOVE_TICKLE");
        for(int slot=0;slot<4;slot++)
        {
            var actor=new BattlerSlot(slot);var target=new BattlerSlot(slot^1);
            SourceBattleEvent Stat(WildBattleStat stat,int before,int after,long sequence=4)=>new SourceStatChangedEvent(sequence,1,actor,target,stat,before,after);
            SourceBattleEvent Fail(string reason,long sequence=4)=>new SourceMoveFailedEvent(sequence,1,actor,target,move,reason);
            SourceBattleEvent Ability(string ability,long sequence=4)=>new SourceAbilityPreventedEvent(sequence,1,actor,target,move,ability);
            SourceBattleEvent[] results=scenario switch
            {
                "swagger-confused"=>[Stat(WildBattleStat.Attack,11,12),new SourceConfusionChangedEvent(5,1,target,0,3,"move")],
                "swagger-own-tempo"=>[Stat(WildBattleStat.Attack,6,8),Ability("ABILITY_OWN_TEMPO",5)],
                "swagger-safeguard"=>[Stat(WildBattleStat.Attack,6,8),Fail("safeguard",5)],
                "swagger-already-confused"=>[Stat(WildBattleStat.Attack,6,8),Fail("already-confused",5)],
                "swagger-capped-own-tempo"=>[new SourceStatAtMaximumEvent(4,1,actor,target,WildBattleStat.Attack),Ability("ABILITY_OWN_TEMPO",5)],
                "swagger-malformed-cap-confused"=>[new SourceStatAtMaximumEvent(4,1,actor,target,WildBattleStat.Attack),Fail("already-confused",5)],
                "swagger-precondition"=>[Fail("confused-and-stat-maxed")],
                "swagger-miss"=>[new SourceMoveMissedEvent(4,1,actor,target,move)],
                "swagger-substitute"=>[Fail("substitute")],
                "tickle-stats"=>[Stat(WildBattleStat.Attack,6,5),Stat(WildBattleStat.Defense,6,5,5)],
                "tickle-cap"=>[new SourceStatAtMinimumEvent(4,1,actor,target,WildBattleStat.Attack),Stat(WildBattleStat.Defense,1,0,5)],
                "tickle-mist"=>[Fail("mist")],
                "tickle-clear-body"=>[Ability("ABILITY_CLEAR_BODY")],
                "tickle-white-smoke"=>[Ability("ABILITY_WHITE_SMOKE")],
                "tickle-hyper-cutter"=>[Ability("ABILITY_HYPER_CUTTER"),Stat(WildBattleStat.Defense,6,5,5)],
                "tickle-precondition"=>[Fail("stats-at-minimum")],
                "tickle-accuracy"=>[Fail("accuracy")],
                _=>[new SourceStatAtMinimumEvent(4,1,actor,target,WildBattleStat.Attack),new SourceStatAtMinimumEvent(5,1,actor,target,WildBattleStat.Defense)]
            };
            var events=new SourceBattleEvent[]{new SourceAttackStringEvent(1,1,actor,move,move),new SourceMoveUsedEvent(2,1,actor,target,0,move,10,9),new SourcePpCommandEvent(3,1,actor,target,0,move,move,10,9,1,0)}.Concat(results).ToImmutableArray();
            var battle=new CampaignDoubleBattle("mixed",new("test"),"test",new(),CampaignDoubleBattleStage.Combat,events,1,[],[],0,0,[],[],[],[],0);
            var decision=AnimationDispatchFixture.AtAttackText(battle,SourceBattleScene.On);
            Assert.AreEqual(expected,decision.Dispatch is not null,scenario+":"+decision.Reason);
            if(slot<2)
            {
                var single=AnimationDispatchFixture.AtAttackText(wild with{Phase=CampaignBattlePhase.Events,EventIndex=1,Events=events.Select(SourceBattleEventAdapter.Export).ToImmutableArray()},true,SourceBattleScene.On);
                Assert.AreEqual(expected,single.Dispatch is not null,"single:"+scenario+":"+single.Reason);
            }
            if(expected)
            {
                Assert.AreEqual(id,decision.Dispatch!.SourceMoveId);Assert.AreEqual(4L,decision.Dispatch.ResultSequence);
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with{Events=events.Add(new SourceMoveFailedEvent(9,1,actor,target,move,"accuracy"))},SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with{Events=events.SetItem(2,new SourcePpCommandEvent(3,1,target,target,0,move,move,10,9,1,0))},SourceBattleScene.On).Dispatch);
            }
        }
    }
}
