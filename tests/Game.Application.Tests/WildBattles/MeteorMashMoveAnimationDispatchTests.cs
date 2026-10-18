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
public sealed class MeteorMashMoveAnimationDispatchTests
{
    private static readonly MoveId Mash = Id("METEOR_MASH");
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("meteor-mash", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("meteor-mash", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };

    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var move = Id(name);
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learnset required: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }

    private static WildBattleCombatant[] Bodies(int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL")).ToArray();
        bodies[actor] = NaturalMon("CLEFAIRY", 45, "METEOR_MASH") with { AccuracyStage = 12 };
        return bodies;
    }

    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(31),
            Parties = [new(BattleSide.Player, [Member(bodies[0]), Member(bodies[2])]), new(BattleSide.Opponent, [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(31) };
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        return result;
    }

    private static SourceKernelResult Find(SourceBattleFrame frame, Func<SourceKernelResult, bool> predicate, string branch)
    {
        for (uint seed = 0; seed < 256; seed++)
        {
            var result = Resolve(frame with { Rng = new(seed) });
            if (!predicate(result)) continue;
            Console.WriteLine($"Meteor Mash observed {branch}: seed={seed}; " +
                string.Join(", ", result.Trace.Select(t => $"{t.Purpose}={t.Raw}")));
            return result;
        }
        throw new InvalidOperationException($"No observed {branch} in 256 actual Meteor Mash action-start seeds.");
    }

    private static bool QualifyingSecondary(SourceKernelResult result) =>
        result.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal) && t.Raw % 100 <= 20);

    private static void AssertProjection(SourceKernelResult result, int actor = 0, int target = 1)
    {
        string state = JsonSerializer.Serialize(result.State), trace = JsonSerializer.Serialize(result.Trace);
        var rng = result.State.Rng;
        AssertOne(result.Events, actor, target);
        AssertOne(result.Events, actor, target);
        Assert.AreEqual(state, JsonSerializer.Serialize(result.State));
        Assert.AreEqual(trace, JsonSerializer.Serialize(result.Trace));
        Assert.AreEqual(rng, result.State.Rng);
    }

    private static void AssertOne(ImmutableArray<SourceBattleEvent> events, int actor, int target)
    {
        var hit = events.Single(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        string before = JsonSerializer.Serialize(events);
        int count = 0;
        Assert.AreEqual(0, events.OfType<SourceMultiHitEvent>().Count());
        for (int index = 0; index < events.Length; index++)
        {
            var battle = Double(events, index);
            var decision = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On, moves: Mechanics.Catalog.Moves);
            Assert.AreEqual(decision, AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On),
                "Ordinary damage admission is independent of later Attack-up or contact receipts.");
            Assert.IsNull(AnimationDispatchFixture.AtResult(battle, SourceBattleScene.On).Dispatch);
            if (events[index] == used)
            {
                var dispatch = decision.Dispatch;
                Assert.IsNotNull(dispatch, decision.Reason);
                count++;
                Assert.AreEqual(309, dispatch.SourceMoveId);
                Assert.AreEqual("meteor-mash", dispatch.BattleId);
                Assert.AreEqual(actor, dispatch.Actor.Value);
                Assert.AreEqual(target, dispatch.Target.Value);
                Assert.AreEqual(used.Sequence, dispatch.Sequence);
                Assert.AreEqual(hit.Sequence, dispatch.ResultSequence);
                Assert.AreEqual(attack.Sequence, dispatch.AttackSequence);
                Assert.AreEqual(pp.Sequence, dispatch.PpSequence);
                Assert.IsNull(dispatch.MoveTurn);
                Assert.IsTrue(dispatch.AcknowledgesCurrentReceipt);
                Assert.AreEqual(hit is SourceSubstituteDamagedEvent, dispatch.HitsSubstitute);
            }
            else Assert.IsNull(decision.Dispatch, "HP, survival and stat pages cannot replay the root.");
            if (actor < 2 && target < 2)
            {
                var single = Single(events, index);
                Assert.AreEqual(decision.Dispatch,
                    AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On, moves: Mechanics.Catalog.Moves).Dispatch);
                Assert.AreEqual(decision.Dispatch,
                    AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.On).Dispatch);
            }
        }
        Assert.AreEqual(1, count);
        Assert.AreEqual(before, JsonSerializer.Serialize(events));
    }

    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string label, bool singles = true)
    {
        string before = JsonSerializer.Serialize(events);
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On,
                moves: Mechanics.Catalog.Moves).Dispatch, label);
            Assert.IsNull(AnimationDispatchFixture.AtResult(Double(events, index), SourceBattleScene.On).Dispatch, label);
            if (!singles) continue;
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On,
                moves: Mechanics.Catalog.Moves).Dispatch, label);
            Assert.IsNull(AnimationDispatchFixture.AtResult(Single(events, index), true, SourceBattleScene.On).Dispatch, label);
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(events), label);
    }

    private static void AssertStatWaves(SourceKernelResult result,int actor=0,bool expected=false)
    {
        int count=0;string before=JsonSerializer.Serialize(result.Events);
        for(int i=0;i<result.Events.Length;i++)
        {
            var dispatch=AnimationDispatchFixture.StatAtResult(Double(result.Events,i),moves:Mechanics.Catalog.Moves);
            if(dispatch is not null)
            {
                count++;Assert.IsInstanceOfType<SourceStatChangedEvent>(result.Events[i]);
                Assert.AreEqual(309,dispatch.SourceMoveId);Assert.AreEqual(actor,dispatch.Affected.Value);Assert.AreEqual(15,dispatch.AnimationArgument);
                Assert.AreEqual(result.Events[i].Sequence,dispatch.Sequence);
            }
            if(actor<2&&result.Events.OfType<SourceMoveUsedEvent>().Single().Target.Value<2)
                Assert.AreEqual(dispatch,AnimationDispatchFixture.StatAtResult(Single(result.Events,i),true,moves:Mechanics.Catalog.Moves));
        }
        Assert.AreEqual(expected?1:0,count);Assert.AreEqual(before,JsonSerializer.Serialize(result.Events));
    }

    [TestMethod]
    public void NaturalMeteorMashOwnsOneRootAtEveryPositionAndSelectedAlly()
    {
        var move=Mechanics.Catalog.Moves[Mash];Assert.AreEqual(309,move.SourceMoveId);Assert.AreEqual("EFFECT_ATTACK_UP_HIT",move.Effect);
        Assert.AreEqual("TYPE_STEEL",move.Type);Assert.AreEqual("MOVE_TARGET_SELECTED",move.Target);Assert.AreEqual(100,move.Power);
        Assert.AreEqual(85,move.Accuracy);Assert.AreEqual(10,move.Pp);Assert.AreEqual(20,move.SecondaryEffectChance);
        Assert.IsTrue(move.Flags.Contains("FLAG_MAKES_CONTACT"));
        for(int actor=0;actor<4;actor++)foreach(int target in new[]{actor^1,actor^2})
        {var result=Resolve(Frame(Bodies(actor)),actor,target);Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Single().Damage>0);AssertProjection(result,actor,target);}
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)]
    public void ActualActorAttackBoostUsesTheSharedSecondaryWaveOnce(int actor)
    {
        var entered=Frame(Bodies(actor));SourceKernelResult? successful=null;int target=actor^1;
        for(uint seed=0;seed<256;seed++)
        {var candidate=Resolve(entered with{Rng=new(seed)},actor,target);if(candidate.Events.OfType<SourceStatChangedEvent>().Any(e=>e.Target.Value==actor)){successful=candidate;break;}}
        Assert.IsNotNull(successful);var stat=successful.Events.OfType<SourceStatChangedEvent>().Single();
        Assert.AreEqual(WildBattleStat.Attack,stat.Stat);Assert.AreEqual(actor,stat.Actor.Value);Assert.AreEqual(actor,stat.Target.Value);Assert.AreEqual(1,stat.After-stat.Before);
        Assert.IsTrue(successful.Events.OfType<SourceHpChangedEvent>().Single().Sequence<stat.Sequence);AssertProjection(successful,actor,target);AssertStatWaves(successful,actor,true);
    }

    [TestMethod]
    public void CappedActorRetainsDamageRootWithoutInventingAnotherStatWave()
    {
        var bodies=Bodies();bodies[0]=bodies[0] with{AttackStage=12};
        var result=Find(Frame(bodies),QualifyingSecondary,"qualifying capped Attack boost");
        Assert.AreEqual(12,result.State.Slots[0].Body!.AttackStage);Assert.AreEqual(0,result.Events.OfType<SourceStatChangedEvent>().Count());
        AssertProjection(result);AssertStatWaves(result);
    }

    [TestMethod]
    [DataRow(1)] [DataRow(200)]
    public void SubstituteBreakingAndSurvivingHitsRetainCommittedRecipient(int substituteHp)
    {
        var bodies=Bodies();bodies[1]=bodies[1] with{CalledVolatile=new(){SubstituteHp=substituteHp}};
        var result=Find(Frame(bodies),QualifyingSecondary,"qualifying secondary behind substitute");
        var hit=result.Events.OfType<SourceSubstituteDamagedEvent>().Single();Assert.AreEqual(substituteHp,hit.HpBefore);Assert.AreEqual(substituteHp==1,hit.HpAfter==0);
        Assert.AreEqual(bodies[1].Pokemon.CurrentHp,result.State.Slots[1].Body!.Pokemon.CurrentHp);Assert.AreEqual(0,result.Events.OfType<SourceHpChangedEvent>().Count());
        AssertProjection(result);AssertStatWaves(result,expected:true);
    }

    [TestMethod]
    public void TargetKnockoutStillAllowsTheActorsCommittedAttackIncrease()
    {
        var bodies=Bodies();bodies[1]=bodies[1] with{Pokemon=bodies[1].Pokemon with{CurrentHp=1}};
        var result=Find(Frame(bodies),QualifyingSecondary,"qualifying self-boost after target knockout");
        Assert.AreEqual(0,result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);AssertProjection(result);AssertStatWaves(result,expected:true);
    }

    [TestMethod]
    public void ProtectActualMissAndWonderGuardImmunityRejectAllEntryPages()
    {
        var bodies=Bodies();bodies[1]=bodies[1] with{Protected=true};var protect=Resolve(Frame(bodies));
        Assert.IsTrue(protect.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="protected"));AssertDenied(protect.Events,"Protect");
        bodies=Bodies();bodies[0]=bodies[0] with{AccuracyStage=0};bodies[1]=bodies[1] with{EvasionStage=12};
        var miss=Find(Frame(bodies),r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"actual miss");AssertDenied(miss.Events,"Miss");
        bodies=Bodies();bodies[1]=NaturalMon("SHEDINJA",100,"HARDEN");var immune=Resolve(Frame(bodies));
        Assert.IsTrue(immune.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Ability=="ABILITY_WONDER_GUARD"));AssertDenied(immune.Events,"Wonder Guard");AssertStatWaves(immune);
    }

    [TestMethod]
    public void ContactKnockoutCannotEraseTheCommittedDamageRoot()
    {
        var bodies=Bodies();bodies[0]=bodies[0] with{Pokemon=bodies[0].Pokemon with{CurrentHp=1}};bodies[1]=NaturalMon("SHARPEDO",100,"FOCUS_ENERGY");
        var result=Resolve(Frame(bodies));var contact=result.Events.OfType<SourceAbilityDamageEvent>().Single();Assert.AreEqual(0,contact.HpAfter);Assert.AreEqual(0,contact.Target.Value);
        Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Single().Sequence<contact.Sequence);AssertProjection(result);
    }

    [TestMethod]
    public void OptionsAndMalformedInvocationCannotBorrowAnUnrelatedHit()
    {
        var result=Resolve(Frame(Bodies()));var events=result.Events;int index=events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events,index),SourceBattleScene.Off,moves:Mechanics.Catalog.Moves).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events,index),false,SourceBattleScene.On,moves:Mechanics.Catalog.Moves).Dispatch);
        var pp=events.OfType<SourcePpCommandEvent>().Single();int pi=events.IndexOf(pp);
        AssertDenied(events.SetItem(pi,pp with{PpOwner=new(1)}),"foreign PP owner");
        var attack=events.OfType<SourceAttackStringEvent>().Single();int ai=events.IndexOf(attack);
        AssertDenied(events.SetItem(ai,attack with{ChosenMove=Id("POUND")}),"foreign chosen move");
        AssertProjection(result);
    }
}
