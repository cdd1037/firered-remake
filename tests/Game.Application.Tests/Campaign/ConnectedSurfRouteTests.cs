using System.Reflection;
using System.Text.Json;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>
/// Controlled placement in the maintained checkpoint's Cinnabar Center, followed by public exit,
/// walking, both Surf producers and strict Continue. Earned badge, Surf learner and story are retained.
/// CurrentCampaignFixture's explicit legacy conversion and test-only hatch total are not a natural playthrough.
/// </summary>
[TestClass, TestCategory("Game")]
public sealed class ConnectedSurfRouteTests : WildBattleFixture
{
    private const string Island="map.cinnabar-island", Water="map.route-21-south", Center="map.cinnabar-island-pokemon-center-1f";
    private static readonly (int Dx,int Dy,Direction Direction)[] Directions=[(0,-1,Direction.North),(1,0,Direction.East),(0,1,Direction.South),(-1,0,Direction.West)];

    private CampaignSession AtCenterExit()
    {
        var session=CampaignSession.CreateCurrent(content);var basis=checkpoint.State;var map=new MapId(Center);
        Assert.IsTrue(basis.Script.Flags.GetValueOrDefault(FieldHmSourceRules.SurfBadge));
        Assert.IsTrue(basis.Party.Any(p=>FieldHmSourceRules.Knows(p,FieldHmSourceRules.SurfMove)));
        Assert.IsTrue(basis.Script.Flags.GetValueOrDefault("FLAG_HIDE_POKEMON_MANSION_B1F_SECRET_KEY"));
        var state=basis with
        {
            World=basis.World with{Player=new(map,7,7,3,Direction.South)},
            Script=StoryState.Empty with{Flags=basis.Script.Flags,Variables=basis.Script.Variables,EffectSequence=basis.Script.EffectSequence},
            Dialogue=null,ItemMenu=null,MonPicture=null,Motions=[],PresentationTicks=0,PresentationEffects=[],EntryPrograms=[],ArrivalFacing=null,
            IsSurfing=false,IsCycling=false,BicycleSlope=false,MapEdits=null,MapLayout=null,FieldTransition=null,FlashLevel=0,FieldWeather=null,
            PlayerVisible=true,ScreenIsBlack=false,ScreenIsWhite=false,Music=content.MapDefinition(map).Music,QuestLog=null
        };
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        state=(CampaignState)typeof(CampaignSession).GetMethod("EnterMap",flags)!.Invoke(session,[state,null,RoamerTransition.Warp])!;
        state=(CampaignState)typeof(CampaignSession).GetMethod("Pump",flags)!.Invoke(session,[state])!;
        typeof(CampaignSession).GetField("_commands",flags)!.SetValue(session,
            new CommandSession<CampaignState,CampaignCommand>(state,session.RulesVersion,content.ContentVersion,checkpoint.Generation));
        Assert.AreEqual(JsonSerializer.Serialize(basis.Party),JsonSerializer.Serialize(state.Party));
        Assert.AreEqual(JsonSerializer.Serialize(basis.Inventory),JsonSerializer.Serialize(state.Inventory));
        StrictRoundtrip(session);
        Send(session,new("move",Direction:Direction.South));DrainEntry(session);
        Assert.AreEqual(new TilePosition(map,7,8,3,Direction.South),session.State.World.Player);
        Assert.AreEqual(0x65,content.Maps[map].SourceBehaviorAt(7,8),"The south-arrow exit first requires standing on its actual warp tile.");
        Send(session,new("move",Direction:Direction.South));DrainEntry(session);
        Assert.AreEqual(Island,session.State.World.Player.MapId.Value);
        Assert.AreEqual(1,session.State.Script.Variables.GetValueOrDefault("VAR_TEMP_1"),"The actual Cinnabar entry script unlocks the gym from the retained Secret Key flag.");
        return session;
    }

    private void DrainEntry(CampaignSession session)
    {
        for(int count=0;count<100;count++)
        {
            Assert.IsNull(session.State.WildBattle);Assert.IsNull(session.State.ForestTrainerBattle);
            if(session.State.Script.Completed&&session.State.Script.Wait is null&&session.State.Dialogue is null&&session.State.EntryPrograms.IsEmpty)return;
            Send(session,session.State.Script.Wait?.Kind==StoryWaitKind.ButtonPress?new("button_press",Text:"A"):
                new("ack",Choice:session.State.Script.Wait?.Kind==StoryWaitKind.Choice?1:null));
        }
        Assert.Fail("The real entry script did not reach a stable field boundary.");
    }

