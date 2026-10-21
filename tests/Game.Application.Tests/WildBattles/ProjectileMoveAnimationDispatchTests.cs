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

// Staged against the public Application projections and the public four-position
// kernel. The event-only fixtures are deliberately separate from natural content.
[TestClass, TestCategory("Game")]
public sealed class ProjectileMoveAnimationDispatchTests
{
    private static readonly (string Name, int Id, string Species, int Level, int Pp)[] Moves =
        [("SONIC_BOOM",49,"MAGNEMITE",16,20),("HYPER_BEAM",63,"DRAGONITE",75,5),
         ("WATERFALL",127,"GOLDEEN",38,15),("BONEMERANG",155,"CUBONE",25,10)];

    // Independent source identities and receipt ownership, not production metadata.
    private static DirectHitAnimationContract Contract(int id) => id switch
    {
        49 => new("projectile-moves", 49, Id("SONIC_BOOM"), 20, DirectHitOwnership.AttackText),
        63 => new("projectile-moves", 63, Id("HYPER_BEAM"), 5, DirectHitOwnership.AttackText),
        127 => new("projectile-moves", 127, Id("WATERFALL"), 15, DirectHitOwnership.AttackText),
        155 => new("projectile-moves", 155, Id("BONEMERANG"), 10, DirectHitOwnership.EachHit, 2, 2),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };

    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) => Contract(49).Double(events, index);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) => Contract(49).Single(events, index);
    private static CampaignMoveAnimationDecision AtText(ImmutableArray<SourceBattleEvent> events, int index) =>
        AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On);
    private static CampaignMoveAnimationDecision AtHit(ImmutableArray<SourceBattleEvent> events, int index) =>
        AnimationDispatchFixture.AtResult(Double(events, index), SourceBattleScene.On);

    private static void AssertDispatches(ImmutableArray<SourceBattleEvent> events, int id, int actor = 0, int target = 1) =>
        Contract(id).AssertDispatches(events, actor, target);
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string branch, bool single = true) =>
        Contract(49).AssertDenied(events, branch, single);

    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var move = Moves.Single(m => m.Id == id);
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = NaturalMon(move.Species, move.Level, move.Name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }

    private static (uint Seed, SourceKernelResult Result) Find(WildBattleCombatant[] bodies,
        Func<SourceKernelResult, bool> predicate, string branch)
    {
        var entered = Frame(bodies, 0);
        // Bounded integration branch discovery, not an original-source golden.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(entered with { Rng = new(seed) });
            if (predicate(result)) return (seed, result);
        }
        throw new InvalidOperationException($"No observed {branch} in 1024 action-start seeds.");
    }

    [TestMethod]
    public void NaturalLearnersExecuteOrderedActionsAtEveryPositionAndSelectedAlly()
    {
        foreach(var move in Moves)for(int actor=0;actor<4;actor++)foreach(int target in new[]{actor^1,actor^2})
        {
            var result=Ordered(Frame(Bodies(move.Id,actor),31),actor,target);
            var pp=result.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(move.Pp,pp.Before);Assert.AreEqual(move.Pp-1,pp.After);
            string state=JsonSerializer.Serialize(result.State),trace=JsonSerializer.Serialize(result.Trace);var rng=result.State.Rng;
            AssertDispatches(result.Events,move.Id,actor,target);
            Assert.AreEqual(state,JsonSerializer.Serialize(result.State));Assert.AreEqual(trace,JsonSerializer.Serialize(result.Trace));Assert.AreEqual(rng,result.State.Rng);
            if(move.Id==49)Assert.AreEqual(20,result.Events.OfType<SourceHpChangedEvent>().Single().Damage,"Sonic Boom's source fixed20 damage survives renderer admission.");
            if(move.Id==155){Assert.AreEqual(2,result.Events.OfType<SourceMultiHitEvent>().Single().PlannedHits);Assert.IsFalse(result.Trace.Any(t=>t.Purpose.Contains("multi-hit-count",StringComparison.Ordinal)));}
        }
    }
    [TestMethod][DataRow(49)][DataRow(63)][DataRow(127)][DataRow(155)]
    public void ProtectAccuracyMissAndForeignPpOwnerNeverBorrowAHitInvocation(int id)
    {
        for(int actor=0;actor<4;actor++)
        {
            var bodies=Bodies(id,actor);bodies[actor^1]=bodies[actor^1] with{Protected=true};var blocked=Resolve(Frame(bodies,31),actor,actor^1);
            Assert.AreEqual(0,Hits(blocked.Events).Length);AssertDenied(blocked.Events,"Protect",single:actor<2);
            var success=Resolve(Frame(Bodies(id,actor),31),actor,actor^1);
            var foreign=success.Events.Select(e=>e is SourcePpCommandEvent pp?pp with{PpOwner=new(actor^2)}:e).ToImmutableArray();AssertDenied(foreign,"foreign PP owner",single:false);
            if(actor<2)
            {
                var singlesForeign=success.Events.Select(e=>e is SourcePpCommandEvent pp?pp with{PpOwner=new(actor^1)}:e).ToImmutableArray();
                AssertDenied(singlesForeign,"representable foreign PP owner");
            }
        }
        var inaccurate=Bodies(id);inaccurate[0]=inaccurate[0] with{AccuracyStage=0};
        var miss=Find(inaccurate,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"accuracy miss").Result;
        Assert.AreEqual(0,Hits(miss.Events).Length);AssertDenied(miss.Events,"accuracy miss");
    }
    [TestMethod][DataRow(49,"GASTLY","LICK")][DataRow(63,"GASTLY","LICK")][DataRow(127,"VAPOREON","TACKLE")][DataRow(155,"PIDGEY","TACKLE")]
    public void NaturalImmunityOrAbsorptionProducesNoProjectileInvocation(int id,string species,string move)
    {
        for(int actor=0;actor<4;actor++)
        {
            var bodies=Bodies(id,actor);bodies[actor^1]=NaturalMon(species,100,move);var result=Resolve(Frame(bodies,31),actor,actor^1);
            Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any()||result.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect));
            AssertDenied(result.Events,"natural immunity/absorption",single:actor<2);
        }
    }
    [TestMethod][DataRow(49)][DataRow(63)][DataRow(127)][DataRow(155)]
    public void SubstituteAndEarlyKoRetainOnlyActualCommittedReceiptOwnership(int id)
    {
        for(int actor=0;actor<4;actor++)foreach(bool ko in new[]{false,true})
        {
            var bodies=Bodies(id,actor);int target=actor^1;
            bodies[target]=ko?bodies[target] with{Pokemon=bodies[target].Pokemon with{CurrentHp=1}}:bodies[target] with{CalledVolatile=new(){SubstituteHp=1}};
            var result=Resolve(Frame(bodies,31),actor,target);
            if(ko){Assert.AreEqual(0,result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);if(id==155)Assert.AreEqual(1,result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);}
            else{Assert.AreEqual(0,result.Events.OfType<SourceSubstituteDamagedEvent>().Single().HpAfter);if(id==155)Assert.AreEqual(2,result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);}
            AssertDispatches(result.Events,id,actor,target);
        }
    }
    [TestMethod]
    public void HyperBeamRechargeKeepsOriginalLockPpAndNoSecondAnimation()
    {
        for(int actor=0;actor<4;actor++)
        {
            var hit=Resolve(Frame(Bodies(63,actor),31),actor,actor^1);AssertDispatches(hit.Events,63,actor,actor^1);
            Assert.AreEqual(WildBattleCalledLockKind.Recharge,hit.State.Slots[actor].Registers.Turn!.Lock!.Kind);
            int pp=hit.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp;var rng=hit.State.Rng;
            var recharge=Resolve(hit.State,actor,actor^1);
            Assert.IsTrue(recharge.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="must-recharge"));Assert.AreEqual(0,recharge.Events.OfType<SourceMoveUsedEvent>().Count());Assert.AreEqual(0,recharge.Events.OfType<SourcePpCommandEvent>().Count());
            Assert.AreEqual(pp,recharge.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);Assert.AreEqual(rng,recharge.State.Rng);Assert.AreEqual(0,recharge.Trace.Length);AssertDenied(recharge.Events,"recharge",single:actor<2);
        }
    }
    [TestMethod]
    public void InvalidBonemerangSummaryCannotReplayEitherHit()
    {
        var result=Resolve(Frame(Bodies(155),31));var summary=result.Events.OfType<SourceMultiHitEvent>().Single();
        foreach(var invalid in new[]{summary with{PlannedHits=3},summary with{ActualHits=1},summary with{Actor=new(1)},summary with{Actor=new(2)},summary with{Move=Id("DOUBLE_KICK")}})
            AssertDenied(result.Events.Select(e=>e==summary?invalid:e).ToImmutableArray(),"unmatched source double-hit summary",single:invalid.Actor.Value<2);
    }
}
