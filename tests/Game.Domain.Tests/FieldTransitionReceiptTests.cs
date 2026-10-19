using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Domain;

[TestClass,TestCategory("Game")]
public sealed class FieldTransitionReceiptTests
{
    private sealed class FieldMap(MapId id,ImmutableArray<FieldWarp> warps,Dictionary<(int,int),int> behaviors):IFieldMap
    {
        public MapId Id=>id;public int Width=>4;public int Height=>4;
        public ImmutableArray<FieldWarp> FieldWarps=>warps;
        public int? SourceBehaviorAt(int x,int y)=>x<0||y<0||x>=4||y>=4?null:behaviors.GetValueOrDefault((x,y));
        public TileCell? GetTile(int x,int y)=>SourceBehaviorAt(x,y) is {} behavior?
            new(false,3,behavior is >=0x62 and <=0x65?TileBehavior.ArrowWarp:behavior is >=0x6c and <=0x6f?TileBehavior.StairWarp:behavior is 0x60 or 0x61 or 0x69?TileBehavior.Warp:TileBehavior.Floor):null;
    }

    [TestMethod]
    public void SameMapArrowWarpHasAuthenticCauseWithoutChangingCoordinatesStepsOrRng()
    {
        MapId id=new("map.same");
        var map=new FieldMap(id,[new(0,1,1,3,id,1),new(1,2,2,3,id,0)],new(){[(1,1)]=0x62,[(2,2)]=0x63});
        var before=new WorldSnapshot(RuleVersions.GridWorldV0,0,7,new(id,1,1,3,Direction.East),new(42));
        var result=FieldMovementRules.Move(before,new(new("test:warp"),0,Direction.East),new Dictionary<MapId,IFieldMap>{{id,map}});
        Assert.AreEqual(TransitionStatus.Accepted,result.Status,result.Diagnostic?.Code);Assert.IsTrue(result.MapChanged);
        Assert.AreEqual(new TilePosition(id,2,2,3,Direction.East),result.State.Player);
        Assert.AreEqual(7L,result.State.StepCount);Assert.AreEqual(before.Rng,result.State.Rng);
        Assert.IsNotNull(result.Presentation);Assert.AreEqual(FieldTransitionCause.PlainWarp,result.Presentation.Cause);
        Assert.AreEqual(before.Player,result.Presentation.From);Assert.AreEqual(result.State.Player,result.Presentation.To);
        Assert.AreEqual(FieldTransitionDeparture.Immediate,result.Presentation.Departure);Assert.AreEqual(FieldTransitionArrival.Plain,result.Presentation.Arrival);
    }

    [TestMethod]
    [DataRow(0x69,FieldTransitionCause.AnimatedDoor,FieldTransitionDeparture.Immediate,0L)]
    [DataRow(0x60,FieldTransitionCause.NonAnimatedDoor,FieldTransitionDeparture.StepOntoWarp,1L)]
    [DataRow(0x61,FieldTransitionCause.PlainWarp,FieldTransitionDeparture.StepOntoWarp,1L)]
    public void DoorAndLadderProducerDoNotMasqueradeAsImmediatePlainWarp(int behavior,FieldTransitionCause cause,FieldTransitionDeparture departure,long steps)
    {
        MapId from=new("map.from"),to=new("map.to");
        var source=new FieldMap(from,[new(0,1,0,3,to,0)],new(){[(1,0)]=behavior});
        var destination=new FieldMap(to,[new(0,2,2,3,from,0)],[]);
        var before=new WorldSnapshot(RuleVersions.GridWorldV0,0,0,new(from,1,1,3,Direction.North),new(42));
        var result=FieldMovementRules.Move(before,new(new("test:door"),0,Direction.North),new Dictionary<MapId,IFieldMap>{{from,source},{to,destination}},true,true);
        Assert.AreEqual(TransitionStatus.Accepted,result.Status,result.Diagnostic?.Code);Assert.IsNotNull(result.Presentation);
        Assert.AreEqual(cause,result.Presentation.Cause);Assert.AreEqual(departure,result.Presentation.Departure);
        Assert.AreEqual(steps,result.State.StepCount);Assert.AreEqual(before.Rng,result.State.Rng);
    }

