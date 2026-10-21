using static FireRed.Tests.Application.WildBattles.DirectHitAnimationFixture;
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
public sealed class AeroblastMoveAnimationDispatchTests
{
    private static readonly (string Name,int Id,int Level,int Pp)[] Moves=[("AEROBLAST",177,77,5)];

    private static SourceMechanicsCatalog Mechanics=>TestAssets.Cached(()=>SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));

    private static readonly DirectHitAnimationContract Contract = new("aeroblast", 177, Id("AEROBLAST"), 5, DirectHitOwnership.AttackText);
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) => Contract.Double(events, index);
    private static void AssertProjection(SourceKernelResult result,bool expected,int actor,int target)
    {AssertProjection(result.Events,result.State,result.Trace,expected,actor,target);}
    private static void AssertProjection(ImmutableArray<SourceBattleEvent> events,SourceBattleFrame state,ImmutableArray<RandomTrace> trace,bool expected,int actor,int target)
    {
        string before=JsonSerializer.Serialize(events),frame=JsonSerializer.Serialize(state),random=JsonSerializer.Serialize(trace);
        var used=events.OfType<SourceMoveUsedEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Id("AEROBLAST"),used.Move);Assert.AreEqual(used.Move,pp.CurrentMove);Assert.AreEqual(actor,pp.Actor.Value);Assert.AreEqual(target,pp.Target.Value);
        if (expected) Contract.AssertDispatches(events, actor, target);
        else Contract.AssertDenied(events, "blocked source invocation", singles: false);
        Assert.AreEqual(before,JsonSerializer.Serialize(events));Assert.AreEqual(frame,JsonSerializer.Serialize(state));Assert.AreEqual(random,JsonSerializer.Serialize(trace));
    }

    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var move = Moves.Single(m => m.Id == id);
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = NaturalMon("LUGIA", move.Level, move.Name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        Assert.IsNull(SourceBattleIdentityRules.Validate(frame));
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }

    private static (uint Seed, SourceKernelResult Result) Find(WildBattleCombatant[] bodies,
        int actor, int target, Func<SourceKernelResult, bool> predicate, string branch)
    {
        var entered = Frame(bodies, 0);
        // Bounded integration discovery only. Missing branches fail; no product-output golden is generated.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(entered with { Rng = new(seed) }, actor, target);
            if (predicate(result)) return (seed, result);
        }
        throw new InvalidOperationException($"No observed {branch} in 1024 action-start seeds.");
    }

    [TestMethod]
    public void NaturalAeroblastUsesOrderedTurnsAtAllFourPositionsAndSelectedAllies()
    {
        Assert.AreEqual("MOVE_TARGET_SELECTED",Mechanics.Catalog.Moves[Id("AEROBLAST")].Target);
        for(int actor=0;actor<4;actor++)foreach(int target in new[]{actor^1,actor^2})
        {
            var result=Ordered(Frame(Bodies(177,actor),0),actor,target);var pp=result.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(5,pp.Before);Assert.AreEqual(4,pp.After);Assert.AreEqual(1,pp.CommandCost);AssertProjection(result.Events,result.State,result.Trace,true,actor,target);
        }
    }
    [TestMethod]
    public void ActualMissAndProtectDoNotCreateAeroblastAnimations()
    {
        for(int actor=0;actor<4;actor++)for(int branch=0;branch<2;branch++)
        {
            int target=actor^1;var bodies=Bodies(177,actor);
            if(branch==0)bodies[actor]=bodies[actor] with{AccuracyStage=0};
            if(branch==1)bodies[target]=bodies[target] with{Protected=true};
            var result=branch==0?Find(bodies,actor,target,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"Aeroblast miss").Result:Resolve(Frame(bodies,0),actor,target);
            AssertProjection(result,false,actor,target);
        }
    }
    [TestMethod]
    public void SubstituteBreakAndTargetKoKeepOneCommittedAeroblastInvocation()
    {
        for(int actor=0;actor<4;actor++)foreach(bool ko in new[]{false,true})
        {
            int target=actor^1;var bodies=Bodies(177,actor);bodies[target]=ko?bodies[target] with{Pokemon=bodies[target].Pokemon with{CurrentHp=1}}:bodies[target] with{CalledVolatile=new(){SubstituteHp=1}};
            var result=Resolve(Frame(bodies,0),actor,target);
            if(ko)Assert.AreEqual(0,result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);else Assert.AreEqual(0,result.Events.OfType<SourceSubstituteDamagedEvent>().Single().HpAfter);
            AssertProjection(result,true,actor,target);
        }
    }
    [TestMethod]
    public void NonContactAeroblastDoesNotTriggerRoughSkinOrReplayItsReceipt()
    {
        for(int actor=0;actor<4;actor++)
        {
            int target=actor^1;var bodies=Bodies(177,actor);bodies[actor]=bodies[actor] with{Pokemon=bodies[actor].Pokemon with{CurrentHp=1}};bodies[target]=NaturalMon("SHARPEDO",100,"BITE");
            var result=Resolve(Frame(bodies,0),actor,target);Assert.AreEqual(1,result.Events.OfType<SourceHpChangedEvent>().Count());Assert.AreEqual(0,result.Events.OfType<SourceAbilityDamageEvent>().Count());Assert.AreEqual(1,result.State.Slots[actor].Body!.Pokemon.CurrentHp);AssertProjection(result,true,actor,target);
        }
    }
    [TestMethod]
    public void ActualHighCriticalAndOrdinaryDamageEachHaveOneObservationalInvocation()
    {
        for(int actor=0;actor<4;actor++)foreach(bool critical in new[]{false,true})
        {
            var result=Find(Bodies(177,actor),actor,actor^1,r=>r.Events.OfType<SourceCriticalHitEvent>().Any()==critical,"Aeroblast critical branch").Result;AssertProjection(result,true,actor,actor^1);
        }
    }
}