    private void WalkToShore(CampaignSession session,int x)
    {
        var map=content.ConnectedFieldMaps![new(Island)];var start=(session.State.World.Player.X,session.State.World.Player.Y);var target=(x,0);
        var queue=new Queue<(int X,int Y)>();var seen=new HashSet<(int,int)>{start};
        var previous=new Dictionary<(int,int),((int,int) Cell,Direction Direction)>();queue.Enqueue(start);
        while(queue.TryDequeue(out var cell)&&cell!=target)
            foreach(var delta in Directions)
            {
                var next=(cell.X+delta.Dx,cell.Y+delta.Dy);var tile=map.CellAt(next.Item1,next.Item2);
                if(seen.Contains(next)||tile is not{Collision:0,Elevation:3,SourceBehavior:0}||
                    session.State.Actors.Values.Any(a=>(a.HideFlag is "0" or "0x0"||!session.State.Script.Flags.GetValueOrDefault(a.HideFlag))&&
                        (a.X==next.Item1&&a.Y==next.Item2||a.TemplateX==next.Item1&&a.TemplateY==next.Item2)))continue;
                seen.Add(next);previous[next]=(cell,delta.Direction);queue.Enqueue(next);
            }
        Assert.IsTrue(seen.Contains(target),"A real walkable street route must connect the Center to the shoreline.");
        var route=new Stack<Direction>();for(var cell=target;cell!=start;cell=previous[cell].Cell)route.Push(previous[cell].Direction);
        foreach(var direction in route){Send(session,new("move",Direction:direction));DrainEntry(session);}
        if(session.State.World.Player.Facing!=Direction.North)Send(session,new("turn",Direction:Direction.North));
        Assert.AreEqual(new TilePosition(new(Island),x,0,3,Direction.North),session.State.World.Player);StrictRoundtrip(session);
    }

    private void RejectUnchanged(CampaignSession session,CampaignCommand command,string error)
    {
        var before=session.State;long revision=session.Revision;
        var rejected=session.Execute("connected-surf:reject:"+commands++,revision,command);
        Assert.AreEqual(CommandStatus.Rejected,rejected.Status);Assert.AreEqual(error,rejected.Error);
        Assert.AreEqual(revision,session.Revision);Assert.AreSame(before,session.State);
    }

    private void Acknowledge(CampaignSession session)=>Send(session,new("field_surf_complete",Text:session.State.SurfEffect!.WaitId));

    private void AssertNoWalking(CampaignState before,CampaignState after)
    {
        Assert.AreEqual(before.World.StepCount,after.World.StepCount);
        foreach(var variable in new[]{"VAR_REPEL_STEP_COUNT","VAR_HAPPINESS_STEP_COUNTER","VAR_POISON_STEP_COUNTER","VAR_MASSAGE_COOLDOWN_STEP_COUNTER",RenewableHiddenItemRules.CounterVariable})
            Assert.AreEqual(before.Script.Variables.GetValueOrDefault(variable),after.Script.Variables.GetValueOrDefault(variable),variable);
        Assert.AreEqual(JsonSerializer.Serialize(before.Party),JsonSerializer.Serialize(after.Party));
        Assert.AreEqual(JsonSerializer.Serialize(before.Inventory),JsonSerializer.Serialize(after.Inventory));
        Assert.AreEqual(JsonSerializer.Serialize(before.GameStats),JsonSerializer.Serialize(after.GameStats));
        Assert.AreEqual(JsonSerializer.Serialize(before.FourIslandDayCare),JsonSerializer.Serialize(after.FourIslandDayCare));
        Assert.AreEqual(before.Exploration!.EarlyEncounter!.RateRng,after.Exploration!.EarlyEncounter!.RateRng);
        Assert.IsNull(after.WildBattle);Assert.IsNull(after.ForestTrainerBattle);
    }

    private void AssertConnectionEntry(CampaignState before,CampaignSession session,TilePosition destination,bool surfing)
    {
        var after=session.State;AssertNoWalking(before,after);
        var expected=SourceRoamerRules.Transition(before.Roamer??RoamerState.Empty,RoamerTransition.Connection,content.CelioRoamerMaps![destination.MapId],before.World.Rng);
        Assert.AreEqual(expected.Rng,after.World.Rng,"Only the existing source map-entry roamer callback may consume global RNG.");
        Assert.AreEqual(JsonSerializer.Serialize(expected.State),JsonSerializer.Serialize(after.Roamer));
        Assert.AreEqual(before.World.Revision+1,after.World.Revision,"One jump commits one world action, with no second completion step.");
        Assert.AreEqual(destination,after.World.Player);Assert.AreEqual(surfing,after.IsSurfing);
        Assert.IsNull(after.SurfEffect);Assert.IsFalse(after.Script.Locked);Assert.IsTrue(after.Script.Completed);
        Assert.IsTrue(after.EntryPrograms.IsEmpty);Assert.IsTrue(after.Motions.IsEmpty);Assert.AreEqual(0,after.PresentationTicks);
        Assert.AreEqual((ushort)0,after.Exploration!.EarlyEncounter!.EncounterRateBuff);Assert.AreEqual((byte)0,after.Exploration.EarlyEncounter.StepsSinceLastEncounter);
        Assert.AreEqual(content.MapDefinition(destination.MapId).Actors.Length,after.Actors.Count);
        Assert.IsNotNull(after.FieldTransition);Assert.AreEqual(FieldTransitionCause.ConnectedEdge,after.FieldTransition.Cause);
        Assert.AreEqual(FieldTransitionDeparture.Immediate,after.FieldTransition.Departure);Assert.AreEqual(FieldTransitionArrival.Plain,after.FieldTransition.Arrival);
        Assert.AreEqual(destination,after.FieldTransition.To);StrictRoundtrip(session);
    }

