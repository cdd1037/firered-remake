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
public sealed class MusicMoveAnimationDispatchTests
{
    private static MoveId Move(int id)=>new("pret:MOVE_"+(id==47?"SING":id==320?"GRASS_WHISTLE":"BELLY_DRUM"));
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("future-sight", new("test"), "test", new(), CampaignDoubleBattleStage.Combat, events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("future-sight", null!, new(), events, index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []);

    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var move = new MoveId("pret:MOVE_" + name);
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level));
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }

    private static WildBattleCombatant[] Bodies(int id,int actor,int mode=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>NaturalMon("SNORLAX",100,"DEFENSE_CURL") with{SpeedStage=0}).ToArray();
        var mon=NaturalMon(id==187?"SNORLAX":id==47?"CLEFAIRY":"ROSELIA",id==187?13:id==47?9:29,id==187?"BELLY_DRUM":id==47?"SING":"GRASS_WHISTLE") with{SpeedStage=12};
        if(id==187){int max=mon.Pokemon.Stats.MaxHp;mon=mon with{AttackStage=mode==2?12:mode==3?0:6,Pokemon=mon.Pokemon with{CurrentHp=mode==1?Math.Max(1,max/2):max}};}
        bodies[actor]=mon;return bodies;
    }

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

    private static void AssertProjection(SourceKernelResult result,int id,int actor,bool expected,bool single=false)
    {
        var events=result.Events;string before=JsonSerializer.Serialize(events),state=JsonSerializer.Serialize(result.State),rng=JsonSerializer.Serialize(result.Trace);int count=0;
        var used=events.OfType<SourceMoveUsedEvent>().Single(e=>e.Move==Move(id));var pp=events.OfType<SourcePpCommandEvent>().Single(e=>e.CurrentMove==Move(id));
        for(int i=0;i<events.Length;i++)
        {
            var decision=single?AnimationDispatchFixture.AtAttackText(Single(events,i),true,SourceBattleScene.On):AnimationDispatchFixture.AtAttackText(Double(events,i),SourceBattleScene.On);
            Assert.IsNull(single?AnimationDispatchFixture.AtResult(Single(events,i),true,SourceBattleScene.On).Dispatch:AnimationDispatchFixture.AtResult(Double(events,i),SourceBattleScene.On).Dispatch);
            if(events[i]==used&&expected){var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);Assert.AreEqual(id,cue.SourceMoveId);Assert.AreEqual(actor,cue.Actor.Value);Assert.AreEqual(used.Target.Value,cue.Target.Value);Assert.AreEqual(pp.Sequence,cue.PpSequence);long receipt=id==187?events.OfType<SourceStatChangedEvent>().Single().Sequence:events.OfType<SourceStatusChangedEvent>().Single().Sequence;Assert.AreEqual(receipt,cue.ResultSequence);count++;}
            else Assert.IsNull(decision.Dispatch);
            if(id==187)Assert.IsNull(AnimationDispatchFixture.StatAtResult(Double(events,i),moves:Mechanics.Catalog.Moves),"Belly Drum has no source General_StatsChange command.");
        }
        Assert.AreEqual(expected?1:0,count);Assert.AreEqual(before,JsonSerializer.Serialize(events));Assert.AreEqual(state,JsonSerializer.Serialize(result.State));Assert.AreEqual(rng,JsonSerializer.Serialize(result.Trace));
    }
    private static SourceKernelResult Find(SourceBattleFrame frame,int actor,int target,Func<SourceKernelResult,bool> predicate)
    {for(uint seed=0;seed<256;seed++){var result=Resolve(frame with{Rng=new(seed)},actor,target);if(predicate(result))return result;}throw new AssertFailedException("No bounded source RNG witness.");}
    [TestMethod][DataRow(47)][DataRow(320)]
    public void NaturalSleepLearnersOwnAllPositionsAndSelectedAlliesWithoutChangingState(int id)
    {
        for(int actor=0;actor<4;actor++)foreach(int target in new[]{actor^1,actor^2})
        {var result=Find(Frame(Bodies(id,actor)),actor,target,r=>r.Events.OfType<SourceStatusChangedEvent>().Any());AssertProjection(result,id,actor,true);}
        var single=Find(Frame(Bodies(id,0),true),0,1,r=>r.Events.OfType<SourceStatusChangedEvent>().Any());AssertProjection(single,id,0,true,true);
    }
    [TestMethod][DataRow(47)][DataRow(320)]
    public void SleepAccuracyFailureAndExistingStatusNeverPlayTheDedicatedRoot(int id)
    {
        var frame=Frame(Bodies(id,0),true);var miss=Find(frame,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Any());AssertProjection(miss,id,0,false,true);
        var bodies=Bodies(id,0);bodies[1]=bodies[1] with{Status=WildBattleStatus.Burn};var blocked=Resolve(Frame(bodies,true),0,1);Assert.IsFalse(blocked.Events.OfType<SourceStatusChangedEvent>().Any());AssertProjection(blocked,id,0,false,true);
    }
    [TestMethod][DataRow(47)][DataRow(320)]
    public void NaturalSelectedSleepMovesRejectSelfTargetsBeforeAnyAnimationReceipt(int id)
    {
        for(int actor=0;actor<4;actor++)
        {
            var frame=Frame(Bodies(id,actor));var result=SourceBattleKernel.ResolveAction(Mechanics.Catalog,frame,new(frame.Slots[actor].Occupant!.Value,frame.Revision,0,new(actor)));
            Assert.IsFalse(result.Succeeded);Assert.AreEqual("source_kernel_target_mode",result.Fault);Assert.IsTrue(result.Events.IsEmpty);Assert.IsTrue(result.Trace.IsEmpty);Assert.AreEqual(frame,result.State);
        }
    }
    [TestMethod]
    public void BellyDrumAuthenticatesExactHalfHpCostAndMaxAttackAcrossAllActorSlots()
    {
        for(int actor=0;actor<4;actor++)foreach(int selected in new[]{actor,actor^1,actor^2})foreach(int mode in new[]{0,3})
        {var bodies=Bodies(187,actor,mode);var turn=Begin(Frame(bodies),actor,selected);for(int n=0;!turn.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Move==Move(187))&&n<12;n++)turn=Next(turn);Assert.IsTrue(turn.Succeeded,turn.Fault);var result=new SourceKernelResult(turn.State,turn.Events,turn.Trace,null,turn.Fault);var stat=result.Events.OfType<SourceStatChangedEvent>().Single();var hp=result.Events.OfType<SourceHpChangedEvent>().Single();Assert.AreEqual(12,stat.After);Assert.AreEqual(Math.Max(1,bodies[actor].Pokemon.Stats.MaxHp/2),hp.Damage);Assert.IsTrue(stat.Sequence<hp.Sequence);Assert.AreEqual(actor,hp.Actor.Value);Assert.AreEqual(actor,hp.Target.Value);AssertProjection(result,187,actor,true);}
        AssertProjection(Resolve(Frame(Bodies(187,0),true),0,0),187,0,true,true);
    }
    [TestMethod][DataRow(1)][DataRow(2)]
    public void BellyDrumLowHpAndCappedAttackFailWithoutHpDebitOrAnimation(int mode)
    {var result=Resolve(Frame(Bodies(187,0,mode),true),0,0);Assert.IsFalse(result.Events.OfType<SourceHpChangedEvent>().Any());Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any());Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any());AssertProjection(result,187,0,false,true);}
    [TestMethod]
    public void BellyDrumReceiptOrderOwnersAndCostArithmeticFailClosed()
    {
        var result=Resolve(Frame(Bodies(187,0)),0,0);var events=result.Events;var stat=events.OfType<SourceStatChangedEvent>().Single();var hp=events.OfType<SourceHpChangedEvent>().Single();int used=events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single()),si=events.IndexOf(stat),hi=events.IndexOf(hp);
        void Reject(ImmutableArray<SourceBattleEvent> malformed)=>Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(malformed,used),SourceBattleScene.On).Dispatch);
        foreach(var replacement in new[]{stat with{Actor=new(1)},stat with{Target=new(1)},stat with{After=11},stat with{Before=12},stat with{Stat=WildBattleStat.Defense},stat with{Sequence=hp.Sequence+1}})Reject(events.SetItem(si,replacement));
        foreach(var replacement in new[]{hp with{Actor=new(1)},hp with{Target=new(1)},hp with{Damage=0},hp with{HpAfter=hp.HpAfter+1},hp with{Critical=true},hp with{RandomPercent=99},hp with{Move=new("pret:MOVE_RECOVER")},hp with{Sequence=stat.Sequence-1}})Reject(events.SetItem(hi,replacement));
        Reject(events.RemoveAt(si));Reject(events.RemoveAt(hi));Reject(events.Insert(hi,hp));Reject(events.SetItem(si,hp).SetItem(hi,stat));
    }
}
