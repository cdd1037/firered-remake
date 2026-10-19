using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass, TestCategory("Game")]
public sealed class CampaignSsAnneDepartureRouteTests
{
    [TestMethod]
    public void PublicDepartureFinishesOneCutsceneAndRetainsProgressThroughContinue()
    {
        var catalog=CurrentCampaignFixture.Catalog;
        var session=CampaignSession.CreateCurrent(catalog);
        var basis=CurrentCampaignFixture.Checkpoint.State;
        var map=catalog.Maps[new("map.ssanne-1f-corridor")];
        // Controlled pre-departure placement, as in the existing ship NPC tests.
        // Preserve the actual rival victory and HM ownership; no battle, warp,
        // cutscene effect, destination or departure result is injected.
        var state=basis with
        {
            World=basis.World with{Player=new(map.Id,19,2,map.GetTile(19,2)!.Elevation,Direction.North)},
            Script=basis.Script with{Completed=true,Locked=false,Wait=null,Stack=[],
                Flags=basis.Script.Flags.Remove("FLAG_HIDE_SS_ANNE").SetItem("FLAG_GOT_SS_TICKET",true),
                Variables=basis.Script.Variables.SetItem("VAR_MAP_SCENE_VERMILION_CITY",1)},
            EntryPrograms=[],ArrivalFacing=null,Dialogue=null,Motions=[],PresentationTicks=0,PresentationEffects=[],
            MapLayout=null,MapEdits=null,PlayerVisible=true,IsCycling=false,IsSurfing=false,FieldWeather=null,
            FlashLevel=0,FieldTransition=null,QuestLog=null,Music=catalog.MapDefinition(map.Id).Music
        };
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        state=(CampaignState)typeof(CampaignSession).GetMethod("EnterMap",flags)!.Invoke(session,[state,null,Game.Domain.Roaming.RoamerTransition.Warp])!;
        state=(CampaignState)typeof(CampaignSession).GetMethod("Pump",flags)!.Invoke(session,[state])!;
        Assert.IsNull(session.ValidatePlayerSave(state));
        session=CampaignSession.RestoreCurrent(catalog,state,CurrentCampaignFixture.Checkpoint.Generation);
        int commands=0,departures=0;bool warped=false;
        var inventory=JsonSerializer.SerializeToElement(state.Inventory);
        var history=JsonSerializer.SerializeToElement(state.ForestTrainerHistory);
        void Run(CampaignCommand command)
        {
            var before=session.State;
            var result=session.Execute("ship-route:"+(++commands),session.Revision,command);
            Assert.AreEqual(CommandStatus.Accepted,result.Status,result.Error);
            if(before.World.Player.MapId==map.Id&&session.State.World.Player.MapId.Value==SsAnneSourceDeparture.Exterior)
            {
                var entry=session.State.FieldTransition;Assert.IsNotNull(entry);
                Assert.IsTrue(session.TryGetFieldTransitionArrival(before,entry,out var arrived),
                    "An ordinary door must retain arrival before the destination OnFrame starts.");
                Assert.AreEqual(SsAnneSourceDeparture.Exterior,arrived.World.Player.MapId.Value,
                    "Throwaway preflight must not replace this entry with its future Vermilion warp.");
                Assert.AreEqual((32,14),(arrived.World.Player.X,arrived.World.Player.Y));
                Assert.IsFalse(session.TryGetFieldTransitionArrival(before with{},entry,out _));
                Assert.IsFalse(session.TryGetFieldTransitionArrival(before,entry with{},out _));
                var committed=session.State;long revision=session.Revision;
                Assert.IsTrue(session.TryGetFieldTransitionArrival(before,entry,out var repeated));
                Assert.AreSame(arrived,repeated);Assert.AreSame(committed,session.State);Assert.AreEqual(revision,session.Revision);
                Assert.IsNull(arrived.Script.Wait);Assert.IsTrue(arrived.Motions.IsEmpty);
                Assert.AreEqual(0,arrived.PresentationTicks);
                Assert.IsTrue(arrived.Actors[SsAnneSourceDeparture.Boat].Visible);
                Assert.IsNotNull(session.State.Script.Wait);
            }
            foreach(var effect in session.State.PresentationEffects.Where(e=>e.Opcode=="ssanne_departure"))
            {
                departures++;
                Assert.AreEqual("88",effect.Args[0]);
                Assert.AreEqual(SsAnneSourceDeparture.TotalFrames(88).ToString(System.Globalization.CultureInfo.InvariantCulture),effect.Args[1]);
                Assert.AreEqual(1136,session.State.PresentationTicks);
                Assert.AreEqual("MUS_SURF",session.State.Music);
                Assert.IsTrue(session.State.Script.Locked);
            }
            if(before.World.Player.MapId.Value==SsAnneSourceDeparture.Exterior&&session.State.World.Player.MapId.Value=="map.vermilion-city")
            {
                warped=true;var receipt=session.State.FieldTransition;Assert.IsNotNull(receipt);
                Assert.AreEqual(FieldTransitionCause.PlainWarp,receipt.Cause);
                Assert.AreEqual((23,34),(receipt.To.X,receipt.To.Y));
                Assert.AreEqual((23,32),(session.State.World.Player.X,session.State.World.Player.Y));
                Assert.AreEqual(before.World.Revision,session.State.World.Revision);
                Assert.AreEqual(before.World.StepCount,session.State.World.StepCount);
                Assert.IsTrue(session.State.Script.Locked);
                Assert.IsNotNull(session.ValidatePlayerSave(session.State));
            }
        }
        Run(new("move",Direction.North));
        Assert.AreEqual(SsAnneSourceDeparture.Exterior,session.State.World.Player.MapId.Value);
        for(int i=0;session.State.Script.Wait is not null;i++)
        {
            Assert.IsTrue(i<40,"The source departure and destination movement must terminate.");
            Run(new("ack",Choice:1));
        }
        Assert.IsTrue(warped);Assert.AreEqual(1,departures);
        Assert.AreEqual(3,session.State.Script.Variables["VAR_MAP_SCENE_VERMILION_CITY"]);
        Assert.IsTrue(session.State.Script.Flags.GetValueOrDefault("FLAG_HIDE_SS_ANNE"));
        Assert.IsFalse(session.State.Script.Locked);Assert.IsTrue(session.State.Script.Completed);
        Assert.AreEqual(Direction.North,session.State.World.Player.Facing);
        Assert.IsTrue(JsonElement.DeepEquals(inventory,JsonSerializer.SerializeToElement(session.State.Inventory)));
        Assert.IsTrue(JsonElement.DeepEquals(history,JsonSerializer.SerializeToElement(session.State.ForestTrainerHistory)));
        var saved=session.PrepareFieldSave();Assert.IsNull(session.ValidatePlayerSave(saved));
        var bytes=SaveCodec.Encode(saved,session.Revision,session.RulesVersion,catalog.ContentVersion);
        var decoded=SaveCodec.Decode<CampaignState>(bytes,session.RulesVersion,catalog.ContentVersion,validate:session.ValidatePlayerSave);
        var restored=CampaignSession.RestoreCurrent(catalog,decoded.State,decoded.Generation);
        Assert.AreEqual(saved.World.Player,restored.State.World.Player);
        Assert.AreEqual(3,restored.State.Script.Variables["VAR_MAP_SCENE_VERMILION_CITY"]);
        Assert.IsTrue(restored.State.Script.Flags.GetValueOrDefault("FLAG_HIDE_SS_ANNE"));
        Assert.IsNull(restored.State.FieldTransition);Assert.IsNull(restored.State.Script.Wait);
        session=restored;
        Run(new("move",Direction.South));
        for(int i=0;session.State.SeagallopMenu is null;i++)
        {Assert.IsTrue(i<20);Assert.IsNotNull(session.State.Script.Wait);Run(new("ack"));}
        Assert.AreEqual("map.vermilion-city",session.State.World.Player.MapId.Value);
        Run(new("ack",Choice:127));
        for(int i=0;session.State.Script.Wait is not null;i++)
        {Assert.IsTrue(i<20);Run(new("ack",Choice:127));}
        Assert.AreEqual("map.vermilion-city",session.State.World.Player.MapId.Value);
        Assert.AreEqual(3,session.State.Script.Variables["VAR_MAP_SCENE_VERMILION_CITY"]);
        Assert.AreEqual(1,departures);
        Assert.IsNull(session.ValidatePlayerSave(session.PrepareFieldSave()));
    }
}
