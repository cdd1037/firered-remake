using System.Collections.Immutable;
using System.Reflection;
using FireRed.Tests.Application.FieldItems;
using FireRed.Tests.Application.WildBattles;
using Game.Application;
using Game.Domain.Encounters;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace FireRed.Tests.Application.Campaign;

/// <summary>Controlled valid late-game identities; every field operation uses the public command boundary.</summary>
[TestClass, TestCategory("Game")]
public sealed class NoncombatFieldMoveTests : WildBattleFixture
{
    private CampaignState WithMove(CampaignState state, int species, string move)
    {
        state = CurrentFixturePokemon.BringToLead(content, state, species);
        var member = state.Party[0]; var definition = catalog.Moves[new(move)];
        return state with { Party = state.Party.SetItem(0, member with { Pokemon = member.Pokemon with
            { Moves = [new(definition.Id, 0, definition.Pp)] } }) };
    }
    private CampaignState Place(CampaignState state, string name, int encounterType = 0, bool surfing = false)
    {
        var map = content.Maps[new(name)]; var definition = content.MapDefinition(map.Id);
        var cell = Enumerable.Range(0, map.Height).SelectMany(y => Enumerable.Range(0, map.Width).Select(x => (x,y)))
            .First(p => map.GetTile(p.x,p.y) is { Blocked:false } && map.SourceEncounterTypeAt(p.x,p.y) == encounterType &&
                !definition.Actors.Any(actor => actor.X == p.x && actor.Y == p.y));
        return state with
        {
            World = state.World with { Player = new(map.Id, cell.x, cell.y, map.GetTile(cell.x,cell.y)!.Elevation, Direction.South) },
            Actors = definition.Actors.ToImmutableSortedDictionary(a=>a.LocalId,a=>new CampaignActor(a.LocalId,a.SourceLocalId,a.GraphicsId,a.X,a.Y,a.Elevation,
                Direction.South,a.MovementType,a.ScriptId,a.HideFlag,false,false,a.X,a.Y),StringComparer.Ordinal),
            ObjectSlots = ImmutableSortedDictionary<int,string>.Empty.Add(0,"LOCALID_PLAYER"), LastTalked = null, SelectedObjectSlot = 0,
            MapEdits = null, MapLayout = null, EntryPrograms = [], Dialogue = null, Motions = [], IsSurfing = surfing, IsCycling = false,
            FlashLevel = definition.RequiresFlash ? 4 : 0,
            Script = StoryState.Empty with { Flags = state.Script.Flags, Variables = state.Script.Variables, EffectSequence = state.Script.EffectSequence }
        };
    }
    private CampaignSession Start(CampaignState state)
    {
        Assert.IsNull(validator.ValidatePlayerSave(state), "valid prepared field fixture");
        return CampaignSession.RestoreCurrent(content,state,checkpoint.Generation);
    }
    private void Ack(CampaignSession session) => Send(session,new("field_move_ack",Text:session.State.NoncombatFieldEffect!.WaitId));
    private void Reject(CampaignSession session, CampaignCommand command, string error)
    {
        var before=session.State; long revision=session.Revision;
        var result=session.Execute("field-reject:"+commands++,revision,command);
        Assert.AreEqual(CommandStatus.Rejected,result.Status); Assert.AreEqual(error,result.Error);
        Assert.AreSame(before,session.State);Assert.AreEqual(revision,session.Revision);
    }
    private void StartScent(CampaignSession session)
    {
        Send(session,new("field_move",Text:NoncombatFieldMoveRules.SweetScent));
        Assert.AreEqual("show-mon",session.State.NoncombatFieldEffect!.Phase);Ack(session);
        Assert.AreEqual("scent",session.State.NoncombatFieldEffect!.Phase);Ack(session);
    }