    [TestMethod]
    [DataRow(14,false)]
    [DataRow(15,true)]
    public void CenterWalkMountReverseDismountAndContinueUseRealConnectedShore(int x,bool interaction)
    {
        var session=AtCenterExit();WalkToShore(session,x);
        // Source FieldMove may append to an open recording but cannot start one itself.
        Send(session,new("party_swap",PartyIndex:1,Choice:0));
        Assert.IsNotNull(session.State.QuestLog);Assert.IsFalse(session.State.QuestLog.Scenes[^1].Closed);
        var origin=session.State;
        Assert.AreEqual(new ConnectedFieldCell(0,0,3),content.ConnectedFieldMaps![new(Island)].CellAt(x,0));
        Assert.AreEqual(new ConnectedFieldCell(0x15,0,1),content.ConnectedFieldMaps[new(Water)].CellAt(x,49));
        Assert.AreEqual(2,content.TerrainAt(new(Water),x,49));
        int surfer=Enumerable.Range(0,origin.Party.Length).First(i=>FieldHmSourceRules.Knows(origin.Party[i],FieldHmSourceRules.SurfMove));
        Assert.IsTrue(session.CanUseFieldSurf(surfer));
        if(interaction)
        {
            Send(session,new("interact"));
            for(int i=0;i<20&&session.State.SurfEffect is null;i++)
                Send(session,session.State.Script.Wait?.Kind==StoryWaitKind.ButtonPress?new("button_press",Text:"A"):
                    new("ack",Choice:session.State.Script.Wait?.Kind==StoryWaitKind.Choice?1:null));
        }
        else Send(session,new("field_surf",PartyIndex:surfer));
        Assert.IsNotNull(session.State.SurfEffect);Assert.AreEqual(FieldSurfSourcePresentation.ShowMon,session.State.SurfEffect.Phase);
        Assert.AreEqual(origin.World.Player,session.State.World.Player);AssertNoWalking(origin,session.State);Assert.AreEqual(origin.World.Rng,session.State.World.Rng);
        Assert.IsNotNull(session.ValidatePlayerSave(session.State));
        RejectUnchanged(session,new("field_surf_complete",Text:"wrong-owner"),"field_surf_callback_mismatch");
        RejectUnchanged(session,new("move",Direction:Direction.North),"field_surf_event_active");
        string firstWait=session.State.SurfEffect.WaitId;Acknowledge(session);
        var jump=session.State;Assert.AreEqual(FieldSurfSourcePresentation.MountJump,jump.SurfEffect!.Phase);
        Assert.AreEqual(origin.World.Player,jump.World.Player,"The callback authenticates the source map and its edits through the jump.");
        CollectionAssert.AreEqual(new[]{origin.World.Player,new TilePosition(new(Island),x,-1,1,Direction.North)},jump.Motions.Single().Path.ToArray());
        Assert.AreEqual(FieldSurfSourcePresentation.JumpCueFrames,jump.PresentationTicks);Assert.AreEqual(origin.World.Rng,jump.World.Rng);
        RejectUnchanged(session,new("field_surf_complete",Text:firstWait),"field_surf_callback_mismatch");
        Acknowledge(session);
        AssertConnectionEntry(origin,session,new(new(Water),x,49,1,Direction.North),true);
        Assert.AreEqual("MUS_SURF",session.State.Music);Assert.AreEqual(0,session.State.Script.Variables.GetValueOrDefault("VAR_TEMP_1"));
        Assert.IsTrue(session.State.QuestLog!.Scenes.Any(scene=>scene.Map==Island&&scene.Closed&&scene.Actions.Any(a=>a.Event==CampaignQuestLogEvent.FieldMove&&a.TextKey=="UsedSurf")));

        // Continue the actual saved water position before taking the reverse shoreline route.
        var saved=SaveCodec.Decode<CampaignState>(SaveCodec.Encode(session.State,session.Revision,session.RulesVersion,content.ContentVersion),
            session.RulesVersion,content.ContentVersion,validate:session.ValidatePlayerSave);
        session=CampaignSession.RestoreCurrent(content,saved.State,saved.Generation);
        Assert.AreEqual(new TilePosition(new(Water),x,49,1,Direction.North),session.State.World.Player);
        Assert.IsTrue(session.State.IsSurfing);Assert.IsTrue(session.State.EntryPrograms.IsEmpty);
        // A real field party-order event opens a new recording after the source closes UsedSurf.
        Send(session,new("party_swap",PartyIndex:1,Choice:0));
        Assert.IsFalse(session.State.QuestLog!.Scenes[^1].Closed);
        var water=session.State;Send(session,new("move",Direction:Direction.South));
        var dismount=session.State;Assert.AreEqual(FieldSurfSourcePresentation.DismountJump,dismount.SurfEffect!.Phase);
        Assert.AreEqual(water.World.Player with{Facing=Direction.South},dismount.World.Player);
        CollectionAssert.AreEqual(new[]{dismount.SurfEffect.Origin,new TilePosition(new(Water),x,50,3,Direction.South)},dismount.Motions.Single().Path.ToArray());
        AssertNoWalking(water,dismount);Assert.AreEqual(water.World.Rng,dismount.World.Rng);
        Acknowledge(session);
        AssertConnectionEntry(water,session,new(new(Island),x,0,3,Direction.South),false);
        Assert.AreEqual(content.MapDefinition(new(Island)).Music,session.State.Music);
        Assert.AreEqual(1,session.State.Script.Variables.GetValueOrDefault("VAR_TEMP_1"),"The real destination OnTransition runs again after dismount.");
        AssertQuestDismount(session,x);StrictRoundtrip(session);
    }

