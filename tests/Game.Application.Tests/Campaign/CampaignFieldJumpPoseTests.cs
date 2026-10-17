using System.Text.Json;
using Game.Application;
using Game.Domain.World;
using FireRed.Tests.Application.WildBattles;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignFieldJumpPoseTests : WildBattleFixture
{
    [TestMethod]
    public void EverySourceJumpCallbackMatchesSharedLiveAndRecordedPose()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Combine(TestAssets.Root,"tests","Reference","field-jump-source-vectors.json")));
        var cases=source.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.AreEqual(20,cases.Length);
        foreach(var item in cases)
        {
            string action=item.GetProperty("action").GetString()!;int duration=item.GetProperty("duration").GetInt32();
            var rows=item.GetProperty("rows").EnumerateArray().Select(r=>r.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();
            Assert.AreEqual(duration+1,rows.Length);
            int dx=rows[^1][1],dy=rows[^1][2];
            var initial=new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId,"player",4,4,Direction.South,true);
            var scene=new CampaignQuestLogScene("map.pallet-town",[initial],[
                new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["PALLET TOWN"]},
                new(0,4+dx/16,4+dy/16,Direction.South,true,CampaignQuestLogEvent.None,duration){StartFrame=0,SourceAction=action}]);
            var log=new CampaignQuestLog([scene]);string before=JsonSerializer.Serialize(log);
            var playback=new CampaignQuestLogPlayback(log,"RED",CurrentCampaignFixture.Catalog.QuestLogSource);
            foreach(var row in rows)
            {
                int frame=row[0];if(frame>0)playback.Tick(1d/60);
                var sample=CampaignFieldMovementTiming.Sample(action,duration,frame);
                Assert.AreEqual(row[1],dx*sample.Fraction,.00001,$"{action} x at {frame}");
                Assert.AreEqual(row[2],dy*sample.Fraction,.00001,$"{action} y at {frame}");
                Assert.IsTrue(sample.IsJump);Assert.AreEqual(row[3],sample.RiderYOffset(1));
                Assert.AreEqual(row[3],sample.DrawYOffset,$"{action} y2 at {frame}");
                if(frame<duration)
                {
                    var replay=playback.Movement(initial.Id)!.Value;
                    Assert.AreEqual(sample.Fraction,replay.Fraction,.00001);
                    Assert.AreEqual(sample.DrawYOffset,replay.DrawYOffset);Assert.IsTrue(replay.IsJump);
                    Assert.AreEqual(initial,replay.From);
                }
                else Assert.IsNull(playback.Movement(initial.Id));
            }
            Assert.AreEqual(before,JsonSerializer.Serialize(log));
        }
    }

    [TestMethod]
    public void FractionalHostFramesHoldSourcePixelsWithoutChangingNonJumpTiming()
    {
        foreach(string action in new[]{"jump_2_down","jump_special_left","jump_in_place_up"})
        {
            int duration=action.Contains("in_place",StringComparison.Ordinal)?16:32;
            for(int frame=0;frame<duration;frame++)
            {
                var whole=CampaignFieldMovementTiming.Sample(action,duration,frame);
                Assert.AreEqual(whole.DrawYOffset,CampaignFieldMovementTiming.Sample(action,duration,frame+.49).DrawYOffset);
                if(!action.Contains("in_place",StringComparison.Ordinal))Assert.AreEqual(whole.Fraction,CampaignFieldMovementTiming.Sample(action,duration,frame+.49).Fraction);
            }
            Assert.AreEqual(0,CampaignFieldMovementTiming.Sample(action,duration,duration+10).DrawYOffset);
        }
        foreach(string action in new[]{"walk_down","walk_slow_up","player_run_left","ride_water_current_right"})
        {
            int duration=CampaignFieldMovementTiming.SourceDuration(action)!.Value;
            for(double frame=0;frame<=duration;frame+=.5)
            {
                var pose=CampaignFieldMovementTiming.Sample(action,duration,frame);
                Assert.AreEqual(CampaignFieldMovementTiming.Fraction(action,duration,frame),pose.Fraction);
                Assert.AreEqual(0,pose.DrawYOffset);Assert.IsFalse(pose.IsJump);Assert.AreEqual(1,pose.RiderYOffset(1));
            }
        }
    }
    [TestMethod]
    public void PublicLedgeMoveKeepsItsAcceptedCoordinatesAndThirtyTwoRecordedTicks()
    {
        var state=FieldFixture();var map=content.Maps[new("map.route-1")];
        state=state with{World=state.World with{Player=new(map.Id,2,4,map.GetTile(2,4)!.Elevation,Direction.South)},QuestLog=null};
        Assert.IsNull(validator.ValidatePlayerSave(state));
        var session=CampaignSession.RestoreCurrent(content,state,checkpoint.Generation);
        var result=session.Execute("jump-pose",session.Revision,new("move",Direction.South));
        Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);
        Assert.AreEqual(2,result.State.World.Player.X);Assert.AreEqual(6,result.State.World.Player.Y);
        var timing=CampaignFieldMovementTiming.Resolve(state,new("move",Direction.South),content,result.State);
        Assert.AreEqual("jump_2_down",timing.SourceAction);Assert.AreEqual(32,timing.Frames);
        var arrival=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127)
            {TextKey="ArrivedInLocation",Arguments=["ROUTE 1"]};
        var log=CampaignQuestLogRecorder.Record(null,state,state,new("move"),content,suppliedEvent:arrival)!;
        log=CampaignQuestLogRecorder.Record(log,state,result.State,new("move",Direction.South),content)!;
        var jump=log.Scenes[0].Actions.Single(a=>a.SourceAction=="jump_2_down");
        Assert.AreEqual(32,jump.Frames);Assert.AreEqual(2,jump.X);Assert.AreEqual(6,jump.Y);
        string unchanged=JsonSerializer.Serialize(result.State);
        var playback=new CampaignQuestLogPlayback(log,"RED",content.QuestLogSource);
        for(int i=0;i<32;i++)playback.Tick(1d/60);
        Assert.AreEqual(unchanged,JsonSerializer.Serialize(result.State));
    }

}
