using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
using Caller = FireRed.Tests.Application.WildBattles.CallerPreludeAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class SmokescreenAnimationKernelTests
{
    private static WildBattleCombatant[] Bodies(int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100)).ToArray();
        bodies[actor] = Kernel.Mon("CHARMANDER", "SMOKESCREEN", 19) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }

    private static void Check(SourceKernelResult result, bool single, bool success)
    {
        var events = result.Events; var stat = events.OfType<SourceStatChangedEvent>().SingleOrDefault();
        Assert.AreEqual(success, stat is not null);
        Shared.Check(result, 108, single, stat);
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        for (int i = 0; i < events.Length; i++)
        {
            var wave = Shared.Wave(events, i, single);
            if (stat is null || events[i] != stat) { Assert.IsNull(wave); continue; }
            Assert.IsNotNull(wave); Assert.AreEqual(108, wave.SourceMoveId);
            Assert.AreEqual(WildBattleStat.Accuracy, stat.Stat); Assert.AreEqual(stat.Before - 1, stat.After);
            Assert.AreEqual(27, wave.AnimationArgument); Assert.AreEqual(stat.Target, wave.Affected);
            Assert.AreEqual(stat.Sequence, wave.Sequence); Assert.AreEqual(pp.Sequence, wave.PpSequence);
        }
    }

    [TestMethod]
    public void NaturalCharmanderOwnsAccuracyDownOneRootAndWaveAcrossEverySinglesAndDoublesSlot()
    {
        var definition = Kernel.Mechanics.Catalog.Moves[Kernel.Id("SMOKESCREEN")];
        Assert.AreEqual(108, definition.SourceMoveId); Assert.AreEqual("EFFECT_ACCURACY_DOWN", definition.Effect);
        Assert.AreEqual(0, definition.Power); Assert.AreEqual("MOVE_TARGET_SELECTED", definition.Target);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Envelope.Resolve(Kernel.Frame(Bodies(actor), single), actor, target);
            Assert.AreEqual(5, result.State.Slots[target].Body!.AccuracyStage); Check(result, single, true);
        }
    }

    [TestMethod, DataRow("minimum"), DataRow("substitute"), DataRow("protect"), DataRow("miss"),
        DataRow("clear-body"), DataRow("white-smoke"), DataRow("keen-eye"), DataRow("mist")]
    public void ActualPreventionSpendsPpWithoutBorrowingRootOrAccuracyWave(string branch)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(actor);
            bodies[target] = branch switch {
                "minimum" => bodies[target] with { AccuracyStage = 0 },
                "substitute" => bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } },
                "protect" => bodies[target] with { Protected = true },
                "clear-body" => Kernel.Mon("TENTACOOL", "ACID"),
                "white-smoke" => Kernel.Mon("TORKOAL", "EMBER"),
                "keen-eye" => Kernel.Mon("PIDGEY", "GUST"), _ => bodies[target]
            };
            string? ability = branch switch { "clear-body" => "ABILITY_CLEAR_BODY", "white-smoke" => "ABILITY_WHITE_SMOKE", "keen-eye" => "ABILITY_KEEN_EYE", _ => null };
            if (ability is not null) Assert.AreEqual(ability, WildBattleEffective.Ability(bodies[target]));
            if (branch == "miss") bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            var frame = Kernel.Frame(bodies, single);
            if (branch == "mist") frame = frame with { Sides = frame.Sides.SetItem(target & 1, frame.Sides[target & 1] with { MistTurnsRemaining = 5 }) };
            var result = branch == "miss" ? Envelope.Find(frame, actor, target,
                r => r.Events.OfType<SourceMoveMissedEvent>().Any() || r.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "accuracy"), "Smokescreen miss")
                : Envelope.Resolve(frame, actor, target);
            Assert.AreEqual(branch == "minimum" ? 0 : 6, result.State.Slots[target].Body!.AccuracyStage);
            if (branch == "minimum") Assert.AreEqual(WildBattleStat.Accuracy, result.Events.OfType<SourceStatAtMinimumEvent>().Single().Stat);
            if (ability is not null) Assert.AreEqual(ability, result.Events.OfType<SourceAbilityPreventedEvent>().Single().Ability);
            Check(result, single, false);
        }
    }

    [TestMethod]
    public void MissingDuplicateForeignWrongStatAndUnprovenCallerReceiptsCannotAuthorizeSmokescreen()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Kernel.Frame(Bodies(), single)).Events;
            var stat = events.OfType<SourceStatChangedEvent>().Single(); int at = events.IndexOf(stat);
            Shared.Deny(events.RemoveAt(at), single); Shared.Deny(Shared.Number(events.Insert(at + 1, stat)), single);
            foreach (var wrong in new[] { stat with { Actor = new(1) }, stat with { Target = new(0) },
                stat with { Stat = WildBattleStat.Evasion }, stat with { After = 4 }, stat with { Turn = stat.Turn + 1 } })
                Shared.Deny(events.SetItem(at, wrong), single);
            SupportStatusAnimationKernelFixture.RejectCommonEnvelopeMutations(events, single);
            int cursor = events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(events, cursor), true, SourceBattleScene.On, Kernel.Mechanics.Catalog.Moves, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, cursor), SourceBattleScene.On, Kernel.Mechanics.Catalog.Moves, (_, _) => false).Dispatch);
        }
    }

    [TestMethod]
    public void ActualMetronomeSmokescreenOwnsItsCommittedAccuracyResult()
    {
        uint seed = Caller.MetronomeSeed(108);
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(); bodies[0] = Kernel.Mon("TOGETIC", "METRONOME", 29) with { AccuracyStage = 12 };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single) with { Rng = new(seed) });
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Caller == Kernel.Id("METRONOME") && e.Result == Kernel.Id("SMOKESCREEN")));
            Assert.AreEqual(WildBattleStat.Accuracy, result.Events.OfType<SourceStatChangedEvent>().Single().Stat);
            CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), 108, result.Events.OfType<SourceStatChangedEvent>().Single());
        }
    }
}
