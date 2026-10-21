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
        // Source GroundImpactDust has three 8-frame images, JumpTallGrass has four.
        // UpdateJumpImpactEffect removes each on the callback after ANIMCMD_END:
        // dust age 25, grass age 33. A shared cue must retain the longer tail.
        for(int frame=0;frame<65;frame++)
        {
            if(frame>0)replay.Tick(1d/60);
            var cue=replay.JumpEffects.Single();Assert.AreEqual(frame,cue.ElapsedFrames,.00001);
            Assert.AreEqual(frame is >=1 and <32,CampaignFieldJumpEffects.ShadowVisible(cue,(_,_)=>0));
            Assert.AreEqual(frame is <32 or >=57?-1:Math.Min(2,(frame-32)/8),CampaignFieldJumpEffects.DustFrame(cue,0));
            Assert.AreEqual(frame<32?-1:Math.Min(3,(frame-32)/8),CampaignFieldJumpEffects.GrassImpactFrame(cue,0x02));
            Assert.AreEqual(-1,CampaignFieldJumpEffects.DustFrame(cue,0x02));
            Assert.AreEqual(-1,CampaignFieldJumpEffects.GrassImpactFrame(cue,0));
            if(frame>32)Assert.AreEqual(6,cue.ToY,"landing effects stay at the landing cell, not the subsequent walking actor");
        }
        var expired=replay.JumpEffects.Single() with{ElapsedFrames=65};
        replay.Tick(1d/60);
        Assert.IsFalse(replay.JumpEffects.Any());
        Assert.AreEqual(-1,CampaignFieldJumpEffects.DustFrame(expired,0));
        Assert.AreEqual(-1,CampaignFieldJumpEffects.GrassImpactFrame(expired,0x02));
        Assert.AreEqual(unchanged,JsonSerializer.Serialize(log));
    }

    [TestMethod]
    public void MapReconstructionAndLargeClockAdvanceDoNotRetainOldEffects()
    {
        var replay=new CampaignQuestLogPlayback(Log(true),"RED");
        replay.Tick(31d/60);Assert.AreEqual(1,replay.JumpEffects.Count());
        replay.Tick(1d/60);Assert.AreEqual("map.route-1",replay.Map);Assert.IsFalse(replay.JumpEffects.Any());
        foreach(int frame in new[]{60,65})
        {
            var skipped=new CampaignQuestLogPlayback(Log(),"RED");skipped.Tick(frame/60d);
            var incremental=new CampaignQuestLogPlayback(Log(),"RED");
            for(int tick=0;tick<frame;tick++)incremental.Tick(1d/60);
            CollectionAssert.AreEqual(incremental.JumpEffects.ToArray(),skipped.JumpEffects.ToArray());
            if(frame==65){Assert.IsFalse(skipped.JumpEffects.Any());continue;}
            var cue=skipped.JumpEffects.Single();
            Assert.AreEqual(-1,CampaignFieldJumpEffects.DustFrame(cue,0));
            Assert.AreEqual(3,CampaignFieldJumpEffects.GrassImpactFrame(cue,0x02));
        }
    }
}
