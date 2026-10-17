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
public sealed class MegahornMoveAnimationDispatchTests
{
    private static readonly (string Name,int Id,int Level,int Pp)[] Moves=[("MEGAHORN",224,53,10)];
    private static MoveId Id(string name)=>new("pret:MOVE_"+name);
    private static SourceMechanicsCatalog Mechanics=>TestAssets.Cached(()=>SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events)=>events.Where(e=>e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).ToArray();
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events,int index)=>new("megahorn",new("test"),"test",new(),CampaignDoubleBattleStage.Combat,events,index,[],[],0,0,[],[],[],[],0);
    private static void AssertProjection(SourceKernelResult result,bool expected,int actor,int target)
    {AssertProjection(result.Events,result.State,result.Trace,expected,actor,target);}
    private static void AssertProjection(ImmutableArray<SourceBattleEvent> events,SourceBattleFrame state,ImmutableArray<RandomTrace> trace,bool expected,int actor,int target)
    {
        string before=JsonSerializer.Serialize(events),frame=JsonSerializer.Serialize(state),random=JsonSerializer.Serialize(trace);int dispatched=0;
        var used=events.OfType<SourceMoveUsedEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Id("MEGAHORN"),used.Move);Assert.AreEqual(used.Move,pp.CurrentMove);Assert.AreEqual(actor,pp.Actor.Value);Assert.AreEqual(target,pp.Target.Value);
        for(int i=0;i<events.Length;i++)
        {
            var battle=Double(events,i);var decision=CampaignMoveAnimationPresentation.AtAttackText(battle,SourceBattleScene.On);
            Assert.AreEqual(decision,CampaignMoveAnimationPresentation.AtAttackText(battle,SourceBattleScene.On));Assert.IsNull(CampaignMoveAnimationPresentation.AtHitResult(battle,SourceBattleScene.On).Dispatch);
            if(expected&&events[i]==used)
            {
                var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);dispatched++;var hit=Hits(events).Single();
                Assert.AreEqual(224,cue.SourceMoveId);Assert.AreEqual(actor,cue.Actor.Value);Assert.AreEqual(target,cue.Target.Value);Assert.AreEqual(used.Sequence,cue.Sequence);Assert.AreEqual(hit.Sequence,cue.ResultSequence);Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);Assert.IsTrue(cue.AcknowledgesCurrentReceipt);Assert.IsNull(cue.MoveTurn);Assert.AreEqual(hit is SourceSubstituteDamagedEvent,cue.HitsSubstitute);
            }
            else Assert.IsNull(decision.Dispatch);
        }
        Assert.AreEqual(expected?1:0,dispatched);Assert.AreEqual(before,JsonSerializer.Serialize(events));Assert.AreEqual(frame,JsonSerializer.Serialize(state));Assert.AreEqual(random,JsonSerializer.Serialize(trace));
    }
    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var move = Id(name); var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learnset required: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var move = Moves.Single(m => m.Id == id);
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = NaturalMon("HERACROSS", move.Level, move.Name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, uint seed)
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
        Assert.IsTrue(entry.Succeeded, entry.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(seed) };
    }
    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        Assert.IsNull(SourceBattleIdentityRules.Validate(frame));
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }
    private static SourceFourActionResult Ordered(SourceBattleFrame frame, int actor, int target)
    {
        var selected = frame.Slots[actor].Body!.Pokemon.Moves[0].Move;
        var choices = frame.Slots.Select(c => {
            var move = Mechanics.Catalog.Moves[c.Body!.Pokemon.Moves[0].Move];
            int recipient = c.Slot.Value == actor ? target : move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : c.Slot.Value ^ 1;
            return new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, new(recipient));
        }).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        for (int step = 0; step < 12; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            if (result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == selected)) return result;
            Assert.IsNotNull(result.Continuation, "The natural Megahorn move must execute within this public turn.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new InvalidOperationException("The selected Megahorn move did not execute in one bounded public turn.");
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
    public void NaturalMegahornUsesOrderedTurnsAtAllFourPositionsAndSelectedAllies()
    {
        Assert.AreEqual("MOVE_TARGET_SELECTED",Mechanics.Catalog.Moves[Id("MEGAHORN")].Target);
        for(int actor=0;actor<4;actor++)foreach(int target in new[]{actor^1,actor^2})
        {
            var result=Ordered(Frame(Bodies(224,actor),0),actor,target);var pp=result.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(10,pp.Before);Assert.AreEqual(9,pp.After);Assert.AreEqual(1,pp.CommandCost);AssertProjection(result.Events,result.State,result.Trace,true,actor,target);
        }
    }
    [TestMethod]
    public void ActualMissProtectAndWonderGuardDoNotCreateMegahornAnimations()
    {
        for(int actor=0;actor<4;actor++)for(int branch=0;branch<3;branch++)
        {
            int target=actor^1;var bodies=Bodies(224,actor);
            if(branch==0)bodies[actor]=bodies[actor] with{AccuracyStage=0};
            if(branch==1)bodies[target]=bodies[target] with{Protected=true};
            if(branch==2)bodies[target]=NaturalMon("SHEDINJA",100,"HARDEN");
            var result=branch==0?Find(bodies,actor,target,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"Megahorn miss").Result:Resolve(Frame(bodies,0),actor,target);
            if(branch==2)Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Ability=="ABILITY_WONDER_GUARD")||result.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect));
            AssertProjection(result,false,actor,target);
        }
    }
    [TestMethod]
    public void SubstituteBreakAndTargetKoKeepOneCommittedMegahornInvocation()
    {
        for(int actor=0;actor<4;actor++)foreach(bool ko in new[]{false,true})
        {
            int target=actor^1;var bodies=Bodies(224,actor);bodies[target]=ko?bodies[target] with{Pokemon=bodies[target].Pokemon with{CurrentHp=1}}:bodies[target] with{CalledVolatile=new(){SubstituteHp=1}};
            var result=Resolve(Frame(bodies,0),actor,target);
            if(ko)Assert.AreEqual(0,result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);else Assert.AreEqual(0,result.Events.OfType<SourceSubstituteDamagedEvent>().Single().HpAfter);
            AssertProjection(result,true,actor,target);
        }
    }
    [TestMethod]
    public void ContactRoughSkinActorKoDoesNotReplayMegahorn()
    {
        for(int actor=0;actor<4;actor++)
        {
            int target=actor^1;var bodies=Bodies(224,actor);bodies[actor]=bodies[actor] with{Pokemon=bodies[actor].Pokemon with{CurrentHp=1}};bodies[target]=NaturalMon("SHARPEDO",100,"BITE");
            var result=Resolve(Frame(bodies,0),actor,target);var hit=result.Events.OfType<SourceHpChangedEvent>().Single();var contact=result.Events.OfType<SourceAbilityDamageEvent>().Single();Assert.AreEqual(actor,contact.Target.Value);Assert.AreEqual(0,contact.HpAfter);Assert.IsTrue(hit.Sequence<contact.Sequence);AssertProjection(result,true,actor,target);
        }
    }
}
