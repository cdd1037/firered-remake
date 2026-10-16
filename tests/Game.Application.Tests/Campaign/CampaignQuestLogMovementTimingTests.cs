using System.Text.Json;
using System.Collections.Immutable;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogMovementTimingTests
{
    private static CampaignState At(string map,int x,int y,bool run=false,bool bike=false,bool surf=false)
    {
        var state=CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog).State;
        return state with{World=state.World with{Player=state.World.Player with{MapId=new(map),X=x,Y=y}},IsCycling=bike,IsSurfing=surf,
            Script=state.Script with{Flags=state.Script.Flags.SetItem("FLAG_SYS_B_DASH",run)},MapEdits=null,MapLayout=null,Motions=[]};
    }
    [TestMethod]
    [DataRow(false,false,false,false,16,"walk_right")]
    [DataRow(false,true,false,false,16,"walk_right")]
    [DataRow(true,false,false,false,16,"walk_right")]
    [DataRow(true,true,false,false,8,"player_run_right")]
    [DataRow(true,true,false,true,8,"walk_fast_right")]
    [DataRow(true,true,true,false,6,"ride_water_current_right")]
    public void SourceOrdinaryWalkRunSurfAndBicycle(bool shoes,bool held,bool bike,bool surf,int frames,string action)
    {
        var state=At("map.pallet-town",6,8,shoes,bike,surf);
        var timing=CampaignFieldMovementTiming.Resolve(state,new("move",Direction.East){RunHeld=held},CurrentCampaignFixture.Catalog);
        Assert.AreEqual(frames,timing.Frames);Assert.AreEqual(action,timing.SourceAction);
    }
    [TestMethod]
    public void ImportedMapRunningPermissionWinsOverHeldInputAndShoes()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=At("map.players-house-1f",3,4,true);
        Assert.IsFalse(catalog.AllowsRunning(state.World.Player.MapId));
        Assert.AreEqual(16,CampaignFieldMovementTiming.Resolve(state,new("move",Direction.North){RunHeld=true},catalog).Frames);
    }
    [TestMethod]
    [DataRow(false,false,Direction.North,17,23,"walk_slow_up")]
    [DataRow(true,false,Direction.North,17,11,"player_run_up_slow")]
    [DataRow(false,true,Direction.North,17,8,"walk_fast_up")]
    [DataRow(false,false,Direction.South,16,23,"walk_slow_down")]
    [DataRow(true,false,Direction.South,16,11,"player_run_down_slow")]
    [DataRow(false,true,Direction.South,16,8,"walk_fast_down")]
    [DataRow(false,false,Direction.East,17,16,"walk_right")]
    public void AuthoredRockStairsUseCurrentNorthAndDestinationSouth(bool run,bool bike,Direction direction,int y,int frames,string action)
    {
        // Source Route24 (10,17) is MB_ROCK_STAIRS, rather than an invented save edit.
        var catalog=CurrentCampaignFixture.Catalog;var state=At("map.route-24",10,y,run,bike);
        Assert.AreEqual(0x2a,catalog.Maps[state.World.Player.MapId].SourceBehaviorAt(10,17));
        var timing=CampaignFieldMovementTiming.Resolve(state,new("move",direction){RunHeld=run},catalog);
        Assert.AreEqual(frames,timing.Frames);Assert.AreEqual(action,timing.SourceAction);
    }
    [TestMethod]
    [DataRow(false,Direction.South,false,4)]
    [DataRow(false,Direction.North,false,16)]
    [DataRow(false,Direction.South,true,16)]
    [DataRow(true,Direction.North,false,1)]
    public void CyclingRoadReusesAuthoritativeSlopeTransition(bool slope,Direction direction,bool brake,int frames)
    {
        var state=At("map.route-17",2,0,bike:true) with{BicycleSlope=slope};
        state=state with{World=state.World with{Player=state.World.Player with{Facing=Direction.South}}};
        var command=new CampaignCommand("cycle_input",direction,Choice:brake?1:0);
        Assert.AreEqual(frames,CampaignFieldMovementTiming.Resolve(state,command,CurrentCampaignFixture.Catalog).Frames);
    }
    [TestMethod]
    [DataRow("ride_water_current_right",6,"0,2,5,8,10,13,16")]
    [DataRow("walk_slow_up",23,"0,1,2,2,3,4,4,5,6,6,7,8,8,9,10,10,11,12,12,13,14,14,15,16")]
    [DataRow("player_run_up_slow",11,"0,1,3,4,6,7,9,10,12,13,15,16")]
    public void NonuniformSourcePixelVectorsAreSharedWithRecap(string action,int duration,string expected)
    {
        int[] pixels=expected.Split(',').Select(int.Parse).ToArray();
        for(int frame=0;frame<pixels.Length;frame++)Assert.AreEqual(pixels[frame]/16d,CampaignFieldMovementTiming.Fraction(action,duration,frame),.00001);
    }
    [TestMethod]
    public void SourceRunningDisallowedTileAndUnknownBehaviorCannotRun()
    {
        Assert.IsTrue(CampaignFieldMovementTiming.RunningAllowed(true,true,true,0));
        Assert.IsFalse(CampaignFieldMovementTiming.RunningAllowed(true,true,true,0x0a));
        Assert.IsFalse(CampaignFieldMovementTiming.RunningAllowed(true,true,true,null));
    }
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void AcceptedLedgeOutcomeOverridesWalkOrBikeSpeed(bool bike)
    {
        var catalog=CurrentCampaignFixture.Catalog;var before=At("map.route-1",2,4,true,bike);
        Assert.AreEqual(0x3b,catalog.Maps[before.World.Player.MapId].SourceBehaviorAt(2,5));
        var after=before with{World=before.World with{Player=before.World.Player with{Y=6}}};
        var timing=CampaignFieldMovementTiming.Resolve(before,new("move",Direction.South){RunHeld=true},catalog,after);
        Assert.AreEqual(32,timing.Frames);Assert.AreEqual("jump_2_down",timing.SourceAction);
        // A plain single-tile result does not inherit ledge timing from the front tile.
        after=after with{World=after.World with{Player=after.World.Player with{Y=5}}};
        Assert.AreEqual(bike?6:8,CampaignFieldMovementTiming.Resolve(before,new("move",Direction.South){RunHeld=true},catalog,after).Frames);
    }
    [TestMethod]
    [DataRow(false,6,"ride_water_current_down")]
    [DataRow(true,8,"walk_fast_down")]
    public void BicycleUsesDestinationCrackedIceOverride(bool cracked,int frames,string action)
    {
        var catalog=CurrentCampaignFixture.Catalog;var before=At("map.four-island-icefall-cave-1f",8,2,bike:true);
        Assert.IsTrue(catalog.AllowsCycling(before.World.Player.MapId));
        Assert.AreEqual(FourIslandFieldSource.ThinIce,catalog.Maps[before.World.Player.MapId].SourceBehaviorAt(8,3));
        // The original ice callback changes this authored thin tile to the imported cracked metatile.
        if(cracked)before=before with{MapEdits=ImmutableSortedDictionary<string,FieldMetatileEdit>.Empty.Add("8:3",new(8,3,IcefallIceRules.CrackedIceMetatile,0,3))};
        var command=new CampaignCommand("move",Direction.South);
        var after=before with{World=before.World with{Player=before.World.Player with{Y=3,Facing=Direction.South}}};
        var timing=CampaignFieldMovementTiming.Resolve(before,command,catalog,after);
        Assert.AreEqual(frames,timing.Frames);Assert.AreEqual(action,timing.SourceAction);
        CampaignQuestLog log=new([CampaignQuestLogRecorder.SavedScene(before,catalog.QuestLogSource)]);
        log=CampaignQuestLogRecorder.Record(log,before,after,command,catalog)!;
        Assert.AreEqual(frames,log.Scenes[0].Actions[^1].Frames);Assert.AreEqual(action,log.Scenes[0].Actions[^1].SourceAction);
    }
    [TestMethod]
    public void RecorderKeepsAcceptedRunTimingAndPlaybackCannotChangeState()
    {
        var catalog=CurrentCampaignFixture.Catalog;var before=At("map.pallet-town",6,8,true);
        var arrival=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["PALLET TOWN"]};
        var log=CampaignQuestLogRecorder.Record(null,before,before,new("move"),catalog,suppliedEvent:arrival)!;
        var after=before with{World=before.World with{Player=before.World.Player with{X=7,Facing=Direction.East}}};
        string unchanged=JsonSerializer.Serialize(after);
        log=CampaignQuestLogRecorder.Record(log,before,after,new("move",Direction.East){RunHeld=true},catalog)!;
        Assert.AreEqual(8,log.Scenes[0].Actions[^1].Frames);Assert.AreEqual("player_run_right",log.Scenes[0].Actions[^1].SourceAction);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log,catalog.QuestLogSource,catalog));
        var playback=new CampaignQuestLogPlayback(log,"RED",catalog.QuestLogSource);playback.Tick(4d/60);
        Assert.AreEqual(.5,playback.Movement(CampaignQuestLogRecorder.PlayerId)!.Value.Fraction,.00001);
        playback.Skip();Assert.AreEqual(unchanged,JsonSerializer.Serialize(after));
    }
    [TestMethod]
    public void RunInputIsDefaultOmittedAndDoesNotChangeAcceptedGameplayOrRng()
    {
        Assert.IsFalse(JsonSerializer.Serialize(new CampaignCommand("move")).Contains("RunHeld"));
        var catalog=CurrentCampaignFixture.Catalog;var walk=CampaignSession.CreateCurrent(catalog);var run=CampaignSession.CreateCurrent(catalog);
        foreach(var direction in new[]{Direction.East,Direction.North,Direction.South})
        {
            string id="timing:"+walk.Revision;
            var a=walk.Execute(id,walk.Revision,new("move",direction));var b=run.Execute(id,run.Revision,new("move",direction){RunHeld=true});
            Assert.AreEqual(CommandStatus.Accepted,a.Status,a.Error);Assert.AreEqual(a.Status,b.Status);Assert.AreEqual(a.Error,b.Error);
            Assert.AreEqual(JsonSerializer.Serialize(a.State with{QuestLog=null}),JsonSerializer.Serialize(b.State with{QuestLog=null}));
        }
    }
}