    [TestMethod]
    public void StepWarpRetainsAcceptedDirectionWhenPriorFacingAndArrivalDiffer()
    {
        MapId from=new("map.from"),to=new("map.to");
        var source=new FieldMap(from,[new(0,1,0,3,to,0)],new(){[(1,0)]=0x60});
        var destination=new FieldMap(to,[new(0,2,2,3,from,0)],[]);
        var before=new WorldSnapshot(RuleVersions.GridWorldV0,0,0,new(from,1,1,3,Direction.East),new(42));
        var result=FieldMovementRules.Move(before,new(new("test:step-direction"),0,Direction.North),
            new Dictionary<MapId,IFieldMap>{{from,source},{to,destination}},true,true);
        Assert.AreEqual(TransitionStatus.Accepted,result.Status,result.Diagnostic?.Code);
        Assert.IsNotNull(result.Presentation);
        Assert.AreEqual(new TilePosition(from,1,0,3,Direction.North),result.Presentation.DepartureStep);
        Assert.AreEqual(Direction.East,result.Presentation.From.Facing);
        Assert.AreEqual(Direction.South,result.Presentation.To.Facing);
        Assert.AreEqual(1L,result.State.StepCount);
        Assert.AreEqual(before.Rng,result.State.Rng);
    }

    [TestMethod]
    [DataRow(0x00,FieldTransitionArrival.Plain)]
    [DataRow(0x69,FieldTransitionArrival.AnimatedDoor)]
    [DataRow(0x60,FieldTransitionArrival.NonAnimatedDoor)]
    [DataRow(0x6c,FieldTransitionArrival.Stair)]
    [DataRow(0x6f,FieldTransitionArrival.Stair)]
    public void StaticArrivalUsesTheSharedResolvedDestinationClassification(int behavior,FieldTransitionArrival arrival)
    {
        MapId from=new("map.from"),to=new("map.to");
        var source=new FieldMap(from,[new(0,1,1,3,to,0)],new(){[(1,1)]=0x65});
        var destination=new FieldMap(to,[new(0,2,1,0,from,0)],new(){[(2,1)]=behavior});
        var before=new WorldSnapshot(RuleVersions.GridWorldV0,0,7,new(from,1,1,3,Direction.South),new(42));
        var result=FieldMovementRules.Move(before,new(new("test:arrival"),0,Direction.South),
            new Dictionary<MapId,IFieldMap>{{from,source},{to,destination}},true,true);
        Assert.AreEqual(TransitionStatus.Accepted,result.Status,result.Diagnostic?.Code);
        Assert.IsNotNull(result.Presentation);
        Assert.AreEqual(arrival,result.Presentation.Arrival);
        Assert.AreEqual(arrival==FieldTransitionArrival.Stair?(int?)behavior:null,result.Presentation.ArrivalStairBehavior);
        Assert.IsNull(result.Presentation.DepartureStairBehavior);
        Assert.AreEqual(arrival,FieldTransitionReceipt.ArrivalAt(behavior));
        Assert.AreEqual(1,result.Presentation.ArrivalAnchor.Y);
        Assert.AreEqual(0,result.Presentation.ArrivalAnchor.Elevation);
        Assert.AreEqual(behavior is 0x60 or 0x69?2:1,result.State.Player.Y);
        Assert.AreEqual(before.StepCount,result.State.StepCount);
        Assert.AreEqual(before.Rng,result.State.Rng);
    }

    [TestMethod]
    public void ConnectedProducerDistinguishesCrossingFromBlockedBoundary()
    {
        MapId from=new("map.from"),to=new("map.to");
        var cells=Enumerable.Repeat(new ConnectedFieldCell(0,0,3),4).ToImmutableArray();
        var a=new ConnectedFieldMap(from,2,2,cells,[new(Direction.East,0,to)]);
        var b=new ConnectedFieldMap(to,2,2,cells,[]);
        var maps=new Dictionary<MapId,ConnectedFieldMap>{{from,a},{to,b}};
        var before=new WorldSnapshot(ConnectedFieldRules.RulesVersion,0,0,new(from,1,1,3,Direction.East),new(42));
        var intent=new ConnectedFieldIntent(new("test:edge"),0,ConnectedFieldAction.Walk,Direction.East);
        var result=ConnectedFieldRules.Complete(before,intent,maps);
        Assert.AreEqual(TransitionStatus.Accepted,result.Status);Assert.IsNotNull(result.Presentation);
        Assert.AreEqual(FieldTransitionCause.ConnectedEdge,result.Presentation.Cause);Assert.AreEqual(new TilePosition(to,0,1,3,Direction.East),result.State.Player);
        Assert.AreEqual(1L,result.State.StepCount);Assert.AreEqual(before.Rng,result.State.Rng);
        maps[to]=b with{Cells=cells.SetItem(2,new(0,1,3))};
        var blocked=ConnectedFieldRules.Complete(before,intent,maps);
        Assert.AreEqual(TransitionStatus.Accepted,blocked.Status);Assert.IsFalse(blocked.MapChanged);Assert.IsNull(blocked.Presentation);
        Assert.AreEqual(before.Player,blocked.State.Player);Assert.AreEqual(0L,blocked.State.StepCount);
    }
}
