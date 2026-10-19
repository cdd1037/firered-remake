using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.World;

namespace FireRed.Tests.Domain;

[TestClass, TestCategory("Game")]
public sealed class ConnectedFieldDestinationTests
{
    private static readonly MapId Source = new("map.source"), Target = new("map.target");
    private static ConnectedFieldMap Map(MapId id,int width,int height,ImmutableArray<ConnectedFieldConnection> connections,int behavior=0,int elevation=3)=>
        new(id,width,height,Enumerable.Repeat(new ConnectedFieldCell(behavior,0,elevation),width*height).ToImmutableArray(),connections);

    [TestMethod]
    [DataRow(Direction.North,3,0,2,5)]
    [DataRow(Direction.South,3,3,2,0)]
    [DataRow(Direction.West,0,3,4,2)]
    [DataRow(Direction.East,3,3,0,2)]
    public void CardinalOffsetResolutionIsTheSameAuthorityAsWalking(Direction direction,int x,int y,int targetX,int targetY)
    {
        var maps=new Dictionary<MapId,ConnectedFieldMap>{[Source]=Map(Source,4,4,[new(direction,1,Target)]),[Target]=Map(Target,5,6,[])};
        var origin=new TilePosition(Source,x,y,3,direction);
        Assert.IsNull(ConnectedFieldRules.ResolveDestination(origin,direction,maps,out var resolved));
        Assert.IsNotNull(resolved);Assert.IsTrue(resolved.MapChanged);
        Assert.AreEqual((Target,targetX,targetY),(resolved.MapId,resolved.X,resolved.Y));
        var world=new WorldSnapshot(ConnectedFieldRules.RulesVersion,7,43,origin,new(123));
        var completed=ConnectedFieldRules.Complete(world,new(new("resolved:step"),7,ConnectedFieldAction.Walk,direction),maps);
        Assert.AreEqual(TransitionStatus.Accepted,completed.Status,completed.Diagnostic?.Code);
        Assert.AreEqual(new TilePosition(Target,targetX,targetY,3,direction),completed.State.Player);
        Assert.AreEqual(44L,completed.State.StepCount);Assert.AreEqual(world.Rng,completed.State.Rng);
    }

    [TestMethod]
    public void SegmentedConnectionKeepsFirstSourceOrderSelectionAndStrictAperture()
    {
        MapId second=new("map.second");
        var maps=new Dictionary<MapId,ConnectedFieldMap>{[Source]=Map(Source,8,4,[new(Direction.North,0,Target),new(Direction.North,4,second)]),
            [Target]=Map(Target,4,6,[]),[second]=Map(second,4,7,[])};
        var atBoundary=new TilePosition(Source,4,0,3,Direction.North);
        Assert.AreEqual("connection-aperture-outside",ConnectedFieldRules.ResolveDestination(atBoundary,Direction.North,maps,out var blocked)?.Code);
        Assert.IsNull(blocked,"The source first inclusive span must not fall through to the next map.");
        var origin=atBoundary with{X=5};
        Assert.IsNull(ConnectedFieldRules.ResolveDestination(origin,Direction.North,maps,out var resolved));
        Assert.AreEqual((second,1,6),(resolved!.MapId,resolved.X,resolved.Y));
        var world=new WorldSnapshot(ConnectedFieldRules.RulesVersion,0,0,atBoundary,new(55));
        Assert.AreEqual("connection-aperture-outside",ConnectedFieldRules.Complete(world,new(new("resolved:aperture"),0,ConnectedFieldAction.Walk,Direction.North),maps).Diagnostic?.Code);
    }

    [TestMethod]
    public void ResolutionDoesNotBypassWaterOrShoreModeAndNeverClampsMissingConnections()
    {
        var maps=new Dictionary<MapId,ConnectedFieldMap>{[Source]=Map(Source,4,4,[new(Direction.North,1,Target)]),[Target]=Map(Target,5,6,[new(Direction.South,-1,Source)],0x15,1)};
        var shore=new TilePosition(Source,2,0,3,Direction.North);
        Assert.IsNull(ConnectedFieldRules.ResolveDestination(shore,Direction.North,maps,out var water));
        Assert.AreEqual((Target,1,5,1,0x15),(water!.MapId,water.X,water.Y,water.Cell.Elevation,water.Cell.SourceBehavior));
        var world=new WorldSnapshot(ConnectedFieldRules.RulesVersion,0,91,shore,new(55));
        var ordinary=ConnectedFieldRules.Complete(world,new(new("resolved:water"),0,ConnectedFieldAction.Walk,Direction.North),maps);
        Assert.AreEqual("unsupported-water-movement",ordinary.Diagnostic?.Code);Assert.AreSame(world,ordinary.State);
        var riding=world with{Player=new(Target,1,5,1,Direction.South)};
        var reverse=ConnectedFieldRules.Complete(riding,new(new("resolved:shore"),0,ConnectedFieldAction.Walk,Direction.South),maps,surfing:true,sourceTerrain:(_,_,_)=>2);
        Assert.AreEqual("surf-connection-dismount-not-supported",reverse.Diagnostic?.Code,"Only the separate correlated Surf completion may dismount.");
        Assert.AreSame(riding,reverse.State);
        maps.Remove(Target);
        Assert.AreEqual("connection-destination-missing",ConnectedFieldRules.ResolveDestination(shore,Direction.North,maps,out _)?.Code);
        maps[Source]=maps[Source] with{Connections=[]};
        Assert.AreEqual("unloaded-field-exit",ConnectedFieldRules.ResolveDestination(shore,Direction.North,maps,out _)?.Code);
    }
}
