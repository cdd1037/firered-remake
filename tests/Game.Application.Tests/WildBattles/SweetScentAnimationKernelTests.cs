using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using F = FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Numbering = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class SweetScentAnimationKernelTests
{
    private static MoveId Move => Kernel.Id("SWEET_SCENT");
    private static SourceBattleFrame Initial(bool single, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon("BULBASAUR", "SWEET_SCENT") with { SpeedStage = 12, AccuracyStage = 12 };
        return Kernel.Frame(bodies, single);
    }
    private static void Check(SourceFourActionResult result, bool single)
    {
        string before = JsonSerializer.Serialize(result); var rows = result.Events; var used = rows.OfType<SourceMoveUsedEvent>().Single();
        var attack = rows.OfType<SourceAttackStringEvent>().Single(); var pp = rows.OfType<SourcePpCommandEvent>().ToArray();
        var stats = rows.OfType<SourceStatChangedEvent>().Where(e => e.Actor == used.Actor && e.After < e.Before).ToArray();
        Assert.IsTrue(AnimationDispatchFixture.Supports(230, Move.Value)); Assert.AreEqual(single ? 1 : 2, pp.Length);
        Assert.AreEqual(used.Target, pp[0].Target); Assert.AreEqual(used.Actor, pp[0].PpOwner); Assert.AreEqual(used.PpBefore, pp[0].Before);
        Assert.AreEqual(Math.Max(0, pp[0].Before - pp[0].CommandCost), pp[0].After);
        if (!single) { Assert.AreEqual(new BattlerSlot(used.Target.Value ^ 2), pp[1].Target); Assert.AreEqual(pp[0].After, pp[1].Before); Assert.AreEqual(pp[1].Before, pp[1].After); }
        int roots = 0, waves = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            var decision = F.Project(rows, i, single, false); Assert.AreEqual(decision, F.Project(rows, i, single, false));
            Assert.IsNull(F.Project(rows, i, single, false, SourceBattleScene.Off).Dispatch); Assert.IsNull(F.Project(rows, i, single, true).Dispatch);
            if (stats.Length > 0 && rows[i] == used)
            {
                var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++; var owner = stats[0];
                Assert.AreEqual(230, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(owner.Target, cue.Target);
                Assert.AreEqual(owner.Sequence, cue.ResultSequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
                Assert.AreEqual(pp.Single(p => p.Target == owner.Target).Sequence, cue.PpSequence); Assert.IsNull(cue.MoveDamage);
            }
            else Assert.IsNull(decision.Dispatch);
            var wave = F.Wave(rows, i, single);
            if (rows[i] is SourceStatChangedEvent stat && stats.Contains(stat))
            {
                Assert.IsNotNull(wave); waves++; Assert.AreEqual(28, wave.AnimationArgument); Assert.AreEqual(stat.Target, wave.Affected);
                Assert.AreEqual(WildBattleStat.Evasion, stat.Stat); Assert.AreEqual(stat.Before - 1, stat.After);
            }
            else Assert.IsNull(wave);
        }
        Assert.AreEqual(stats.Length > 0 ? 1 : 0, roots); Assert.AreEqual(stats.Length, waves); Assert.AreEqual(before, JsonSerializer.Serialize(result));
    }

    [TestMethod, DataRow(6), DataRow(1), DataRow(0)]
    public void NaturalBothTargetEvasionDropOwnsOneRootAndOneWavePerSuccessfulPass(int stage)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int selected in single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 3 })
        {
            var frame = Initial(single, actor); foreach (int target in single ? new[] { selected } : new[] { selected, selected ^ 2 })
                frame = F.Body(frame, target, b => b with { EvasionStage = stage });
            var result = F.Ordered(frame, actor, selected); Check(result, single);
            Assert.AreEqual(stage == 0 ? (single ? 1 : 2) : 0, result.Events.OfType<SourceStatAtMinimumEvent>().Count());
        }
    }

    [TestMethod, DataRow("cap"), DataRow("substitute"), DataRow("protect"), DataRow("clear-body"), DataRow("white-smoke")]
    public void FirstOrSecondPreventedPassCannotEraseItsPartnersActualDrop(string branch)
    {
        for (int actor = 0; actor < 4; actor++) foreach (bool first in new[] { false, true })
        {
            int target = actor ^ 1, blocked = first ? target : target ^ 2; var frame = Initial(false, actor);
            frame = F.Body(frame, blocked, b => branch switch {
                "cap" => b with { EvasionStage = 0 }, "substitute" => b with { CalledVolatile = new() { SubstituteHp = 1 } },
                "protect" => b with { Protected = true }, "clear-body" => Kernel.Mon("TENTACOOL", "ACID") with { SpeedStage = 0 },
                _ => Kernel.Mon("TORKOAL", "EMBER") with { SpeedStage = 0 } });
            var result = F.Ordered(frame, actor, target); Check(result, false);
            Assert.AreEqual(new BattlerSlot(blocked ^ 2), result.Events.OfType<SourceStatChangedEvent>().Single().Target);
            Assert.IsTrue(result.Events.Any(e => e is SourceStatAtMinimumEvent or SourceMoveFailedEvent or SourceMoveMissedEvent or SourceAbilityPreventedEvent));
        }
    }

    [TestMethod, DataRow("mist"), DataRow("miss"), DataRow("substitute")]
    public void BothPreventedPassesNeverAnimateFromPpAlone(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single);
            if (branch == "mist") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { MistTurnsRemaining = 5 }) };
            if (branch == "miss") frame = F.Body(frame, 0, b => b with { AccuracyStage = 0 });
            foreach (int target in single ? new[] { 1 } : new[] { 1, 3 }) frame = F.Body(frame, target, b => branch == "substitute"
                ? b with { CalledVolatile = new() { SubstituteHp = 1 } } : b with { EvasionStage = 12 });
            var result = branch == "miss" ? F.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Count() == (single ? 1 : 2), "Sweet Scent all-pass miss") : F.Ordered(frame, 0, 1);
            Check(result, single); Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any());
        }
    }

    [TestMethod]
    public void RealWhiteHerbRestorationOwnsOnlyItsItemGroupAndDoesNotChangeEitherMovePass()
    {
        foreach (bool single in new[] { false, true }) foreach (int holder in single ? new[] { 1 } : new[] { 1, 3 })
        {
            // Neutral Speed keeps this item group specific to the Evasion drop.
            var frame = F.Body(Initial(single), holder, b => b with { HeldItem = "ITEM_WHITE_HERB", SpeedStage = 6 });
            var result = F.Ordered(frame, 0, 1); Check(result, single); var rows = result.Events;
            var item = rows.OfType<SourceHeldItemConsumedEvent>().Single(); var restored = rows.OfType<SourceStatChangedEvent>().Single(e => e.Actor.Value == holder && e.Target.Value == holder);
            Assert.AreEqual("ITEM_WHITE_HERB", item.Item); Assert.AreEqual(WildBattleStat.Evasion, restored.Stat); Assert.AreEqual(5, restored.Before); Assert.AreEqual(6, restored.After);
            Assert.AreEqual(6, result.State.Slots[holder].Body!.EvasionStage); Assert.IsNotNull(item.StatChanges);
            int index = rows.IndexOf(restored); var held = single ? CampaignHeldItemAnimationPresentation.AtResult(F.Single(rows, index), true, SourceBattleScene.On, new MoveId?[2])
                : CampaignHeldItemAnimationPresentation.AtResult(Envelope.Double(rows, index), SourceBattleScene.On, new MoveId?[4]);
            Assert.IsNotNull(held); Assert.AreEqual(item.Sequence, held.ReceiptSequence);
            foreach (var invalid in new[] { rows.Remove(item), rows.SetItem(rows.IndexOf(item), item with { StatChanges = null }) })
                Assert.IsNull(F.Wave(invalid, index, single), "An unowned restoration cannot masquerade as Sweet Scent's Evasion wave.");
        }
    }

    [TestMethod]
    public void RealMetronomeLeafIsNotReplayedAsADirectSweetScentRoot()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(single), 0, _ => Kernel.Mon("CLEFAIRY", "METRONOME") with { SpeedStage = 12, AccuracyStage = 12 });
            frame = frame with { Rng = new(CallerPreludeAnimationKernelFixture.MetronomeSeed(230)) };
            var result = Envelope.Resolve(frame); Assert.AreEqual(Move, result.Events.OfType<SourceCallerSelectionEvent>().Single().Result);
            Assert.AreEqual(single ? 1 : 2, result.Events.OfType<SourceStatChangedEvent>().Count());
            CallerPreludeAnimationKernelFixture.DenyLeaf(result.Events, single, 230);
        }
    }

    [TestMethod]
    public void SweetScentRequiresCurrentSourceDefinitionAndRealCatalogSupport()
    {
        foreach (bool single in new[] { false, true })
        {
            var rows = F.Ordered(Initial(single), 0, 1).Events; int index = rows.IndexOf(rows.OfType<SourceMoveUsedEvent>().Single());
            var definitions = Kernel.Mechanics.Catalog.Moves;
            foreach (var metadata in new[] { null, definitions.Remove(Move), definitions.SetItem(Move, definitions[Move] with { SourceMoveId = 1 }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(F.Single(rows, index), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(rows, index), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(F.Single(rows, index), true, SourceBattleScene.On, definitions, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(rows, index), SourceBattleScene.On, definitions, (_, _) => false).Dispatch);
        }
    }

    [TestMethod]
    public void ForeignPpAndMalformedEvasionResultsCannotOwnThePublishedRoot()
    {
        foreach (bool single in new[] { false, true })
        {
            var rows = F.Ordered(Initial(single), 0, 1).Events; var pp = rows.OfType<SourcePpCommandEvent>().First();
            var stat = rows.OfType<SourceStatChangedEvent>().First(); int s = rows.IndexOf(stat);
            F.Deny(rows.SetItem(rows.IndexOf(pp), pp with { PpOwner = new(1) }), single);
            F.Deny(Numbering.Number(rows.Insert(rows.IndexOf(pp), pp)), single);
            F.Deny(Numbering.Number(rows.Insert(s + 1, stat)), single);
            F.Deny(rows.SetItem(s, stat with { Stat = WildBattleStat.Accuracy }), single);
            F.Deny(rows.SetItem(s, stat with { Target = new(0) }), single);
            var attack = rows.OfType<SourceAttackStringEvent>().Single(); F.Deny(rows.SetItem(rows.IndexOf(attack), attack with { ChosenMove = Kernel.Id("METRONOME") }), single);
        }
    }
}
