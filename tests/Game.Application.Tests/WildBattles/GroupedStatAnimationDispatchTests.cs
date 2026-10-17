using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class GroupedStatAnimationDispatchTests
{
    // These projections intentionally receive an empty Frame. Presentation must
    // reconstruct the invocation from its receipts, never the committed end state.
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index = 1) =>
        new("grouped-stat", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);

    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index = 1) =>
        new("grouped-stat", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(),
            index, null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };

    private static MoveId Move(int id) => new(id switch
    {
        322 => "pret:MOVE_COSMIC_POWER", 339 => "pret:MOVE_BULK_UP", 321 => "pret:MOVE_TICKLE", 207 => "pret:MOVE_SWAGGER",
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    });

    private static ImmutableArray<SourceBattleEvent> Invocation(int id, int actor, int target,
        params SourceBattleEvent[] results)
    {
        var move = Move(id);
        return [new SourceAttackStringEvent(1, 1, new(actor), move, move),
            new SourceMoveUsedEvent(2, 1, new(actor), new(target), 0, move, 10, 9),
            new SourcePpCommandEvent(3, 1, new(actor), new(target), 0, move, move, 10, 9, 1, 0),
            .. results];
    }

    private static ImmutableArray<SourceBattleEvent> Scenario(int id, string branch, int actor, int target)
    {
        SourceBattleEvent Stat(WildBattleStat stat, int before, int after, long sequence = 4) =>
            new SourceStatChangedEvent(sequence, 1, new(actor), new(target), stat, before, after);
        SourceBattleEvent Cap(WildBattleStat stat, long sequence = 4) => id == 321
            ? new SourceStatAtMinimumEvent(sequence, 1, new(actor), new(target), stat)
            : new SourceStatAtMaximumEvent(sequence, 1, new(actor), new(target), stat);
        SourceBattleEvent Ability(string ability, long sequence = 4) =>
            new SourceAbilityPreventedEvent(sequence, 1, new(actor), new(target), Move(id), ability);
        SourceBattleEvent Failed(string reason, long sequence = 4) =>
            new SourceMoveFailedEvent(sequence, 1, new(actor), new(target), Move(id), reason);
        SourceBattleEvent Confused() => new SourceConfusionChangedEvent(5, 1, new(target), 0, 3, "move");
        SourceBattleEvent[] results = (id, branch) switch
        {
            (322, "both") => [Stat(WildBattleStat.Defense, 6, 7), Stat(WildBattleStat.SpecialDefense, 6, 7, 5)],
            (322, "clamped") => [Stat(WildBattleStat.Defense, 11, 12), Stat(WildBattleStat.SpecialDefense, 11, 12, 5)],
            (322, "defense-cap") => [Cap(WildBattleStat.Defense), Stat(WildBattleStat.SpecialDefense, 6, 7, 5)],
            (322, "spdef-cap") => [Stat(WildBattleStat.Defense, 6, 7), Cap(WildBattleStat.SpecialDefense, 5)],
            (339, "both") => [Stat(WildBattleStat.Attack, 6, 7), Stat(WildBattleStat.Defense, 6, 7, 5)],
            (339, "clamped") => [Stat(WildBattleStat.Attack, 11, 12), Stat(WildBattleStat.Defense, 11, 12, 5)],
            (339, "attack-cap") => [Cap(WildBattleStat.Attack), Stat(WildBattleStat.Defense, 6, 7, 5)],
            (339, "defense-cap") => [Stat(WildBattleStat.Attack, 6, 7), Cap(WildBattleStat.Defense, 5)],
            (321, "both") => [Stat(WildBattleStat.Attack, 6, 5), Stat(WildBattleStat.Defense, 6, 5, 5)],
            (321, "attack-cap") => [Cap(WildBattleStat.Attack), Stat(WildBattleStat.Defense, 1, 0, 5)],
            (321, "defense-cap") => [Stat(WildBattleStat.Attack, 1, 0), Cap(WildBattleStat.Defense, 5)],
            (321, "hyper-cutter") => [Ability("ABILITY_HYPER_CUTTER"), Stat(WildBattleStat.Defense, 6, 5, 5)],
            (321, "hyper-cutter-cap") => [Ability("ABILITY_HYPER_CUTTER"), Cap(WildBattleStat.Defense, 5)],
            (321, "mist") => [Failed("mist")],
            (321, "clear-body") => [Ability("ABILITY_CLEAR_BODY")],
            (321, "white-smoke") => [Ability("ABILITY_WHITE_SMOKE")],
            (207, "confusion") => [Stat(WildBattleStat.Attack, 6, 8), Confused()],
            (207, "clamped") => [Stat(WildBattleStat.Attack, 11, 12), Confused()],
            (207, "own-tempo") => [Stat(WildBattleStat.Attack, 6, 8), Ability("ABILITY_OWN_TEMPO", 5)],
            (207, "safeguard") => [Stat(WildBattleStat.Attack, 6, 8), Failed("safeguard", 5)],
            (207, "already-confused") => [Stat(WildBattleStat.Attack, 6, 8), Failed("already-confused", 5)],
            (207, "cap-confusion") => [Cap(WildBattleStat.Attack), Confused()],
            (207, "cap-own-tempo") => [Cap(WildBattleStat.Attack), Ability("ABILITY_OWN_TEMPO", 5)],
            (207, "cap-safeguard") => [Cap(WildBattleStat.Attack), Failed("safeguard", 5)],
            _ => throw new ArgumentOutOfRangeException(nameof(branch), branch)
        };
        return Invocation(id, actor, target, results);
    }

    private static bool IsCap(SourceBattleEvent value) =>
        value is SourceStatAtMinimumEvent or SourceStatAtMaximumEvent;

    private static void AssertValid(ImmutableArray<SourceBattleEvent> events, int id, int affected,
        int trigger, int argument, bool checkSingle = true)
    {
        var battle = Double(events);
        Assert.AreEqual<long?>(4L, CampaignStatAnimationPresentation.GroupedMoveResultSequence(battle));
        var root = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On);
        Assert.IsNotNull(root.Dispatch, root.Reason);
        Assert.AreEqual(id, root.Dispatch.SourceMoveId);
        Assert.AreEqual(4L, root.Dispatch.ResultSequence);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.Off).Dispatch);
        for (int index = 0; index < events.Length; index++)
        {
            var current = battle with { EventIndex = index };
            var dispatch = AnimationDispatchFixture.StatAtResult(current);
            if (index == trigger)
            {
                Assert.IsNotNull(dispatch, $"{id}: trigger {index}");
                Assert.AreEqual("grouped-stat", dispatch.BattleId);
                Assert.AreEqual(events[index].Sequence, dispatch.Sequence);
                Assert.AreEqual(1L, dispatch.AttackSequence);
                Assert.AreEqual(3L, dispatch.PpSequence);
                Assert.AreEqual(id, dispatch.SourceMoveId);
                Assert.AreEqual(affected, dispatch.Affected.Value);
                Assert.AreEqual(argument, dispatch.AnimationArgument);
            }
            else Assert.IsNull(dispatch, $"{id}: unexpected replay at {index}");
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(current, true));
            Assert.AreEqual(IsCap(events[index]), CampaignStatAnimationPresentation.IsSilentResultReceipt(current),
                $"{id}: only the script's individual caps are silent at {index}");
        }
        Assert.AreEqual(events, battle.Events);
        if (!checkSingle) return;
        var single = Single(events);
        Assert.AreEqual<long?>(4L, CampaignStatAnimationPresentation.GroupedMoveResultSequence(single, true));
        Assert.IsNull(CampaignStatAnimationPresentation.GroupedMoveResultSequence(single, false));
        var singleRoot = AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On);
        Assert.IsNotNull(singleRoot.Dispatch, singleRoot.Reason);
        Assert.AreEqual(4L, singleRoot.Dispatch.ResultSequence);
        for (int index = 0; index < events.Length; index++)
        {
            var current = single with { EventIndex = index };
            Assert.AreEqual(AnimationDispatchFixture.StatAtResult(battle with { EventIndex = index }),
                AnimationDispatchFixture.StatAtResult(current, true));
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(current, false));
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(current, true, true));
            Assert.AreEqual(IsCap(events[index]), CampaignStatAnimationPresentation.IsSilentResultReceipt(current, true));
            Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(current, false));
        }
    }

    [TestMethod]
    [DataRow(322, "both", 3, 55)] [DataRow(322, "clamped", 3, 55)]
    [DataRow(322, "defense-cap", 3, 19)] [DataRow(322, "spdef-cap", 3, 16)]
    [DataRow(339, "both", 3, 55)] [DataRow(339, "clamped", 3, 55)]
    [DataRow(339, "attack-cap", 3, 16)] [DataRow(339, "defense-cap", 3, 15)]
    [DataRow(321, "both", 3, 57)] [DataRow(321, "attack-cap", 4, 23)]
    [DataRow(321, "defense-cap", 3, 22)] [DataRow(321, "hyper-cutter", 4, 23)]
    [DataRow(321, "hyper-cutter-cap", -1, 0)] [DataRow(321, "mist", -1, 0)]
    [DataRow(321, "clear-body", -1, 0)] [DataRow(321, "white-smoke", -1, 0)]
    [DataRow(207, "confusion", 3, 39)] [DataRow(207, "clamped", 3, 39)]
    [DataRow(207, "own-tempo", 3, 39)] [DataRow(207, "safeguard", 3, 39)]
    [DataRow(207, "already-confused", 3, 39)] [DataRow(207, "cap-confusion", -1, 0)]
    [DataRow(207, "cap-own-tempo", -1, 0)] [DataRow(207, "cap-safeguard", -1, 0)]
    public void OriginalScriptBranchesBindOneWaveAndSilenceOnlyIndividualCapsAtEveryPosition(
        int id, string branch, int trigger, int argument)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = id is 322 or 339 ? actor : actor ^ 1;
            AssertValid(Scenario(id, branch, actor, target), id, target, trigger, argument, actor < 2);
            if (id is not (322 or 339))
                AssertValid(Scenario(id, branch, actor, actor ^ 2), id, actor ^ 2, trigger, argument, false);
        }
    }

    private static void AssertRejected(ImmutableArray<SourceBattleEvent> events, string reason,
        bool checkSingle = true)
    {
        var battle = Double(events);
        Assert.IsNull(CampaignStatAnimationPresentation.GroupedMoveResultSequence(battle), reason);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On).Dispatch, reason);
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(battle with { EventIndex = index }), reason);
            Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(battle with { EventIndex = index }), reason);
        }
        if (!checkSingle) return;
        var single = Single(events);
        Assert.IsNull(CampaignStatAnimationPresentation.GroupedMoveResultSequence(single, true), reason);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch, reason);
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(single with { EventIndex = index }, true), reason);
            Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(single with { EventIndex = index }, true), reason);
        }
    }

    [TestMethod]
    [DataRow(322)] [DataRow(339)] [DataRow(321)] [DataRow(207)]
    public void InvocationCannotBorrowSwitchedForeignDuplicateOrIncompleteReceipts(int id)
    {
        int target = id is 322 or 339 ? 0 : 1;
        var good = Scenario(id, id == 207 ? "confusion" : "both", 0, target);
        var attack = (SourceAttackStringEvent)good[0];
        var used = (SourceMoveUsedEvent)good[1];
        var pp = (SourcePpCommandEvent)good[2];
        var stat = (SourceStatChangedEvent)good[3];
        MoveId foreign = new("pret:MOVE_TACKLE");
        var cases = new (string Reason, ImmutableArray<SourceBattleEvent> Events)[]
        {
            ("foreign attack actor", good.SetItem(0, attack with { Actor = new(1) })),
            ("called attack chosen", good.SetItem(0, attack with { ChosenMove = foreign })),
            ("foreign attack current", good.SetItem(0, attack with { CurrentMove = foreign })),
            ("duplicate attack sequence", good.SetItem(0, attack with { Sequence = 2 })),
            ("foreign used actor", good.SetItem(1, used with { Actor = new(1) })),
            ("foreign used target", good.SetItem(1, used with { Target = new(target ^ 1) })),
            ("foreign used move", good.SetItem(1, used with { Move = foreign })),
            ("foreign used turn", good.SetItem(1, used with { Turn = 2 })),
            ("foreign PP actor", good.SetItem(2, pp with { Actor = new(1) })),
            ("foreign PP target", good.SetItem(2, pp with { Target = new(target ^ 1) })),
            ("foreign PP owner", good.SetItem(2, pp with { PpOwner = new(1) })),
            ("wrong PP slot", good.SetItem(2, pp with { PpSlot = 1 })),
            ("PP slot outside move list", good.SetItem(1, used with { Slot = 4 }).SetItem(2, pp with { PpSlot = 4 })),
            ("called PP chosen", good.SetItem(2, pp with { ChosenMove = foreign })),
            ("foreign PP current", good.SetItem(2, pp with { CurrentMove = foreign })),
            ("wrong PP before", good.SetItem(2, pp with { Before = 11 })),
            ("wrong PP after", good.SetItem(2, pp with { After = 8 })),
            ("unspent PP", good.SetItem(1, used with { PpAfter = 10 }).SetItem(2, pp with { After = 10 })),
            ("foreign PP turn", good.SetItem(2, pp with { Turn = 2 })),
            ("duplicate PP sequence", good.SetItem(2, pp with { Sequence = 2 })),
            ("foreign stat actor", good.SetItem(3, stat with { Actor = new(1) })),
            ("foreign stat target", good.SetItem(3, stat with { Target = new(target ^ 1) })),
            ("foreign stat turn", good.SetItem(3, stat with { Turn = 2 })),
            ("no stat change", good.SetItem(3, stat with { After = stat.Before })),
            ("wrong stat", good.SetItem(3, stat with { Stat = WildBattleStat.Speed })),
            ("out of range stage", good.SetItem(3, stat with { Before = 13, After = 12 })),
            ("duplicate result sequence", good.SetItem(4, good[4] with { Sequence = 4 })),
            ("switched result order", good.SetItem(3, good[4] with { Sequence = 4 }).SetItem(4, good[3] with { Sequence = 5 })),
            ("duplicate primary receipt", good.Add(stat with { Sequence = 6 })),
            ("missing final result", good.RemoveAt(4)),
            ("missing first result", good.RemoveAt(3)),
            ("missing PP", good.RemoveAt(2)),
            ("interrupted group", good.SetItem(4, new SourceTurnReadyEvent(5, 1, 0))),
            ("switch between results", good.SetItem(4, new SourceCalledPartySwitchedEvent(5, 1, new(target), 0, 1, WildBattleCalledSwitchKind.Forced)))
        };
        foreach (var (reason, events) in cases) AssertRejected(events, $"{id}: {reason}");
    }

    [TestMethod]
    public void CapsAndProtectionMustFollowTheirOwnSourcePreconditions()
    {
        AssertRejected(Invocation(322, 0, 0,
            new SourceStatAtMaximumEvent(4, 1, new(0), new(0), WildBattleStat.Defense),
            new SourceStatAtMaximumEvent(5, 1, new(0), new(0), WildBattleStat.SpecialDefense)), "Cosmic Power both capped must fail earlier");
        AssertRejected(Scenario(322, "both", 0, 1), "Cosmic Power is self-only");
        AssertRejected(Scenario(322, "both", 0, 2), "Cosmic Power cannot target its ally", false);
        foreach(string blocked in new[]{"ABILITY_CLEAR_BODY","ABILITY_WHITE_SMOKE","ABILITY_HYPER_CUTTER"})
            AssertRejected(Scenario(322, "both", 0, 0).SetItem(3,new SourceAbilityPreventedEvent(4,1,new(0),new(0),Move(322),blocked)), "Self stat increases do not use prevention receipts");
        AssertRejected(Invocation(339, 0, 0,
            new SourceStatAtMaximumEvent(4, 1, new(0), new(0), WildBattleStat.Attack),
            new SourceStatAtMaximumEvent(5, 1, new(0), new(0), WildBattleStat.Defense)), "Bulk Up both capped must fail earlier");
        AssertRejected(Invocation(321, 0, 1,
            new SourceStatAtMinimumEvent(4, 1, new(0), new(1), WildBattleStat.Attack),
            new SourceStatAtMinimumEvent(5, 1, new(0), new(1), WildBattleStat.Defense)), "Tickle both capped must fail earlier");
        AssertRejected(Invocation(207, 0, 1,
            new SourceStatAtMaximumEvent(4, 1, new(0), new(1), WildBattleStat.Attack),
            new SourceMoveFailedEvent(5, 1, new(0), new(1), Move(207), "already-confused")), "Swagger cap plus existing confusion must fail earlier");
        AssertRejected(Scenario(339, "both", 0, 1), "Bulk Up is self-only");
        AssertRejected(Scenario(339, "both", 0, 2), "Bulk Up cannot target its ally", false);
        foreach (int id in new[] { 322, 339, 321, 207 })
        {
            int target = id is 322 or 339 ? 0 : 1;
            foreach (string failure in new[] { "accuracy", "substitute", "stats-at-maximum", "stats-at-minimum", "confused-and-stat-maxed" })
                AssertRejected(Invocation(id, 0, target,
                    new SourceMoveFailedEvent(4, 1, new(0), new(target), Move(id), failure)), $"{id}: {failure}");
        }
        var tickle = Scenario(321, "both", 0, 1);
        AssertRejected(tickle.SetItem(3, new SourceStatAtMaximumEvent(4, 1, new(0), new(1), WildBattleStat.Attack)), "Tickle cannot use an upper cap");
        AssertRejected(tickle.SetItem(3, new SourceAbilityPreventedEvent(4, 1, new(0), new(1), Move(321), "ABILITY_KEEN_EYE")), "unrelated protection");
        var swagger = Scenario(207, "confusion", 0, 1);
        var confusion = (SourceConfusionChangedEvent)swagger[4];
        foreach (var bad in new[] { confusion with { Side = new(0) }, confusion with { Before = 1 },
            confusion with { After = 1 }, confusion with { After = 6 }, confusion with { Reason = "held-berry" }, confusion with { Turn = 2 } })
            AssertRejected(swagger.SetItem(4, bad), "Swagger must own the following primary confusion receipt");
        AssertRejected(swagger.SetItem(3, new SourceStatChangedEvent(4, 1, new(0), new(1), WildBattleStat.Attack, 6, 7)), "Swagger requests two stages even when clamped");
    }

    [TestMethod]
    public void WhiteHerbRestorationIsASeparateTargetOwnedSuffixAfterStatsOrCaps()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (string branch in new[] { "both", "attack-cap", "defense-cap" })
        {
            int target = actor ^ 1;
            var prefix = Scenario(321, branch, actor, target);
            int restoredBefore = branch == "both" ? 5 : 0;
            ImmutableArray<SourceBattleEvent> suffix = [
                new SourceStatChangedEvent(6, 1, new(target), new(target), WildBattleStat.Attack, restoredBefore, 6),
                new SourceStatChangedEvent(7, 1, new(target), new(target), WildBattleStat.Defense, restoredBefore, 6),
                new SourceHeldItemConsumedEvent(8, 1, new(target), "ITEM_WHITE_HERB", 10, 10, 0)
                { StatChanges = [new(WildBattleStat.Attack, restoredBefore, 6), new(WildBattleStat.Defense, restoredBefore, 6)] }];
            var events = prefix.AddRange(suffix);
            int trigger = branch == "attack-cap" ? 4 : 3;
            int argument = branch == "both" ? 57 : branch == "attack-cap" ? 23 : 22;
            AssertValid(events, 321, target, trigger, argument, actor < 2);
            var held = CampaignHeldItemAnimationPresentation.AtResult(Double(events, 5), SourceBattleScene.On, new MoveId?[4]);
            Assert.IsNotNull(held); Assert.AreEqual(6L, held.Sequence); Assert.AreEqual(8L, held.ReceiptSequence);
            Assert.AreEqual(target, held.Affected.Value);
            AssertRejected(events.RemoveAt(7), "restoration without typed item receipt", actor < 2);
            var consumed = (SourceHeldItemConsumedEvent)events[7];
            AssertRejected(events.SetItem(7, consumed with { StatChanges = [new(WildBattleStat.Attack, 4, 6)] }),
                "restoration differs from item metadata", actor < 2);
        }
    }

    [TestMethod]
    public void LumConfusionCureCannotReplaceOrEraseSwaggerPrimaryConfusion()
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (string branch in new[] { "confusion", "cap-confusion" })
        {
            int target = actor ^ 1;
            var prefix = Scenario(207, branch, actor, target);
            var events = prefix.Add(new SourceConfusionChangedEvent(6, 1, new(target), 3, 0, "held-berry"))
                .Add(new SourceHeldItemConsumedEvent(7, 1, new(target), "ITEM_LUM_BERRY", 10, 10, 0));
            AssertValid(events, 207, target, branch == "confusion" ? 3 : -1, branch == "confusion" ? 39 : 0, actor < 2);
            var held = CampaignHeldItemAnimationPresentation.AtResult(Double(events, 5), SourceBattleScene.On, new MoveId?[4]);
            Assert.IsNotNull(held); Assert.AreEqual(6L, held.Sequence); Assert.AreEqual(7L, held.ReceiptSequence);
            AssertRejected(events.RemoveAt(4), "the later held cure is not Swagger's confusion result", actor < 2);
            AssertRejected(events.SetItem(4, new SourceConfusionChangedEvent(5, 1, new(target), 0, 3, "held-berry")),
                "a held reason cannot establish primary confusion", actor < 2);
        }
    }

    [TestMethod]
    public void CosmicPowerCannotBorrowHeldItemStatsForItsRequiredSecondDirectCommand()
    {
        for(int actor=0;actor<4;actor++)
        {
            var first = new SourceStatChangedEvent(4,1,new(actor),new(actor),WildBattleStat.Defense,6,7);
            var restoration = new SourceStatChangedEvent(5,1,new(actor),new(actor),WildBattleStat.SpecialDefense,5,6);
            var consumed = new SourceHeldItemConsumedEvent(6,1,new(actor),"ITEM_WHITE_HERB",10,10,0)
                {StatChanges=[new(WildBattleStat.SpecialDefense,5,6)]};
            AssertRejected(Invocation(322,actor,actor,first,restoration,consumed),
                "An authenticated item restoration cannot stand in for Cosmic Power's SPDEF command",actor<2);
            var complete=Scenario(322,"both",actor,actor);
            AssertRejected(complete.Add(restoration with{Sequence=6}),"An unauthenticated later stat cannot enter the direct pair",actor<2);
            AssertRejected(complete.Add(restoration with{Sequence=6}).Add(consumed with{Sequence=7,StatChanges=[new(WildBattleStat.SpecialDefense,4,6)]}),
                "Malformed held metadata cannot authorize a foreign result suffix",actor<2);
        }
    }

    [TestMethod]
    public void CompletedInvocationCannotBorrowAcrossANewAttackOrSwitchBoundary()
    {
        var first = Scenario(321, "both", 0, 1);
        var later = Scenario(207, "confusion", 1, 0).Select(e => e with { Sequence = e.Sequence + 10 }).ToImmutableArray();
        var events = first.AddRange(later);
        Assert.AreEqual<long?>(4L, CampaignStatAnimationPresentation.GroupedMoveResultSequence(Double(events)));
        Assert.AreEqual<long?>(14L, CampaignStatAnimationPresentation.GroupedMoveResultSequence(Double(events, 6)));
        Assert.AreEqual(57, AnimationDispatchFixture.StatAtResult(Double(events, 3))!.AnimationArgument);
        Assert.AreEqual(39, AnimationDispatchFixture.StatAtResult(Double(events, 8))!.AnimationArgument);
        var incomplete = first.RemoveAt(4).AddRange(later);
        Assert.IsNull(CampaignStatAnimationPresentation.GroupedMoveResultSequence(Double(incomplete)));
        Assert.IsNull(AnimationDispatchFixture.StatAtResult(Double(incomplete, 3)));
        var switched = first.Insert(4, new SourceCalledPartySwitchedEvent(5, 1, new(1), 0, 1, WildBattleCalledSwitchKind.Forced))
            .SetItem(5, first[4] with { Sequence = 6 });
        AssertRejected(switched, "a result after a switch belongs to a different occupant");
    }

    [TestMethod]
    public void PhaseAndIndexGatesFailClosedAndExistingSingleStatDispatchIsPreserved()
    {
        var events = Scenario(339, "attack-cap", 0, 0);
        var finished = Double(events, 3) with { Stage = CampaignDoubleBattleStage.Finished };
        Assert.IsNull(AnimationDispatchFixture.StatAtResult(finished));
        Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(finished));
        Assert.IsNull(CampaignStatAnimationPresentation.GroupedMoveResultSequence(finished with { EventIndex = 1 }));
        var singleResults = Single(events, 3) with { Phase = CampaignBattlePhase.Results };
        Assert.IsNull(AnimationDispatchFixture.StatAtResult(singleResults, true));
        Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(singleResults, true));
        Assert.IsNull(CampaignStatAnimationPresentation.GroupedMoveResultSequence(singleResults with { EventIndex = 1 }, true));
        foreach (int index in new[] { -1, events.Length, events.Length + 1 })
        {
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(Double(events, index)));
            Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(Double(events, index)));
            Assert.IsNull(CampaignStatAnimationPresentation.GroupedMoveResultSequence(Double(events, index)));
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(Single(events, index), true));
            Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(Single(events, index), true));
            Assert.IsNull(CampaignStatAnimationPresentation.GroupedMoveResultSequence(Single(events, index), true));
        }
        MoveId barrier = new("pret:MOVE_BARRIER");
        ImmutableArray<SourceBattleEvent> existing = [new SourceAttackStringEvent(1, 1, new(0), barrier, barrier),
            new SourceMoveUsedEvent(2, 1, new(0), new(0), 0, barrier, 10, 9),
            new SourcePpCommandEvent(3, 1, new(0), new(0), 0, barrier, barrier, 10, 9, 1, 0),
            new SourceStatChangedEvent(4, 1, new(0), new(0), WildBattleStat.Defense, 11, 12)];
        Assert.AreEqual(40, AnimationDispatchFixture.StatAtResult(Double(existing, 3))!.AnimationArgument);
        Assert.AreEqual(40, AnimationDispatchFixture.StatAtResult(Single(existing, 3), true)!.AnimationArgument);
        Assert.IsNull(CampaignStatAnimationPresentation.GroupedMoveResultSequence(Double(existing)));
        Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(Double(existing, 3)));
        var capped = existing.SetItem(3, new SourceStatAtMaximumEvent(4, 1, new(0), new(0), WildBattleStat.Defense));
        Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(Double(capped, 3)));
        Assert.IsFalse(CampaignStatAnimationPresentation.IsSilentResultReceipt(Single(capped, 3), true));
    }

    private static WildBattleCombatant KernelMon(SourceMechanicsCatalog mechanics, string species, int level, string move)
    {
        var pokemon = SourcePokemonFactory.Create(mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        MoveId id = new("pret:MOVE_" + move);
        Assert.IsTrue(mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == id && m.Level <= level),
            $"{species} must naturally learn {move} by level {level}");
        int pp = mechanics.Catalog.Moves[id].Pp;
        return new(pokemon with { Moves = [new(id, pp, pp)] });
    }

    private static SourceBattleFrame KernelFrame(PokemonCatalog catalog, WildBattleCombatant[] bodies, uint seed)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        SourceBattleFrame frame = new()
        {
            Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer,
            Rng = new(seed), Parties = [new(BattleSide.Player, [Member(bodies[0]), Member(bodies[2])]),
                new(BattleSide.Opponent, [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        Assert.IsFalse(entry.Events.OfType<SourceHeldItemConsumedEvent>().Any());
        return entry.State;
    }

    private static ImmutableArray<ImmutableArray<SourceBattleEvent>> KernelTurn(PokemonCatalog catalog,
        SourceBattleFrame frame, int actor, int target)
    {
        var choices = frame.Slots.Where(c => frame.CanAct(c.Slot)).Select(c => new SourceActionSelection(
            c.Occupant!.Value, SourceActionKind.Move, 0, c.Slot.Value == actor ? new(target) : c.Slot)).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(catalog, frame, frame.Revision, choices);
        var batches = ImmutableArray.CreateBuilder<ImmutableArray<SourceBattleEvent>>();
        for (int step = 0; step < 16; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault);
            Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            batches.Add(result.Events);
            if (result.CompletedCleanTurn) return batches.ToImmutable();
            Assert.IsNotNull(result.Continuation);
            frame = result.State;
            result = SourceFourActionTurns.Advance(catalog, frame, result.Continuation.Value);
        }
        Assert.Fail("Public turn did not finish within its bounded action/end-turn boundaries.");
        return [];
    }

    private static ImmutableArray<SourceBattleEvent> NaturalKernelGroup(int id, int actor, int target, string item = "ITEM_NONE")
    {
        var mechanics = CurrentCampaignFixture.Catalog.PokemonContent!.Pokemon.Mechanics;
        var catalog = mechanics.Catalog;
        // Swagger has an accuracy roll. Search a finite, deterministic seed range,
        // require an actual emitted success, and fail rather than silently skip it.
        for (uint seed = 1; seed <= 32; seed++)
        {
            var bodies = Enumerable.Range(0, 4).Select(_ => KernelMon(mechanics, "MAGIKARP", 5, "SPLASH")).ToArray();
            bodies[actor] = id switch
            {
                339 => KernelMon(mechanics, "COMBUSKEN", 28, "BULK_UP"),
                321 => KernelMon(mechanics, "AIPOM", 25, "TICKLE"),
                207 => KernelMon(mechanics, "POOCHYENA", 25, "SWAGGER"),
                _ => throw new ArgumentOutOfRangeException(nameof(id))
            };
            bodies[target] = bodies[target] with { HeldItem = item };
            var batches = KernelTurn(catalog, KernelFrame(catalog, bodies, seed), actor, target);
            var batch = batches.Single(b => b.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == Move(id)));
            if (batch.OfType<SourceStatChangedEvent>().Any(e => e.Actor.Value == actor && e.Target.Value == target && e.Stat == WildBattleStat.Attack))
                return batch;
        }
        Assert.Fail($"No real successful {Move(id).Value} result in the bounded public-kernel seed range.");
        return [];
    }

    [TestMethod]
    [DataRow(339, 55)] [DataRow(321, 57)] [DataRow(207, 39)]
    public void PublicKernelNaturallyLearnedMovesDispatchAtAllFourPositionsAndSelectedAllies(int id, int argument)
    {
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in id is 322 or 339 ? new[] { actor } : new[] { actor ^ 1, actor ^ 2 })
        {
            var events = NaturalKernelGroup(id, actor, target);
            var used = events.OfType<SourceMoveUsedEvent>().Single(e => e.Actor.Value == actor && e.Move == Move(id));
            var first = events.OfType<SourceStatChangedEvent>().First(e => e.Actor.Value == actor && e.Target.Value == target);
            int usedIndex = events.IndexOf(used), resultIndex = events.IndexOf(first);
            var battle = Double(events, usedIndex);
            Assert.AreEqual<long?>(first.Sequence, CampaignStatAnimationPresentation.GroupedMoveResultSequence(battle));
            var root = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On);
            Assert.IsNotNull(root.Dispatch, root.Reason); Assert.AreEqual(first.Sequence, root.Dispatch.ResultSequence);
            var dispatches = Enumerable.Range(0, events.Length).Select(i => AnimationDispatchFixture.StatAtResult(battle with { EventIndex = i }))
                .Where(d => d is not null).ToArray();
            Assert.AreEqual(1, dispatches.Length); var dispatch = dispatches[0]!;
            Assert.AreEqual(first.Sequence, dispatch.Sequence); Assert.AreEqual(argument, dispatch.AnimationArgument);
            Assert.AreEqual(target, dispatch.Affected.Value); Assert.AreEqual(id, dispatch.SourceMoveId);
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(battle with { EventIndex = resultIndex }, true));
            Assert.AreEqual(events, battle.Events);
        }
    }

    [TestMethod]
    [DataRow(321, "ITEM_WHITE_HERB", 57)] [DataRow(207, "ITEM_LUM_BERRY", 39)]
    public void PublicKernelMoveEndHeldItemSuffixKeepsIndependentMoveAndItemAnimations(int id, string item, int argument)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int target = actor ^ 1;
            var events = NaturalKernelGroup(id, actor, target, item);
            var used = events.OfType<SourceMoveUsedEvent>().Single(e => e.Actor.Value == actor && e.Move == Move(id));
            var first = events.OfType<SourceStatChangedEvent>().First(e => e.Actor.Value == actor && e.Target.Value == target);
            var consumed = events.OfType<SourceHeldItemConsumedEvent>().Single(e => e.Side.Value == target && e.Item == item);
            Assert.IsTrue(first.Sequence < consumed.Sequence);
            var battle = Double(events, events.IndexOf(used));
            Assert.AreEqual<long?>(first.Sequence, CampaignStatAnimationPresentation.GroupedMoveResultSequence(battle));
            var root = AnimationDispatchFixture.AtAttackText(battle, SourceBattleScene.On);
            Assert.IsNotNull(root.Dispatch, root.Reason); Assert.AreEqual(first.Sequence, root.Dispatch.ResultSequence);
            var stats = Enumerable.Range(0, events.Length).Select(i => AnimationDispatchFixture.StatAtResult(battle with { EventIndex = i }))
                .Where(d => d is not null).ToArray();
            Assert.AreEqual(1, stats.Length); Assert.AreEqual(first.Sequence, stats[0]!.Sequence);
            Assert.AreEqual(argument, stats[0]!.AnimationArgument);
            var held = Enumerable.Range(0, events.Length).Select(i => CampaignHeldItemAnimationPresentation.AtResult(
                battle with { EventIndex = i }, SourceBattleScene.On, new MoveId?[4])).Where(d => d is not null).ToArray();
            Assert.AreEqual(1, held.Length); Assert.AreEqual(consumed.Sequence, held[0]!.ReceiptSequence);
            Assert.AreEqual(target, held[0]!.Affected.Value); Assert.IsTrue(held[0]!.Sequence > first.Sequence);
            if (id == 321)
            {
                Assert.IsNotNull(consumed.StatChanges); Assert.AreEqual(2, consumed.StatChanges.Value.Length);
                Assert.AreEqual(2, events.OfType<SourceStatChangedEvent>().Count(e => e.Actor.Value == target && e.Target.Value == target));
            }
            else
            {
                var confusion = events.OfType<SourceConfusionChangedEvent>().Where(e => e.Side.Value == target).ToArray();
                Assert.AreEqual(2, confusion.Length); Assert.AreEqual("move", confusion[0].Reason);
                Assert.AreEqual("held-berry", confusion[1].Reason); Assert.AreEqual(0, confusion[1].After);
            }
        }
    }
}
