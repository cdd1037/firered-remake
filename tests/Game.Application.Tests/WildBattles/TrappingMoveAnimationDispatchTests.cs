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

[TestClass,TestCategory("Game")]
public sealed class TrappingMoveAnimationDispatchTests
{
    private static SourceMechanicsCatalog Mechanics=>TestAssets.Cached(()=>SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static MoveId Move(int id)=>new("pret:MOVE_"+(id==212?"MEAN_LOOK":"BLOCK"));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> e,int i)=>new("trapping",new("test"),"test",new(),CampaignDoubleBattleStage.Combat,e,i,[],[],0,0,[],[],[],[],0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> e,int i)=>new("trapping",null!,new(),e,i,null!,null!,CampaignBattlePhase.Events,[],0,[]);
    private static WildBattleCombatant Mon(string species,params string[] names)
    {
        var p=SourcePokemonFactory.Create(Mechanics,new(new("pret:SPECIES_"+species),50,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
        var moves=names.Select(name=>{var m=new MoveId("pret:MOVE_"+name);Assert.IsTrue(Mechanics.Catalog.Species[p.Species].LevelUpMoves.Any(x=>x.Move==m&&x.Level<=50));int pp=Mechanics.Catalog.Moves[m].Pp;return new PokemonMoveSlot(m,pp,pp);}).ToImmutableArray();
        return new(p with{Moves=moves});
    }
    private static WildBattleCombatant[] Bodies(int id,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Mon("SNORLAX","TACKLE")).ToArray();
        bodies[actor]=Mon(id==212?"GASTLY":"SNORLAX",id==212?"MEAN_LOOK":"BLOCK") with {AccuracyStage=12};
        return bodies;
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


    private static void Check(SourceKernelResult result, int id, int actor, bool single = false)
    {
        string snapshot = JsonSerializer.Serialize(result);
        var events = result.Events;
        var used = events.OfType<SourceMoveUsedEvent>().Single(x => x.Move == Move(id));
        var trapped = events.OfType<SourceTowerVolatileEvent>().Where(v => v.Effect == "mean-look" && v.Applied).ToArray();
        Assert.IsTrue(trapped.Length <= 1);
        int roots = 0;
        for (int i = 0; i < events.Length; i++)
        {
            CampaignMoveAnimationDecision At(SourceBattleScene scene) => single
                ? AnimationDispatchFixture.AtAttackText(Single(events, i), true, scene)
                : AnimationDispatchFixture.AtAttackText(Double(events, i), scene);
            var decision = At(SourceBattleScene.On);
            Assert.AreEqual(decision, At(SourceBattleScene.On));
            Assert.IsNull(At(SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single
                ? AnimationDispatchFixture.AtResult(Single(events, i), true, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtResult(Double(events, i), SourceBattleScene.On).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.StatAtResult(Single(events, i), true)
                : AnimationDispatchFixture.StatAtResult(Double(events, i)));
            if (events[i] == used && trapped.Length == 1)
            {
                var cue = decision.Dispatch;
                Assert.IsNotNull(cue, decision.Reason);
                var pp = events.OfType<SourcePpCommandEvent>().Single();
                Assert.AreEqual(id, cue.SourceMoveId);
                Assert.AreEqual(actor, cue.Actor.Value);
                Assert.AreEqual(used.Target, cue.Target);
                Assert.AreEqual(pp.Sequence, cue.PpSequence);
                Assert.AreEqual(pp.Sequence + 1, cue.ResultSequence);
                Assert.AreEqual(new SourceTowerVolatileEvent(cue.ResultSequence, used.Turn,
                    used.Target, "mean-look", true, "move"), trapped.Single());
                Assert.AreEqual(new BattlerSlot(actor), result.State.Slots[used.Target.Value].Registers.MeanLookSource);
                roots++;
            }
            else Assert.IsNull(decision.Dispatch);
        }
        Assert.AreEqual(trapped.Length, roots);
        Assert.AreEqual(snapshot, JsonSerializer.Serialize(result),
            "Dispatch must preserve state, RNG, trace and receipts.");
    }

    private static void Pp(SourceKernelResult result, int actor, int target, int cost)
    {
        var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
        var used = result.Events.OfType<SourceMoveUsedEvent>().Single();
        Assert.AreEqual(actor, pp.Actor.Value);
        Assert.AreEqual(actor, pp.PpOwner.Value);
        Assert.AreEqual(target, pp.Target.Value);
        Assert.AreEqual(0, pp.PpSlot);
        Assert.AreEqual(used.Move, pp.ChosenMove);
        Assert.AreEqual(used.Move, pp.CurrentMove);
        Assert.AreEqual(used.PpBefore, pp.Before);
        Assert.AreEqual(used.PpAfter, pp.After);
        Assert.AreEqual(cost, pp.CommandCost);
        Assert.AreEqual(Math.Max(0, pp.Before - cost), pp.After);
        Assert.AreEqual(pp.After, result.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
    }

    [TestMethod][DataRow(212)][DataRow(335)]
    public void NaturalLearnersDispatchAtEveryPositionAndBothSinglesSides(int id)
    {
        // Pinned source: Gastly level 48 Mean Look; Snorlax level 41 Block.
        foreach (bool single in new[] { false, true })
        for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var result = Resolve(Frame(Bodies(id, actor), single), actor, actor ^ 1);
            Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any());
            Assert.AreEqual(1, result.Events.OfType<SourceTowerVolatileEvent>().Count());
            Assert.AreEqual(5, result.Events.OfType<SourcePpCommandEvent>().Single().Before);
            Pp(result, actor, actor ^ 1, 1);
            Check(result, id, actor, single);
        }
    }

    [TestMethod][DataRow(212)][DataRow(335)]
    public void SelectedAllyKeepsActualRecipientAtEveryPosition(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var result = Resolve(Frame(Bodies(id, actor)), actor, actor ^ 2);
            Assert.AreEqual(actor ^ 2, result.Events.OfType<SourceTowerVolatileEvent>().Single().Side.Value);
            Pp(result, actor, actor ^ 2, 1);
            Check(result, id, actor);
        }
    }

    [TestMethod][DataRow(212)][DataRow(335)]
    public void ProtectSubstituteAlreadyTrappedAndActualDigNeverDispatch(int id)
    {
        foreach (bool single in new[] { false, true })
        foreach (string branch in new[] { "protect", "substitute", "already-trapped", "semi-invulnerable" })
        {
            var bodies = Bodies(id);
            if (branch == "protect") bodies[1] = bodies[1] with { Protected = true };
            if (branch == "substitute") bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } };
            if (branch == "semi-invulnerable") bodies[1] = Mon("DIGLETT", "DIG");
            var frame = Frame(bodies, single);
            if (branch == "already-trapped") frame = Resolve(frame).State;
            if (branch == "semi-invulnerable") frame = Resolve(frame, 1, 0).State;
            var result = Resolve(frame);
            Assert.IsFalse(result.Events.OfType<SourceTowerVolatileEvent>().Any(v => v.Effect == "mean-look" && v.Applied));
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(f => f.Reason == (branch == "protect" ? "protected" : branch)));
            Pp(result, 0, 1, 1);
            Check(result, id, 0, single);
        }
    }

    [TestMethod][DataRow(212)][DataRow(335)]
    public void PressureAndLastPpClampPreserveDirectCommandReceipt(int id)
    {
        foreach (bool single in new[] { false, true })
        foreach (int remaining in new[] { 1, 2, 5 })
        {
            var bodies = Bodies(id);
            bodies[1] = Mon("DUSCLOPS", "ASTONISH");
            Assert.AreEqual("ABILITY_PRESSURE", WildBattleEffective.Ability(bodies[1]));
            bodies[0] = bodies[0] with { Pokemon = bodies[0].Pokemon with {
                Moves = bodies[0].Pokemon.Moves.SetItem(0, bodies[0].Pokemon.Moves[0] with { CurrentPp = remaining }) } };
            var result = Resolve(Frame(bodies, single));
            Assert.AreEqual(1, result.Events.OfType<SourceTowerVolatileEvent>().Count());
            Pp(result, 0, 1, 2);
            Check(result, id, 0, single);
        }
    }

    private static void Reject(ImmutableArray<SourceBattleEvent> events, bool single)
    {
        int used = Array.FindIndex(events.ToArray(), e => e is SourceMoveUsedEvent);
        Assert.IsNull(single
            ? AnimationDispatchFixture.AtAttackText(Single(events, used), true, SourceBattleScene.On).Dispatch
            : AnimationDispatchFixture.AtAttackText(Double(events, used), SourceBattleScene.On).Dispatch);
    }

    [TestMethod][DataRow(212)][DataRow(335)]
    public void NewRootsRejectForeignMissingDuplicateMalformedAndCallerOwnedEvidence(int id)
    {
        // Exhaustive shared PP field mutations remain in WebMoveAnimationDispatchTests.
        foreach (bool single in new[] { false, true })
        {
            var result = Resolve(Frame(Bodies(id), single));
            Check(result, id, 0, single);
            var events = result.Events;
            int used = Array.FindIndex(events.ToArray(), e => e is SourceMoveUsedEvent);
            Assert.IsNull(single
                ? AnimationDispatchFixture.AtAttackText(Single(events, used), true, SourceBattleScene.On, null).Dispatch
                : AnimationDispatchFixture.AtAttackText(Double(events, used), SourceBattleScene.On, null).Dispatch);
            Assert.IsNull(single
                ? CampaignMoveAnimationPresentation.AtAttackText(Single(events, used), true, SourceBattleScene.On,
                    Mechanics.Catalog.Moves, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Double(events, used), SourceBattleScene.On,
                    Mechanics.Catalog.Moves, (_, _) => false).Dispatch);
            int p = Array.FindIndex(events.ToArray(), e => e is SourcePpCommandEvent);
            int v = Array.FindIndex(events.ToArray(), e => e is SourceTowerVolatileEvent);
            int a = Array.FindIndex(events.ToArray(), e => e is SourceAttackStringEvent);
            var pp = (SourcePpCommandEvent)events[p];
            var trapped = (SourceTowerVolatileEvent)events[v];
            var attack = (SourceAttackStringEvent)events[a];
            Reject(events.SetItem(p, pp with { PpOwner = new(1) }), single);
            Reject(events.RemoveAt(p), single);
            Reject(events.Insert(p + 1, pp), single);
            Reject(events.RemoveAt(v), single);
            Reject(events.Insert(v + 1, trapped with { Sequence = trapped.Sequence + 1 }), single);
            foreach (var foreign in new[] { trapped with { Side = new(0) }, trapped with { Effect = "other" },
                trapped with { Applied = false }, trapped with { Reason = "ability" }, trapped with { Turn = trapped.Turn + 1 } })
                Reject(events.SetItem(v, foreign), single);
            Reject(events.RemoveAt(a), single);
            Reject(events.SetItem(a, attack with { CurrentMove = new("MOVE_" + (id == 212 ? "MEAN_LOOK" : "BLOCK")) }), single);
            var caller = new MoveId("pret:MOVE_METRONOME");
            Reject(events.SetItem(a, attack with { ChosenMove = caller }).SetItem(p, pp with { ChosenMove = caller }), single);
            Reject(events.Insert(v, new SourceMoveFailedEvent(trapped.Sequence, trapped.Turn,
                new(0), new(1), Move(id), "substitute")), single);
        }
    }
}
