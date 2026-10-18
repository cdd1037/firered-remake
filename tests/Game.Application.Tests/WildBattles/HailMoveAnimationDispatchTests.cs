using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class HailMoveAnimationDispatchTests
{
    private static readonly MoveId Hail=new("pret:MOVE_HAIL");
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("hail", new("test"), "test", new(), CampaignDoubleBattleStage.Combat, events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("hail", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };

    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var move = new MoveId("pret:MOVE_" + name);
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level));
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }

    private static WildBattleCombatant[] Bodies(int actor)
    {var bodies=Enumerable.Range(0,4).Select(_=>NaturalMon("SNORLAX",100,"DEFENSE_CURL") with{SpeedStage=0}).ToArray();bodies[actor]=NaturalMon("SPHEAL",31,"HAIL") with{SpeedStage=12};return bodies;}

    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single = false)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = single ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = single ? KoReplacementTiming.SourceImmediate : KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = single ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer,
            SourceAbsentMask = (byte)(single ? 12 : 0), TurnStartAbsentMask = (byte)(single ? 12 : 0), Rng = new(31),
            Parties = [new(BattleSide.Player, single ? [Member(bodies[0])] : [Member(bodies[0]), Member(bodies[2])]),
                new(BattleSide.Opponent, single ? [Member(bodies[1])] : [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => single && i >= 2 ? new SourceBattleSlot(new(i), null, 0, null)
                : new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(0) };
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }

    private static SourceFourActionResult Begin(SourceBattleFrame frame, int actor, int target)
    {
        var choices = frame.Slots.Take(frame.BattlerCount).Select(c => new SourceActionSelection(c.Occupant!.Value,
            SourceActionKind.Move, 0, new(c.Slot.Value == actor ? target : c.Slot.Value))).ToImmutableArray();
        return SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
    }

    private static SourceFourActionResult Next(SourceFourActionResult result)
    {
        Assert.IsNotNull(result.Continuation, "Expected a retained public turn continuation.");
        var next = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        Assert.IsTrue(next.Succeeded, next.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(next.State)); return next;
    }


    private static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events,int index,bool single=false)=>single
        ?AnimationDispatchFixture.AtAttackText(Single(events,index),true,SourceBattleScene.On)
        :AnimationDispatchFixture.AtAttackText(Double(events,index),SourceBattleScene.On);
    private static void AssertSetup(SourceKernelResult result,int actor,bool expected,bool single=false)
    {
        string before=JsonSerializer.Serialize(result);int count=0;
        for(int i=0;i<result.Events.Length;i++)
        {
            var cue=Project(result.Events,i,single).Dispatch;
            if(cue?.SourceMoveId==258)
            {
                count++;Assert.IsInstanceOfType<SourceMoveUsedEvent>(result.Events[i]);Assert.AreEqual(actor,cue.Actor.Value);Assert.AreEqual(actor,cue.Target.Value);
                var receipt=result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="hail"&&e.Reason=="move");
                Assert.AreEqual(receipt.Sequence,cue.ResultSequence);Assert.AreEqual(0,receipt.Before);Assert.AreEqual(5,receipt.After);
            }
            Assert.AreNotEqual(258,(single?AnimationDispatchFixture.AtResult(Single(result.Events,i),true,SourceBattleScene.On):AnimationDispatchFixture.AtResult(Double(result.Events,i),SourceBattleScene.On)).Dispatch?.SourceMoveId??0);
        }
        Assert.AreEqual(expected?1:0,count);Assert.AreEqual(before,JsonSerializer.Serialize(result));
    }
    [TestMethod]
    public void NaturalHailOwnsEveryActorAndSinglesWithoutChangingCommittedReceipts()
    {
        for(int actor=0;actor<4;actor++)AssertSetup(Resolve(Frame(Bodies(actor)),actor,actor),actor,true);
        foreach(int actor in new[]{0,1})AssertSetup(Resolve(Frame(Bodies(actor),true),actor,actor),actor,true,true);
    }
    [TestMethod,DataRow("rain"),DataRow("sun"),DataRow("sandstorm")]
    public void SuccessfulHailReplacesOtherWeather(string weather)
    {
        var frame=Frame(Bodies(0));frame=frame with{Field=frame.Field with{RainTurnsRemaining=weather=="rain"?3:0,SunTurnsRemaining=weather=="sun"?3:0,SandstormTurnsRemaining=weather=="sandstorm"?3:0}};
        var result=Resolve(frame,0,0);AssertSetup(result,0,true);Assert.AreEqual(5,result.State.Field.HailTurnsRemaining);Assert.AreEqual(0,result.State.Field.RainTurnsRemaining+result.State.Field.SunTurnsRemaining+result.State.Field.SandstormTurnsRemaining);
    }
    [TestMethod]
    public void AlreadyHailFailsWithoutAnotherSetupRoot()
    {
        var first=Resolve(Frame(Bodies(0)),0,0);var second=Resolve(first.State,0,0);
        Assert.IsTrue(second.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="already-hail"));AssertSetup(second,0,false);
    }
    [TestMethod]
    public void ForecastAndAirLockPreserveWeatherSetupOwnership()
    {
        foreach(bool suppressed in new[]{false,true})
        {
            var bodies=Bodies(0);bodies[1]=NaturalMon("CASTFORM",30,"TACKLE");if(suppressed)bodies[3]=NaturalMon("RAYQUAZA",70,"TWISTER");
            var result=Resolve(Frame(bodies),0,0);AssertSetup(result,0,true);Assert.AreEqual(5,result.State.Field.HailTurnsRemaining);
            Assert.AreEqual(suppressed?SourceCastformForm.Normal:SourceCastformForm.Snowy,result.State.Slots[1].Body!.CalledOverlay?.CastformForm??SourceCastformForm.Normal);
        }
    }
    [TestMethod]
    public void WeatherDamagePagesDoNotReplayDedicatedSetup()
    {
        var result=Begin(Frame(Bodies(0)),0,0);int cues=0,damage=0;
        for(int step=0;;step++)
        {
            Assert.IsTrue(step<32);Assert.IsTrue(result.Succeeded,result.Fault);string before=JsonSerializer.Serialize(result);
            for(int i=0;i<result.Events.Length;i++){var cue=Project(result.Events,i).Dispatch;if(cue?.SourceMoveId==258){cues++;Assert.IsInstanceOfType<SourceMoveUsedEvent>(result.Events[i]);}}
            damage+=result.Events.OfType<SourceWeatherDamageEvent>().Count(e=>e.Weather=="hail");Assert.AreEqual(before,JsonSerializer.Serialize(result));
            if(result.CompletedCleanTurn)break;result=Next(result);
        }
        Assert.AreEqual(1,cues);Assert.IsTrue(damage>0);
    }
    [TestMethod]
    public void MissingForeignDuplicateAndCalledWeatherReceiptsFailClosed()
    {
        var result=Resolve(Frame(Bodies(0)),0,0);var events=result.Events;var attack=events.OfType<SourceMoveUsedEvent>().Single();int at=events.IndexOf(attack);var receipt=events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="hail");int ri=events.IndexOf(receipt);
        void Reject(ImmutableArray<SourceBattleEvent> changed)=>Assert.IsNull(Project(changed,at).Dispatch);
        foreach(var replacement in new[]{receipt with{Side=new(1)},receipt with{Volatile="rain"},receipt with{Before=1},receipt with{After=4},receipt with{Reason="residual"}})Reject(events.SetItem(ri,replacement));
        Reject(events.RemoveAt(ri));Reject(events.Insert(ri,receipt));
        var attackString=events.OfType<SourceAttackStringEvent>().Single();
        Reject(events.SetItem(events.IndexOf(attackString),attackString with{ChosenMove=new("pret:MOVE_METRONOME")}));
        Reject(events.SetItem(at,attack with{Target=new(1)}));
        var pp=events.OfType<SourcePpCommandEvent>().Single();int pi=events.IndexOf(pp);
        Reject(events.SetItem(pi,pp with{ChosenMove=new("pret:MOVE_METRONOME")}));
        Reject(events.SetItem(pi,pp with{CommandCost=0}));Reject(events.SetItem(pi,pp with{Actor=new(1)}));
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events,at),SourceBattleScene.Off).Dispatch);
    }
}
