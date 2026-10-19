using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

[TestClass,TestCategory("Game")]
public sealed class CampaignQuestLogCaptionLockTests
{
    private static readonly CampaignQuestLogActor Origin=new(CampaignQuestLogRecorder.PlayerId,"player",3,4,Direction.North,true);
    private static readonly CampaignQuestLogActor Destination=new(CampaignQuestLogRecorder.PlayerId,"player",8,9,Direction.South,true);
    private const string Arrival="RED arrived at source.";
    private const string Center="RED restored the POKEMON at a POKEMON CENTER.";
    private const string Trainer="RED defeated a TRAINER in battle.";

    private static CampaignQuestLogAction Caption(int start,int duration,CampaignQuestLogEvent kind=CampaignQuestLogEvent.Arrived)=>
        new(-1,0,0,Direction.South,false,kind,duration){StartFrame=start};

    private static CampaignQuestLogAction Transition(int start,string map,CampaignQuestLogTransitionTiming? timing,
        CampaignQuestLogActor? destination=null)=>
        new(-1,0,0,Direction.South,false,CampaignQuestLogEvent.None,timing?.EndFrame??1)
        {
            StartFrame=start,TransitionTiming=timing,
            Transition=new(map,[destination??Destination],null,[])
        };

    private static CampaignQuestLogAction Walk(int start,int x,int y)=>
        new(0,x,y,Direction.East,true,CampaignQuestLogEvent.None,16){StartFrame=start};

    private static CampaignQuestLog Recording(params CampaignQuestLogAction[] actions)=>
        new([new("source",[Origin],actions.ToImmutableArray())]);

    private static void AdvanceTo(CampaignQuestLogPlayback playback,double frame)=>
        playback.Tick((frame-playback.Frame)/60d);

    [TestMethod]
    [DataRow(FieldTransitionCause.PlainWarp,FieldTransitionArrival.Plain,24,48)]
    [DataRow(FieldTransitionCause.PlainWarp,FieldTransitionArrival.Plain,50,74)]
    [DataRow(FieldTransitionCause.AnimatedDoor,FieldTransitionArrival.Plain,81,105)]
    [DataRow(FieldTransitionCause.PlainWarp,FieldTransitionArrival.AnimatedDoor,24,105)]
    [DataRow(FieldTransitionCause.AnimatedDoor,FieldTransitionArrival.AnimatedDoor,81,162)]
    public void PriorCaptionKeepsRemainingTimeWhileFieldAndMapUseTheFullObservedInterval(
        FieldTransitionCause cause,FieldTransitionArrival arrival,int change,int end)
    {
        var timing=new CampaignQuestLogTransitionTiming(cause,change,end)
        {
            Arrival=arrival,
            DepartureDoor=cause==FieldTransitionCause.AnimatedDoor?new(3,3):null,
            ArrivalDoor=arrival==FieldTransitionArrival.AnimatedDoor?new(8,8):null
        };
        var log=Recording(Caption(0,64),Transition(16,"target",timing),
            Caption(20,8,CampaignQuestLogEvent.PokemonCenter),Walk(16+end,9,9));
        string unchanged=JsonSerializer.Serialize(log);
        Assert.IsNull(CampaignQuestLogRecorder.Validate(log));
        var playback=new CampaignQuestLogPlayback(log);

        AdvanceTo(playback,15);Assert.AreEqual(Arrival,playback.Caption);
        AdvanceTo(playback,16);Assert.AreEqual(Arrival,playback.Caption);
        Assert.AreEqual(timing,playback.ActiveTransitionTiming);
        AdvanceTo(playback,16+change-1);Assert.AreEqual("source",playback.Map);
        Assert.AreEqual(Arrival,playback.Caption,"Locking does not clear the existing description.");
        AdvanceTo(playback,16+change);Assert.AreEqual("target",playback.Map);
        Assert.IsNull(playback.Caption,"Map reconstruction clears the description without spending its remaining time.");
        Assert.AreEqual(change,playback.TransitionElapsedFrames,.00001);

        AdvanceTo(playback,16+end);Assert.IsNull(playback.ActiveTransitionTiming);
        Assert.AreEqual(16+end,playback.Frame,.00001);
        Assert.IsFalse(playback.Finished);Assert.IsNull(playback.Caption);
        Assert.AreEqual(0,playback.Movement(CampaignQuestLogRecorder.PlayerId)!.Value.Fraction,.00001);
        AdvanceTo(playback,24+end);
        Assert.AreEqual(.5,playback.Movement(CampaignQuestLogRecorder.PlayerId)!.Value.Fraction,.00001);
        AdvanceTo(playback,63+end);Assert.IsNull(playback.Caption);Assert.IsFalse(playback.Finished);
        AdvanceTo(playback,64+end);Assert.AreEqual(Center,playback.Caption);
        AdvanceTo(playback,71+end);Assert.AreEqual(Center,playback.Caption);Assert.IsFalse(playback.Finished);
        AdvanceTo(playback,72+end);Assert.IsNull(playback.Caption);Assert.IsTrue(playback.Finished);
        Assert.AreEqual(unchanged,JsonSerializer.Serialize(log));
    }