    [TestMethod]
    public void TeleportCancelAndConfirmedWarpUseLastExteriorHealLocationWithoutHealingOrPp()
    {
        var state=Place(WithMove(FieldFixture(),63,NoncombatFieldMoveRules.Teleport),"map.route-1",1);
        state=state with{LastHealingLocation="HEAL_LOCATION_PALLET_TOWN"};
        var donor=state.Party[0]; state=state with{Party=state.Party.SetItem(0,donor with{Pokemon=donor.Pokemon with{CurrentHp=0}})};
        var session=Start(state);var original=session.State;
        Send(session,new("field_move",Text:NoncombatFieldMoveRules.Teleport));
        Assert.AreEqual("confirm",session.State.NoncombatFieldEffect!.Phase);
        Reject(session,new("move"),"field_move_confirmation_pending");
        Assert.AreEqual("save_event_active",session.ValidatePlayerSave(session.State));
        Send(session,new("field_move_confirm",Choice:1));
        Assert.AreEqual(original.World.Player,session.State.World.Player);Assert.IsNull(session.State.NoncombatFieldEffect);
        Send(session,new("field_move",Text:NoncombatFieldMoveRules.Teleport));Send(session,new("field_move_confirm",Choice:0));
        string wait=session.State.NoncombatFieldEffect!.WaitId;Ack(session);
        Reject(session,new("field_move_ack",Text:wait),"field_move_callback_mismatch");
        Ack(session); Assert.AreEqual("map.pallet-town",session.State.World.Player.MapId.Value);
        Assert.AreEqual((6,8),(session.State.World.Player.X,session.State.World.Player.Y));Ack(session);
        Assert.IsNull(session.State.NoncombatFieldEffect);Assert.AreEqual(0,session.State.Party[0].Pokemon.CurrentHp);
        Assert.AreEqual(CanonicalJson.Hash(original.Party),CanonicalJson.Hash(session.State.Party));
        Assert.AreEqual(CanonicalJson.Hash(original.Inventory),CanonicalJson.Hash(session.State.Inventory));
        Reject(session,new("field_move_ack",Text:wait),"field_move_not_active");
    }

    [TestMethod]
    public void AllImportedHealingLocationsRemainTeleportTargetsAcrossFlyRegions()
    {
        var basis=Place(WithMove(FieldFixture(),63,NoncombatFieldMoveRules.Teleport),"map.route-1",1);
        foreach(var location in content.FieldFly!.Destinations)
        {
            var session=Start(basis with{LastHealingLocation=location.HealLocation});
            Send(session,new("field_move",Text:NoncombatFieldMoveRules.Teleport));
            var destination=session.State.NoncombatFieldEffect!.Destination!;
            Assert.AreEqual(location.MapId,destination.MapId);
            Assert.AreEqual((location.X,location.Y),(destination.X,destination.Y));
            Send(session,new("field_move_confirm",Choice:1));
        }
    }

    [TestMethod]
    public void DigSharesEscapeRopeDestinationAndDoesNotNeedOrConsumeAnItem()
    {
        var state=Place(WithMove(FieldFixture(),51,NoncombatFieldMoveRules.Dig),"map.mt-moon-1f",1);
        var destination=content.EscapeRoutes.Exits[state.World.Player.MapId][0];
        state=state with{EscapeDestination=destination,Inventory=state.Inventory! with{BagItems=state.Inventory.BagItems.RemoveAll(slot=>slot.Item==EscapeRopeRules.Item)}};
        var session=Start(state);var party=CanonicalJson.Hash(state.Party);var inventory=CanonicalJson.Hash(state.Inventory);
        Assert.IsNull(content.EscapeRoutes.ResolveArrival(state.World.Player.MapId,destination,out var expected));
        Send(session,new("field_move",Text:NoncombatFieldMoveRules.Dig));Send(session,new("field_move_confirm",Choice:0));
        Ack(session);Ack(session);Assert.AreEqual(expected,session.State.World.Player);Ack(session);
        Assert.AreEqual(party,CanonicalJson.Hash(session.State.Party));Assert.AreEqual(inventory,CanonicalJson.Hash(session.State.Inventory));
    }

    [TestMethod]
    public void TeleportRejectsIndoorAndDigRejectsOutdoorWithoutMutation()
    {
        var teleport=Start(Place(WithMove(FieldFixture(),63,NoncombatFieldMoveRules.Teleport),"map.players-house-1f"));
        Reject(teleport,new("field_move",Text:NoncombatFieldMoveRules.Teleport),"field_move_cannot_use_here");
        var dig=Start(Place(WithMove(FieldFixture(),51,NoncombatFieldMoveRules.Dig),"map.pallet-town"));
        Reject(dig,new("field_move",Text:NoncombatFieldMoveRules.Dig),"field_move_destination_unavailable");
    }

    [TestMethod]
    public void SoftBoiledRequiresRecipientIdentityAndStaleCommandsCannotHealAgain()
    {
        var state=Place(WithMove(FieldFixture(),35,NoncombatFieldMoveRules.SoftBoiled),"map.pallet-town");
        state=state with{Party=state.Party.SetItem(1,state.Party[1] with{Pokemon=state.Party[1].Pokemon with{CurrentHp=1}})};
        var session=Start(state);int amount=state.Party[0].Pokemon.Stats.MaxHp/5;
        Reject(session,new("field_heal",Text:NoncombatFieldMoveRules.SoftBoiled,Choice:1){TargetInstanceId=state.Party[0].InstanceId},"field_heal_target_stale");
        var command=new CampaignCommand("field_heal",Text:NoncombatFieldMoveRules.SoftBoiled,Choice:1){TargetInstanceId=state.Party[1].InstanceId};
        long oldRevision=session.Revision;Send(session,command);
        Assert.AreEqual(state.Party[0].Pokemon.CurrentHp-amount,session.State.Party[0].Pokemon.CurrentHp);
        Assert.AreEqual(1+amount,session.State.Party[1].Pokemon.CurrentHp);
        var after=session.State;var stale=session.Execute("delayed-heal",oldRevision,command);
        Assert.AreEqual("stale_revision",stale.Error);Assert.AreSame(after,session.State);
        Assert.AreEqual(0,session.State.Party[0].Pokemon.Moves[0].CurrentPp);
    }