    private void AssertQuestDismount(CampaignSession session,int x)
    {
        var log=session.State.QuestLog!;Assert.IsNull(CampaignQuestLogRecorder.Validate(log,content.QuestLogSource,content));
        var scene=log.Scenes[^1];Assert.AreEqual(Water,scene.Map);
        var jump=scene.Actions.Single(a=>a.SourceAction=="jump_special_with_effect_down");
        Assert.AreEqual((x,50,32),(jump.X,jump.Y,jump.Frames));
        var marker=scene.Actions.Single(a=>a.Transition is not null);Assert.IsNull(marker.TransitionTiming);
        Assert.AreEqual(Island,marker.Transition!.Map);Assert.IsTrue(marker.StartFrame>=jump.StartFrame+jump.Frames);
        var replay=new CampaignQuestLogPlayback(new([scene]),session.State.PlayerName,content.QuestLogSource);
        bool sawJump=false,sawIsland=false;
        for(int frame=0;frame<1500&&!replay.Finished;frame++)
        {
            if(replay.Movement(CampaignQuestLogRecorder.PlayerId) is{IsJump:true} motion)
            {sawJump=true;Assert.AreEqual(Water,replay.Map);Assert.AreEqual(49,motion.From.Y);}
            if(replay.Map==Island)sawIsland=true;
            replay.Tick(1d/60);
        }
        Assert.IsTrue(replay.Finished);Assert.IsTrue(sawJump);Assert.IsTrue(sawIsland);
        Assert.AreEqual(Island,replay.Map);Assert.AreEqual((x,0),(replay.Actors.Single(a=>a.Id==CampaignQuestLogRecorder.PlayerId).X,replay.Actors.Single(a=>a.Id==CampaignQuestLogRecorder.PlayerId).Y));
    }

    [TestMethod]
    [DataRow(Direction.North,0,-1)]
    [DataRow(Direction.East,1,0)]
    [DataRow(Direction.South,0,1)]
    [DataRow(Direction.West,-1,0)]
    public void JumpProjectionUsesOneAdjacentSourceChartCellRegardlessOfDestinationOffset(Direction direction,int dx,int dy)
    {
        var origin=new TilePosition(new("map.origin"),20,30,3,direction);
        var destination=new TilePosition(new("map.destination"),105,87,1,direction);
        var effect=new CampaignSurfEffect(0,1,131,"LAPRAS",origin,destination,99,FieldSurfSourcePresentation.MountJump,"projection");
        Assert.AreEqual(new TilePosition(origin.MapId,20+dx,30+dy,1,direction),FieldSurfSourcePresentation.JumpDestination(effect));
        Assert.AreEqual(destination with{MapId=origin.MapId},FieldSurfSourcePresentation.JumpDestination(effect with{Destination=destination with{MapId=origin.MapId}}));
    }
}
