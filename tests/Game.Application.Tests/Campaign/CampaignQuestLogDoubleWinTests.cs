using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using FireRed.Tests.Application.WildBattles;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogDoubleWinTests:WildBattleFixture
{
    [TestMethod]
    [DataRow(0,false,0)]
    [DataRow(2,false,2)]
    [DataRow(1,false,0)]
    [DataRow(1,true,2)]
    [DataRow(-1,false,-1)]
    public void CompletionUsesExactAttackerOrSourceLivingFallbackAndNeverGuessesUnknown(int attacker,bool leftFainted,int expected)
    {
        var session=Controlled([Owned("SNORLAX",30,1,"TACKLE"),Owned("PIDGEY",20,2,"TACKLE")],Owned("PIDGEY",20,99,"TACKLE"));
        var basis=session.State;
        var entry=SourceTwinsTrainerEncounter.Begin(catalog,content.Route8Twins!,basis.Party.Select(p=>new WildBattleCalledPartyMember(p.Pokemon,p.HeldItem,p.Status,p.IsEgg)).ToImmutableArray(),basis.World.Rng,false,false,false);
        Assert.IsTrue(entry.Succeeded,entry.Fault);
        var frame=entry.State!;var left=frame.Get(new(0));
        if(leftFainted)frame=frame with{Slots=frame.Slots.SetItem(0,left with{Body=left.Body! with{Pokemon=left.Body.Pokemon with{CurrentHp=0}}})};
        int opponent=frame.Get(new(3)).Body!.Pokemon.SourceSpeciesId;
        frame=frame with{Outcome=WildBattleOutcome.Won,LastOpponentFaint=new(opponent,attacker<0?null:new BattlerSlot(attacker))};
        var battle=new CampaignDoubleBattle("quest-double",content.Route8Twins!.Trainer.Id,"test-actor",frame,CampaignDoubleBattleStage.Finished,[],0,[],[30,20],0,0,[],[],[],[],0);
        var before=basis with{WildBattle=null,DoubleBattle=battle with{Stage=CampaignDoubleBattleStage.Combat},DoubleBattleHistory=[],QuestLog=null,
            Script=basis.Script with{Flags=basis.Script.Flags.SetItem("FLAG_SYS_GAME_CLEAR",true)}};
        var after=before with{DoubleBattle=battle,DoubleBattleHistory=[new(battle.Id,battle.Trainer.Value,WildBattleOutcome.Won,0,0,[0,1])]};
        var serialized=System.Text.Json.JsonSerializer.Serialize(after);
        var log=CampaignQuestLogRecorder.Record(null,before,after,new("battle_ack"),content,_=>content.Route8Twins.Trainer,recordingOpen:false);
        if(expected<0)Assert.IsNull(log);
        else
        {
            var caption=log!.Scenes.Single().Actions.Single();
            Assert.AreEqual(CampaignQuestLogEvent.TrainerWon,caption.Event);
            Assert.AreEqual(content.SourceSpeciesName(opponent),caption.Arguments[2]);
            Assert.AreEqual(content.SourceSpeciesName(frame.Get(new(expected)).Body!.Pokemon.SourceSpeciesId),caption.Arguments[3]);
            Assert.IsTrue(log.Scenes.Single().Closed);
        }
        Assert.AreEqual(serialized,System.Text.Json.JsonSerializer.Serialize(after));
        Assert.IsNull(CampaignQuestLogRecorder.Record(null,after,after,new("battle_ack"),content,_=>content.Route8Twins.Trainer,recordingOpen:false));
        var gated=after with{Script=after.Script with{Flags=after.Script.Flags.SetItem("FLAG_SYS_GAME_CLEAR",false)}};
        Assert.IsNull(CampaignQuestLogRecorder.Record(null,before,gated,new("battle_ack"),content,_=>content.Route8Twins.Trainer,recordingOpen:false));
    }
}
