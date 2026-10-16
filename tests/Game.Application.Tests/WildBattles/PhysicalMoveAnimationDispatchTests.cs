using Game.Application;
using Game.Domain.Options;
using Game.Domain.Ids;
using Game.Domain.WildBattle;
using Game.Domain.World;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class PhysicalMoveAnimationDispatchTests:WildBattleFixture
{
    [TestMethod]
    [DataRow("FIRE_PUNCH",7)][DataRow("EMBER",52)][DataRow("FLAMETHROWER",53)][DataRow("POISON_POWDER",77)][DataRow("STUN_SPORE",78)][DataRow("SLEEP_POWDER",79)][DataRow("SNATCH",289)][DataRow("HEADBUTT",29)][DataRow("PECK",64)][DataRow("DOUBLE_EDGE",38)][DataRow("SLAM",21)][DataRow("VINE_WHIP",22)][DataRow("VICE_GRIP",11)][DataRow("STOMP",23)][DataRow("JUMP_KICK",26)][DataRow("ROLLING_KICK",27)]
    [DataRow("BODY_SLAM",34)][DataRow("TAKE_DOWN",36)][DataRow("LOW_KICK",67)]
    [DataRow("STRENGTH",70)][DataRow("HI_JUMP_KICK",136)]
    public void PhysicalRootRequiresCommittedSuccessfulSourceInvocation(string name,int id)
    {
        var session=CampaignSession.RestoreCurrent(content,FieldFixture(),checkpoint.Generation);
        Send(session,new("turn",Direction:Direction.North));Drain(session);
        var move=new MoveId("pret:MOVE_"+name);var actor=WildBattleSide.Player;var target=WildBattleSide.Opponent;
        var battle=session.State.WildBattle! with
        {
            Phase=CampaignBattlePhase.Events,EventIndex=1,
            Events=[new WildBattleAttackStringEvent(1,1,actor,move,move),
                new WildBattleMoveUsedEvent(2,1,actor,target,0,move,10,9),
                new WildBattlePpCommandEvent(3,1,actor,target,0,move,move,10,9,1,0),
                id is 77 or 78 or 79?new WildBattleStatusChangedEvent(4,1,target,0,id==77?WildBattleStatus.Poison:id==78?WildBattleStatus.Paralysis:3,"move"):id==289?new WildBattleSnatchReadyEvent(4,1,actor,0):new WildBattleHpChangedEvent(4,1,actor,target,move,10,5,5,false,100)]
        };
        string hash=CanonicalJson.Hash(battle);
        var result=CampaignMoveAnimationPresentation.AtAttackText(battle,true,SourceBattleScene.On);
        Assert.IsNotNull(result.Dispatch);Assert.AreEqual(id,result.Dispatch.SourceMoveId);Assert.AreEqual(4L,result.Dispatch.ResultSequence);
        Assert.AreEqual(hash,CanonicalJson.Hash(battle));
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle,true,SourceBattleScene.Off).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(battle,false,SourceBattleScene.On).Dispatch);
        var missed=battle with{Events=battle.Events.SetItem(3,new WildBattleMoveMissedEvent(4,1,actor,target,move))};
        Assert.AreEqual("source-no-animation-result",CampaignMoveAnimationPresentation.AtAttackText(missed,true,SourceBattleScene.On).Reason);
        var withoutPp=battle with{Events=battle.Events.RemoveAt(2)};
        Assert.AreEqual("unmatched-source-pp-command",CampaignMoveAnimationPresentation.AtAttackText(withoutPp,true,SourceBattleScene.On).Reason);
    }
}