    [TestMethod]
    [DataRow(NoncombatFieldMoveRules.SoftBoiled)]
    [DataRow(NoncombatFieldMoveRules.MilkDrink)]
    public void HealingRulesUseDonorMaxFifthStrictThresholdAndRecipientCap(string move)
    {
        var member=checkpoint.State.Party[0];var moveDefinition=catalog.Moves[new(move)];
        member=member with{Pokemon=member.Pokemon with{Moves=[new(moveDefinition.Id,0,moveDefinition.Pp)]}};
        int amount=member.Pokemon.Stats.MaxHp/5;
        var target=checkpoint.State.Party[1];target=target with{Pokemon=target.Pokemon with{CurrentHp=target.Pokemon.Stats.MaxHp-1}};
        var party=ImmutableArray.Create(member,target);
        var result=NoncombatFieldMoveRules.Heal(party,0,1,move);
        Assert.IsTrue(result.Succeeded);Assert.AreEqual(amount,result.DonatedHp);Assert.AreEqual(1,result.RestoredHp);
        Assert.AreEqual(member.Pokemon.CurrentHp-amount,result.Party[0].Pokemon.CurrentHp);
        Assert.AreEqual(target.Pokemon.Stats.MaxHp,result.Party[1].Pokemon.CurrentHp);
        var threshold=party.SetItem(0,member with{Pokemon=member.Pokemon with{CurrentHp=amount}});
        Assert.AreEqual("field_heal_not_enough_hp",NoncombatFieldMoveRules.Heal(threshold,0,1,move).Error);
        Assert.IsTrue(NoncombatFieldMoveRules.Heal(threshold.SetItem(0,member with{Pokemon=member.Pokemon with{CurrentHp=amount+1}}),0,1,move).Succeeded);
        Assert.AreEqual("field_heal_target_invalid",NoncombatFieldMoveRules.Heal(party,0,0,move).Error);
        Assert.AreEqual("field_heal_target_invalid",NoncombatFieldMoveRules.Heal(party.SetItem(1,target with{Pokemon=target.Pokemon with{CurrentHp=0}}),0,1,move).Error);
        Assert.AreEqual("field_heal_target_invalid",NoncombatFieldMoveRules.Heal(result.Party,0,1,move).Error);
        Assert.AreEqual("field_heal_target_invalid",NoncombatFieldMoveRules.Heal(party.SetItem(1,target with{IsEgg=true}),0,1,move).Error);
        Assert.AreEqual("field_heal_move_missing",NoncombatFieldMoveRules.Heal(party.SetItem(0,member with{IsEgg=true}),0,1,move).Error);
        Assert.AreEqual(CanonicalJson.Hash(member.Pokemon.Moves),CanonicalJson.Hash(result.Party[0].Pokemon.Moves));
        Assert.AreEqual(member.Status,result.Party[0].Status);Assert.AreEqual(target.Status,result.Party[1].Status);
    }

