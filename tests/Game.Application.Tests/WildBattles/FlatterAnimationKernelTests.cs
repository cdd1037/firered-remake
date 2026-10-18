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
public sealed class FlatterAnimationKernelTests
{
    private static MoveId Move => Kernel.Id("FLATTER");
    private static SourceBattleFrame Initial(bool single, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon("NIDORAN_F", "FLATTER") with { SpeedStage = 12, AccuracyStage = 12 };
        return Kernel.Frame(bodies, single);
    }
    private static void Check(ImmutableArray<SourceBattleEvent> rows, bool single)
    {
        string before = JsonSerializer.Serialize(rows); var used = rows.OfType<SourceMoveUsedEvent>().Single();
        var attack = rows.OfType<SourceAttackStringEvent>().Single(); var pp = rows.OfType<SourcePpCommandEvent>().Single();
        var result = rows[rows.IndexOf(pp) + 1]; var raised = result as SourceStatChangedEvent;
        Assert.IsTrue(AnimationDispatchFixture.Supports(260, Move.Value)); Assert.AreEqual("EFFECT_FLATTER", Kernel.Mechanics.Catalog.Moves[Move].Effect);
        Assert.AreEqual(Move, pp.ChosenMove); Assert.AreEqual(Move, pp.CurrentMove); Assert.AreEqual(used.Actor, pp.PpOwner);
        Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Slot, pp.PpSlot); Assert.AreEqual(used.PpBefore, pp.Before);
        Assert.AreEqual(used.PpAfter, pp.After); Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        if (raised is not null) { Assert.AreEqual(WildBattleStat.SpecialAttack, raised.Stat); Assert.AreEqual(raised.Before + 1, raised.After); }
        else Assert.IsTrue(result is SourceStatAtMaximumEvent { Stat: WildBattleStat.SpecialAttack });
        int roots = 0, waves = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            var decision = F.Project(rows, i, single, false); Assert.AreEqual(decision, F.Project(rows, i, single, false));
            Assert.IsNull(F.Project(rows, i, single, true).Dispatch); Assert.IsNull(F.Project(rows, i, single, false, SourceBattleScene.Off).Dispatch);
            if (rows[i] == used)
            {
                var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++;
                Assert.AreEqual(260, cue.SourceMoveId); Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
                Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(result.Sequence, cue.ResultSequence);
                Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target); Assert.IsNull(cue.MovePower); Assert.IsNull(cue.MoveDamage);
            }
            else Assert.IsNull(decision.Dispatch);
            var wave = F.Wave(rows, i, single);
            if (raised is not null && rows[i] == raised)
            { Assert.IsNotNull(wave); waves++; Assert.AreEqual(18, wave.AnimationArgument); Assert.AreEqual(used.Target, wave.Affected); }
            else Assert.IsNull(wave);
            bool silent = single ? CampaignStatAnimationPresentation.IsSilentResultReceipt(F.Single(rows, i), true, Kernel.Mechanics.Catalog.Moves)
                : CampaignStatAnimationPresentation.IsSilentResultReceipt(Envelope.Double(rows, i), Kernel.Mechanics.Catalog.Moves);
            Assert.AreEqual(rows[i] == result && raised is null, silent);
        }
        Assert.AreEqual(1, roots); Assert.AreEqual(raised is null ? 0 : 1, waves); Assert.AreEqual(before, JsonSerializer.Serialize(rows));
    }

    [TestMethod, DataRow(6), DataRow(11), DataRow(12)]
    public void NaturalSpecialAttackAndConfusionGroupOwnsAllPositionsAndSelectedAllies(int stage)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : Enumerable.Range(0, 4).Where(t => t != actor).ToArray())
        {
            var frame = F.Body(Initial(single, actor), target, b => b with { SpecialAttackStageDelta = stage - 6 });
            var result = F.Ordered(frame, actor, target); string before = JsonSerializer.Serialize(result); Check(result.Events, single);
            var confusion = result.Events.OfType<SourceConfusionChangedEvent>().Single(); Assert.AreEqual(new BattlerSlot(target), confusion.Side);
            Assert.AreEqual(0, confusion.Before); Assert.IsTrue(confusion.After is >= 2 and <= 5); Assert.AreEqual("move", confusion.Reason);
            Assert.AreEqual(before, JsonSerializer.Serialize(result));
        }
    }

    [TestMethod, DataRow("own-tempo", false), DataRow("own-tempo", true), DataRow("safeguard", false), DataRow("safeguard", true), DataRow("already-confused", false)]
    public void LaterConfusionPreventionPreservesTheAuthoredRootAndAnyActualSpecialAttackWave(string prevention, bool capped)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(single);
            if (prevention == "own-tempo") frame = F.Body(frame, 1, _ => Kernel.Mon("SPINDA", "TACKLE", 100) with { SpeedStage = 0 });
            if (prevention == "safeguard") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { SafeguardTurnsRemaining = 5 }) };
            frame = F.Body(frame, 1, b => b with { SpecialAttackStageDelta = capped ? 6 : 0, ConfusionTurnsRemaining = prevention == "already-confused" ? 3 : 0 });
            var result = F.Ordered(frame, 0, 1); Check(result.Events, single);
            Assert.IsFalse(result.Events.OfType<SourceConfusionChangedEvent>().Any(e => e.Reason == "move"));
            Assert.IsTrue(result.Events.Any(e => e is SourceMoveFailedEvent or SourceAbilityPreventedEvent));
        }
    }

    [TestMethod, DataRow("maxed-and-confused"), DataRow("substitute"), DataRow("protect"), DataRow("miss")]
    public void FailuresBeforeTheAuthoredAnimationHaveNoRootOrStatWave(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(single), 1, b => branch switch {
                "maxed-and-confused" => b with { SpecialAttackStageDelta = 6, ConfusionTurnsRemaining = 3 },
                "substitute" => b with { CalledVolatile = new() { SubstituteHp = 1 } }, "protect" => b with { Protected = true }, _ => b with { EvasionStage = 12 } });
            if (branch == "miss") frame = F.Body(frame, 0, b => b with { AccuracyStage = 0 });
            var result = branch == "miss" ? F.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "Flatter accuracy miss") : F.Ordered(frame, 0, 1);
            F.Deny(result.Events, single); for (int i = 0; i < result.Events.Length; i++) Assert.IsNull(F.Wave(result.Events, i, single));
            Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any());
        }
    }

    [TestMethod]
    public void ImmediatePersimCureKeepsTheEventTimeGroupInsteadOfInferringFromFinalConfusion()
    {
        foreach (bool single in new[] { false, true })
        {
            var result = F.Ordered(F.Body(Initial(single), 1, b => b with { HeldItem = "ITEM_PERSIM_BERRY" }), 0, 1); Check(result.Events, single);
            var rows = result.Events; var confusion = rows.OfType<SourceConfusionChangedEvent>().ToArray(); Assert.AreEqual(2, confusion.Length);
            Assert.AreEqual("move", confusion[0].Reason); Assert.AreEqual("persim-berry", confusion[1].Reason); Assert.AreEqual(confusion[0].After, confusion[1].Before);
            Assert.AreEqual(0, result.State.Slots[1].Body!.ConfusionTurnsRemaining); var item = rows.OfType<SourceHeldItemConsumedEvent>().Single();
            int at = rows.IndexOf(confusion[1]); var held = single ? CampaignHeldItemAnimationPresentation.AtResult(F.Single(rows, at), true, SourceBattleScene.On, new MoveId?[2])
                : CampaignHeldItemAnimationPresentation.AtResult(Envelope.Double(rows, at), SourceBattleScene.On, new MoveId?[4]);
            Assert.IsNotNull(held); Assert.AreEqual(item.Sequence, held.ReceiptSequence);
            F.Deny(rows.Remove(item), single);
        }
    }

    [TestMethod, DataRow("missing"), DataRow("duplicate"), DataRow("stat"), DataRow("delta"), DataRow("target"), DataRow("duration"), DataRow("reason"), DataRow("pp")]
    public void MalformedOrForeignGroupedResultsFailClosed(string mutation)
    {
        foreach (bool single in new[] { false, true })
        {
            var rows = F.Ordered(Initial(single), 0, 1).Events; var stat = rows.OfType<SourceStatChangedEvent>().Single();
            var confused = rows.OfType<SourceConfusionChangedEvent>().Single(); var pp = rows.OfType<SourcePpCommandEvent>().Single();
            int at = rows.IndexOf(stat), next = rows.IndexOf(confused);
            var invalid = mutation switch {
                "missing" => Numbering.Number(rows.Remove(confused)), "duplicate" => Numbering.Number(rows.Insert(at + 1, stat)),
                "stat" => rows.SetItem(at, stat with { Stat = WildBattleStat.Attack }), "delta" => rows.SetItem(at, stat with { After = stat.Before + 2 }),
                "target" => rows.SetItem(next, confused with { Side = new(0) }), "duration" => rows.SetItem(next, confused with { After = 6 }),
                "reason" => rows.SetItem(next, confused with { Reason = "persim-berry" }),
                _ => rows.SetItem(rows.IndexOf(pp), pp with { PpOwner = new(1) }) };
            F.Deny(invalid, single); for (int i = 0; i < invalid.Length; i++) Assert.IsNull(F.Wave(invalid, i, single));
        }
    }

    [TestMethod]
    public void ActualCalledFlatterDoesNotReplayTheDirectRootAndRequiresCurrentMetadata()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(single), 0, _ => Kernel.Mon("CLEFAIRY", "METRONOME") with { SpeedStage = 12, AccuracyStage = 12 });
            var result = Envelope.Resolve(frame with { Rng = new(CallerPreludeAnimationKernelFixture.MetronomeSeed(260)) });
            Assert.AreEqual(Move, result.Events.OfType<SourceCallerSelectionEvent>().Single().Result); Assert.AreEqual(1, result.Events.OfType<SourceStatChangedEvent>().Count());
            CallerPreludeAnimationKernelFixture.DenyLeaf(result.Events, single, 260);
            var rows = F.Ordered(Initial(single), 0, 1).Events; int at = rows.IndexOf(rows.OfType<SourceMoveUsedEvent>().Single());
            var definitions = Kernel.Mechanics.Catalog.Moves;
            foreach (var metadata in new[] { null, definitions.Remove(Move), definitions.SetItem(Move, definitions[Move] with { SourceMoveId = 1 }),
                definitions.SetItem(Move, definitions[Move] with { Power = 1 }), definitions.SetItem(Move, definitions[Move] with { Target = "MOVE_TARGET_USER" }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(F.Single(rows, at), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(rows, at), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(F.Single(rows, at), true, SourceBattleScene.On, definitions, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(rows, at), SourceBattleScene.On, definitions, (_, _) => false).Dispatch);
        }
    }
}
