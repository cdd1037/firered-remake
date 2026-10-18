using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using F = FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class DragonArmorAnimationKernelTests
{
    private static MoveId Move(int id) => Kernel.Id(id == 349 ? "DRAGON_DANCE" : id == 151 ? "ACID_ARMOR" : "SKETCH");
    private static SourceBattleFrame Initial(int id, bool single, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon(id == 349 ? "GYARADOS" : "GRIMER", id == 349 ? "DRAGON_DANCE" : "ACID_ARMOR") with { SpeedStage = 12 };
        return Kernel.Frame(bodies, single);
    }
    private static void Check(SourceFourActionResult result, int id, bool single, SourceBattleEvent? owner, int? argument)
    {
        Shared.Check(result, id, single, owner); Assert.IsTrue(AnimationDispatchFixture.Supports(id, Move(id).Value));
        var used = result.Events.OfType<SourceMoveUsedEvent>().Single(); int waves = 0;
        for (int i = 0; i < result.Events.Length; i++)
        {
            var root = F.Project(result.Events, i, single, false); Assert.AreEqual(root, Kernel.Project(result.Events, i, single));
            var wave = F.Wave(result.Events, i, single);
            if (argument is not null && result.Events[i] == owner)
            { Assert.IsNotNull(wave); Assert.AreEqual(argument, wave.AnimationArgument); Assert.AreEqual(used.Actor, wave.Affected); waves++; }
            else Assert.IsNull(wave, "Each move owns one generic wave; item restorations own their separate animation.");
            bool silent = single ? CampaignStatAnimationPresentation.IsSilentResultReceipt(F.Single(result.Events, i), true, Kernel.Mechanics.Catalog.Moves)
                : CampaignStatAnimationPresentation.IsSilentResultReceipt(Envelope.Double(result.Events, i), Kernel.Mechanics.Catalog.Moves);
            Assert.AreEqual(id == 349 && owner is not null && result.Events[i] is SourceStatAtMaximumEvent, silent);
        }
        Assert.AreEqual(argument is null ? 0 : 1, waves);
    }

    [TestMethod, DataRow(6, 6, 55), DataRow(11, 11, 55), DataRow(12, 6, 17), DataRow(6, 12, 15), DataRow(12, 12, 0)]
    public void DragonDanceUsesOrderedAttackSpeedPairAndOneCombinedWaveIncludingIndividualCaps(int attack, int speed, int argument)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = F.Body(Initial(349, single, actor), actor, b => b with { AttackStage = attack, SpeedStage = speed });
            var result = F.Ordered(frame, actor, actor);
            var rows = result.Events.Where(e => e is SourceStatChangedEvent or SourceStatAtMaximumEvent).ToArray();
            if (argument == 0)
            { Assert.AreEqual(0, rows.Length); Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "stats-at-maximum")); Check(result, 349, single, null, null); }
            else
            {
                Assert.AreEqual(2, rows.Length);
                Assert.AreEqual(WildBattleStat.Attack, rows[0] is SourceStatChangedEvent a ? a.Stat : ((SourceStatAtMaximumEvent)rows[0]).Stat);
                Assert.AreEqual(WildBattleStat.Speed, rows[1] is SourceStatChangedEvent s ? s.Stat : ((SourceStatAtMaximumEvent)rows[1]).Stat);
                Check(result, 349, single, rows[0], argument);
            }
            Assert.AreEqual(Math.Min(12, attack + 1), result.State.Slots[actor].Body!.AttackStage);
            Assert.AreEqual(Math.Min(12, speed + 1), result.State.Slots[actor].Body!.SpeedStage);
        }
    }

    [TestMethod, DataRow(0), DataRow(6), DataRow(10), DataRow(11), DataRow(12)]
    public void AcidArmorUsesRequestedPlusTwoEvenWhenClampedAndHasNoRootAtMaximum(int stage)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = F.Body(Initial(151, single, actor), actor, b => b with { DefenseStage = stage });
            var result = F.Ordered(frame, actor, actor); var stat = result.Events.OfType<SourceStatChangedEvent>().SingleOrDefault();
            Assert.AreEqual(Math.Min(12, stage + 2), result.State.Slots[actor].Body!.DefenseStage);
            if (stage == 12) Assert.AreEqual(1, result.Events.OfType<SourceStatAtMaximumEvent>().Count());
            else { Assert.IsNotNull(stat); Assert.AreEqual(WildBattleStat.Defense, stat.Stat); Assert.AreEqual(stage, stat.Before); Assert.AreEqual(Math.Min(12, stage + 2), stat.After); }
            Check(result, 151, single, stat, stage == 12 ? null : 40);
        }
    }

    [TestMethod, DataRow(349), DataRow(151)]
    public void ActualWhiteHerbSuffixCannotEraseTheEarlierMoveOrReplayItsStatWave(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(id, single), 0, b => b with { AttackStage = 4, SpeedStage = 6, HeldItem = "ITEM_WHITE_HERB" });
            var result = F.Ordered(frame, 0, 0); var owner = result.Events.OfType<SourceStatChangedEvent>().First();
            var item = result.Events.OfType<SourceHeldItemConsumedEvent>().Single(e => e.Item == "ITEM_WHITE_HERB");
            Assert.IsTrue(owner.Sequence < item.Sequence); Assert.IsNotNull(item.StatChanges); Assert.AreEqual(6, result.State.Slots[0].Body!.AttackStage);
            Check(result, id, single, owner, id == 349 ? 55 : 40);
            // Erasing the authenticated consumption cannot turn restoration into move evidence.
            F.Deny(Shared.Number(result.Events.Remove(item)), single);
            F.Deny(result.Events.SetItem(result.Events.IndexOf(item), item with { StatChanges = null }), single);
        }
    }

    [TestMethod, DataRow("missing"), DataRow("duplicate"), DataRow("reversed"), DataRow("foreign"), DataRow("wrong-stat"),
        DataRow("no-change"), DataRow("wrong-step"), DataRow("owner"), DataRow("called"), DataRow("extra")]
    public void DragonDanceRejectsIncompleteForeignOrMalformedPairAndPpAuthority(string mutation)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(Initial(349, single), 0, b => b with { SpeedStage = 6 });
            var events = F.Ordered(frame, 0, 0).Events; var stats = events.OfType<SourceStatChangedEvent>().ToArray();
            var first = stats[0]; var second = stats[1]; int a = events.IndexOf(first), b = events.IndexOf(second);
            var pp = events.OfType<SourcePpCommandEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
            events = mutation switch {
                "missing" => Shared.Number(events.RemoveAt(b)), "duplicate" => Shared.Number(events.Insert(b + 1, second)),
                "reversed" => Shared.Number(events.SetItem(a, second).SetItem(b, first)),
                "foreign" => events.SetItem(b, second with { Target = new(1) }), "wrong-stat" => events.SetItem(b, second with { Stat = WildBattleStat.Defense }),
                "no-change" => events.SetItem(b, second with { After = second.Before }), "wrong-step" => events.SetItem(b, second with { After = second.Before + 2 }),
                "owner" => events.SetItem(events.IndexOf(pp), pp with { PpOwner = new(1) }),
                "called" => events.SetItem(events.IndexOf(attack), attack with { ChosenMove = Kernel.Id("METRONOME") }),
                _ => Shared.Number(events.Insert(b + 1, first with { Stat = WildBattleStat.Defense })) };
            F.Deny(events, single); for (int i = 0; i < events.Length; i++) Assert.IsNull(F.Wave(events, i, single));
        }
    }

    [TestMethod, DataRow(349), DataRow(151)]
    public void RealMetronomeStatLeavesOwnTheirFirstResultBoundary(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Kernel.Frame(CallerPreludeAnimationKernelFixture.Bodies(118), single) with {
                Rng = new(CallerPreludeAnimationKernelFixture.MetronomeSeed(id)) };
            var result = Envelope.Resolve(frame, 0, 1);
            Assert.IsTrue(result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Move == Move(id)));
            CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), id, result.Events.First(e => e is SourceStatChangedEvent or SourceStatAtMaximumEvent));
        }
    }

    [TestMethod, DataRow(166), DataRow(349), DataRow(151)]
    public void SourceDefinitionCanonicalCatalogPhaseAndSupportRemainRequired(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            SourceBattleFrame frame;
            if (id == 166)
            {
                var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
                bodies[0] = Kernel.Mon("SMEARGLE", "SKETCH") with { SpeedStage = 12 };
                frame = Envelope.Resolve(Kernel.Frame(bodies, single), 1, 1).State;
            }
            else frame = Initial(id, single);
            var events = F.Ordered(frame, 0, id == 166 ? 1 : 0).Events;
            int used = Array.FindIndex(events.ToArray(), e => e is SourceMoveUsedEvent); var definitions = Kernel.Mechanics.Catalog.Moves; var definition = definitions[Move(id)];
            Assert.IsTrue(AnimationDispatchFixture.Supports(id, definition.Id.Value));
            foreach (var catalog in new IReadOnlyDictionary<MoveId, PokemonMoveDefinition>?[] { null, definitions.Remove(Move(id)),
                definitions.SetItem(Move(id), definition with { SourceMoveId = 1 }), definitions.SetItem(Move(id), definition with { Power = 40 }),
                definitions.SetItem(Move(id), definition with { Target = id == 166 ? "MOVE_TARGET_USER" : "MOVE_TARGET_SELECTED" }) })
            {
                var decision = single ? AnimationDispatchFixture.AtAttackText(F.Single(events, used), true, SourceBattleScene.On, catalog)
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, used), SourceBattleScene.On, catalog);
                Assert.IsNull(decision.Dispatch);
            }
            var unsupported = single ? CampaignMoveAnimationPresentation.AtAttackText(F.Single(events, used), true, SourceBattleScene.On, definitions, (_, _) => false)
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, used), SourceBattleScene.On, definitions, (_, _) => false);
            Assert.IsNull(unsupported.Dispatch);
            foreach (int index in new[] { -1, events.Length }) Assert.IsNull(F.Project(events, index, single, false).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(F.Single(events, used) with { Phase = CampaignBattlePhase.Results }, true, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, used) with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On).Dispatch);
        }
    }
}
