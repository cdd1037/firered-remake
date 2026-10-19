using System.Text.Json;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class FieldJumpRecapEffectsTests
{
    private static CampaignQuestLog Log(bool transition=false)
    {
        var actor=new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId,"player",4,4,Direction.South,true);
        CampaignQuestLogAction[] actions=[
            new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["PALLET TOWN"]},
            new(0,4,6,Direction.South,true,CampaignQuestLogEvent.None,32){SourceAction="jump_2_down"},
            new(0,4,7,Direction.South,true,CampaignQuestLogEvent.None,16){StartFrame=32,SourceAction="walk_down"},
            new(0,4,7,Direction.South,true,CampaignQuestLogEvent.None,1){StartFrame=70}];
        if(transition)actions[2]=new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.None,1)
            {StartFrame=32,Transition=new("map.route-1",[actor with{X=4,Y=6}],null,[])};
        return new([new("map.pallet-town",[actor],[..actions])]);
    }

    [TestMethod]
    public void RecordedLandingTailSurvivesNextWalkAndUsesSameSourceSamples()
    {
        var log=Log();string unchanged=JsonSerializer.Serialize(log);
        var replay=new CampaignQuestLogPlayback(log,"RED");
        for(int frame=0;frame<=57;frame++)
        {
            if(frame>0)replay.Tick(1d/60);
            if(frame==57){Assert.IsFalse(replay.JumpEffects.Any());continue;}
            var cue=replay.JumpEffects.Single();Assert.AreEqual(frame,cue.ElapsedFrames,.00001);
            Assert.AreEqual(frame is >=1 and <32,CampaignFieldJumpEffects.ShadowVisible(cue,(_,_)=>0));
            Assert.AreEqual(frame<32?-1:Math.Min(2,(frame-32)/8),CampaignFieldJumpEffects.DustFrame(cue,0));
            if(frame>32)Assert.AreEqual(6,cue.ToY,"dust is at the landing cell, not the subsequent walking actor");
        }
        Assert.AreEqual(unchanged,JsonSerializer.Serialize(log));
    }

    [TestMethod]
    public void MapReconstructionAndLargeClockAdvanceDoNotRetainOldEffects()
    {
        var replay=new CampaignQuestLogPlayback(Log(true),"RED");
        replay.Tick(31d/60);Assert.AreEqual(1,replay.JumpEffects.Count());
        replay.Tick(1d/60);Assert.AreEqual("map.route-1",replay.Map);Assert.IsFalse(replay.JumpEffects.Any());
        var skipped=new CampaignQuestLogPlayback(Log(),"RED");skipped.Tick(60d/60);
        Assert.IsFalse(skipped.JumpEffects.Any());
    }
}
