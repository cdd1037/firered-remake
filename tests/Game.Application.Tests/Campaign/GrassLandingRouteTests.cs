using System.Text.Json;
using Game.Application;
using Game.Domain.World;
using FireRed.Tests.Application.WildBattles;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class GrassLandingRouteTests : WildBattleFixture
{
    [TestMethod]
    public void PublicRouteOneGrassLedgeKeepsCommittedStateAndUsesSameLiveAndRecapOwner()
    {
        var state=FieldFixture();var map=content.Maps[new("map.route-1")];
        state=state with{World=state.World with{Player=new(map.Id,10,4,map.GetTile(10,4)!.Elevation,Direction.South)},QuestLog=null};
        Assert.IsNull(validator.ValidatePlayerSave(state));
        Assert.AreEqual(0x02,map.SourceBehaviorAt(10,6));
        var session=CampaignSession.RestoreCurrent(content,state,checkpoint.Generation);
        var result=session.Execute("grass-ledge",session.Revision,new("move",Direction.South));
        Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);
        Assert.AreEqual(10,result.State.World.Player.X);Assert.AreEqual(6,result.State.World.Player.Y);
        var timing=CampaignFieldMovementTiming.Resolve(state,new("move",Direction.South),content,result.State);
        Assert.AreEqual("jump_2_down",timing.SourceAction);Assert.AreEqual(32,timing.Frames);
        var arrival=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127)
            {TextKey="ArrivedInLocation",Arguments=["ROUTE 1"]};
        var log=CampaignQuestLogRecorder.Record(null,state,state,new("move"),content,suppliedEvent:arrival)!;
        log=CampaignQuestLogRecorder.Record(log,state,result.State,new("move",Direction.South),content)!;
        var jump=log.Scenes[0].Actions.Single(a=>a.SourceAction=="jump_2_down");
        var live=new CampaignFieldGrassLandingEffects();
        live.QueueSpawn(CampaignQuestLogRecorder.PlayerId,10,4,0);
        live.QueueMovement(CampaignQuestLogRecorder.PlayerId,10,4,10,6,timing.SourceAction,32,jump.StartFrame);
        var playback=new CampaignQuestLogPlayback(log,"RED",content.QuestLogSource,
            fieldBehavior:(id,_,_,x,y)=>content.Maps[new(id)].SourceBehaviorAt(x,y));
        string saved=JsonSerializer.Serialize(result.State),recorded=JsonSerializer.Serialize(log);
        for(int frame=0;frame<=80;frame++)
        {
            if(frame>0)playback.Tick(1d/60);
            live.AdvanceTo(frame,map.SourceBehaviorAt,_=>true);
            Assert.AreEqual(JsonSerializer.Serialize(live.Covers),JsonSerializer.Serialize(playback.GrassCovers),$"frame {frame}");
        }
        Assert.AreEqual(1,live.Covers.Length);Assert.AreEqual(10,live.Covers[0].X);Assert.AreEqual(6,live.Covers[0].Y);
        Assert.AreEqual(saved,JsonSerializer.Serialize(result.State));Assert.AreEqual(recorded,JsonSerializer.Serialize(log));
    }

    [TestMethod]
    public void GrassImpactHasFourSourceFramesAndOneEndedFrame()
    {
        for(int age=-1;age<=34;age++)
        {
            var cue=new CampaignFieldJumpCue("actor","map.route-1",10,4,10,6,"jump_2_down",32,32+age);
            Assert.AreEqual(age<0||age>=33?-1:Math.Min(3,age/8),CampaignFieldJumpEffects.GrassImpactFrame(cue,0x02));
            Assert.AreEqual(-1,CampaignFieldJumpEffects.GrassImpactFrame(cue,0));
            Assert.AreEqual(-1,CampaignFieldJumpEffects.DustFrame(cue,0x02));
        }
    }
    [TestMethod]
    public void EveryImportedGrassLedgeDestinationUsesTheSameTileAnchoredOwner()
    {
        int cases=0;
        foreach(var map in content.Maps.Values)
        for(int y=0;y<map.Height;y++)for(int x=0;x<map.Width;x++)
        {
            var direction=map.SourceBehaviorAt(x,y) switch
            {0x38=>(1,0,"jump_2_right"),0x39=>(-1,0,"jump_2_left"),
             0x3a=>(0,-1,"jump_2_up"),0x3b=>(0,1,"jump_2_down"),_=>(0,0,"")};
            if(direction.Item3.Length==0||!CampaignFieldGrassLandingEffects.TallGrass(map.SourceBehaviorAt(x+direction.Item1,y+direction.Item2)))continue;
            var owner=new CampaignFieldGrassLandingEffects();
            owner.QueueMovement("actor",x-direction.Item1,y-direction.Item2,x+direction.Item1,y+direction.Item2,direction.Item3,32,0);
            owner.AdvanceTo(32,map.SourceBehaviorAt,_=>true);
            Assert.AreEqual(1,owner.Covers.Length,map.Id.Value+":"+x+","+y);
            Assert.AreEqual(x+direction.Item1,owner.Covers[0].X);Assert.AreEqual(y+direction.Item2,owner.Covers[0].Y);
            cases++;
        }
        Assert.IsTrue(cases>0,"real imported grass ledges must be exercised");
        Console.WriteLine($"Imported grass ledge geometries: {cases}");
    }

    [TestMethod]
    public void RecordedOrdinaryDepartureUsesTheSameSourceOccupancyAsLiveMovement()
    {
        var actor=new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId,"player",10,4,Direction.South,true);
        var log=new CampaignQuestLog([new CampaignQuestLogScene("map.route-1",[actor],[
            new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["ROUTE 1"]},
            new(0,10,6,Direction.South,true,CampaignQuestLogEvent.None,32){SourceAction="jump_2_down"},
            new(0,10,7,Direction.South,true,CampaignQuestLogEvent.None,16){StartFrame=40,SourceAction="walk_down"}])]);
        var replay=new CampaignQuestLogPlayback(log,"RED",content.QuestLogSource,fieldBehavior:(_,_,_,_,_)=>2);
        var live=new CampaignFieldGrassLandingEffects();
        live.QueueSpawn(actor.Id,10,4,0);
        live.QueueMovement(actor.Id,10,4,10,6,"jump_2_down",32,0);
        live.QueueMovement(actor.Id,10,6,10,7,"walk_down",16,40);
        for(int frame=0;frame<=80;frame++)
        {
            if(frame>0)replay.Tick(1d/60);
            live.AdvanceTo(frame,(_,_)=>2,_=>true);
            Assert.AreEqual(JsonSerializer.Serialize(live.Covers),JsonSerializer.Serialize(replay.GrassCovers),$"departure frame {frame}");
            Assert.AreEqual(frame is >=32 and <=56?1:0,replay.GrassCovers.Count(c=>c.X==10&&c.Y==6));
        }
    }

    [TestMethod]
    public void PublicOrdinaryGrassEntryUsesCommittedTimingWithoutMutatingSavedState()
    {
        var state=FieldFixture();var map=content.Maps[new("map.route-1")];
        state=state with{World=state.World with{Player=new(map.Id,16,5,map.GetTile(16,5)!.Elevation,Direction.South)},QuestLog=null};
        Assert.IsNull(validator.ValidatePlayerSave(state));
        Assert.AreEqual(0,map.SourceBehaviorAt(16,5));Assert.AreEqual(2,map.SourceBehaviorAt(16,6));
        var session=CampaignSession.RestoreCurrent(content,state,checkpoint.Generation);
        var result=session.Execute("walk-grass",session.Revision,new("move",Direction.South));
        Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);
        var timing=CampaignFieldMovementTiming.Resolve(state,new("move",Direction.South),content,result.State);
        Assert.AreEqual("walk_down",timing.SourceAction);Assert.AreEqual(16,timing.Frames);
        var owner=new CampaignFieldGrassLandingEffects();owner.QueueSpawn("player",16,5,0);
        owner.QueueMovement("player",16,5,16,6,timing.SourceAction,timing.Frames,0);
        string committed=JsonSerializer.Serialize(result.State);
        for(int frame=0;frame<=70;frame++)
        {
            owner.AdvanceTo(frame,map.SourceBehaviorAt,_=>true);
            if(frame==0)Assert.IsEmpty(owner.Covers);
            else Assert.AreEqual(frame<=40?1+(frame-1)/10:0,owner.Covers.Single().Frame);
        }
        Assert.AreEqual(committed,JsonSerializer.Serialize(result.State));
    }

    [TestMethod]
    public void RecordedDynamicActorSpawnsBeforeFirstMovementAndOnlyOnce()
    {
        var player=new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId,"player",1,1,Direction.South,true);
        var npc=new CampaignQuestLogActor("dynamic-npc","npc",3,1,Direction.South,true);
        var log=new CampaignQuestLog([new CampaignQuestLogScene("map.route-1",[player],[
            new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["ROUTE 1"]},
            new(-1,3,2,Direction.South,true,CampaignQuestLogEvent.None,16){StartFrame=4,SourceAction="walk_down",Spawn=npc},
            new(-1,3,3,Direction.South,true,CampaignQuestLogEvent.None,16){StartFrame=20,SourceAction="walk_down",Spawn=npc with{Y=2}}])]);
        var replay=new CampaignQuestLogPlayback(log,"RED",content.QuestLogSource,fieldBehavior:(_,_,_,_,_)=>2);
        var live=new CampaignFieldGrassLandingEffects();live.QueueSpawn(player.Id,1,1,0);live.QueueSpawn(npc.Id,3,1,4);
        live.QueueMovement(npc.Id,3,1,3,2,"walk_down",16,4);live.QueueMovement(npc.Id,3,2,3,3,"walk_down",16,20);
        for(int frame=0;frame<=90;frame++)
        {
            if(frame>0)replay.Tick(1d/60);
            live.AdvanceTo(frame,(_,_)=>2,_=>true);
            Assert.AreEqual(JsonSerializer.Serialize(live.Covers),JsonSerializer.Serialize(replay.GrassCovers),$"dynamic spawn frame {frame}");
        }
        Assert.AreEqual(2,replay.GrassCovers.Count);
        Assert.AreEqual(3,replay.GrassCovers.Single(c=>c.ActorId==npc.Id).Y);
    }

    [TestMethod]
    public void InitialNpcAndPlayerGrassAndRecordedStepsShareTheSameTimeline()
    {
        var player=new CampaignQuestLogActor(CampaignQuestLogRecorder.PlayerId,"player",1,1,Direction.South,true);
        var npc=new CampaignQuestLogActor("npc","npc",3,1,Direction.South,false);
        var log=new CampaignQuestLog([new CampaignQuestLogScene("map.route-1",[player,npc],[
            new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["ROUTE 1"]},
            new(0,1,2,Direction.South,true,CampaignQuestLogEvent.None,16){StartFrame=4,SourceAction="walk_down"},
            new(1,3,2,Direction.South,false,CampaignQuestLogEvent.None,8){StartFrame=8,SourceAction="walk_fast_down"}])]);
        var replay=new CampaignQuestLogPlayback(log,"RED",content.QuestLogSource,fieldBehavior:(_,_,_,_,_)=>2);
        var live=new CampaignFieldGrassLandingEffects();
        live.QueueSpawn(player.Id,1,1,0);live.QueueSpawn(npc.Id,3,1,0);
        live.QueueMovement(player.Id,1,1,1,2,"walk_down",16,4);live.QueueMovement(npc.Id,3,1,3,2,"walk_fast_down",8,8);
        for(int frame=0;frame<=90;frame++)
        {
            if(frame>0)replay.Tick(1d/60);
            live.AdvanceTo(frame,(_,_)=>2,_=>true);
            Assert.AreEqual(JsonSerializer.Serialize(live.Covers),JsonSerializer.Serialize(replay.GrassCovers),$"frame {frame}");
        }
        Assert.AreEqual(2,live.Covers.Length);Assert.IsTrue(live.Covers.All(c=>c.Frame==0&&c.Y==2));
    }

}
