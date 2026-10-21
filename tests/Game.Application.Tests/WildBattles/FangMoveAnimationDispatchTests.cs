using static FireRed.Tests.Application.WildBattles.DirectHitAnimationFixture;
using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class FangMoveAnimationDispatchTests
{
    private static readonly (string Name, int Id, string Species, int Level)[] Moves =
        [("HYPER_FANG",158,"RATTATA",13),("SUPER_FANG",162,"RATTATA",34),("POISON_FANG",305,"ZUBAT",41)];

    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("fang-moves", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("fang-moves", null!, new(), events, index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []);

    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var move = Moves.Single(m => m.Id == id);
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("LAPRAS", 100, "SING") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = NaturalMon(move.Species, move.Level, move.Name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        return result;
    }

    private static SourceFourActionResult Run(SourceBattleFrame frame,int actor=0,int target=1)
    {
        if(frame.Slots[actor].Body!.Pokemon.Moves[0].Move==Id("HYPER_FANG"))return Ordered(frame,actor,target);
        var result=Resolve(frame,actor,target);return new(result.State,result.Events,result.Trace,result.Fault);
    }
    private static SourceFourActionResult Find(SourceBattleFrame entered,Func<SourceFourActionResult,bool> predicate,string branch,int actor=0,int target=1)
    {
        for(uint seed=0;seed<1024;seed++){var result=Run(entered with{Rng=new(seed)},actor,target);if(predicate(result))return result;}
        throw new InvalidOperationException($"No observed {branch} in1024 actual action-start seeds.");
    }
    private static bool Qualifying(SourceFourActionResult result,int chance)=>result.Trace.Any(t=>t.Purpose.EndsWith(".secondary-effect",StringComparison.Ordinal)&&t.Raw%100<=chance);
    private static void AssertProjection(SourceFourActionResult result,int id,int actor=0,int target=1)
    {
        string state=JsonSerializer.Serialize(result.State),trace=JsonSerializer.Serialize(result.Trace);var rng=result.State.Rng;
        AssertOne(result.Events,id,actor,target);AssertOne(result.Events,id,actor,target);
        Assert.AreEqual(state,JsonSerializer.Serialize(result.State));Assert.AreEqual(trace,JsonSerializer.Serialize(result.Trace));Assert.AreEqual(rng,result.State.Rng);
    }
    private static void AssertOne(ImmutableArray<SourceBattleEvent> events, int id, int actor = 0, int target = 1)
    {
        var hit = events.Single(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent);
        var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single(); string before = JsonSerializer.Serialize(events); int count = 0;
        Assert.AreEqual(0, events.OfType<SourceMultiHitEvent>().Count());
        for (int index = 0; index < events.Length; index++)
        {
            var battle = Double(events, index); var decision = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On, moves:Mechanics.Catalog.Moves);
            Assert.IsNull(AnimationDispatchFixture.AtResult(battle, SourceBattleScene.On).Dispatch);
            if (events[index] == used)
            {
                var dispatch = decision.Dispatch; Assert.IsNotNull(dispatch, decision.Reason); count++;
                Assert.AreEqual(id, dispatch.SourceMoveId); Assert.AreEqual(actor, dispatch.Actor.Value); Assert.AreEqual(target, dispatch.Target.Value);
                Assert.AreEqual(used.Sequence, dispatch.Sequence); Assert.AreEqual(hit.Sequence, dispatch.ResultSequence);
                Assert.AreEqual(attack.Sequence, dispatch.AttackSequence); Assert.AreEqual(pp.Sequence, dispatch.PpSequence);
                Assert.IsNull(dispatch.MoveTurn); Assert.IsTrue(dispatch.AcknowledgesCurrentReceipt);
                Assert.AreEqual(hit is SourceSubstituteDamagedEvent, dispatch.HitsSubstitute);
                Assert.AreEqual(decision, AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On, moves:Mechanics.Catalog.Moves));
            }
            else Assert.IsNull(decision.Dispatch, "A secondary stat/status/faint page cannot replay the fang root.");
            if (actor < 2 && target < 2)
            {
                var single = Single(events, index);
                Assert.AreEqual(decision.Dispatch, AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On, moves:Mechanics.Catalog.Moves).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.On).Dispatch);
            }
        }
        Assert.AreEqual(1, count); Assert.AreEqual(before, JsonSerializer.Serialize(events));
    }
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events,bool singles=true)
    {
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On, moves:Mechanics.Catalog.Moves).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(Double(events, index), SourceBattleScene.On).Dispatch);
            if(singles)Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On, moves:Mechanics.Catalog.Moves).Dispatch);
        }
    }

    [TestMethod][DataRow(158)][DataRow(162)][DataRow(305)]
    public void NaturalFangsOwnOneCommittedRootAtAllFourPositionsAndSelectedAllies(int id)
    {
        for(int actor=0;actor<4;actor++)foreach(int target in new[]{actor^1,actor^2})
        {
            var result=Run(Frame(Bodies(id,actor),31),actor,target);AssertProjection(result,id,actor,target);
            Assert.AreEqual(1,result.Events.OfType<SourcePpCommandEvent>().Count());Assert.AreEqual(0,result.Events.OfType<SourceMultiHitEvent>().Count());
        }
    }
    [TestMethod][DataRow(100,50)][DataRow(101,51)][DataRow(1,0)]
    public void SuperFangUsesCurrentHpFloorAndIgnoresEndureWithoutCriticalOrDamageDraw(int hp,int after)
    {
        var bodies=Bodies(162);bodies[1]=bodies[1] with{Pokemon=bodies[1].Pokemon with{CurrentHp=hp},Enduring=true};
        var result=Run(Frame(bodies,31));var hit=result.Events.OfType<SourceHpChangedEvent>().Single();Assert.AreEqual(hp,hit.HpBefore);Assert.AreEqual(after,hit.HpAfter);Assert.AreEqual(hp-after,hit.Damage);
        Assert.IsFalse(hit.Critical);Assert.AreEqual(100,hit.RandomPercent);Assert.AreEqual(0,result.Events.OfType<SourceDamageAdjustedEvent>().Count());Assert.AreEqual(0,result.Events.OfType<SourceCriticalHitEvent>().Count());
        Assert.IsFalse(result.Trace.Any(t=>t.Purpose.EndsWith(".damage",StringComparison.Ordinal)||t.Purpose.EndsWith(".critical",StringComparison.Ordinal)||t.Purpose.Contains("focus-band",StringComparison.Ordinal)));
        Assert.AreEqual(1,result.Trace.Count(t=>t.Purpose.EndsWith(".secondary-effect",StringComparison.Ordinal)));AssertProjection(result,162);
    }
    [TestMethod][DataRow(158)][DataRow(162)][DataRow(305)]
    public void SubstituteSurvivalAndBreakKeepOneSubstituteReceiptAndBlockSecondary(int id)
    {
        foreach(int subHp in new[]{1,200})
        {
            var bodies=Bodies(id);bodies[1]=bodies[1] with{Pokemon=bodies[1].Pokemon with{CurrentHp=101},CalledVolatile=new(){SubstituteHp=subHp}};
            var result=Find(Frame(bodies,0),r=>Qualifying(r,id==158?10:30),"substitute secondary roll");var hit=result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
            Assert.AreEqual(subHp,hit.HpBefore);Assert.AreEqual(subHp==1,hit.HpAfter==0);Assert.AreEqual(101,result.State.Slots[1].Body!.Pokemon.CurrentHp);Assert.AreEqual(0,result.Events.OfType<SourceHpChangedEvent>().Count());
            if(id==162)Assert.AreEqual(Math.Min(50,subHp),hit.HpBefore-hit.HpAfter);
            Assert.AreEqual(0,result.Events.OfType<SourceFlinchEvent>().Count());Assert.AreEqual(0,result.Events.OfType<SourceStatusChangedEvent>().Count());AssertProjection(result,id);
        }
    }
    [TestMethod][DataRow(158)][DataRow(162)][DataRow(305)]
    public void ActualProtectMissAndPrimaryTypeImmunityNeverAuthorizeFang(int id)
    {
        var bodies=Bodies(id);bodies[1]=bodies[1] with{Protected=true};var protect=Run(Frame(bodies,31));Assert.IsTrue(protect.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="protected"));AssertDenied(protect.Events);
        bodies=Bodies(id);bodies[0]=bodies[0] with{AccuracyStage=0};bodies[1]=bodies[1] with{EvasionStage=12};var miss=Find(Frame(bodies,0),r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"natural accuracy miss");AssertDenied(miss.Events);
        bodies=Bodies(id);bodies[1]=(id==305?NaturalMon("STEELIX",100,"HARDEN"):NaturalMon("GASTLY",100,"MEAN_LOOK")) with{SpeedStage=0};
        foreach(bool substitute in new[]{false,true})
        {
            if(substitute)bodies[1]=bodies[1] with{CalledVolatile=new(){SubstituteHp=50}};
            var immune=Run(Frame(bodies,31));Assert.IsTrue(immune.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect));Assert.AreEqual(bodies[1].Pokemon.CurrentHp,immune.State.Slots[1].Body!.Pokemon.CurrentHp);AssertDenied(immune.Events);
            if(substitute)Assert.AreEqual(50,immune.State.Slots[1].Body!.CalledVolatile!.SubstituteHp);
        }
    }
    [TestMethod]
    public void ActualHyperFlinchAndInnerFocusOrShieldDustPreventionDoNotReplayRoot()
    {
        var successful=Find(Frame(Bodies(158),0),r=>r.Events.OfType<SourceFlinchEvent>().Any(e=>e.Applied),"Hyper Fang flinch");AssertProjection(successful,158);
        foreach(var target in new[]{("ZUBAT","MEAN_LOOK","ABILITY_INNER_FOCUS"),("CATERPIE","STRING_SHOT","ABILITY_SHIELD_DUST")})
        {
            var bodies=Bodies(158);bodies[1]=NaturalMon(target.Item1,100,target.Item2) with{SpeedStage=0};Assert.AreEqual(target.Item3,bodies[1].Pokemon.AbilityId);
            var blocked=Find(Frame(bodies,0),r=>Qualifying(r,10),"prevented Hyper Fang flinch");Assert.AreEqual(0,blocked.Events.OfType<SourceFlinchEvent>().Count());Assert.AreEqual(0,blocked.Events.OfType<SourceAbilityPreventedEvent>().Count());AssertProjection(blocked,158);
        }
    }
    [TestMethod]
    public void ActualToxicSuccessAndSilentStatusPreventionKeepOnePrimaryRoot()
    {
        var toxic=Find(Frame(Bodies(305),0),r=>r.Events.OfType<SourceStatusChangedEvent>().Any(e=>e.After==WildBattleSourceFieldEffects.ToxicPoison),"Poison Fang toxic success");var status=toxic.Events.OfType<SourceStatusChangedEvent>().Single();Assert.AreEqual("move",status.Reason);Assert.IsTrue(status.Sequence>toxic.Events.OfType<SourceHpChangedEvent>().Single().Sequence);AssertProjection(toxic,305);
        foreach(var target in new[]{("SNORLAX","DEFENSE_CURL","ABILITY_IMMUNITY"),("CATERPIE","STRING_SHOT","ABILITY_SHIELD_DUST")})
        {
            var bodies=Bodies(305);bodies[1]=NaturalMon(target.Item1,100,target.Item2);Assert.AreEqual(target.Item3,bodies[1].Pokemon.AbilityId);var blocked=Find(Frame(bodies,0),r=>Qualifying(r,30),"prevented toxic");
            Assert.AreEqual(0,blocked.Events.OfType<SourceStatusChangedEvent>().Count());Assert.AreEqual(0,blocked.Events.OfType<SourceAbilityPreventedEvent>().Count());AssertProjection(blocked,305);
        }
        foreach(bool safeguard in new[]{false,true})
        {
            var bodies=Bodies(305);if(!safeguard)bodies[1]=bodies[1] with{Status=WildBattleStatus.Paralysis};var frame=Frame(bodies,0);if(safeguard)frame=frame with{Sides=frame.Sides.SetItem(1,frame.Sides[1] with{SafeguardTurnsRemaining=5})};
            var blocked=Find(frame,r=>Qualifying(r,30),"Safeguard or existing status toxic prevention");Assert.AreEqual(0,blocked.Events.OfType<SourceStatusChangedEvent>().Count());AssertProjection(blocked,305);
        }
    }
    [TestMethod][DataRow("TENTACOOL","BARRIER",false)][DataRow("BULBASAUR","GROWTH",false)]
    [DataRow("TENTACOOL","BARRIER",true)][DataRow("BULBASAUR","GROWTH",true)]
    public void RealLateToxicTypeNoEffectFollowsSuccessfulDamageOrEndureAtEveryPosition(string species,string move,bool endure)
    {
        for(int actor=0;actor<4;actor++)
        {
            int target=actor^1;var bodies=Bodies(305,actor);bodies[target]=NaturalMon(species,100,move);if(endure)bodies[target]=bodies[target] with{Pokemon=bodies[target].Pokemon with{CurrentHp=1},Enduring=true};
            var result=Find(Frame(bodies,0),r=>r.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect),"late toxic type prevention",actor,target);
            var hit=result.Events.OfType<SourceHpChangedEvent>().Single();var late=result.Events.OfType<SourceEffectivenessEvent>().Single(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect);Assert.IsTrue(late.Sequence>hit.Sequence);Assert.AreEqual(0,result.Events.OfType<SourceStatusChangedEvent>().Count());
            if(endure)
            {
                Assert.AreEqual(1,hit.HpBefore);Assert.AreEqual(1,hit.HpAfter);Assert.AreEqual(0,hit.Damage);var adjusted=result.Events.OfType<SourceDamageAdjustedEvent>().Single();Assert.IsTrue(adjusted.Endured);Assert.IsTrue(adjusted.DamageBefore>0);Assert.AreEqual(0,adjusted.DamageAfter);Assert.IsTrue(adjusted.Sequence<hit.Sequence);
                var survived=result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="endure"&&e.Reason=="endured");Assert.IsTrue(survived.Sequence>hit.Sequence&&survived.Sequence<late.Sequence);
            }
            else Assert.IsTrue(hit.Damage>0&&hit.HpAfter>0);
            Assert.AreEqual(species=="TENTACOOL",result.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NotVeryEffective));AssertProjection(result,305,actor,target);
        }
    }
    [TestMethod][DataRow(false)][DataRow(true)]
    public void ZeroLossLateToxicRequiresTheActualCriticalAndSurvivalOrder(bool critical)
    {
        var bodies=Bodies(305);bodies[1]=NaturalMon("BULBASAUR",100,"GROWTH") with{Enduring=true};bodies[1]=bodies[1] with{Pokemon=bodies[1].Pokemon with{CurrentHp=1}};
        var result=Find(Frame(bodies,0),r=>r.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect)&&r.Events.OfType<SourceHpChangedEvent>().Single().Critical==critical,"critical/noncritical zero-loss late toxic");AssertProjection(result,305);
        var events=result.Events;var hp=events.OfType<SourceHpChangedEvent>().Single();Assert.AreEqual(0,hp.Damage);Assert.AreEqual(critical?1:0,events.OfType<SourceCriticalHitEvent>().Count());
        if(critical)
        {
            var marker=events.OfType<SourceCriticalHitEvent>().Single();var survived=events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="endure"&&e.Reason=="endured");Assert.IsTrue(hp.Sequence<marker.Sequence&&marker.Sequence<survived.Sequence);AssertDenied(events.Remove(marker));AssertDenied(events.Replace(marker,marker with{Target=new(3)}),singles:false);AssertDenied(events.Replace(marker,marker with{Target=new(0)}));
        }
    }
    [TestMethod]
    public void LateToxicClassificationRequiresMetadataAndExactSurvivalPrefix()
    {
        var bodies=Bodies(305);bodies[1]=NaturalMon("BULBASAUR",100,"GROWTH") with{Enduring=true};bodies[1]=bodies[1] with{Pokemon=bodies[1].Pokemon with{CurrentHp=1}};
        var result=Find(Frame(bodies,0),r=>r.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect),"zero-loss late toxic");AssertProjection(result,305);
        var events=result.Events;int used=events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());var wrong=Mechanics.Catalog.Moves.ToDictionary(p=>p.Key,p=>p.Value);wrong[Id("POISON_FANG")]=wrong[Id("POISON_FANG")] with{Effect="EFFECT_HIT"};
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events,used),SourceBattleScene.On,moves:null).Dispatch);Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events,used),true,SourceBattleScene.On,moves:null).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events,used),SourceBattleScene.On,moves:wrong).Dispatch);Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events,used),true,SourceBattleScene.On,moves:wrong).Dispatch);
        var survival=events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="endure"&&e.Reason=="endured");AssertDenied(events.Remove(survival));
        AssertDenied(events.Replace(survival,survival with{Side=new(0)}));var adjusted=events.OfType<SourceDamageAdjustedEvent>().Single();AssertDenied(events.Remove(adjusted));AssertDenied(events.Replace(adjusted,adjusted with{Endured=false}));
        var hit=events.OfType<SourceHpChangedEvent>().Single();AssertDenied(events.Replace(hit,hit with{HpAfter=0}));
    }
    [TestMethod][DataRow(158)][DataRow(162)][DataRow(305)]
    public void ContactFaintAndBadDirectOwnershipDoNotAlterCommittedFangEvents(int id)
    {
        var bodies=Bodies(id);bodies[0]=bodies[0] with{Pokemon=bodies[0].Pokemon with{CurrentHp=1}};bodies[1]=NaturalMon("SHARPEDO",100,"FOCUS_ENERGY") with{SpeedStage=0};var result=Run(Frame(bodies,31));var contact=result.Events.OfType<SourceAbilityDamageEvent>().Single();Assert.AreEqual(0,contact.HpAfter);AssertProjection(result,id);
        var events=result.Events;var pp=events.OfType<SourcePpCommandEvent>().Single();AssertDenied(events.Replace(pp,pp with{Actor=new(1)}));var hit=events.OfType<SourceHpChangedEvent>().Single();AssertDenied(events.Replace(hit,hit with{Target=new(3)}),singles:false);AssertDenied(events.Replace(hit,hit with{Target=new(0)}));
    }
}