    [TestMethod]
    public void CaptionEndingExactlyAtLockStartDoesNotGainTheLockButItsSuccessorWaitsForRelease()
    {
        var playback=new CampaignQuestLogPlayback(Recording(Caption(0,16),
            Transition(16,"target",new(FieldTransitionCause.PlainWarp,24,48)),
            Caption(16,8,CampaignQuestLogEvent.PokemonCenter)));
        AdvanceTo(playback,15);Assert.AreEqual(Arrival,playback.Caption);
        AdvanceTo(playback,16);Assert.IsNull(playback.Caption);
        AdvanceTo(playback,63);Assert.IsNull(playback.Caption);Assert.IsFalse(playback.Finished);
        AdvanceTo(playback,64);Assert.AreEqual(Center,playback.Caption);
        AdvanceTo(playback,71);Assert.AreEqual(Center,playback.Caption);
        AdvanceTo(playback,72);Assert.IsNull(playback.Caption);Assert.IsTrue(playback.Finished);
    }

    [TestMethod]
    [DataRow(16)]
    [DataRow(40)]
    [DataRow(63)]
    [DataRow(64)]
    public void CaptionScheduledAtOrInsideTheLockBeginsAtReleaseWithItsFullDuration(int scheduled)
    {
        var log=Recording(Transition(16,"target",new(FieldTransitionCause.PlainWarp,24,48)),Caption(scheduled,8));
        var playback=new CampaignQuestLogPlayback(log);
        Assert.IsNull(playback.Caption);
        AdvanceTo(playback,63);Assert.IsNull(playback.Caption);
        AdvanceTo(playback,64);Assert.AreEqual(Arrival,playback.Caption);
        AdvanceTo(playback,71);Assert.AreEqual(Arrival,playback.Caption);
        AdvanceTo(playback,72);Assert.IsNull(playback.Caption);Assert.IsTrue(playback.Finished);
        Assert.AreEqual(scheduled,log.Scenes[0].Actions[1].StartFrame,"Deferral is presentation-only.");
    }

    private static CampaignQuestLog MultipleWarps()=>Recording(
        Caption(0,64),
        Transition(16,"middle",new(FieldTransitionCause.PlainWarp,24,48)),
        Caption(20,32,CampaignQuestLogEvent.PokemonCenter),
        Walk(64,9,9),
        Transition(80,"target",new(FieldTransitionCause.PlainWarp,24,48),Destination with{X=12,Y=13}),
        Caption(92,16,CampaignQuestLogEvent.TrainerWon),
        Walk(128,13,13));

