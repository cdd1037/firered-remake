using Game.Application;
using Game.Content;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogTimelineTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LocalizedCaptionsKeepRecordedAndPlayerControlLookingTextLiteral(bool translated)
    {
        using var output = TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(output.Path, "source"));
        if (translated) File.WriteAllText(Path.Combine(output.Path, "source/quest-log.json"),
            System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string> {
                ["SwitchMon1WithMon2"] = "{PLAYER} changed the partners\n{STR_VAR_1} and {STR_VAR_2}." }));
        var source = PretQuestLogSource.Load(TestAssets.Root, SourceTextPack.Load(output.Path));
        const string player = "{STR_VAR_1}", first = "{PLAYER}\\n$", second = "{DYNAMIC 0x00}";
        var action = new CampaignQuestLogAction(-1, 0, 0, Direction.South, false, CampaignQuestLogEvent.PartyOrder, 127) {
            TextKey = "SwitchMon1WithMon2", Arguments = [first, second] };
        var log = new CampaignQuestLog([new("map.pallet-town",
            [new(CampaignQuestLogRecorder.PlayerId, "player", 6, 8, Direction.South, true)], [action])]);
        string saved = System.Text.Json.JsonSerializer.Serialize(log);
        var playback = new CampaignQuestLogPlayback(log, player, source);
        Assert.AreEqual(translated ? player + " changed the partners\n" + first + " and " + second + "." :
            player + " switched the POKéMON\n" + first + " with " + second + ".", playback.Caption);
        playback.Tick(10);
        Assert.IsTrue(playback.Finished);
        Assert.AreEqual(saved, System.Text.Json.JsonSerializer.Serialize(log));
    }

    [TestMethod]
    public void CaptionAndTwoActorTracksAdvanceTogetherWithoutMutatingRecording()
    {
        var scene=new CampaignQuestLogScene("map.pallet-town",[
            new(CampaignQuestLogRecorder.PlayerId,"player",1,1,Direction.South,true),
            new("npc","OBJ_EVENT_GFX_MAN",4,4,Direction.South,true)], [
            new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["PALLET TOWN"]},
            new(0,2,1,Direction.East,true,CampaignQuestLogEvent.None,16){StartFrame=0},
            new(1,4,5,Direction.South,true,CampaignQuestLogEvent.None,32){StartFrame=0},
            new(0,3,1,Direction.East,true,CampaignQuestLogEvent.None,16){StartFrame=16}]);
        var log=new CampaignQuestLog([scene]);var original=System.Text.Json.JsonSerializer.Serialize(log);
        var playback=new CampaignQuestLogPlayback(log,"RED",CurrentCampaignFixture.Catalog.QuestLogSource);
        playback.Tick(8d/60);
        Assert.IsNotNull(playback.Caption);Assert.AreEqual(.5,playback.Movement(CampaignQuestLogRecorder.PlayerId)!.Value.Fraction,.001);
        Assert.AreEqual(.25,playback.Movement("npc")!.Value.Fraction,.001);
        playback.Tick(16d/60);
        Assert.AreEqual(2,playback.Movement(CampaignQuestLogRecorder.PlayerId)!.Value.From.X);
        Assert.AreEqual(.75,playback.Movement("npc")!.Value.Fraction,.001);
        playback.Tick(8d/60);Assert.IsNull(playback.Movement("npc"));Assert.IsFalse(playback.Finished);
        playback.Tick(10);Assert.IsTrue(playback.Finished);Assert.AreEqual(original,System.Text.Json.JsonSerializer.Serialize(log));
    }

    [TestMethod]
    public void RecorderPreservesScriptedPathAndIndependentStartFramesWithinBudget()
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var caption=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["PALLET TOWN"]};
        var log=CampaignQuestLogRecorder.Record(null,state,state,new("move"),catalog,suppliedEvent:caption)!;
        var p=state.World.Player;var p1=p with{X=p.X+1,Facing=Direction.East};var p2=p with{X=p.X+2,Facing=Direction.East};
        var after=state with{World=state.World with{Player=p2},Motions=[new(CampaignQuestLogRecorder.PlayerId,[p,p1,p2],["walk_fast_right","delay_8","walk_normal_right","step_end"])]};
        log=CampaignQuestLogRecorder.Record(log,state,after,new("ack"),catalog)!;
        var moves=log.Scenes[0].Actions.Where(a=>a.Event==CampaignQuestLogEvent.None).ToArray();
        Assert.AreEqual(3,moves.Length);CollectionAssert.AreEqual(new[]{0,8,16},moves.Select(a=>a.StartFrame).ToArray());
        CollectionAssert.AreEqual(new[]{8,8,16},moves.Select(a=>a.Frames).ToArray());
        Assert.AreEqual(p1.X,moves[0].X);Assert.AreEqual(p2.X,moves[2].X);
        Assert.AreEqual(2,log.Version);Assert.IsNull(CampaignQuestLogRecorder.Validate(log,catalog.QuestLogSource));
        Assert.IsTrue(CampaignQuestLogRecorder.Budget(log.Scenes[0].Actions,catalog.QuestLogSource)<=256);
    }
    [TestMethod]
    [DataRow("face_right",1)]
    [DataRow("walk_slow_right",23)]
    [DataRow("walk_slower_right",31)]
    [DataRow("walk_slowest_right",160)]
    [DataRow("jump_special_right",32)]
    public void MovementDurationsFollowPinnedStepFunctions(string opcode,int expected)
    {
        var catalog=CurrentCampaignFixture.Catalog;var state=CampaignSession.CreateCurrent(catalog).State;
        var caption=new CampaignQuestLogAction(-1,0,0,Direction.South,false,CampaignQuestLogEvent.Arrived,127){TextKey="ArrivedInLocation",Arguments=["PALLET TOWN"]};
        var log=CampaignQuestLogRecorder.Record(null,state,state,new("move"),catalog,suppliedEvent:caption)!;
        var p=state.World.Player;var next=p with{X=p.X+1,Facing=Direction.East};
        var after=state with{World=state.World with{Player=next},Motions=[new(CampaignQuestLogRecorder.PlayerId,[p,next],[opcode,"step_end"])]};
        log=CampaignQuestLogRecorder.Record(log,state,after,new("ack"),catalog)!;
        Assert.AreEqual(expected,log.Scenes[0].Actions.Single(a=>a.Event==CampaignQuestLogEvent.None).Frames);
    }

}