    [TestMethod]
    public void SweetScentBypassesRepelAndWalkingRateWithoutTakingAStep()
    {
        var state=WithMove(FieldFixture(),45,NoncombatFieldMoveRules.SweetScent);
        state=state with{Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_REPEL_STEP_COUNT",250)}};
        var session=Start(state);var rng=state.Exploration!.EarlyEncounter!.RateRng;
        StartScent(session);Assert.IsNotNull(session.State.WildBattle);Assert.IsNull(session.State.NoncombatFieldEffect);
        Assert.AreEqual(rng,session.State.Exploration!.EarlyEncounter!.RateRng);
        Assert.AreEqual(0,session.State.Exploration.EarlyEncounter.StepsSinceLastEncounter);
        Assert.AreEqual(state.World.StepCount,session.State.World.StepCount);Assert.AreEqual(250,session.RepelSteps);
        Assert.AreEqual(0,session.State.Party[0].Pokemon.Moves[0].CurrentPp);
    }

    [TestMethod]
    public void SweetScentUsesWaterTableAndSafariDispatcher()
    {
        var state=Place(WithMove(FieldFixture(),45,NoncombatFieldMoveRules.SweetScent),"map.pallet-town",2,true);
        var session=Start(state);StartScent(session);Assert.IsNotNull(session.State.WildBattle);
        var pokemon=session.State.WildBattle!.Frame.Party(Game.Domain.Battle.BattleSide.Opponent).Members[0].Pokemon;
        Assert.IsTrue(content.WaterEncounters![state.World.Player.MapId].Slots.Any(slot=>slot.Species==pokemon.Species));
        var safari=Place(WithMove(FieldFixture(),45,NoncombatFieldMoveRules.SweetScent),"map.safari-zone-center",1);
        // Safari is an intentionally unsaveable active session. Enter through the existing visit fixture seam.
        safari=safari with{SafariVisit=new(30,600,true),Script=safari.Script with{Flags=safari.Script.Flags.SetItem("FLAG_SYS_SAFARI_MODE",true)}};
        var safariSession=CampaignSession.CreateCurrent(content);
        typeof(CampaignSession).GetField("_commands",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(safariSession,
            new CommandSession<CampaignState,CampaignCommand>(safari,safariSession.RulesVersion,content.ContentVersion,checkpoint.Generation));
        StartScent(safariSession);Assert.IsNotNull(safariSession.State.SafariBattle);Assert.IsNull(safariSession.State.WildBattle);
        Assert.AreEqual(600,safariSession.State.SafariVisit!.Steps);
    }

    [TestMethod]
    public void SweetScentRoamerGateRunsBeforeOrdinaryTableAndIgnoresRepel()
    {
        var state=WithMove(FieldFixture(),45,NoncombatFieldMoveRules.SweetScent);
        var rng=state.World.Rng;
        while(Gen3Rng.Next(rng,"probe",4).Trace.Result!=0)rng=Gen3Rng.Next(rng,"probe").State;
        state=state with{World=state.World with{Rng=rng},Roamer=state.Roamer! with{Runtime=state.Roamer.Runtime with{Location=content.CelioRoamerMaps![state.World.Player.MapId]}},
            Script=state.Script with{Variables=state.Script.Variables.SetItem("VAR_REPEL_STEP_COUNT",250)}};
        var session=Start(state);StartScent(session);
        Assert.IsNotNull(session.State.WildBattle?.CelioRoamer);
        Assert.AreEqual(RoamerEncounterMethod.SweetScentLand,session.State.WildBattle!.CelioRoamer!.Method);
        Assert.AreEqual(250,session.RepelSteps);
        Assert.AreEqual(state.Roamer.Individual,session.State.WildBattle.CelioRoamer.Individual);
    }

    [TestMethod]
    public void SweetScentFailureRestoresFieldAndDoesNotConsumeRng()
    {
        var state=Place(WithMove(FieldFixture(),45,NoncombatFieldMoveRules.SweetScent),"map.players-house-1f");
        var session=Start(state);StartScent(session);
        Assert.AreEqual("fail",session.State.NoncombatFieldEffect!.Phase);Assert.AreEqual(state.World.Rng,session.State.World.Rng);
        Ack(session);Assert.IsNull(session.State.NoncombatFieldEffect);Assert.IsNotNull(session.State.Dialogue);
        Assert.IsTrue(session.VisibleText!.Contains("nothing here",StringComparison.Ordinal));
    }

    [TestMethod]
    public void TravelAndScentClocksReachTerminalStatesWithSourceMotionAndHold()
    {
        foreach(string move in new[]{NoncombatFieldMoveRules.Dig,NoncombatFieldMoveRules.Teleport})
        foreach(bool outgoing in new[]{false,true})
        {
            var timeline=new FieldReturnTimeline(move,outgoing,Direction.South);
            int ticks=0;while(!timeline.Complete&&ticks++<300)timeline.Tick();
            Assert.IsTrue(timeline.Complete,$"{move}/{outgoing}");
            Assert.AreEqual(outgoing?16:0,timeline.Fade);
            if(!outgoing)Assert.AreEqual(0,timeline.OffsetY);
        }
        var scent=new SweetScentTimeline(false);int frames=0;
        while(!scent.Complete&&frames++<200)scent.Tick();
        Assert.IsTrue(scent.Complete);Assert.IsTrue(frames>=64+25);Assert.AreEqual(8,scent.BackgroundCoefficient);
        var reverse=new SweetScentTimeline(true);frames=0;while(!reverse.Complete&&frames++<100)reverse.Tick();
        Assert.IsTrue(reverse.Complete);Assert.AreEqual(0,reverse.BackgroundCoefficient);
    }
}