    [TestMethod]
    public void ASecondWarpReachedByTheExtendedTimerAlsoPausesItAndKeepsQueuedCaptionsSerial()
    {
        var log=MultipleWarps();string unchanged=JsonSerializer.Serialize(log);
        var playback=new CampaignQuestLogPlayback(log);
        AdvanceTo(playback,39);Assert.AreEqual(Arrival,playback.Caption);
        AdvanceTo(playback,40);Assert.AreEqual("middle",playback.Map);Assert.IsNull(playback.Caption);
        AdvanceTo(playback,72);
        Assert.AreEqual(.5,playback.Movement(CampaignQuestLogRecorder.PlayerId)!.Value.Fraction,.00001);
        AdvanceTo(playback,103);Assert.AreEqual("middle",playback.Map);
        AdvanceTo(playback,104);Assert.AreEqual("target",playback.Map);
        AdvanceTo(playback,136);
        Assert.AreEqual(.5,playback.Movement(CampaignQuestLogRecorder.PlayerId)!.Value.Fraction,.00001);
        AdvanceTo(playback,159);Assert.IsNull(playback.Caption);Assert.IsFalse(playback.Finished);
        AdvanceTo(playback,160);Assert.AreEqual(Center,playback.Caption);
        AdvanceTo(playback,191);Assert.AreEqual(Center,playback.Caption);
        AdvanceTo(playback,192);Assert.AreEqual(Trainer,playback.Caption);
        AdvanceTo(playback,207);Assert.AreEqual(Trainer,playback.Caption);
        AdvanceTo(playback,208);Assert.IsTrue(playback.Finished);Assert.IsNull(playback.Caption);
        Assert.AreEqual(unchanged,JsonSerializer.Serialize(log));
    }

    [TestMethod]
    public void AdjacentWarpLocksDeferAQueuedCaptionUntilBothHaveReleased()
    {
        var playback=new CampaignQuestLogPlayback(Recording(
            Transition(0,"middle",new(FieldTransitionCause.PlainWarp,24,48)),
            Caption(20,8),
            Transition(48,"target",new(FieldTransitionCause.PlainWarp,24,48))));
        AdvanceTo(playback,48);Assert.IsNull(playback.Caption);
        AdvanceTo(playback,95);Assert.IsNull(playback.Caption);Assert.IsFalse(playback.Finished);
        AdvanceTo(playback,96);Assert.AreEqual(Arrival,playback.Caption);
        AdvanceTo(playback,104);Assert.IsNull(playback.Caption);Assert.IsTrue(playback.Finished);
    }

