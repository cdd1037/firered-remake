using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class SpreadSecondaryPrefixTests
{
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static ImmutableDictionary<MoveId, PokemonMoveDefinition> Moves => Mechanics.Catalog.Moves;
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("spread-secondary", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("spread-secondary", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };
    private static WildBattleStat Stat(PokemonMoveDefinition move) => move.Effect switch {
        "EFFECT_DEFENSE_DOWN_HIT" => WildBattleStat.Defense,
        "EFFECT_SPEED_DOWN_HIT" => WildBattleStat.Speed,
        "EFFECT_ACCURACY_DOWN_HIT" => WildBattleStat.Accuracy,
        _ => throw new ArgumentOutOfRangeException(nameof(move))
    };
    private static int Argument(WildBattleStat stat) => stat switch {
        WildBattleStat.Defense => 23, WildBattleStat.Speed => 24, WildBattleStat.Accuracy => 27,
        _ => throw new ArgumentOutOfRangeException(nameof(stat))
    };
    private static ImmutableArray<SourceBattleEvent> Two(PokemonMoveDefinition move, int actor = 0, int target = 1) => [
        new SourceAttackStringEvent(1, 1, new(actor), move.Id, move.Id),
        new SourceMoveUsedEvent(2, 1, new(actor), new(target), 0, move.Id, 10, 9),
        new SourcePpCommandEvent(3, 1, new(actor), new(target), 0, move.Id, move.Id, 10, 9, 1, 0),
        new SourceHpChangedEvent(4, 1, new(actor), new(target), move.Id, 100, 90, 10, false, 100),
        new SourceStatChangedEvent(5, 1, new(actor), new(target), Stat(move), 6, 5),
        new SourcePpCommandEvent(6, 1, new(actor), new(target ^ 2), 0, move.Id, move.Id, 9, 9, 1, 0),
        new SourceHpChangedEvent(7, 1, new(actor), new(target ^ 2), move.Id, 100, 90, 10, false, 100),
        new SourceStatChangedEvent(8, 1, new(actor), new(target ^ 2), Stat(move), 6, 5)
    ];
    private static void AssertWaves(ImmutableArray<SourceBattleEvent> events, long[] sequences, bool singles = false,
        IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? moves = null)
    {
        moves ??= Moves;string before = JsonSerializer.Serialize(events);int count = 0;
        for (int index = 0; index < events.Length; index++)
        {
            var battle = Double(events, index);
            var dispatch = CampaignStatAnimationPresentation.AtResult(battle, moves: moves);
            if (events[index] is SourceStatChangedEvent && sequences.Contains(events[index].Sequence))
            {
                var stat = (SourceStatChangedEvent)events[index];
                var pp = events.Take(index).OfType<SourcePpCommandEvent>().Last();
                var attack = events.Take(index).OfType<SourceAttackStringEvent>().Last();
                Assert.IsNotNull(dispatch, $"Expected own-pass stat wave at {stat.Sequence}.");count++;
                Assert.AreEqual(stat.Sequence, dispatch.Sequence);Assert.AreEqual(pp.Sequence, dispatch.PpSequence);
                Assert.AreEqual(attack.Sequence, dispatch.AttackSequence);Assert.AreEqual(stat.Target, dispatch.Affected);
                Assert.AreEqual(moves[pp.CurrentMove].SourceMoveId, dispatch.SourceMoveId);
                Assert.AreEqual(Argument(stat.Stat), dispatch.AnimationArgument);
                Assert.AreEqual(dispatch, CampaignStatAnimationPresentation.AtResult(battle, moves: moves));
            }
            else Assert.IsNull(dispatch, $"Receipt {events[index].Sequence} cannot borrow a different pass's result.");
            Assert.IsNull(CampaignStatAnimationPresentation.AtResult(battle, true, moves));
            if (singles)
            {
                var single = Single(events, index);
                Assert.AreEqual(dispatch, CampaignStatAnimationPresentation.AtResult(single, true, moves: moves));
                Assert.IsNull(CampaignStatAnimationPresentation.AtResult(single, false, moves: moves));
                Assert.IsNull(CampaignStatAnimationPresentation.AtResult(single, true, true, moves));
            }
        }
        Assert.AreEqual(sequences.Length, count);Assert.AreEqual(before, JsonSerializer.Serialize(events));
    }

    [TestMethod]
    [DataRow("ACID", 51, WildBattleStat.Defense)]
    [DataRow("BUBBLE", 145, WildBattleStat.Speed)]
    [DataRow("ICY_WIND", 196, WildBattleStat.Speed)]
    [DataRow("MUDDY_WATER", 330, WildBattleStat.Accuracy)]
    public void BothMetadataOwnsEachOpposingPassAndSinglesUseOnlyTheirOnePass(string name, int sourceMove, WildBattleStat stat)
    {
        var move = Moves[Id(name)];Assert.AreEqual(sourceMove, move.SourceMoveId);Assert.AreEqual("MOVE_TARGET_BOTH", move.Target);Assert.AreEqual(stat, Stat(move));
        for (int actor = 0; actor < 4; actor++)
        foreach (int target in new[] { actor ^ 1, actor ^ 3 })
            AssertWaves(Two(move, actor, target), [5, 8]);
        for (int actor = 0; actor < 2; actor++)AssertWaves(Two(move, actor, actor ^ 1).Take(5).ToImmutableArray(), [5], singles: true);
        // A structurally valid source presentation fixture proves target/effect
        // metadata chooses the path; admission is not a numeric four-move switch.
        var structural = move with { Id = Id("TEST_SPREAD_SECONDARY"), SourceMoveId = 33 };
        Assert.IsTrue(SourceMoveDefinitionRules.IsValid(structural));
        AssertWaves(Two(structural), [5, 8], moves: ImmutableDictionary<MoveId, PokemonMoveDefinition>.Empty.Add(structural.Id, structural));
    }

    [TestMethod]
    public void LaterMalformedPassCannotRevokeFirstWaveOrBorrowItsDamage()
    {
        var good = Two(Moves[Id("ICY_WIND")]);var pp = (SourcePpCommandEvent)good[5];
        foreach (var malformed in new[] {
            pp with { Actor = new(2) }, pp with { PpOwner = new(2) }, pp with { Target = new(1) }, pp with { Target = new(2) },
            pp with { CurrentMove = Id("BUBBLE") }, pp with { ChosenMove = Id("BUBBLE") }, pp with { PpSlot = 1 },
            pp with { Before = 10, After = 10 }, pp with { After = 8 }, pp with { CommandCost = 0 },
            pp with { Turn = 2 }, pp with { Sequence = 5 }
        }) AssertWaves(good.SetItem(5, malformed), [5]);
        AssertWaves(good.RemoveAt(5), [5]);AssertWaves(good.RemoveAt(6), [5]);
        var hit = (SourceHpChangedEvent)good[6];var stat = (SourceStatChangedEvent)good[7];
        AssertWaves(good.SetItem(6, hit with { Target = new(1) }), [5]);
        AssertWaves(good.SetItem(7, stat with { Target = new(1) }), [5]);
        AssertWaves(good.SetItem(6, hit with { Actor = new(2) }), [5]);
        AssertWaves(good.SetItem(7, stat with { Actor = new(2) }), [5]);
    }

    [TestMethod]
    public void FirstPassAuthenticationIsRequiredBeforeAnySuppressedRevisit()
    {
        var good = Two(Moves[Id("BUBBLE")]);var pp = (SourcePpCommandEvent)good[2];
        foreach (var malformed in new[] {
            pp with { Actor = new(2) }, pp with { PpOwner = new(2) }, pp with { Target = new(3) },
            pp with { ChosenMove = Id("ICY_WIND") }, pp with { PpSlot = 1 }, pp with { Before = 0, After = 0 },
            pp with { Before = 9, After = 9 }, pp with { CommandCost = 0 }, pp with { After = 8 }
        }) AssertWaves(good.SetItem(2, malformed), []);
        AssertWaves(good.RemoveAt(2), []);
        var attack = (SourceAttackStringEvent)good[0];var used = (SourceMoveUsedEvent)good[1];
        AssertWaves(good.SetItem(0, attack with { ChosenMove = Id("ICY_WIND") }), []);
        AssertWaves(good.SetItem(1, used with { Target = new(3) }), []);
        AssertWaves(Two(Moves[Id("BUBBLE")], 0, 2), []);
    }

    [TestMethod]
    public void PositivePressureCostAndFinalClampedPpDoNotInventSecondSpending()
    {
        var good = Two(Moves[Id("ICY_WIND")]);var used = (SourceMoveUsedEvent)good[1];
        var first = (SourcePpCommandEvent)good[2];var second = (SourcePpCommandEvent)good[5];
        foreach (int before in new[] { 1, 2, 10 })
        foreach (int cost in new[] { 2, 3 })
        {
            int after = Math.Max(0, before - cost);
            AssertWaves(good.SetItem(1, used with { PpBefore = before, PpAfter = after })
                .SetItem(2, first with { Before = before, After = after, CommandCost = cost })
                .SetItem(5, second with { Before = after, After = after, CommandCost = cost }), [5, 8]);
        }
    }

    [TestMethod]
    public void SourcePpWidthsAndPressureCostBoundsRejectClampedForgeries()
    {
        var good = Two(Moves[Id("ICY_WIND")]);var used = (SourceMoveUsedEvent)good[1];
        var first = (SourcePpCommandEvent)good[2];var second = (SourcePpCommandEvent)good[5];
        foreach (int cost in new[] { 4, 255, int.MaxValue })
        {
            var clamped = good.SetItem(1, used with { PpBefore = 1, PpAfter = 0 })
                .SetItem(2, first with { Before = 1, After = 0, CommandCost = cost })
                .SetItem(5, second with { Before = 0, After = 0, CommandCost = cost });
            AssertWaves(clamped, []);
            AssertWaves(clamped.SetItem(2, first with { Before = 1, After = 0, CommandCost = 1 }), [5]);
        }
        AssertWaves(good.SetItem(1, used with { PpBefore = 256, PpAfter = 255 })
            .SetItem(2, first with { Before = 256, After = 255 })
            .SetItem(5, second with { Before = 255, After = 255 }), []);
        var selected = Two(Moves[Id("CRUSH_CLAW")]).Take(5).ToImmutableArray();
        AssertWaves(selected.SetItem(1, ((SourceMoveUsedEvent)selected[1]) with { PpBefore = 1, PpAfter = 0 })
            .SetItem(2, ((SourcePpCommandEvent)selected[2]) with { Before = 1, After = 0, CommandCost = 3 }), []);
    }

    [TestMethod]
    public void ThirdPassRepeatedTargetAndSelectedTargetRevisitsDoNotReplay()
    {
        var move = Moves[Id("ACID")];var good = Two(move);
        var third = good.Add(new SourcePpCommandEvent(9, 1, new(0), new(1), 0, move.Id, move.Id, 9, 9, 1, 0))
            .Add(new SourceHpChangedEvent(10, 1, new(0), new(1), move.Id, 90, 80, 10, false, 100))
            .Add(new SourceStatChangedEvent(11, 1, new(0), new(1), WildBattleStat.Defense, 5, 4));
        AssertWaves(third, [5, 8]);
        AssertWaves(Two(Moves[Id("CRUSH_CLAW")]), [5]);
        var singleEvents = good.SetItem(5, ((SourcePpCommandEvent)good[5]) with { Target = new(1) })
            .SetItem(6, ((SourceHpChangedEvent)good[6]) with { Target = new(1) })
            .SetItem(7, ((SourceStatChangedEvent)good[7]) with { Target = new(1) });
        AssertWaves(singleEvents, [5], singles: true);
    }

    [TestMethod]
    public void EachPassCriticalAndEffectivenessPrefixIsIndependentlyOrdered()
    {
        var move = Moves[Id("ACID")];var good = Two(move);var stat = (SourceStatChangedEvent)good[7];
        var critical = good.Take(7).ToImmutableArray().SetItem(6, ((SourceHpChangedEvent)good[6]) with { Critical = true })
            .Add(new SourceCriticalHitEvent(8, 1, new(0), new(3)))
            .Add(new SourceEffectivenessEvent(9, 1, new(0), new(3), move.Id, WildBattleEffectiveness.SuperEffective))
            .Add(stat with { Sequence = 10 });
        AssertWaves(critical, [5, 10]);AssertWaves(critical.RemoveAt(7), [5]);
        AssertWaves(critical.SetItem(8, ((SourceEffectivenessEvent)critical[8]) with { Target = new(1) }), [5]);
        AssertWaves(critical.SetItem(7, new SourceCriticalHitEvent(8, 1, new(0), new(1))), [5]);
    }

    [TestMethod]
    public void RageItemAndDuplicateSuffixesCannotLendAStatToEitherPass()
    {
        var move = Moves[Id("ACID")];var good = Two(move);
        var events = good.Take(5).ToImmutableArray()
            .Add(new SourceStatChangedEvent(6, 1, new(1), new(1), WildBattleStat.Attack, 6, 7))
            .Add(new SourceStatChangedEvent(7, 1, new(1), new(1), WildBattleStat.Defense, 5, 6))
            .Add(new SourceHeldItemConsumedEvent(8, 1, new(1), "ITEM_WHITE_HERB", 90, 90, 0) { StatChanges = [new(WildBattleStat.Defense, 5, 6)] })
            .AddRange(good.Skip(5).Select(e => e with { Sequence = e.Sequence + 3 }))
            .Add(new SourceStatChangedEvent(12, 1, new(3), new(3), WildBattleStat.Defense, 5, 6))
            .Add(new SourceHeldItemConsumedEvent(13, 1, new(3), "ITEM_WHITE_HERB", 90, 90, 0) { StatChanges = [new(WildBattleStat.Defense, 5, 6)] })
            .Add(new SourceStatChangedEvent(14, 1, new(0), new(3), WildBattleStat.Defense, 5, 4));
        AssertWaves(events, [5, 11]);
        AssertWaves(events.SetItem(7, ((SourceHeldItemConsumedEvent)events[7]) with { StatChanges = null }), [5, 11]);
        // A new PP never makes a stat sufficient: its own damage prefix is still mandatory.
        AssertWaves(events.RemoveAt(9), [5]);
    }

    [TestMethod]
    public void TurnSwitchAndInvocationBoundariesPreventRevisitBorrowing()
    {
        var good = Two(Moves[Id("ICY_WIND")]);
        SourceBattleEvent[] boundaries = [new SourceTurnReadyEvent(6, 1, 0),
            new SourceCalledPartySwitchedEvent(6, 1, new(3), 0, 1, WildBattleCalledSwitchKind.Forced),
            new SourceAttackStringEvent(6, 1, new(0), Id("BUBBLE"), Id("BUBBLE")),
            new SourceMoveUsedEvent(6, 1, new(0), new(3), 0, Id("BUBBLE"), 10, 9)];
        foreach (var boundary in boundaries)
            AssertWaves(good.Take(5).ToImmutableArray().Add(boundary).AddRange(good.Skip(5).Select(e => e with { Sequence = e.Sequence + 1 })), [5]);
        AssertWaves(good.SetItem(6, ((SourceHpChangedEvent)good[6]) with { Turn = 2 }), [5]);
        AssertWaves(good.SetItem(7, ((SourceStatChangedEvent)good[7]) with { Turn = 2 }), [5]);
    }
}
