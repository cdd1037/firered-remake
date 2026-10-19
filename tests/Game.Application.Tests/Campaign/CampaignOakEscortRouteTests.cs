using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CampaignOakEscortRouteTests
{
    [TestMethod]
    [DataRow(12)]
    [DataRow(13)]
    public void FreshGameEscortUsesSourceArrivalAndReachesSaveableStarterChoice(int triggerX)
    {
        var catalog=CurrentCampaignFixture.Catalog;
        var session=CampaignSession.CreateCurrent(catalog);
        int sequence=0; CampaignState? arrival=null;
        void Run(CampaignCommand command)
        {
            var origin=session.State;
            var result=session.Execute("oak-escort:"+(++sequence),session.Revision,command);
            Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);
            if(origin.World.Player.MapId.Value=="map.pallet-town"&&session.State.World.Player.MapId.Value=="map.professor-oaks-lab")
            {
                Assert.IsFalse(origin.PlayerVisible,"The source entry movement hides the player before warping.");
                var receipt=session.State.FieldTransition;
                Assert.IsNotNull(receipt);
                Assert.IsFalse(origin.Script.Flags.GetValueOrDefault("FLAG_DONT_TRANSITION_MUSIC"));
                Assert.AreEqual(true,receipt.SuppressMusicFade,"The flag is set inside this final ACK, before warp.");
                Assert.IsTrue(session.TryGetFieldTransitionArrival(origin,receipt,out var captured),"The genuine escort warp must expose its arrival before the lab scene.");
                Assert.IsFalse(session.TryGetFieldTransitionArrival(origin with{},receipt,out _),"Only the accepted command owns this transition.");
                Assert.IsTrue(captured.PlayerVisible);
                Assert.AreEqual("MUS_OAK",captured.SavedMusic);
                Assert.IsTrue(captured.Script.Flags.GetValueOrDefault("FLAG_DONT_TRANSITION_MUSIC"));
                Assert.AreEqual(captured.World.Player,receipt.To);
                Assert.AreEqual(1,captured.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB"));
                Assert.IsTrue(captured.EntryPrograms.IsEmpty);
                Assert.IsTrue(captured.Script.Stack.IsEmpty);
                Assert.IsTrue(captured.Motions.IsEmpty);
                arrival=captured;
                Assert.AreEqual(6,captured.World.Player.X);Assert.AreEqual(12,captured.World.Player.Y);
                Assert.AreEqual(Direction.North,captured.World.Player.Facing,"The destination OnWarp explicitly turns the player north.");
            }
        }
        void Drain(){for(int i=0;session.State.Script.Wait is not null;i++){Assert.IsTrue(i<150);Run(new("ack",Choice:1));}}
        void WalkTo(int x,int y)
        {
            var start=session.State.World.Player;var map=catalog.Maps[start.MapId];
            var queue=new Queue<(int X,int Y)>();var seen=new HashSet<(int,int)>{(start.X,start.Y)};
            var previous=new Dictionary<(int,int),((int X,int Y) From,Direction Direction)>();queue.Enqueue((start.X,start.Y));
            while(queue.TryDequeue(out var at))
            {
                if(at==(x,y))break;
                foreach(var(dx,dy,d)in new[]{(0,-1,Direction.North),(1,0,Direction.East),(0,1,Direction.South),(-1,0,Direction.West)})
                {
                    var next=(at.X+dx,at.Y+dy);
                    if(seen.Contains(next)||map.GetTile(next.Item1,next.Item2)is not{Blocked:false}||session.State.Actors.Values.Any(a=>a.Active&&a.X==next.Item1&&a.Y==next.Item2))continue;
                    seen.Add(next);previous[next]=(at,d);queue.Enqueue(next);
                }
            }
            Assert.IsTrue(seen.Contains((x,y)));var path=new List<Direction>();var cursor=(x,y);
            while(cursor!=(start.X,start.Y)){var step=previous[cursor];path.Add(step.Direction);cursor=step.From;}path.Reverse();
            foreach(var direction in path){Run(new("move",direction));Drain();}
        }
        Drain();WalkTo(10,2);Run(new("move",Direction.West));Drain();Run(new("move",Direction.West));Drain();
        WalkTo(4,8);Run(new("move",Direction.South));Drain();Run(new("move",Direction.South));Drain();
        Assert.AreEqual("map.pallet-town",session.State.World.Player.MapId.Value);
        WalkTo(triggerX,2);Run(new("move",Direction.North));
        Assert.AreEqual(triggerX-12,session.State.Script.Variables.GetValueOrDefault("VAR_TEMP_1"));
        Assert.IsNull(session.State.FieldTransition);
        Assert.IsNotNull(session.ValidatePlayerSave(session.State));
        var blocked=session.Execute("oak-blocked:"+(++sequence),session.Revision,new("move",Direction.South));
        Assert.AreEqual(CommandStatus.Rejected,blocked.Status);
        Drain();
        Assert.IsNotNull(arrival);
        Assert.AreEqual("map.professor-oaks-lab",session.State.World.Player.MapId.Value);
        Assert.AreEqual(6,session.State.World.Player.X);Assert.AreEqual(4,session.State.World.Player.Y);
        Assert.AreEqual(Direction.North,session.State.World.Player.Facing);
        Assert.AreEqual(2,session.State.Script.Variables.GetValueOrDefault("VAR_MAP_SCENE_PALLET_TOWN_PROFESSOR_OAKS_LAB"));
        Assert.IsFalse(session.State.Script.Locked);Assert.IsNull(session.State.Script.Wait);
        Assert.IsTrue(session.State.Party.IsEmpty);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_VISITED_OAKS_LAB"));
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_HIDE_OAK_IN_PALLET_TOWN"));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault("FLAG_HIDE_OAK_IN_HIS_LAB"));
        Assert.IsFalse(session.State.Script.Flags.GetValueOrDefault("FLAG_DONT_TRANSITION_MUSIC"));
        var saved=session.PrepareFieldSave();Assert.IsNull(session.ValidatePlayerSave(saved));
        var bytes=SaveCodec.Encode(saved,session.Revision,session.RulesVersion,catalog.ContentVersion);
        var decoded=SaveCodec.Decode<CampaignState>(bytes,session.RulesVersion,catalog.ContentVersion,validate:session.ValidatePlayerSave);
        var restored=CampaignSession.RestoreCurrent(catalog,decoded.State,decoded.Generation);
        Assert.AreEqual(saved.World.Player,restored.State.World.Player);
        Assert.IsNull(restored.State.FieldTransition);Assert.IsNull(restored.State.Script.Wait);
    }
}