    [TestMethod]
    public void NonOverlappingLocksDoNotAlterEarlierOrLaterCaptionDurations()
    {
        var playback=new CampaignQuestLogPlayback(Recording(Caption(0,8),
            Transition(16,"target",new(FieldTransitionCause.PlainWarp,24,48)),
            Caption(80,8,CampaignQuestLogEvent.PokemonCenter)));
        AdvanceTo(playback,7);Assert.AreEqual(Arrival,playback.Caption);
        AdvanceTo(playback,8);Assert.IsNull(playback.Caption);
        AdvanceTo(playback,64);Assert.IsNull(playback.Caption);
        AdvanceTo(playback,79);Assert.IsNull(playback.Caption);
        AdvanceTo(playback,80);Assert.AreEqual(Center,playback.Caption);
        AdvanceTo(playback,87);Assert.AreEqual(Center,playback.Caption);
        AdvanceTo(playback,88);Assert.IsNull(playback.Caption);Assert.IsTrue(playback.Finished);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LegacyUnknownAndConnectedMarkersNeitherPauseNorClearAnExistingCaption(bool connected)
    {
        CampaignQuestLogTransitionTiming? timing=connected?new(FieldTransitionCause.ConnectedEdge,0,1):null;
        var playback=new CampaignQuestLogPlayback(Recording(Caption(0,64),Transition(16,"target",timing)));
        AdvanceTo(playback,16);Assert.AreEqual("target",playback.Map);Assert.AreEqual(Arrival,playback.Caption);
        AdvanceTo(playback,17);Assert.AreEqual(Arrival,playback.Caption);
        AdvanceTo(playback,63);Assert.AreEqual(Arrival,playback.Caption);Assert.IsFalse(playback.Finished);
        AdvanceTo(playback,64);Assert.IsNull(playback.Caption);Assert.IsTrue(playback.Finished);
    }

    [TestMethod]
    public void ThirtySixtyAndOneHundredTwentyHertzMatchSingleTicksAtEveryRelevantBoundary()
    {
        var log=MultipleWarps();string unchanged=JsonSerializer.Serialize(log);
        foreach(int frame in new[]{8,16,39,40,63,64,72,79,80,103,104,127,128,136,159,160,191,192,207,208})
        {
            var single=new CampaignQuestLogPlayback(log);single.Tick(frame/60d);
            foreach(int hz in new[]{30,60,120})
            {
                var stepped=new CampaignQuestLogPlayback(log);
                double remaining=frame,step=60d/hz;
                while(remaining>0)
                {
                    double amount=Math.Min(step,remaining);stepped.Tick(amount/60d);remaining-=amount;
                }
                string context=$"{hz} Hz at field frame {frame}";
                Assert.AreEqual(single.Frame,stepped.Frame,.00001,context);
                Assert.AreEqual(single.Map,stepped.Map,context);
                Assert.AreEqual(single.Caption,stepped.Caption,context);
                Assert.AreEqual(single.Finished,stepped.Finished,context);
                Assert.AreEqual(single.ActiveTransitionTiming,stepped.ActiveTransitionTiming,context);
                Assert.AreEqual(single.TransitionElapsedFrames,stepped.TransitionElapsedFrames,.00001,context);
                Assert.AreEqual(single.ArtRevision,stepped.ArtRevision,context);
                CollectionAssert.AreEqual(single.Actors.ToArray(),stepped.Actors.ToArray(),context);
                Assert.AreEqual(single.Movement(CampaignQuestLogRecorder.PlayerId),stepped.Movement(CampaignQuestLogRecorder.PlayerId),context);
            }
        }
        Assert.AreEqual(unchanged,JsonSerializer.Serialize(log));
    }

    [TestMethod]
    [DataRow(CampaignQuestLogEvent.Arrived,"ArrivedInLocation","HOME",95)]
    [DataRow(CampaignQuestLogEvent.Arrived,"ArrivedInLocation","PALLET TOWN",127)]
    [DataRow(CampaignQuestLogEvent.WithdrewItem,"WithdrewItemFromPC","MASTER BALL",191)]
    [DataRow(CampaignQuestLogEvent.DepositedItem,"StoredItemInPC","MASTER BALL",255)]
    public void OriginalTextDurationStillDeterminesWhenTheNextCaptionCanStart(
        CampaignQuestLogEvent kind,string key,string argument,int duration)
    {
        // Original text templates, kept small and in-memory: this timing test needs
        // neither imported world fixtures nor a full campaign catalog.
        var source=new QuestLogSource(
            new Dictionary<string,string>
            {
                ["ArrivedInLocation"]="Arrived in {STR_VAR_1}.",
                ["WithdrewItemFromPC"]="{PLAYER} withdrew the item {STR_VAR_1}\nfrom the PC.",
                ["StoredItemInPC"]="Removed the item {STR_VAR_1} from\nthe BAG's ITEMS POCKET and stored it\non the PC."
            }.ToImmutableDictionary(),
            new Dictionary<string,string>{{"source","SOURCE"},{"target","TARGET"}}.ToImmutableDictionary(),
            new Dictionary<int,int>{{2,8},{(int)CampaignQuestLogEvent.Arrived,16},
                {(int)CampaignQuestLogEvent.WithdrewItem,16},{(int)CampaignQuestLogEvent.DepositedItem,16}}.ToImmutableDictionary(),
            [],[],ImmutableDictionary<int,string>.Empty,[],[],[],"source",[],[]);
        var first=Caption(0,1,kind) with{TextKey=key,Arguments=[argument]};
        var next=Caption(20,1) with{TextKey="ArrivedInLocation",Arguments=["HOME"]};
        var log=Recording(first,Transition(16,"target",new(FieldTransitionCause.PlainWarp,24,48)),next);
        string unchanged=JsonSerializer.Serialize(log);
        var playback=new CampaignQuestLogPlayback(log,"RED",source);
        AdvanceTo(playback,39);Assert.AreEqual(source.Expand(key,"RED",[argument]),playback.Caption);
        AdvanceTo(playback,duration+47);Assert.IsNull(playback.Caption);Assert.IsFalse(playback.Finished);
        AdvanceTo(playback,duration+48);Assert.AreEqual("Arrived in HOME.",playback.Caption);
        AdvanceTo(playback,duration+48+94);Assert.AreEqual("Arrived in HOME.",playback.Caption);
        AdvanceTo(playback,duration+48+95);Assert.IsNull(playback.Caption);Assert.IsTrue(playback.Finished);
        Assert.AreEqual(unchanged,JsonSerializer.Serialize(log));
    }
}
