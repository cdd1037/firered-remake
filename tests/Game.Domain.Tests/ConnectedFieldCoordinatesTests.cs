using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;
namespace FireRed.Tests.Domain;
[TestClass,TestCategory("Game")]
public sealed class ConnectedFieldCoordinatesTests
{
    [TestMethod]
    public void SourceBorderCoordinatesUseOffsetsAndNeverResolveDistantMaps()
    {
        MapId a=new("map.a"),b=new("map.b");
        var maps=new Dictionary<MapId,ConnectedFieldMap>{[a]=new(a,20,30,[],[new(Direction.West,4,b)]),[b]=new(b,10,12,[],[new(Direction.East,-4,a)])};
        Assert.AreEqual((b,9,0),ConnectedFieldCoordinates.Resolve(maps,a,-1,4)!.Value);
        Assert.AreEqual((b,3,7),ConnectedFieldCoordinates.Resolve(maps,a,-7,11)!.Value);
        Assert.AreEqual((a,0,4),ConnectedFieldCoordinates.Resolve(maps,b,10,0)!.Value);
        Assert.IsNull(ConnectedFieldCoordinates.Resolve(maps,a,-8,4));
        Assert.IsNull(ConnectedFieldCoordinates.Resolve(maps,a,-1,3));
        Assert.AreEqual((a,19,29),ConnectedFieldCoordinates.Resolve(maps,a,19,29)!.Value);
    }
}
