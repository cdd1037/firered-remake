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
public sealed class ImpactAdapterMoveAnimationDispatchTests
{
    private static readonly (int Id, string Name, string Species, int Level, int Pp)[] Moves =
        [(132,"CONSTRICT","TENTACOOL",12,35), (146,"DIZZY_PUNCH","KANGASKHAN",43,10),
         (282,"KNOCK_OFF","FARFETCHD",21,20), (302,"NEEDLE_ARM","CACNEA",37,15),
         (338,"FRENZY_PLANT","VENUSAUR",50,5)];
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static MoveTutorCatalog Tutors => TestAssets.Cached(() =>
        PretMoveTutors.Load(TestAssets.Root, Mechanics.Catalog).Catalog.WithCapeBrink(CapeBrinkTutorCatalog.Authenticate(Mechanics.Catalog)));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("impact-adapter-moves", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("impact-adapter-moves", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };

    private static WildBattleCombatant NaturalMon(string species, int level, string name, uint personality = 0)
    {
        var move = Id(name);
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
        if (name == "FRENZY_PLANT")
        {
            var party = new PartyPokemon(1, pokemon with { Friendship = 255 }, "VENUSAUR", "TEST", 0, 2,
                "MAP_PALLET_TOWN", level, 4, "ITEM_POKE_BALL", "ITEM_NONE");
            var offer = CapeBrinkTutorRules.Offer(Tutors.CapeBrink!, [party], ImmutableHashSet<string>.Empty);
            Assert.IsTrue(offer.Allowed, offer.Error); Assert.AreEqual(move, offer.Tutor!.Move);
            var taught = MoveTutorRules.Teach(Tutors, Mechanics.Catalog, party, offer.Tutor.Index,
                pokemon.Moves.Length == 4 ? 0 : null);
            Assert.IsTrue(taught.Succeeded, taught.Error);
            pokemon = taught.Pokemon.Pokemon;
            Assert.AreEqual(move, pokemon.Moves[taught.LearnedSlot].Move);
        }
        else Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learner required: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX",100,"DEFENSE_CURL") with { SpeedStage = 2 }).ToArray();
        if (id == 168) bodies[actor] = NaturalMon("POOCHYENA",45,"THIEF");
        else { var move = Moves.Single(m => m.Id == id); bodies[actor] = NaturalMon(move.Species,move.Level,move.Name); }
        bodies[actor] = bodies[actor] with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, uint seed = 31, bool single = false)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = single ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = single ? KoReplacementTiming.SourceImmediate : KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = single ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer,
            SourceAbsentMask = (byte)(single ? 12 : 0), TurnStartAbsentMask = (byte)(single ? 12 : 0), Rng = new(31),
            Parties = [new(BattleSide.Player, single ? [Member(bodies[0])] : [Member(bodies[0]),Member(bodies[2])]),
                new(BattleSide.Opponent, single ? [Member(bodies[1])] : [Member(bodies[1]),Member(bodies[3])])],
            Slots = Enumerable.Range(0,4).Select(i => single && i >= 2 ? new SourceBattleSlot(new(i),null,0,null)
                : new SourceBattleSlot(new(i),new((BattleSide)(i & 1),i / 2),1,bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog,frame,frame.Revision);
        Assert.IsTrue(entry.Succeeded,entry.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(seed) };
    }
    private static SourceFourActionResult Run(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        var selected = frame.Slots[actor].Body!.Pokemon.Moves[0].Move;
        if (selected != Id("NEEDLE_ARM"))
        {
            var hit = SourceBattleKernel.ResolveAction(Mechanics.Catalog,frame,
                new(frame.Slots[actor].Occupant!.Value,frame.Revision,0,new(target)));
            Assert.IsTrue(hit.Succeeded,hit.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(hit.State));
            return new(hit.State,hit.Events,hit.Trace,hit.Fault);
        }
        var choices = frame.Slots.Where(c => c.Body is not null).Select(c => {
            var move = Mechanics.Catalog.Moves[c.Body!.Pokemon.Moves[0].Move];
            int recipient = c.Slot.Value == actor ? target : move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : c.Slot.Value ^ 1;
            return new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,new(recipient));
        }).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog,frame,frame.Revision,choices);
        for (int step = 0; step < 12; step++)
        {
            Assert.IsTrue(result.Succeeded,result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            if (result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == selected)) return result;
            Assert.IsNotNull(result.Continuation,"Needle Arm must execute within its public ordered turn.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog,result.State,result.Continuation.Value);
        }
        throw new InvalidOperationException("Needle Arm did not execute in one bounded source turn.");
    }
    private static SourceFourActionResult Find(SourceBattleFrame entered, Func<SourceFourActionResult,bool> predicate,
        string branch, int actor = 0, int target = 1)
    {
        // Bounded real-kernel branch discovery, not an independent source golden.
        for (uint seed = 0; seed < 1024; seed++)
        { var result = Run(entered with { Rng = new(seed) },actor,target); if (predicate(result)) return result; }
        throw new InvalidOperationException($"No observed {branch} in 1024 action-start seeds.");
    }
    private static bool Qualifies(SourceFourActionResult result,int chance) =>
        result.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect",StringComparison.Ordinal) && t.Raw % 100 <= chance);
    private static void AssertOne(SourceFourActionResult result,int id,int actor = 0,int target = 1)
    {
        string snapshot = JsonSerializer.Serialize(result); var events = result.Events;
        var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        var hit = events.Single(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent);
        Assert.AreEqual(0,events.OfType<SourceMultiHitEvent>().Count()); int count = 0;
        for (int index = 0; index < events.Length; index++)
        {
            var battle = Double(events,index);
            var decision = AnimationDispatchFixture.AtAttackText(battle,SourceBattleScene.On);
            Assert.AreEqual(decision,AnimationDispatchFixture.AtAttackText(battle,SourceBattleScene.On));
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(battle,SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle,SourceBattleScene.Off).Dispatch);
            if (events[index] == used)
            {
                var dispatch = decision.Dispatch; Assert.IsNotNull(dispatch,decision.Reason); count++;
                Assert.AreEqual(id,dispatch.SourceMoveId); Assert.AreEqual("impact-adapter-moves",dispatch.BattleId);
                Assert.AreEqual(actor,dispatch.Actor.Value); Assert.AreEqual(target,dispatch.Target.Value);
                Assert.AreEqual(used.Sequence,dispatch.Sequence); Assert.AreEqual(hit.Sequence,dispatch.ResultSequence);
                Assert.AreEqual(attack.Sequence,dispatch.AttackSequence); Assert.AreEqual(pp.Sequence,dispatch.PpSequence);
                Assert.IsNull(dispatch.MoveTurn); Assert.IsTrue(dispatch.AcknowledgesCurrentReceipt);
                Assert.AreEqual(hit is SourceSubstituteDamagedEvent,dispatch.HitsSubstitute);
            }
            else Assert.IsNull(decision.Dispatch,"Secondary, held-item, contact, faint and recharge pages cannot replay this root.");
            if (actor >= 2 || target >= 2) continue;
            var single = Single(events,index);
            Assert.AreEqual(decision.Dispatch,AnimationDispatchFixture.AtAttackText(single,true,SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(single,true,SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(single,true,SourceBattleScene.Off).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(single,false,SourceBattleScene.On).Dispatch);
        }
        Assert.AreEqual(1,count); Assert.AreEqual(snapshot,JsonSerializer.Serialize(result),"Projection cannot mutate state, events or RNG trace.");
    }
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events,bool singles = true)
    {
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events,index),SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(Double(events,index),SourceBattleScene.On).Dispatch);
            if (!singles) continue;
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events,index),true,SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(Single(events,index),true,SourceBattleScene.On).Dispatch);
        }
    }
    private static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events) =>
        events.Select((e,i) => e with { Sequence = i + 1 }).ToImmutableArray();

    [TestMethod][DataRow(132)][DataRow(146)][DataRow(282)][DataRow(302)][DataRow(338)]
    public void NaturalLearnersAndCapeBrinkTutorOwnOneRootAtEverySlotAndInActualSingles(int id)
    {
        foreach (bool single in new[] { false,true })
        for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : new[] { actor ^ 1,actor ^ 2 })
        {
            var result = Run(Frame(Bodies(id,actor),single:single),actor,target);
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single(); int expected = Moves.Single(m => m.Id == id).Pp;
            Assert.AreEqual(expected,pp.Before); Assert.AreEqual(expected - 1,pp.After);
            Assert.AreEqual(1,pp.CommandCost); Assert.AreEqual(actor,pp.PpOwner.Value); AssertOne(result,id,actor,target);
        }
    }

    [TestMethod][DataRow(132)][DataRow(146)][DataRow(282)][DataRow(302)][DataRow(338)]
    public void RealProtectAndAccuracyMissNeverAuthorizeAnImpact(int id)
    {
        var bodies = Bodies(id); bodies[1] = bodies[1] with { Protected = true };
        var protect = Run(Frame(bodies)); Assert.IsTrue(protect.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "protected"));
        AssertDenied(protect.Events);
        bodies = Bodies(id); bodies[0] = bodies[0] with { AccuracyStage = 0 }; bodies[1] = bodies[1] with { EvasionStage = 12 };
        var miss = Find(Frame(bodies),r => r.Events.OfType<SourceMoveMissedEvent>().Any(),"accuracy miss"); AssertDenied(miss.Events);
    }

    [TestMethod][DataRow(132,"GASTLY","LICK")][DataRow(146,"GASTLY","LICK")]
    [DataRow(302,"SHEDINJA","HARDEN")][DataRow(338,"SHEDINJA","HARDEN")]
    public void NaturalPrimaryImmunityRejectsAnImpactAtEveryPosition(int id,string species,string move)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(id,actor); bodies[actor ^ 1] = NaturalMon(species,100,move);
            var result = Run(Frame(bodies),actor,actor ^ 1);
            Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_WONDER_GUARD") ||
                result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            AssertDenied(result.Events,singles:actor < 2);
        }
    }

    [TestMethod][DataRow(132)][DataRow(146)][DataRow(282)][DataRow(302)][DataRow(338)]
    public void SubstituteSurvivalBreakEndureZeroLossAndEarlyKoRetainOneCommittedHit(int id)
    {
        foreach (int substituteHp in new[] { 1,255 })
        {
            var bodies = Bodies(id); bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = substituteHp }, HeldItem = "ITEM_SCOPE_LENS" };
            var result = Run(Frame(bodies)); var hit = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
            Assert.AreEqual(substituteHp,hit.HpBefore); Assert.AreEqual(substituteHp == 1,hit.HpAfter == 0);
            Assert.AreEqual(bodies[1].Pokemon.CurrentHp,result.State.Slots[1].Body!.Pokemon.CurrentHp);
            Assert.AreEqual(0,result.Events.OfType<SourceStatChangedEvent>().Count()); Assert.AreEqual(0,result.Events.OfType<SourceConfusionChangedEvent>().Count());
            Assert.AreEqual(0,result.Events.OfType<SourceFlinchEvent>().Count()); Assert.AreEqual(0,result.Events.OfType<SourceHeldItemKnockedOffEvent>().Count());
            Assert.AreEqual("ITEM_SCOPE_LENS",result.State.Slots[1].Body!.HeldItem); AssertOne(result,id);
        }
        foreach (bool endure in new[] { false,true })
        {
            var bodies = Bodies(id); bodies[1] = bodies[1] with { Pokemon = bodies[1].Pokemon with { CurrentHp = 1 }, Enduring = endure };
            var result = Run(Frame(bodies)); var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.AreEqual(endure ? 1 : 0,hit.HpAfter); Assert.AreEqual(endure ? 0 : 1,hit.HpBefore - hit.HpAfter);
            if (endure) Assert.AreEqual(0,hit.Damage); else Assert.IsTrue(hit.Damage >= 1);
            if (endure) { var adjusted = result.Events.OfType<SourceDamageAdjustedEvent>().Single(); Assert.IsTrue(adjusted.Endured); Assert.AreEqual(0,adjusted.DamageAfter); }
            else Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e => e.Side.Value == 1));
            AssertOne(result,id);
        }
    }

    [TestMethod][DataRow(132,10)][DataRow(146,20)][DataRow(302,30)]
    public void ActualSecondarySuccessOccursAfterTheOneDamageReceipt(int id,int chance)
    {
        var success = Find(Frame(Bodies(id)),r => id == 132 ? r.Events.OfType<SourceStatChangedEvent>().Any(e => e.Stat == WildBattleStat.Speed)
            : id == 146 ? r.Events.OfType<SourceConfusionChangedEvent>().Any(e => e.Side.Value == 1 && e.After > 0)
            : r.Events.OfType<SourceFlinchEvent>().Any(e => e.Applied),"actual secondary success");
        Assert.IsTrue(Qualifies(success,chance)); var hit = success.Events.OfType<SourceHpChangedEvent>().Single();
        var secondary = success.Events.Single(e => e is SourceStatChangedEvent or SourceConfusionChangedEvent or SourceFlinchEvent);
        Assert.IsTrue(secondary.Sequence > hit.Sequence); AssertOne(success,id);
        if (id == 132)
        {
            var bodies = Bodies(id); bodies[1] = bodies[1] with { SpeedStage = 0 };
            var capped = Find(Frame(bodies),r => Qualifies(r,chance),"qualifying capped Constrict");
            Assert.AreEqual(0,capped.Events.OfType<SourceStatChangedEvent>().Count()); AssertOne(capped,id);
        }
    }

    [TestMethod][DataRow(132,"TENTACOOL","BARRIER",0u,"ABILITY_CLEAR_BODY",10)]
    [DataRow(132,"TORKOAL","IRON_DEFENSE",0u,"ABILITY_WHITE_SMOKE",10)]
    [DataRow(132,"CATERPIE","STRING_SHOT",0u,"ABILITY_SHIELD_DUST",10)]
    [DataRow(146,"SLOWPOKE","GROWL",1u,"ABILITY_OWN_TEMPO",20)]
    [DataRow(146,"CATERPIE","STRING_SHOT",0u,"ABILITY_SHIELD_DUST",20)]
    [DataRow(302,"ZUBAT","MEAN_LOOK",0u,"ABILITY_INNER_FOCUS",30)]
    [DataRow(302,"CATERPIE","STRING_SHOT",0u,"ABILITY_SHIELD_DUST",30)]
    public void NaturalSecondaryPreventionIsSilentAndKeepsTheDamageRoot(int id,string species,string move,uint personality,string ability,int chance)
    {
        var bodies = Bodies(id); bodies[1] = NaturalMon(species,100,move,personality) with { SpeedStage = id == 132 ? 2 : 0 };
        Assert.AreEqual(ability,bodies[1].Pokemon.AbilityId);
        var result = Find(Frame(bodies),r => Qualifies(r,chance),"qualifying ability-blocked secondary");
        Assert.AreEqual(0,result.Events.OfType<SourceStatChangedEvent>().Count()); Assert.AreEqual(0,result.Events.OfType<SourceConfusionChangedEvent>().Count());
        Assert.AreEqual(0,result.Events.OfType<SourceFlinchEvent>().Count()); Assert.AreEqual(0,result.Events.OfType<SourceAbilityPreventedEvent>().Count()); AssertOne(result,id);
    }

    [TestMethod][DataRow(132)][DataRow(146)]
    public void MistSafeguardAndExistingConfusionDoNotEraseCommittedDamage(int id)
    {
        var frame = Frame(Bodies(id)); frame = frame with { Sides = frame.Sides.SetItem(1,id == 132
            ? frame.Sides[1] with { MistTurnsRemaining = 5 } : frame.Sides[1] with { SafeguardTurnsRemaining = 5 }) };
        var result = Find(frame,r => Qualifies(r,id == 132 ? 10 : 20),"Mist or Safeguard secondary prevention");
        Assert.AreEqual(0,result.Events.OfType<SourceStatChangedEvent>().Count()); Assert.AreEqual(0,result.Events.OfType<SourceConfusionChangedEvent>().Count()); AssertOne(result,id);
        if (id != 146) return;
        var bodies = Bodies(id); bodies[1] = bodies[1] with { ConfusionTurnsRemaining = 3 };
        var existing = Find(Frame(bodies),r => Qualifies(r,20),"existing confusion");
        Assert.AreEqual(3,existing.State.Slots[1].Body!.ConfusionTurnsRemaining); Assert.AreEqual(0,existing.Events.OfType<SourceConfusionChangedEvent>().Count()); AssertOne(existing,id);
    }

    [TestMethod][DataRow(false)][DataRow(true)]
    public void KnockOffHeldItemAndEmptyItemBranchesKeepExactlyOneDamageRoot(bool held)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Bodies(282,actor); int target = actor ^ 1;
            bodies[target] = bodies[target] with { HeldItem = held ? "ITEM_SCOPE_LENS" : "ITEM_NONE" };
            var result = Run(Frame(bodies),actor,target); Assert.AreEqual(held ? 1 : 0,result.Events.OfType<SourceHeldItemKnockedOffEvent>().Count());
            if (held) { var knocked = result.Events.OfType<SourceHeldItemKnockedOffEvent>().Single(); Assert.AreEqual("ITEM_SCOPE_LENS",knocked.Item); Assert.IsTrue(knocked.Sequence > result.Events.OfType<SourceHpChangedEvent>().Single().Sequence); }
            Assert.AreEqual("ITEM_NONE",result.State.Slots[target].Body!.HeldItem); AssertOne(result,282,actor,target);
        }
    }

    [TestMethod][DataRow(282,false,false)][DataRow(282,false,true)][DataRow(282,true,false)][DataRow(282,true,true)]
    [DataRow(168,false,false)][DataRow(168,false,true)][DataRow(168,true,false)][DataRow(168,true,true)]
    public void LateStickyHoldPreservesKnockOffAndThiefDamageIncludingEndureAtOneHp(int id,bool endure,bool critical)
    {
        var bodies = Bodies(id); bodies[1] = NaturalMon("GRIMER",100,"HARDEN",1) with { HeldItem = "ITEM_SCOPE_LENS", Enduring = endure };
        if (endure) bodies[1] = bodies[1] with { Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } };
        Assert.AreEqual("ABILITY_STICKY_HOLD",bodies[1].Pokemon.AbilityId);
        var result = Find(Frame(bodies),r => r.Events.OfType<SourceHpChangedEvent>().Single().Critical == critical,
            "critical/noncritical Sticky Hold hit"); var events = result.Events;
        var hit = events.OfType<SourceHpChangedEvent>().Single(); var marker = events.OfType<SourceAbilityPreventedEvent>().Single();
        Assert.AreEqual("ABILITY_STICKY_HOLD",marker.Ability); Assert.IsTrue(marker.Sequence > hit.Sequence);
        Assert.AreEqual("ITEM_SCOPE_LENS",result.State.Slots[1].Body!.HeldItem);
        Assert.AreEqual(0,events.OfType<SourceHeldItemKnockedOffEvent>().Count()); Assert.AreEqual(0,events.OfType<SourceHeldItemStolenEvent>().Count());
        if (endure) { Assert.AreEqual(1,hit.HpAfter); Assert.AreEqual(0,hit.Damage); }
        else Assert.IsTrue(hit.Damage > 0 && hit.HpAfter > 0);
        Assert.AreEqual(critical && !endure ? 1 : 0,events.OfType<SourceCriticalHitEvent>().Count(),
            "Item transfer follows the generic hit prefix, which does not print critical text for zero HP loss.");
        AssertOne(result,id);
        if (critical && !endure)
        {
            var criticalMarker = events.OfType<SourceCriticalHitEvent>().Single();
            AssertDenied(events.Remove(criticalMarker)); AssertDenied(events.Replace(criticalMarker,criticalMarker with { Target = new(0) }));
        }
        AssertDenied(events.Remove(hit)); AssertDenied(events.Replace(marker,marker with { Target = new(0) }));
        AssertDenied(events.Replace(marker,marker with { Ability = "ABILITY_WONDER_GUARD" }));
        AssertDenied(events.Replace(hit,hit with { HpAfter = 0 }));
        var moved = events.Remove(marker).ToList(); moved.Insert(moved.IndexOf(hit),marker); AssertDenied(Number(moved));
        var interrupted = events.ToList(); interrupted.Insert(interrupted.IndexOf(marker),new SourceFlinchEvent(0,marker.Turn,new(1),true));
        AssertDenied(Number(interrupted));
        if (endure)
        {
            var adjusted = events.OfType<SourceDamageAdjustedEvent>().Single(); AssertDenied(events.Remove(adjusted));
            AssertDenied(events.Replace(adjusted,adjusted with { Endured = false }));
            var survived = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "endure" && e.Reason == "endured");
            AssertDenied(events.Remove(survived)); AssertDenied(events.Replace(survived,survived with { Side = new(0) }));
        }
        var wrong = Mechanics.Catalog.Moves.ToDictionary(p => p.Key,p => p.Value); wrong[hit.Move] = wrong[hit.Move] with { Effect = "EFFECT_HIT" };
        int index = events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(events,index),SourceBattleScene.On,moves:wrong).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events,index),true,SourceBattleScene.On,moves:wrong).Dispatch);
    }

    [TestMethod]
    public void ThiefStillReachesStickyHoldOnAnActualKoWhileKnockOffDoesNot()
    {
        foreach (int id in new[] { 168,282 })
        {
            var bodies = Bodies(id); bodies[1] = NaturalMon("GRIMER",100,"HARDEN",1) with { HeldItem = "ITEM_SCOPE_LENS" };
            bodies[1] = bodies[1] with { Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } };
            var result = Run(Frame(bodies)); Assert.AreEqual(0,result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
            Assert.AreEqual(id == 168 ? 1 : 0,result.Events.OfType<SourceAbilityPreventedEvent>().Count());
            AssertOne(result,id);
        }
    }

    [TestMethod]
    public void NeedleArmMinimizeBonusChangesDamageWithoutCreatingAnotherRoot()
    {
        var bodies = Bodies(302); var plain = Run(Frame(bodies));
        bodies[1] = bodies[1] with { Minimized = true }; var minimized = Run(Frame(bodies));
        var normalHit = plain.Events.OfType<SourceHpChangedEvent>().Single(); var larger = minimized.Events.OfType<SourceHpChangedEvent>().Single();
        Assert.IsTrue(larger.Damage > normalHit.Damage); Assert.AreEqual(normalHit.Critical,larger.Critical);
        Assert.AreEqual(normalHit.RandomPercent,larger.RandomPercent); Assert.AreEqual(plain.State.Rng,minimized.State.Rng);
        Assert.AreEqual(JsonSerializer.Serialize(plain.Trace),JsonSerializer.Serialize(minimized.Trace));
        AssertOne(plain,302); AssertOne(minimized,302);
    }

    [TestMethod]
    public void FrenzyPlantRechargeHasNoSecondRootPpChargeOrRngDraw()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var hit = Run(Frame(Bodies(338,actor)),actor,actor ^ 1); AssertOne(hit,338,actor,actor ^ 1);
            Assert.AreEqual(WildBattleCalledLockKind.Recharge,hit.State.Slots[actor].Registers.Turn!.Lock!.Kind);
            int pp = hit.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp;
            var recharge = Run(hit.State,actor,actor ^ 1);
            Assert.IsTrue(recharge.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "must-recharge"));
            Assert.AreEqual(0,recharge.Events.OfType<SourceMoveUsedEvent>().Count()); Assert.AreEqual(0,recharge.Events.OfType<SourcePpCommandEvent>().Count());
            Assert.AreEqual(pp,recharge.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp); Assert.AreEqual(hit.State.Rng,recharge.State.Rng);
            Assert.AreEqual(0,recharge.Trace.Length); AssertDenied(recharge.Events,singles:actor < 2);
        }
    }

    [TestMethod][DataRow(132)][DataRow(146)][DataRow(282)][DataRow(302)][DataRow(338)]
    public void SourceContactFlagsControlRoughSkinAndNeverReplayTheRoot(int id)
    {
        var bodies = Bodies(id); bodies[0] = bodies[0] with { Pokemon = bodies[0].Pokemon with { CurrentHp = 1 } };
        bodies[1] = NaturalMon("SHARPEDO",100,"FOCUS_ENERGY") with { SpeedStage = 0 };
        var result = Run(Frame(bodies)); bool contact = id != 338;
        Assert.AreEqual(contact,Mechanics.Catalog.Moves[bodies[0].Pokemon.Moves[0].Move].Flags.Contains("FLAG_MAKES_CONTACT"));
        Assert.AreEqual(contact ? 1 : 0,result.Events.OfType<SourceAbilityDamageEvent>().Count());
        if (contact) { var skin = result.Events.OfType<SourceAbilityDamageEvent>().Single(); Assert.AreEqual(0,skin.HpAfter); Assert.AreEqual(0,skin.Target.Value); }
        else Assert.AreEqual(1,result.State.Slots[0].Body!.Pokemon.CurrentHp);
        AssertOne(result,id);
    }

    [TestMethod][DataRow(132)][DataRow(146)][DataRow(282)][DataRow(302)][DataRow(338)]
    public void NaturalPressureAndLastPpClampKeepTheActualPpOwner(int id)
    {
        foreach (int remaining in new[] { 1,3 })
        {
            var bodies = Bodies(id); bodies[1] = NaturalMon("RAIKOU",100,"ROAR") with { SpeedStage = 0 };
            Assert.AreEqual("ABILITY_PRESSURE",bodies[1].Pokemon.AbilityId);
            bodies[0] = bodies[0] with { Pokemon = bodies[0].Pokemon with { Moves = bodies[0].Pokemon.Moves.SetItem(0,
                bodies[0].Pokemon.Moves[0] with { CurrentPp = remaining }) } };
            var result = Run(Frame(bodies)); var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(remaining,pp.Before); Assert.AreEqual(Math.Max(0,remaining - 2),pp.After); Assert.AreEqual(2,pp.CommandCost); Assert.AreEqual(0,pp.PpOwner.Value);
            AssertOne(result,id);
        }
    }

    [TestMethod][DataRow(132)][DataRow(146)][DataRow(282)][DataRow(302)][DataRow(338)]
    public void MalformedHeadersPpOwnershipHitIdentityAndCursorNeverBorrowTheRoot(int id)
    {
        var result = Run(Frame(Bodies(id))); AssertOne(result,id); var events = result.Events;
        var attack = events.OfType<SourceAttackStringEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
        var hit = events.OfType<SourceHpChangedEvent>().Single();
        AssertDenied(events.Remove(attack)); AssertDenied(events.Replace(attack,attack with { Actor = new(1) }));
        AssertDenied(events.Remove(pp)); AssertDenied(events.Replace(pp,pp with { PpOwner = new(1) }));
        AssertDenied(events.Replace(pp,pp with { CurrentMove = Id("TACKLE") })); AssertDenied(events.Replace(pp,pp with { CommandCost = 0 }));
        AssertDenied(events.Remove(hit)); AssertDenied(events.Replace(hit,hit with { Target = new(0) }));
        AssertDenied(events.Replace(hit,hit with { Move = Id("TACKLE") })); AssertDenied(events.Replace(hit,hit with { Sequence = pp.Sequence }));
        AssertDenied(Number(events.Add(hit)));
        int index = events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events,index) with { Phase = CampaignBattlePhase.ChooseMove },true,SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events,-1),true,SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events,events.Length),true,SourceBattleScene.On).Dispatch);
    }
}
