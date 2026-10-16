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
public sealed class MultiHitAnimationDispatchTests
{
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index = 0) =>
        new("multi-hit", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index = 0) =>
        new("multi-hit", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };
    private static CampaignMoveAnimationDecision At(ImmutableArray<SourceBattleEvent> events, int index) =>
        CampaignMoveAnimationPresentation.AtHitResult(Double(events, index), SourceBattleScene.On);
    private static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events, long first = 1) =>
        events.Select((e, i) => e with { Sequence = first + i }).ToImmutableArray();
    private static ImmutableArray<SourceBattleEvent> Group(string name = "DOUBLE_SLAP", int actor = 0, int hits = 3)
    {
        var move = Id(name); var source = new BattlerSlot(actor); var target = new BattlerSlot(actor ^ 1);
        List<SourceBattleEvent> events = [
            new SourceAttackStringEvent(1, 7, source, move, move),
            new SourceMoveUsedEvent(2, 7, source, target, 0, move, 10, 9),
            new SourcePpCommandEvent(3, 7, source, target, 0, move, move, 10, 9, 1, 0)];
        for (int i = 0; i < hits; i++)
            events.Add(new SourceHpChangedEvent(4 + i, 7, source, target, move, 50 - 5 * i, 45 - 5 * i, 5, false, 100));
        events.Add(new SourceMultiHitEvent(4 + hits, 7, source, target, move, hits, hits));
        return [.. events];
    }
    private static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).ToArray();
    private static void AssertDispatches(ImmutableArray<SourceBattleEvent> events, int sourceMoveId, int actor, int target)
    {
        var hitEvents = Hits(events);
        Assert.IsTrue(hitEvents.Length > 0, "The fixture must contain successful hit receipts.");
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        int ordinal = 0;
        for (int index = 0; index < events.Length; index++)
        {
            var result = At(events, index);
            if (events[index] is not (SourceHpChangedEvent or SourceSubstituteDamagedEvent))
            {
                Assert.IsNull(result.Dispatch, $"Only hit receipts dispatch: {events[index].GetType().Name}.");
                continue;
            }
            Assert.IsNotNull(result.Dispatch, result.Reason);
            Assert.AreEqual("multi-hit", result.Dispatch.BattleId);
            Assert.AreEqual(events[index].Sequence, result.Dispatch.Sequence);
            Assert.AreEqual(events[index].Sequence, result.Dispatch.ResultSequence);
            Assert.AreEqual(attack.Sequence, result.Dispatch.AttackSequence);
            Assert.AreEqual(pp.Sequence, result.Dispatch.PpSequence);
            Assert.AreEqual(sourceMoveId, result.Dispatch.SourceMoveId);
            Assert.AreEqual(actor, result.Dispatch.Actor.Value);
            Assert.AreEqual(target, result.Dispatch.Target.Value);
            Assert.AreEqual((byte)ordinal++, result.Dispatch.MoveTurn);
            Assert.IsFalse(result.Dispatch.AcknowledgesCurrentReceipt,"Per-hit playback resumes the same receipt without submitting ACK.");
            Assert.AreEqual(events[index] is SourceSubstituteDamagedEvent, result.Dispatch.HitsSubstitute);
            Assert.AreEqual(result, At(events, index), "Projection is repeatable without an authoritative acknowledgement.");
        }
        Assert.AreEqual(hitEvents.Length, ordinal);
    }
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string label)
    {
        for (int i = 0; i < events.Length; i++)
            Assert.IsNull(At(events, i).Dispatch, $"{label}, event {i} ({events[i].GetType().Name})");
    }

    [TestMethod]
    [DataRow("DOUBLE_SLAP", 3)]
    [DataRow("COMET_PUNCH", 4)]
    public void EveryCommittedHitUsesItsOwnReceiptAndZeroBasedMoveTurnAtAllFourPositions(string name, int id)
    {
        for (int actor = 0; actor < 4; actor++)
        for (int hits = 2; hits <= 5; hits++)
        {
            var events = Group(name, actor, hits);
            string before = JsonSerializer.Serialize(events);
            AssertDispatches(events, id, actor, actor ^ 1);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(events, 1), SourceBattleScene.On).Dispatch,
                "The attack-text page must not play an extra first hit.");
            Assert.AreEqual(before, JsonSerializer.Serialize(events));
        }
    }

    [TestMethod]
    [DataRow("DOUBLE_SLAP", 3)]
    [DataRow("COMET_PUNCH", 4)]
    public void SelectedAlliesUseTheSameCommittedReceiptOrdinal(string name, int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var ally = new BattlerSlot(actor ^ 2);
            var events = Group(name, actor, 5).Select(e => e switch
            {
                SourceMoveUsedEvent x => x with { Target = ally },
                SourcePpCommandEvent x => x with { Target = ally },
                SourceHpChangedEvent x => x with { Target = ally },
                SourceMultiHitEvent x => x with { Target = ally },
                _ => e
            }).ToImmutableArray();
            AssertDispatches(events, id, actor, ally.Value);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(events, 1), SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod]
    [DataRow("DOUBLE_SLAP", 3)]
    [DataRow("COMET_PUNCH", 4)]
    public void CanonicalSinglesExportMatchesTypedReceiptsAndHonorsRulesScenePhaseAndCursorGates(string name, int id)
    {
        for (int actor = 0; actor < 2; actor++)
        {
            var events = Group(name, actor, 5); var single = Single(events);
            for (int i = 0; i < events.Length; i++)
            {
                var expected = At(events, i);
                var actual = CampaignMoveAnimationPresentation.AtHitResult(single with { EventIndex = i }, true, SourceBattleScene.On);
                Assert.AreEqual(expected.Dispatch, actual.Dispatch);
            }
            var battle = single with { EventIndex = 3 };
            Assert.AreEqual(id, CampaignMoveAnimationPresentation.AtHitResult(battle, true, SourceBattleScene.On).Dispatch!.SourceMoveId);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtHitResult(battle, false, SourceBattleScene.On).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtHitResult(battle, true, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtHitResult(battle with { Phase = CampaignBattlePhase.ChooseMove }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtHitResult(battle with { EventIndex = -1 }, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtHitResult(battle with { EventIndex = events.Length }, true, SourceBattleScene.On).Dispatch);
        }
        var twice = Double(Group(name), 3);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtHitResult(twice, SourceBattleScene.Off).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtHitResult(twice with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtHitResult(twice with { EventIndex = -1 }, SourceBattleScene.On).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtHitResult(twice with { EventIndex = twice.Events.Length }, SourceBattleScene.On).Dispatch);
    }

    [TestMethod]
    public void SubstituteBreakBodyDamageAndInterleavedItemOrContactEffectsKeepOnlyHitOrdinals()
    {
        var move = Id("DOUBLE_SLAP"); var a = new BattlerSlot(2); var t = new BattlerSlot(3);
        var events = Number(Group(actor: 2).Take(3).Concat(new SourceBattleEvent[] {
            // Damage is the calculated amount, so it may exceed the substitute's lost HP.
            new SourceSubstituteDamagedEvent(0, 7, a, t, move, 2, 0, 8, false, 100),
            new SourceMtMoonVolatileEvent(0, 7, t, "substitute", 2, 0, "faded"),
            new SourceHpChangedEvent(0, 7, a, t, move, 50, 45, 5, false, 100),
            new SourceHeldItemConsumedEvent(0, 7, t, "ITEM_ORAN_BERRY", 45, 55, 10),
            new SourceAbilityActivatedEvent(0, 7, t, "ABILITY_ROUGH_SKIN"),
            new SourceAbilityDamageEvent(0, 7, t, a, move, "ABILITY_ROUGH_SKIN", 30, 28, 2),
            new SourceStatusChangedEvent(0, 7, a, 0, WildBattleStatus.Paralysis, "contact"),
            new SourceHpChangedEvent(0, 7, a, t, move, 55, 50, 5, true, 92),
            new SourceCriticalHitEvent(0, 7, a, t),
            new SourceMultiHitEvent(0, 7, a, t, move, 3, 3)
        }));
        AssertDispatches(events, 3, 2, 3);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SuccessfulZeroLossFocusBandOrEndureReceiptsStillAnimate(bool endure)
    {
        var move = Id("COMET_PUNCH"); var a = new BattlerSlot(0); var t = new BattlerSlot(1);
        int hits = endure ? 1 : 3;
        var events = Group("COMET_PUNCH").Take(3).ToList();
        for (int i = 0; i < hits; i++)
        {
            events.Add(new SourceDamageAdjustedEvent(0, 7, t, move, endure ? "ITEM_NONE" : "ITEM_FOCUS_BAND", 12, 0, endure, !endure));
            events.Add(new SourceHpChangedEvent(0, 7, a, t, move, 1, 1, 0, false, 100));
        }
        // SourceGenericEffectiveness emits resultmessage after the hit loop and before its summary.
        if (!endure) events.Add(new SourceHeldItemSurvivalEvent(0, 7, t, move, "ITEM_FOCUS_BAND"));
        events.Add(new SourceMultiHitEvent(0, 7, a, t, move, endure ? 5 : 3, hits));
        AssertDispatches(Number(events), 4, 0, 1);
    }

    [TestMethod]
    public void MalformedInvocationPpAndSummaryCannotAuthorizeAnyHit()
    {
        var good = Group(hits: 2);
        var attack = (SourceAttackStringEvent)good[0]; var used = (SourceMoveUsedEvent)good[1];
        var pp = (SourcePpCommandEvent)good[2]; var summary = (SourceMultiHitEvent)good[^1];
        var wrong = Id("COMET_PUNCH");
        (string Label, ImmutableArray<SourceBattleEvent> Events)[] bad = [
            ("missing attack", good.RemoveAt(0)),
            ("missing PP", good.RemoveAt(2)),
            ("missing summary", good.RemoveAt(good.Length - 1)),
            ("nonadjacent attack", Number(good.Insert(1, new SourceAbilityActivatedEvent(0, 7, new(0), "ABILITY_CUTE_CHARM")))),
            ("wrong attack actor", good.SetItem(0, attack with { Actor = new(2) })),
            ("wrong attack turn", good.SetItem(0, attack with { Turn = 6 })),
            ("called attack", good.SetItem(0, attack with { ChosenMove = Id("METRONOME") })),
            ("wrong attack current move", good.SetItem(0, attack with { CurrentMove = wrong })),
            ("wrong used actor", good.SetItem(1, used with { Actor = new(2) })),
            ("wrong used target", good.SetItem(1, used with { Target = new(3) })),
            ("wrong used slot", good.SetItem(1, used with { Slot = 1 })),
            ("wrong used PP before", good.SetItem(1, used with { PpBefore = 11 })),
            ("wrong used PP after", good.SetItem(1, used with { PpAfter = 8 })),
            ("wrong PP actor", good.SetItem(2, pp with { Actor = new(2) })),
            ("wrong PP owner", good.SetItem(2, pp with { PpOwner = new(2) })),
            ("wrong PP target", good.SetItem(2, pp with { Target = new(3) })),
            ("wrong PP turn", good.SetItem(2, pp with { Turn = 6 })),
            ("called PP move", good.SetItem(2, pp with { ChosenMove = Id("METRONOME") })),
            ("wrong current PP move", good.SetItem(2, pp with { CurrentMove = wrong })),
            ("duplicate PP", Number(good.Insert(3, pp))),
            ("PP after first hit", Number(good.RemoveAt(2).Insert(3, pp))),
            ("attack sequence overlaps used", good.SetItem(0, attack with { Sequence = used.Sequence })),
            ("summary actor", good.SetItem(good.Length - 1, summary with { Actor = new(2) })),
            ("summary target", good.SetItem(good.Length - 1, summary with { Target = new(3) })),
            ("summary move", good.SetItem(good.Length - 1, summary with { Move = wrong })),
            ("summary turn", good.SetItem(good.Length - 1, summary with { Turn = 8 })),
            ("too few planned", good.SetItem(good.Length - 1, summary with { PlannedHits = 1 })),
            ("too many planned", good.SetItem(good.Length - 1, summary with { PlannedHits = 6 })),
            ("zero actual", good.SetItem(good.Length - 1, summary with { ActualHits = 0 })),
            ("actual exceeds planned", good.SetItem(good.Length - 1, summary with { ActualHits = 3 })),
            ("receipt missing", good.RemoveAt(3)),
            ("receipt extra", Number(good.Insert(4, good[3]))),
            ("duplicate summary", Number(good.Add(summary))),
            ("summary before final hit", Number(good.RemoveAt(good.Length - 1).Insert(4, summary))),
            ("duplicate sequence", good.SetItem(3, good[3] with { Sequence = 3 })),
            ("backwards sequence", good.SetItem(4, good[4] with { Sequence = 2 })),
            ("hit turn boundary", good.SetItem(4, good[4] with { Turn = 8 }))
        ];
        foreach (var invalid in bad) AssertDenied(invalid.Events, invalid.Label);
    }

    [TestMethod]
    public void WrongHitIdentityAndFailedOrImmuneResultsFailClosedEvenWithPlausibleSummary()
    {
        var good = Group(hits: 2); var hp = (SourceHpChangedEvent)good[3]; var move = hp.Move;
        foreach (var invalid in new[] {
            hp with { Actor = new(2) }, hp with { Target = new(3) }, hp with { Move = Id("COMET_PUNCH") }
        }) AssertDenied(good.SetItem(3, invalid), "wrong hit identity");
        SourceBattleEvent[] failures = [
            new SourceMoveMissedEvent(0, 7, new(0), new(1), move),
            new SourceMoveFailedEvent(0, 7, new(0), new(1), move, "protected"),
            new SourceAbilityPreventedEvent(0, 7, new(0), new(1), move, "ABILITY_WONDER_GUARD"),
            new SourceEffectivenessEvent(0, 7, new(0), new(1), move, WildBattleEffectiveness.NoEffect)
        ];
        foreach (var failure in failures) AssertDenied(Number(good.Insert(3, failure)), failure.GetType().Name);
        var substitute = new SourceSubstituteDamagedEvent(hp.Sequence, hp.Turn, hp.Actor, hp.Target, hp.Move, 5, 0, 8, false, 100);
        foreach (var invalid in new[] {
            substitute with { Actor = new(2) }, substitute with { Target = new(3) },
            substitute with { Move = Id("COMET_PUNCH") }
        }) AssertDenied(good.SetItem(3, invalid), "wrong substitute hit identity");
        foreach (string unsupported in new[] { "FURY_SWIPES", "BULLET_SEED", "TACKLE" })
            AssertDenied(Group(unsupported), "outside admitted per-hit slice: " + unsupported);
    }

    [TestMethod]
    public void NextInvocationCannotSupplyMissingSummaryAndItsHitOrdinalStartsAgainAtZero()
    {
        var first = Group(hits: 2); var next = Number(Group("COMET_PUNCH", 2, 3), first[^1].Sequence + 1);
        var combined = first.AddRange(next);
        Assert.AreEqual((byte)0, At(combined, 3).Dispatch!.MoveTurn);
        Assert.AreEqual((byte)1, At(combined, 4).Dispatch!.MoveTurn);
        Assert.AreEqual((byte)0, At(combined, first.Length + 3).Dispatch!.MoveTurn);
        Assert.AreEqual((byte)2, At(combined, first.Length + 5).Dispatch!.MoveTurn);
        var missing = first.RemoveAt(first.Length - 1).AddRange(next);
        Assert.IsNull(At(missing, 3).Dispatch);
        Assert.IsNull(At(missing, 4).Dispatch);
        Assert.AreEqual((byte)0, At(missing, first.Length + 2).Dispatch!.MoveTurn);
    }

    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var mechanics = Mechanics; var move = Id(name);
        var pokemon = SourcePokemonFactory.Create(mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learnset fixture: {species}/{name}/{level}.");
        int pp = mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    private static SourceBattleFrame KernelFrame(WildBattleCombatant[] bodies, uint seed)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        return new() {
            Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer,
            Rng = new(seed),
            Parties = [new(BattleSide.Player, [Member(bodies[0]), Member(bodies[2])]),
                new(BattleSide.Opponent, [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
    }
    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor, int target)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty);
        return result;
    }

    [TestMethod]
    [DataRow("DOUBLE_SLAP", "CLEFAIRY", 13, 3)]
    [DataRow("COMET_PUNCH", "KANGASKHAN", 5, 4)]
    public void PublicKernelOriginalSourceVectorsEmitOneDispatchPerRealHitAtEveryPosition(string name, string species, int level, int id)
    {
        using var source = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root, "tests/Game.Battle.Tests/Moves/source-vectors.json")));
        Assert.AreEqual(PokemonFactory.SourceCommit, source.RootElement.GetProperty("sourceCommit").GetString());
        var vectors = source.RootElement.GetProperty("multihit");
        Assert.AreEqual(48, vectors.GetArrayLength(), "Original complete multihit fixture required.");
        foreach (var vector in vectors.EnumerateArray())
        for (int actor = 0; actor < 4; actor++)
        {
            var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL")).ToArray();
            bodies[actor] = NaturalMon(species, level, name) with { AccuracyStage = 12 };
            var result = Resolve(KernelFrame(bodies, vector.GetProperty("seed").GetUInt32()), actor, actor ^ 1);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(vector.GetProperty("hits").GetInt32(), summary.PlannedHits);
            Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
            Assert.AreEqual(summary.ActualHits, Hits(result.Events).Length);
            Assert.AreEqual(1, result.Events.OfType<SourcePpCommandEvent>().Count());
            Assert.AreEqual(vector.GetProperty("draws").GetInt32(), result.Trace.Length);
            Assert.AreEqual(vector.GetProperty("finalSeed").GetUInt32(), result.State.Rng.Seed);
            // Use the full public event batch and an empty presentation frame. The matcher
            // cannot obtain hit identity, substitute state or ordinals from the final frame.
            AssertDispatches(result.Events, id, actor, actor ^ 1);
        }
    }

    [TestMethod]
    [DataRow("DOUBLE_SLAP", "CLEFAIRY", 3)]
    [DataRow("COMET_PUNCH", "KANGASKHAN", 4)]
    public void PublicKernelSubstituteBreakThenBodyAndKoTruncationUseOnlyActualReceipts(string name, string species, int id)
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (bool ko in new[] { false, true })
        {
            int target = actor ^ 1;
            var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL")).ToArray();
            bodies[actor] = NaturalMon(species, 30, name) with { AccuracyStage = 12 };
            bodies[target] = ko
                ? bodies[target] with { Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } }
                : bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } };
            var result = Resolve(KernelFrame(bodies, 31), actor, target);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            if (ko)
            {
                Assert.AreEqual(1, summary.ActualHits);
                Assert.IsTrue(summary.PlannedHits > summary.ActualHits);
                Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
                Assert.AreEqual(target, result.Events.OfType<SourceFaintedEvent>().Single().Side.Value);
                int usedIndex = result.Events.IndexOf(result.Events.OfType<SourceMoveUsedEvent>().Single());
                Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(result.Events, usedIndex), SourceBattleScene.On).Dispatch,
                    "A KO-truncated one-hit action still dispatches only at its hit receipt.");
            }
            else
            {
                var substitute = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
                Assert.AreEqual(1, substitute.HpBefore); Assert.AreEqual(0, substitute.HpAfter);
                Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Any());
                Assert.AreEqual(summary.PlannedHits, summary.ActualHits);
                Assert.AreEqual(summary.ActualHits - 1, result.Events.OfType<SourceHpChangedEvent>().Count());
            }
            AssertDispatches(result.Events, id, actor, target);
        }
    }

    [TestMethod]
    [DataRow("DOUBLE_SLAP", "CLEFAIRY", 3)]
    [DataRow("COMET_PUNCH", "KANGASKHAN", 4)]
    public void PublicKernelRoughSkinActorKoStopsAfterOneHitAtEveryPosition(string name, string species, int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1;
            var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL")).ToArray();
            var attacker = NaturalMon(species, 30, name);
            bodies[actor] = attacker with { AccuracyStage = 12, Pokemon = attacker.Pokemon with { CurrentHp = 1 } };
            bodies[target] = NaturalMon("SHARPEDO", 100, "BITE");
            Assert.AreEqual("ABILITY_ROUGH_SKIN", bodies[target].Pokemon.AbilityId);
            var result = Resolve(KernelFrame(bodies, 31), actor, target);
            var summary = result.Events.OfType<SourceMultiHitEvent>().Single();
            var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
            var contact = result.Events.OfType<SourceAbilityDamageEvent>().Single();
            Assert.AreEqual(1, summary.ActualHits);
            Assert.IsTrue(summary.PlannedHits > summary.ActualHits);
            Assert.AreEqual(actor, hit.Actor.Value); Assert.AreEqual(target, hit.Target.Value);
            Assert.AreEqual("ABILITY_ROUGH_SKIN", contact.Ability);
            Assert.AreEqual(target, contact.Holder.Value); Assert.AreEqual(actor, contact.Target.Value);
            Assert.AreEqual(1, contact.HpBefore); Assert.AreEqual(0, contact.HpAfter);
            Assert.IsTrue(hit.Sequence < contact.Sequence && contact.Sequence < summary.Sequence);
            Assert.AreEqual(actor, result.Events.OfType<SourceFaintedEvent>().Single().Side.Value);
            Assert.AreEqual(1, Hits(result.Events).Length);
            AssertDispatches(result.Events, id, actor, target);
            int usedIndex = result.Events.IndexOf(result.Events.OfType<SourceMoveUsedEvent>().Single());
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(result.Events, usedIndex), SourceBattleScene.On).Dispatch);
        }
    }
}
