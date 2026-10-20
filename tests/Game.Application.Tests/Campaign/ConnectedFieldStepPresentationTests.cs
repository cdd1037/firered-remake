using Game.Application;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class ConnectedFieldStepPresentationTests
{
    private static (TilePosition From,TilePosition To,FieldTransitionReceipt Receipt,
        Dictionary<MapId,ConnectedFieldMap> Maps,(int X,int Y) Origin) Crossing(Direction direction,int offset)
    {
        MapId sourceId=new("map.source"),destinationId=new("map.destination");
        // Both maps are smaller than 240x160, and have unequal dimensions.
        var connection=new ConnectedFieldConnection(direction,offset,destinationId);
        var source=new ConnectedFieldMap(sourceId,7,9,[],[connection]);
        var destination=new ConnectedFieldMap(destinationId,11,6,[],[]);
        var origin=ConnectedFieldCoordinates.Origin(source,destination,connection);
        var (x,y)=direction switch
        {
            Direction.North=>(3,0),Direction.South=>(3,8),
            Direction.West=>(0,3),_=>(6,3)
        };
        var (dx,dy)=Delta(direction);
        var from=new TilePosition(sourceId,x,y,3,direction);
        var to=new TilePosition(destinationId,x+dx-origin.X,y+dy-origin.Y,3,direction);
        return(from,to,new(FieldTransitionCause.ConnectedEdge,FieldTransitionDeparture.Immediate,
            FieldTransitionArrival.Plain,from,to,to),new(){{sourceId,source},{destinationId,destination}},origin);
    }
    private static (int X,int Y) Delta(Direction direction)=>direction switch
    {
        Direction.North=>(0,-1),Direction.South=>(0,1),Direction.West=>(-1,0),_=>(1,0)
    };

    [TestMethod]
    [DataRow(Direction.North,-2)]
    [DataRow(Direction.North,2)]
    [DataRow(Direction.South,-2)]
    [DataRow(Direction.South,2)]
    [DataRow(Direction.West,-2)]
    [DataRow(Direction.West,2)]
    [DataRow(Direction.East,-2)]
    [DataRow(Direction.East,2)]
    public void ConnectionRebasesOldPoseWithoutMovingCameraOrMapPixels(Direction direction,int offset)
    {
        var c=Crossing(direction,offset);
        var rebased=CampaignFieldMovementTiming.ConnectedStepOrigin(c.From,c.To,c.Receipt,c.Maps);
        Assert.IsNotNull(rebased);
        var (dx,dy)=Delta(direction);
        Assert.AreEqual((c.To.X-dx,c.To.Y-dy),rebased.Value);
        // Source field_camera.c retains the sub-tile camera movement while
        // fieldmap.c SetPositionFromConnection changes the map coordinate basis.
        int oldCameraX=c.From.X*16+8-120,oldCameraY=c.From.Y*16+8-80;
        int newCameraX=rebased.Value.X*16+8-120,newCameraY=rebased.Value.Y*16+8-80;
        Assert.AreEqual(c.Origin.X*16-oldCameraX,-newCameraX,"destination map left edge");
        Assert.AreEqual(c.Origin.Y*16-oldCameraY,-newCameraY,"destination map top edge");
        Assert.AreEqual(c.From.X*16-oldCameraX,rebased.Value.X*16-newCameraX,"player screen X");
        Assert.AreEqual(c.From.Y*16-oldCameraY,rebased.Value.Y*16-newCameraY,"player screen Y");
    }

    [TestMethod]
    [DataRow("walk_right",16,1)]
    [DataRow("player_run_right",8,2)]
    [DataRow("walk_fast_right",8,2)]
    [DataRow("ride_water_current_right",6,3)]
    public void CrossingUsesExistingWalkingRunningSurfingAndCyclingPixelCadence(string action,int duration,int maxPixels)
    {
        var c=Crossing(Direction.East,2);
        var from=CampaignFieldMovementTiming.ConnectedStepOrigin(c.From,c.To,c.Receipt,c.Maps)!.Value;
        Assert.AreEqual(-1,from.X);
        double previous=from.X*16;
        for(int frame=0;frame<=duration;frame++)
        {
            double fraction=CampaignFieldMovementTiming.Sample(action,duration,frame).Fraction;
            double pixel=from.X*16+(c.To.X-from.X)*16*fraction;
            Assert.IsTrue(pixel>=previous&&pixel-previous<=maxPixels+1e-7,$"frame {frame}: {previous} -> {pixel}");
            previous=pixel;
        }
        Assert.AreEqual(c.To.X*16d,previous);
    }

    [TestMethod]
    public void WarpStaleReceiptWrongDestinationAndUnknownConnectionCannotInterpolate()
    {
        var c=Crossing(Direction.North,2);
        Assert.IsNull(CampaignFieldMovementTiming.ConnectedStepOrigin(c.From,c.To,null,c.Maps));
        Assert.IsNull(CampaignFieldMovementTiming.ConnectedStepOrigin(c.From,c.To,c.Receipt,null));
        foreach(var cause in new[]{FieldTransitionCause.Unknown,FieldTransitionCause.PlainWarp,
            FieldTransitionCause.AnimatedDoor,FieldTransitionCause.NonAnimatedDoor,FieldTransitionCause.StairWarp})
            Assert.IsNull(CampaignFieldMovementTiming.ConnectedStepOrigin(c.From,c.To,c.Receipt with{Cause=cause},c.Maps));
        Assert.IsNull(CampaignFieldMovementTiming.ConnectedStepOrigin(c.From with{X=c.From.X+1},c.To,c.Receipt,c.Maps));
        var wrong=c.To with{X=c.To.X+1};
        Assert.IsNull(CampaignFieldMovementTiming.ConnectedStepOrigin(c.From,wrong,c.Receipt with{To=wrong},c.Maps));
        c.Maps[c.From.MapId]=c.Maps[c.From.MapId] with{Connections=[]};
        Assert.IsNull(CampaignFieldMovementTiming.ConnectedStepOrigin(c.From,c.To,c.Receipt,c.Maps));
    }

    [TestMethod]
    [DataRow(FieldSurfSourcePresentation.MountJump)]
    [DataRow(FieldSurfSourcePresentation.DismountJump)]
    public void CompletedConnectedSurfJumpCannotRestartAsAnOrdinaryStep(string phase)
    {
        var c=Crossing(Direction.North,2);
        var basis=CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog).State;
        var before=basis with
        {
            World=basis.World with{Player=c.From},
            SurfEffect=new(-1,0,0,"",c.From,c.To,0,phase,"controlled-jump")
        };
        var after=before with{World=before.World with{Player=c.To},SurfEffect=null,Motions=[],FieldTransition=c.Receipt};
        Assert.IsNull(CampaignFieldMovementTiming.ConnectedStepOrigin(before,after,c.Maps));
        Assert.IsNotNull(CampaignFieldMovementTiming.ConnectedStepOrigin(before with{SurfEffect=null},after,c.Maps));
    }

    [TestMethod]
    public void AuthoredArrivalMotionRetainsItsOwnOriginInsteadOfReceivingAnExtraStep()
    {
        var c=Crossing(Direction.South,-2);
        var basis=CampaignSession.CreateCurrent(CurrentCampaignFixture.Catalog).State;
        var before=basis with{World=basis.World with{Player=c.From}};
        var after=basis with
        {
            World=basis.World with{Player=c.To},FieldTransition=c.Receipt,
            Motions=[new("LOCALID_PLAYER",[c.To,c.To with{Y=c.To.Y+1}],["walk_down","step_end"])]
        };
        Assert.IsNull(CampaignFieldMovementTiming.ConnectedStepOrigin(before,after,c.Maps));
    }

    [TestMethod]
    public void EveryLoadedSourceConnectionKeepsItsOwnOffsetAndDimensions()
    {
        var maps=CurrentCampaignFixture.Catalog.ConnectedFieldMaps!;
        int checkedConnections=0;
        foreach(var map in maps.Values)
            foreach(var connection in map.Connections)
            {
                if(!maps.TryGetValue(connection.DestinationMapId,out var destination))continue;
                var origin=ConnectedFieldCoordinates.Origin(map,destination,connection);
                var (dx,dy)=Delta(connection.Direction);
                int span=dx==0?map.Width:map.Height;
                bool tested=false;
                for(int n=0;n<span;n++)
                {
                    int x=dx==0?n:dx>0?map.Width-1:0,y=dy==0?n:dy>0?map.Height-1:0;
                    var target=ConnectedFieldCoordinates.Resolve(maps,map.Id,x+dx,y+dy);
                    if(target?.Map!=destination.Id)continue;
                    var from=new TilePosition(map.Id,x,y,3,connection.Direction);
                    var to=new TilePosition(destination.Id,target.Value.X,target.Value.Y,3,connection.Direction);
                    var receipt=new FieldTransitionReceipt(FieldTransitionCause.ConnectedEdge,
                        FieldTransitionDeparture.Immediate,FieldTransitionArrival.Plain,from,to,to);
                    Assert.AreEqual((x-origin.X,y-origin.Y),
                        CampaignFieldMovementTiming.ConnectedStepOrigin(from,to,receipt,maps),map.Id.Value+" -> "+destination.Id.Value);
                    tested=true;
                }
                if(tested)checkedConnections++;
            }
        Assert.IsTrue(checkedConnections>0,"The real imported connection set must not be empty.");
    }
}
