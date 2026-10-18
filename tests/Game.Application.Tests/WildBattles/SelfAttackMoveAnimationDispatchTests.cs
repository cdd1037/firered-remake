using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class SelfAttackMoveAnimationDispatchTests
{
    private static SourceMechanicsCatalog Mechanics => CurrentCampaignFixture.Catalog.PokemonContent!.Pokemon.Mechanics;
    private static string Name(int id) => id switch
    {
        14 => "SWORDS_DANCE", 159 => "SHARPEN", 96 => "MEDITATE", 133 => "AMNESIA", 336 => "HOWL", 294 => "TAIL_GLOW",
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    private static MoveId Move(int id) => new("pret:MOVE_" + Name(id));
    private static int Increase(int id) => id is 14 or 133 or 294 ? 2 : 1;
    private static int Argument(int id) => id == 294 ? 42 : id == 133 ? 43 : id == 14 ? 39 : 15;

    private static WildBattleStat Stat(int id) => id == 294 ? WildBattleStat.SpecialAttack : id == 133 ? WildBattleStat.SpecialDefense : WildBattleStat.Attack;
    private static int Stage(WildBattleCombatant body,int id) => id == 294 ? 6 + body.SpecialAttackStageDelta : id == 133 ? 6 + body.SpecialDefenseStageDelta : body.AttackStage;

    // Deliberately empty committed frames: projection must use the source receipts,
    // never reconstruct a stat change from the already-committed end state.
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("self-attack", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("self-attack", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(),
            index, null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };

    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        MoveId move = new("pret:MOVE_" + name);
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Pinned natural source learner required: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }

    private static WildBattleCombatant[] Bodies(int id, int actor, int stage = 6, int? pp = null)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("MAGIKARP", 5, "SPLASH")).ToArray();
        var (species, level) = id switch
        {
            14 => ("FARFETCHD", 31), 159 => ("PORYGON", 24), 96 => ("DROWZEE", 27), 133 => ("SLOWPOKE", 36), 336 => ("POOCHYENA", 5), 294 => ("VOLBEAT", 21),
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
        var body = NaturalMon(species, level, Name(id));
        body = id == 294 ? body with { SpecialAttackStageDelta = stage - 6 } : id == 133 ? body with { SpecialDefenseStageDelta = stage - 6 } : body with { AttackStage = stage };
        if (pp is {} remaining)
            body = body with { Pokemon = body.Pokemon with
                { Moves = body.Pokemon.Moves.SetItem(0, body.Pokemon.Moves[0] with { CurrentPp = remaining }) } };
        bodies[actor] = body;
        return bodies;
    }

    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame
        {
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
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(31) };
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor)
    {
        string before = JsonSerializer.Serialize(frame);
        var body = frame.Slots[actor].Body!;
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(actor)));
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty);
        Assert.AreEqual(before, JsonSerializer.Serialize(frame), "The public kernel must not mutate its input frame.");
        Assert.AreEqual(frame.Rng, result.State.Rng, "Ordinary self-stat increases have no accuracy or effect roll.");
        Assert.AreEqual(0, result.Trace.Length);
        Assert.AreEqual(body.Pokemon.Moves[0].CurrentPp - 1, result.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
        Assert.AreEqual(body.Pokemon.CurrentHp, result.State.Slots[actor].Body!.Pokemon.CurrentHp);
        Assert.AreEqual(body.DefenseStage, result.State.Slots[actor].Body!.DefenseStage);
        Assert.AreEqual(body.SpeedStage, result.State.Slots[actor].Body!.SpeedStage);
        for (int slot = 0; slot < 4; slot++)
            if (slot != actor) Assert.AreEqual(frame.Slots[slot].Body, result.State.Slots[slot].Body);
        return result;
    }

    private static void AssertReceipts(SourceKernelResult result, int id, int actor, int stage)
    {
        var events = result.Events;
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Move(id), attack.ChosenMove); Assert.AreEqual(Move(id), attack.CurrentMove);
        Assert.AreEqual(Move(id), used.Move); Assert.AreEqual(Move(id), pp.ChosenMove); Assert.AreEqual(Move(id), pp.CurrentMove);
        Assert.AreEqual(actor, attack.Actor.Value); Assert.AreEqual(actor, used.Actor.Value); Assert.AreEqual(actor, used.Target.Value);
        Assert.AreEqual(actor, pp.Actor.Value); Assert.AreEqual(actor, pp.Target.Value); Assert.AreEqual(actor, pp.PpOwner.Value);
        Assert.AreEqual(0, used.Slot); Assert.AreEqual(0, pp.PpSlot); Assert.AreEqual(1, pp.CommandCost);
        Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After); Assert.AreEqual(pp.Before - 1, pp.After);
        Assert.AreEqual(result.State.Rng.Cursor, pp.RngCursor);
        Assert.AreEqual(attack.Sequence + 1, used.Sequence); Assert.AreEqual(used.Sequence + 1, pp.Sequence);
        Assert.AreEqual(events.IndexOf(attack) + 1, events.IndexOf(used));
        Assert.AreEqual(events.IndexOf(used) + 1, events.IndexOf(pp));
        var outcome = events.Single(e => e is SourceStatChangedEvent or SourceStatAtMaximumEvent);
        Assert.AreEqual(events.IndexOf(pp) + 1, events.IndexOf(outcome));
        Assert.AreEqual(pp.Sequence + 1, outcome.Sequence); Assert.AreEqual(pp.Turn, outcome.Turn);
        int after = Math.Min(12, stage + Increase(id));
        Assert.AreEqual(after, Stage(result.State.Slots[actor].Body!,id));
        if (stage == 12)
        {
            Assert.IsInstanceOfType<SourceStatAtMaximumEvent>(outcome);
            var cap = (SourceStatAtMaximumEvent)outcome;
            Assert.AreEqual(actor, cap.Actor.Value); Assert.AreEqual(actor, cap.Target.Value); Assert.AreEqual(Stat(id), cap.Stat);
        }
        else
        {
            Assert.IsInstanceOfType<SourceStatChangedEvent>(outcome);
            var stat = (SourceStatChangedEvent)outcome;
            Assert.AreEqual(actor, stat.Actor.Value); Assert.AreEqual(actor, stat.Target.Value); Assert.AreEqual(Stat(id), stat.Stat);
            Assert.AreEqual(stage, stat.Before); Assert.AreEqual(after, stat.After);
        }
        Assert.AreEqual(0, events.OfType<SourceMoveFailedEvent>().Count());
        Assert.AreEqual(0, events.OfType<SourceMoveMissedEvent>().Count());
        Assert.AreEqual(0, events.OfType<SourceAbilityPreventedEvent>().Count());
        Assert.AreEqual(0, events.OfType<SourceHpChangedEvent>().Count());
    }

    private static void AssertProjection(SourceKernelResult result, int id, int actor, int stage, bool single, bool rootAvailable = true)
    {
        var events = result.Events;
        string before = JsonSerializer.Serialize(events), state = JsonSerializer.Serialize(result.State), trace = JsonSerializer.Serialize(result.Trace);
        var rng = result.State.Rng;
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        var stat = events.OfType<SourceStatChangedEvent>().SingleOrDefault();
        int roots = 0, waves = 0;
        for (int index = 0; index < events.Length; index++)
        {
            CampaignMoveAnimationDecision Root(SourceBattleScene scene) => single
                ? AnimationDispatchFixture.AtAttackText(Single(events, index), true, scene)
                : AnimationDispatchFixture.AtAttackText(Double(events, index), scene);
            CampaignStatAnimationDispatch? Wave() => single
                ? AnimationDispatchFixture.StatAtResult(Single(events, index), true)
                : AnimationDispatchFixture.StatAtResult(Double(events, index));
            var root = Root(SourceBattleScene.On);
            Assert.AreEqual(root, Root(SourceBattleScene.On));
            Assert.IsNull(Root(SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.AtResult(Single(events, index), true, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtResult(Double(events, index), SourceBattleScene.On).Dispatch);
            if (events[index] == used && stage < 12 && rootAvailable)
            {
                var cue = root.Dispatch; Assert.IsNotNull(cue, root.Reason); roots++;
                Assert.AreEqual("self-attack", cue.BattleId); Assert.AreEqual(id, cue.SourceMoveId);
                Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
                Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(stat!.Sequence, cue.ResultSequence);
                Assert.AreEqual(actor, cue.Actor.Value); Assert.AreEqual(actor, cue.Target.Value);
                Assert.IsFalse(cue.HitsSubstitute); Assert.IsNull(cue.MoveTurn); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
            }
            else Assert.IsNull(root.Dispatch, "Only the successful invocation's visible MoveUsed page may own its root.");
            var wave = Wave(); Assert.AreEqual(wave, Wave());
            if (events[index] == stat)
            {
                Assert.IsNotNull(wave); waves++;
                Assert.AreEqual("self-attack", wave.BattleId); Assert.AreEqual(id, wave.SourceMoveId);
                Assert.AreEqual(stat!.Sequence, wave.Sequence); Assert.AreEqual(attack.Sequence, wave.AttackSequence);
                Assert.AreEqual(pp.Sequence, wave.PpSequence); Assert.AreEqual(actor, wave.Affected.Value);
                Assert.AreEqual(Argument(id), wave.AnimationArgument, "Requested stat magnitude stays source-coded even when 11 -> 12 is clamped.");
                Assert.IsTrue(CampaignResultAnimationPolicy.Allows(CampaignResultAnimationKind.StatChange, SourceBattleScene.Off),
                    "Source Cmd_playanimation exempts General_StatsChange from Battle Scene Off.");
            }
            else Assert.IsNull(wave, "Attack text, PP, cap and later pages cannot replay the shared stat wave.");
            Assert.IsNull(single ? AnimationDispatchFixture.StatAtResult(Single(events, index), true, true)
                : AnimationDispatchFixture.StatAtResult(Double(events, index), true));
            Assert.IsFalse(single ? CampaignStatAnimationPresentation.IsSilentResultReceipt(Single(events, index), true,moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves)
                : CampaignStatAnimationPresentation.IsSilentResultReceipt(Double(events, index),moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves), "An ordinary self-stat cap keeps its result text.");
            if (single)
            {
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), false, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(Single(events, index), false));
                Assert.AreEqual(root.Dispatch, AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On).Dispatch);
                Assert.AreEqual(wave, AnimationDispatchFixture.StatAtResult(Double(events, index)));
            }
        }
        Assert.AreEqual(stage < 12 && rootAvailable ? 1 : 0, roots); Assert.AreEqual(stage < 12 ? 1 : 0, waves);
        Assert.AreEqual(before, JsonSerializer.Serialize(events)); Assert.AreEqual(state, JsonSerializer.Serialize(result.State));
        Assert.AreEqual(trace, JsonSerializer.Serialize(result.Trace)); Assert.AreEqual(rng, result.State.Rng);
    }

    [TestMethod]
    [DataRow(14, "EFFECT_ATTACK_UP_2")] [DataRow(159, "EFFECT_ATTACK_UP")]
    [DataRow(96, "EFFECT_ATTACK_UP")] [DataRow(133, "EFFECT_SPECIAL_DEFENSE_UP_2")] [DataRow(336, "EFFECT_ATTACK_UP")] [DataRow(294, "EFFECT_SPECIAL_ATTACK_UP_2")]
    public void SourceDefinitionsAndGeneratedLabelsOwnDedicatedRootAvailability(int id, string effect)
    {
        var move = Mechanics.Catalog.Moves[Move(id)];
        Assert.AreEqual(id, move.SourceMoveId); Assert.AreEqual(effect, move.Effect);
        Assert.AreEqual(id == 294 ? "TYPE_BUG" : id is 96 or 133 ? "TYPE_PSYCHIC" : "TYPE_NORMAL", move.Type); Assert.AreEqual("MOVE_TARGET_USER", move.Target);
        Assert.AreEqual(0, move.Power); Assert.AreEqual(id == 294 ? 100 : 0, move.Accuracy); Assert.AreEqual(id == 294 ? 20 : id is 96 or 336?40:id==133?20:30, move.Pp);
        Assert.AreEqual(0, move.SecondaryEffectChance); Assert.AreEqual(0, move.Priority);
        CollectionAssert.AreEqual(new[] { "FLAG_SNATCH_AFFECTED" }, move.Flags.ToArray());
        Assert.IsTrue(SourceMoveDefinitionRules.IsValid(move));
        Assert.IsTrue(AnimationDispatchFixture.Supports(id, Move(id).Value), "Requires the actual published matching generated root.");
        Assert.IsFalse(AnimationDispatchFixture.Supports(id == 14 ? 159 : 14, Move(id).Value));
    }

    [TestMethod]
    [DataRow(14, 0)] [DataRow(14, 6)] [DataRow(14, 10)] [DataRow(14, 11)] [DataRow(14, 12)]
    [DataRow(159, 0)] [DataRow(159, 6)] [DataRow(159, 11)] [DataRow(159, 12)]
    [DataRow(96, 0)] [DataRow(96, 6)] [DataRow(96, 11)] [DataRow(96, 12)]
    [DataRow(133, 0)] [DataRow(133, 6)] [DataRow(133, 10)] [DataRow(133, 11)] [DataRow(133, 12)]
    [DataRow(336, 0)] [DataRow(336, 6)] [DataRow(336, 11)] [DataRow(336, 12)]
    [DataRow(294, 0)] [DataRow(294, 6)] [DataRow(294, 10)] [DataRow(294, 11)] [DataRow(294, 12)]
    public void NaturalLearnersOwnFullClampedAndCappedResultsAcrossEveryActorPosition(int id, int stage)
    {
        foreach (bool single in new[] { false, true })
        for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var result = Resolve(Frame(Bodies(id, actor, stage), single), actor);
            AssertReceipts(result, id, actor, stage);
            AssertProjection(result, id, actor, stage, single);
        }
    }

    [TestMethod]
    [DataRow(14)] [DataRow(159)] [DataRow(96)] [DataRow(133)] [DataRow(336)] [DataRow(294)]
    public void FinalPpStillBelongsToActorAndCappedAttemptStillSpendsIt(int id)
    {
        foreach (bool single in new[] { false, true })
        for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int stage in new[] { 6, 12 })
        {
            var result = Resolve(Frame(Bodies(id, actor, stage, pp: 1), single), actor);
            AssertReceipts(result, id, actor, stage);
            Assert.AreEqual(0, result.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
            AssertProjection(result, id, actor, stage, single);
        }
    }

    [TestMethod]
    [DataRow(14)] [DataRow(159)] [DataRow(96)] [DataRow(133)] [DataRow(336)] [DataRow(294)]
    public void UserTargetMovesNormalizeAnySelectedLiveSlotToTheActor(int id)
    {
        foreach(bool single in new[]{false,true})
        for(int actor=0;actor<(single?2:4);actor++)
        for(int selected=0;selected<(single?2:4);selected++)
        {
            var frame=Frame(Bodies(id,actor),single);
            if(selected!=actor)
            {
                var direct=SourceBattleKernel.ResolveAction(Mechanics.Catalog,frame,
                    new(frame.Slots[actor].Occupant!.Value,frame.Revision,0,new(selected)));
                Assert.IsFalse(direct.Succeeded);Assert.AreEqual("source_kernel_target_mode",direct.Fault);
            }
            var choices=frame.Slots.Take(frame.BattlerCount).Select(c=>new SourceActionSelection(c.Occupant!.Value,
                SourceActionKind.Move,0,new(c.Slot.Value==actor?selected:c.Slot.Value))).ToImmutableArray();
            var ordered=SourceFourActionTurns.BeginActions(Mechanics.Catalog,frame,frame.Revision,choices);
            for(int step=0;step<12&&!ordered.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==actor&&e.Move==Move(id));step++)
            {
                Assert.IsTrue(ordered.Succeeded,ordered.Fault);Assert.IsNotNull(ordered.Continuation);
                ordered=SourceFourActionTurns.Advance(Mechanics.Catalog,ordered.State,ordered.Continuation.Value);
            }
            Assert.IsTrue(ordered.Succeeded,ordered.Fault);
            Assert.IsTrue(ordered.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Actor.Value==actor&&e.Move==Move(id)));
            var result=new SourceKernelResult(ordered.State,ordered.Events,ordered.Trace,null,ordered.Fault);
            AssertReceipts(result,id,actor,6);AssertProjection(result,id,actor,6,single);
        }
    }

    private static void AssertRejected(ImmutableArray<SourceBattleEvent> events, string reason, bool rejectWave = true,
        IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves = null, bool explicitMetadata = false)
    {
        string before = JsonSerializer.Serialize(events);
        for (int index = 0; index < events.Length; index++)
        {
            var twice = Double(events, index); var single = Single(events, index);
            Assert.IsNull((explicitMetadata ? AnimationDispatchFixture.AtAttackText(twice, SourceBattleScene.On, moves)
                : AnimationDispatchFixture.AtAttackText(twice, SourceBattleScene.On)).Dispatch, reason);
            Assert.IsNull((explicitMetadata ? AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On, moves)
                : AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On)).Dispatch, reason);
            Assert.IsNull(AnimationDispatchFixture.AtResult(twice, SourceBattleScene.On).Dispatch, reason);
            Assert.IsNull(AnimationDispatchFixture.AtResult(single, true, SourceBattleScene.On).Dispatch, reason);
            if (!rejectWave) continue;
            Assert.IsNull(explicitMetadata ? AnimationDispatchFixture.StatAtResult(twice, moves)
                : AnimationDispatchFixture.StatAtResult(twice), reason);
            Assert.IsNull(explicitMetadata ? AnimationDispatchFixture.StatAtResult(single, true, moves)
                : AnimationDispatchFixture.StatAtResult(single, true), reason);
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(events), reason);
    }

    [TestMethod]
    [DataRow(14)] [DataRow(159)] [DataRow(96)] [DataRow(133)] [DataRow(336)] [DataRow(294)]
    public void ForeignOwnersInvalidStagesAndInterruptedReceiptsCannotBorrowRootOrWave(int id)
    {
        var events = Resolve(Frame(Bodies(id, 0), false), 0).Events;
        var attack = events.OfType<SourceAttackStringEvent>().Single(); int ai = events.IndexOf(attack);
        var used = events.OfType<SourceMoveUsedEvent>().Single(); int ui = events.IndexOf(used);
        var pp = events.OfType<SourcePpCommandEvent>().Single(); int pi = events.IndexOf(pp);
        var stat = events.OfType<SourceStatChangedEvent>().Single(); int si = events.IndexOf(stat);
        MoveId foreign = new("pret:MOVE_TACKLE");
        var cases = new (string Reason, ImmutableArray<SourceBattleEvent> Events)[]
        {
            ("foreign attack actor", events.SetItem(ai, attack with { Actor = new(1) })),
            ("called attack", events.SetItem(ai, attack with { ChosenMove = foreign })),
            ("foreign current attack", events.SetItem(ai, attack with { CurrentMove = foreign })),
            ("foreign attack turn", events.SetItem(ai, attack with { Turn = attack.Turn + 1 })),
            ("foreign used actor", events.SetItem(ui, used with { Actor = new(1) })),
            ("foreign used target", events.SetItem(ui, used with { Target = new(1) })),
            ("foreign used move", events.SetItem(ui, used with { Move = foreign })),
            ("foreign PP owner", events.SetItem(pi, pp with { PpOwner = new(1) })),
            ("foreign PP actor", events.SetItem(pi, pp with { Actor = new(1) })),
            ("foreign PP target", events.SetItem(pi, pp with { Target = new(1) })),
            ("called PP", events.SetItem(pi, pp with { ChosenMove = foreign })),
            ("foreign current PP", events.SetItem(pi, pp with { CurrentMove = foreign })),
            ("foreign PP turn", events.SetItem(pi, pp with { Turn = pp.Turn + 1 })),
            ("foreign PP before", events.SetItem(pi, pp with { Before = pp.Before - 1 })),
            ("foreign PP after", events.SetItem(pi, pp with { After = pp.After - 1 })),
            ("unspent PP", events.SetItem(ui, used with { PpAfter = used.PpBefore }).SetItem(pi, pp with { After = pp.Before })),
            ("foreign stat actor", events.SetItem(si, stat with { Actor = new(1) })),
            ("opponent stat", events.SetItem(si, stat with { Target = new(1) })),
            ("foreign stat turn", events.SetItem(si, stat with { Turn = stat.Turn + 1 })),
            ("wrong stat", events.SetItem(si, stat with { Stat = WildBattleStat.Defense })),
            ("unchanged stat", events.SetItem(si, stat with { After = stat.Before })),
            ("wrong requested magnitude", events.SetItem(si, stat with { After = stat.Before + (Increase(id) == 2 ? 1 : 2) })),
            ("out of range before", events.SetItem(si, stat with { Before = -1, After = Increase(id) - 1 })),
            ("out of range after", events.SetItem(si, stat with { Before = 12, After = 13 })),
            ("stat before PP", events.SetItem(si, stat with { Sequence = pp.Sequence })),
            ("missing attack", events.RemoveAt(ai)), ("missing used", events.RemoveAt(ui)),
            ("missing PP", events.RemoveAt(pi)), ("missing outcome", events.RemoveAt(si)),
            ("swapped PP and stat", events.SetItem(pi, stat).SetItem(si, pp)),
            ("interrupted result", events.SetItem(pi, new SourceTurnReadyEvent(pp.Sequence, pp.Turn, 0)))
        };
        foreach (var (reason, malformed) in cases) AssertRejected(malformed, $"{id}: {reason}");
        // Slot/cost authentication belongs to root admission. A generic stat wave
        // does not claim to be an independent PP-command integrity validator.
        AssertRejected(events.SetItem(pi, pp with { PpSlot = 1 }), "foreign PP slot", rejectWave: false);
        AssertRejected(events.SetItem(pi, pp with { CommandCost = 2 }), "wrong PP debit cost", rejectWave: false);
        AssertRejected(events.SetItem(ui, used with { Slot = 4 }).SetItem(pi, pp with { PpSlot = 4 }), "slot outside move list", rejectWave: false);
        AssertRejected(events.Insert(si + 1, stat with { Sequence = stat.Sequence + 1 }), "duplicate successful result", rejectWave: false);
    }

    [TestMethod]
    [DataRow(14)] [DataRow(159)] [DataRow(96)] [DataRow(133)] [DataRow(336)] [DataRow(294)]
    public void MissingOrMutatedDefinitionCannotAuthorizeDedicatedOrGeneralAnimation(int id)
    {
        var events = Resolve(Frame(Bodies(id, 0), false), 0).Events;
        var moves = Mechanics.Catalog.Moves; var move = moves[Move(id)];
        AssertRejected(events, "missing catalog", moves: null, explicitMetadata: true);
        AssertRejected(events, "missing definition", moves: moves.Remove(Move(id)), explicitMetadata: true);
        PokemonMoveDefinition[] invalid = [move with { Id = new("pret:MOVE_TACKLE") }, move with { SourceMoveId = 0 },
            move with { SourceMoveId = 355 }, move with { Pp = 0 }, move with { Power = -1 },
            move with { Accuracy = 101 }, move with { Type = "TYPE_" }, move with { Target = "MOVE_TARGET_" },
            move with { Flags = default }, move with { BattleEffectImplemented = true },
            move with { Effect = "EFFECT_" }, move with { Effect = id == 14 ? "EFFECT_ATTACK_UP" : "EFFECT_ATTACK_UP_2" },
            move with { Effect = "EFFECT_DEFENSE_UP" }];
        foreach (var changed in invalid)
            AssertRejected(events, "invalid or incompatible move definition", moves: moves.SetItem(Move(id), changed), explicitMetadata: true);
        // A valid-looking numeric ID cannot borrow another generated root label.
        AssertRejected(events, "wrong canonical source root", rejectWave: false,
            moves: moves.SetItem(Move(id), move with { SourceMoveId = id == 14 ? 159 : 14 }), explicitMetadata: true);
    }

    [TestMethod]
    [DataRow(14)] [DataRow(159)] [DataRow(96)] [DataRow(133)] [DataRow(336)] [DataRow(294)]
    public void MissingRendererSuppressesRootWithoutSuppressingCommittedGeneralWave(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Resolve(Frame(Bodies(id, 0), single), 0); var events = result.Events;
            int used = events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());
            int stat = events.IndexOf(events.OfType<SourceStatChangedEvent>().Single());
            var root = single ? CampaignMoveAnimationPresentation.AtAttackText(Single(events, used), true, SourceBattleScene.On, Mechanics.Catalog.Moves, (_, _) => false)
                : CampaignMoveAnimationPresentation.AtAttackText(Double(events, used), SourceBattleScene.On, Mechanics.Catalog.Moves, (_, _) => false);
            Assert.IsNull(root.Dispatch); Assert.AreEqual("script-outside-slice", root.Reason);
            var wave = single ? AnimationDispatchFixture.StatAtResult(Single(events, stat), true)
                : AnimationDispatchFixture.StatAtResult(Double(events, stat));
            Assert.IsNotNull(wave); Assert.AreEqual(id, wave.SourceMoveId); Assert.AreEqual(Argument(id), wave.AnimationArgument);
        }
    }

    [TestMethod]
    public void NaturalMeditateSharesAttackUpSemanticsWithItsOwnPublishedRoot()
    {
        var meditate = Mechanics.Catalog.Moves[Move(96)];
        Assert.AreEqual(96, meditate.SourceMoveId); Assert.AreEqual("EFFECT_ATTACK_UP", meditate.Effect);
        Assert.AreEqual("TYPE_PSYCHIC", meditate.Type); Assert.AreEqual(40, meditate.Pp);
        Assert.IsTrue(AnimationDispatchFixture.Supports(96, Move(96).Value), "Meditate requires its own published source root.");
        Assert.IsFalse(AnimationDispatchFixture.Supports(159, Move(96).Value), "Shared effect metadata cannot borrow Sharpen's root label.");
        foreach (bool single in new[] { false, true })
        for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int stage in new[] { 6, 11, 12 })
        {
            var result = Resolve(Frame(Bodies(96, actor, stage), single), actor);
            AssertReceipts(result, 96, actor, stage);
            AssertProjection(result, 96, actor, stage, single);
        }
    }
}
