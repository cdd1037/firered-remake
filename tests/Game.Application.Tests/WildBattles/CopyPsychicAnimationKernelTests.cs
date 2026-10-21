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
public sealed class CopyPsychicAnimationKernelTests
{
    [TestMethod, DataRow(101), DataRow(102), DataRow(104), DataRow(354)]
    public void OrdinaryRootsUseNaturalLearnersAndPublicTurnReceiptsAcrossEveryPosition(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = F.Initial(id, single, actor); var result = F.Ordered(frame, actor, id == 104 ? actor : actor ^ 1);
            var used = result.Events.OfType<SourceMoveUsedEvent>().Single();
            SourceBattleEvent outcome = id switch {
                102 => result.Events.OfType<SourceMoveCopiedEvent>().Single(),
                104 => result.Events.OfType<SourceStatChangedEvent>().Single(), _ => Envelope.Hits(result.Events).Single() };
            F.Check(result.Events, id, single, used, outcome);
            Assert.AreEqual(F.Spec(id).Effect, Kernel.Mechanics.Catalog.Moves[used.Move].Effect);
            if (id == 101) { var hit = (SourceHpChangedEvent)outcome; Assert.AreEqual(50, hit.Damage); Assert.AreEqual(50, hit.HpBefore - hit.HpAfter); }
            if (id == 102)
            {
                var copied = (SourceMoveCopiedEvent)outcome; Assert.IsFalse(copied.Permanent); Assert.AreEqual(used.Slot, copied.MoveSlot);
                Assert.AreEqual(Kernel.Id("SPLASH"), copied.CopiedMove); Assert.AreEqual(copied.CopiedMove, copied.Replacement.Move);
                Assert.AreEqual(5, copied.Replacement.CurrentPp); Assert.AreEqual(40, copied.Replacement.MaximumPp);
                Assert.AreEqual(used.Move, result.State.Parties[actor & 1].Members[actor / 2].Pokemon.Moves[0].Move);
                Assert.AreEqual(used.PpAfter, result.State.Parties[actor & 1].Members[actor / 2].Pokemon.Moves[0].CurrentPp);
                Assert.AreEqual(copied.Replacement, result.State.Slots[actor].Body!.Pokemon.Moves[0]);
            }
            if (id == 104)
            {
                var stat = (SourceStatChangedEvent)outcome; Assert.AreEqual(WildBattleStat.Evasion, stat.Stat);
                Assert.AreEqual(6, stat.Before); Assert.AreEqual(7, stat.After); Assert.AreEqual(used.Actor, stat.Target);
                var wave = F.Wave(result.Events, result.Events.IndexOf(stat), single); Assert.IsNotNull(wave); Assert.AreEqual(21, wave.AnimationArgument);
            }
            if (id == 354)
            {
                var stat = result.Events.OfType<SourceStatChangedEvent>().Single(); Assert.AreEqual(WildBattleStat.SpecialAttack, stat.Stat);
                Assert.AreEqual(used.Actor, stat.Actor); Assert.AreEqual(used.Actor, stat.Target); Assert.AreEqual(6, stat.Before); Assert.AreEqual(4, stat.After);
                Assert.IsTrue(stat.Sequence > outcome.Sequence); var wave = F.Wave(result.Events, result.Events.IndexOf(stat), single);
                Assert.IsNotNull(wave); Assert.AreEqual(49, wave.AnimationArgument); Assert.AreEqual(354, wave.SourceMoveId);
            }
        }
    }
    [TestMethod, DataRow(0), DataRow(1), DataRow(2), DataRow(3)]
    public void MimicUsesItsSelectedSlotAndPreservesItsPpUpsInTheTemporaryCopy(int ups)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(F.Initial(102, single), 0, b => b with { Pokemon = b.Pokemon with {
                Moves = [new(Kernel.Id("POUND"), 35, 35), new(F.Move(102), 10 + ups * 2, 10 + ups * 2, ups)] } });
            var result = F.Ordered(frame, 0, 1, moveSlot: 1); var copy = result.Events.OfType<SourceMoveCopiedEvent>().Single();
            Assert.AreEqual(1, copy.MoveSlot); Assert.AreEqual(ups, copy.Replacement.PpUps); Assert.AreEqual(40 + 8 * ups, copy.Replacement.MaximumPp);
            Assert.AreEqual(5, copy.Replacement.CurrentPp); Assert.AreEqual(F.Move(102), result.State.Parties[0].Members[0].Pokemon.Moves[1].Move);
            F.Check(result.Events, 102, single, result.Events.OfType<SourceMoveUsedEvent>().Single(), copy);
        }
    }
    [TestMethod, DataRow("substitute"), DataRow("protected"), DataRow("no-history"), DataRow("already-known")]
    public void MimicFailureNeverAnimatesFromPpAlone(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = branch == "no-history" ? Kernel.Frame(F.Bodies(102), single) : F.Initial(102, single);
            if (branch == "substitute") frame = F.Body(frame, 1, b => b with { CalledVolatile = new() { SubstituteHp = 100 } });
            if (branch == "protected") frame = F.Body(frame, 1, b => b with { Protected = true });
            if (branch == "already-known")
            {
                // Jigglypuff naturally knows Pound; a real donor Pound action
                // gives it an authentic but already-known candidate.
                var donor = Kernel.Mon("JIGGLYPUFF", "POUND", 100) with { SpeedStage = 0 };
                frame = F.Body(frame, 1, _ => donor); frame = F.Body(frame, 0, b => b with { Pokemon = b.Pokemon with { Moves = b.Pokemon.Moves.Add(new(Kernel.Id("POUND"), 35, 35)) } });
                frame = Envelope.Resolve(frame, 1, 0).State;
            }
            var result = F.Ordered(frame, 0, 1); Assert.AreEqual(0, result.Events.OfType<SourceMoveCopiedEvent>().Count());
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any()); F.Deny(result.Events, single);
        }
    }
    [TestMethod, DataRow("permanent"), DataRow("actor"), DataRow("target"), DataRow("move"), DataRow("slot"), DataRow("replacement"), DataRow("pp"), DataRow("max-pp"), DataRow("ups"), DataRow("duplicate"), DataRow("turn"), DataRow("sequence"), DataRow("owner")]
    public void MimicRejectsMalformedCopyOrForeignPpReceipts(string mutation)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = F.Ordered(F.Initial(102, single), 0, 1).Events; var copy = events.OfType<SourceMoveCopiedEvent>().Single();
            int i = events.IndexOf(copy); var pp = events.OfType<SourcePpCommandEvent>().Single();
            events = mutation switch {
                "permanent" => events.SetItem(i, copy with { Permanent = true }), "actor" => events.SetItem(i, copy with { Actor = new(1) }),
                "target" => events.SetItem(i, copy with { Target = new(0) }), "move" => events.SetItem(i, copy with { Move = Kernel.Id("SKETCH") }),
                "slot" => events.SetItem(i, copy with { MoveSlot = 1 }), "replacement" => events.SetItem(i, copy with { Replacement = copy.Replacement with { Move = Kernel.Id("TACKLE") } }),
                "pp" => events.SetItem(i, copy with { Replacement = copy.Replacement with { CurrentPp = 4 } }),
                "max-pp" => events.SetItem(i, copy with { Replacement = copy.Replacement with { MaximumPp = 39 } }),
                "ups" => events.SetItem(i, copy with { Replacement = copy.Replacement with { PpUps = 4 } }),
                "duplicate" => Shared.Number(events.Insert(i + 1, copy)), "turn" => events.SetItem(i, copy with { Turn = copy.Turn + 1 }),
                "sequence" => events.SetItem(i, copy with { Sequence = pp.Sequence }),
                "owner" => events.SetItem(events.IndexOf(pp), pp with { PpOwner = new(1) }), _ => throw new InvalidOperationException() };
            F.Deny(events, single);
        }
    }
    [TestMethod]
    public void SinglesMimicAuthenticatesTheExportedCopyAndPpWithoutCanonicalBackchannel()
    {
        var result = F.Ordered(F.Initial(102, true), 0, 1); var used = result.Events.OfType<SourceMoveUsedEvent>().Single();
        var battle = F.Single(result.Events, result.Events.IndexOf(used)); Assert.AreEqual(result.Events, battle.Events);
        Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(battle, true, SourceBattleScene.On).Dispatch);
        int copied = Array.FindIndex(battle.Events.ToArray(), e => e is SourceMoveCopiedEvent);
        var copy = (SourceMoveCopiedEvent)battle.Events[copied];
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with { Events = battle.Events.SetItem(copied, copy with { Permanent = true }) }, true, SourceBattleScene.On).Dispatch);
        int p = Array.FindIndex(battle.Events.ToArray(), e => e is SourcePpCommandEvent); var pp = (SourcePpCommandEvent)battle.Events[p];
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with { Events = battle.Events.SetItem(p, pp with { PpOwner = new BattlerSlot(1) }) }, true, SourceBattleScene.On).Dispatch);
    }
    [TestMethod, DataRow(101), DataRow(354)]
    public void OrdinaryPsychicRootsUseSubstituteHitsAndRejectMissProtectionAndImmunity(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (string branch in new[] { "substitute", "protected", "immune", "miss" })
        {
            var frame = F.Initial(id, single);
            if (branch == "substitute") frame = F.Body(frame, 1, b => b with { CalledVolatile = new() { SubstituteHp = 100 } });
            if (branch == "protected") frame = F.Body(frame, 1, b => b with { Protected = true });
            if (branch == "immune") frame = F.Body(frame, 1, _ => id == 101 ? Kernel.Mon("SNORLAX", "DEFENSE_CURL", 100) with { SpeedStage = 0 }
                : Kernel.Mon("POOCHYENA", "TACKLE", 100) with { SpeedStage = 0 });
            if (branch == "miss") { frame = F.Body(frame, 0, b => b with { AccuracyStage = 0 }); frame = F.Body(frame, 1, b => b with { EvasionStage = 12 }); }
            var result = branch == "miss" ? F.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), $"{id}-ordinary-miss") : F.Ordered(frame, 0, 1);
            if (branch == "substitute")
            {
                var hit = result.Events.OfType<SourceSubstituteDamagedEvent>().Single(); F.Check(result.Events, id, single, result.Events.OfType<SourceMoveUsedEvent>().Single(), hit);
                if (id == 354) { var stat = result.Events.OfType<SourceStatChangedEvent>().Single(); Assert.IsNotNull(F.Wave(result.Events, result.Events.IndexOf(stat), single)); }
            }
            else
            {
                F.Deny(result.Events, single);
                if (id == 354) Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any(e => e.Stat == WildBattleStat.SpecialAttack));
            }
        }
    }
    [TestMethod]
    public void DoubleTeamAtMaximumHasNoDedicatedRootOrGenericWave()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(F.Initial(104, single), 0, b => b with { EvasionStage = 12 }); var result = F.Ordered(frame, 0, 0);
            Assert.AreEqual(1, result.Events.OfType<SourceStatAtMaximumEvent>().Count()); F.Deny(result.Events, single);
            for (int i = 0; i < result.Events.Length; i++) Assert.IsNull(F.Wave(result.Events, i, single));
        }
    }
    [TestMethod, DataRow(6, false), DataRow(1, false), DataRow(0, false), DataRow(6, true)]
    public void PsychoBoostWaveUsesRequestedMinusTwoAndSurvivesWhiteHerbRestoration(int stage, bool whiteHerb)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(F.Initial(354, single), 0, b => b with { SpecialAttackStageDelta = stage - 6, HeldItem = whiteHerb ? "ITEM_WHITE_HERB" : "ITEM_NONE" });
            var result = F.Ordered(frame, 0, 1); F.Check(result.Events, 354, single, result.Events.OfType<SourceMoveUsedEvent>().Single(), Envelope.Hits(result.Events).Single());
            var change = result.Events.OfType<SourceStatChangedEvent>().FirstOrDefault(e => e.Stat == WildBattleStat.SpecialAttack && e.After < e.Before);
            if (stage == 0) { Assert.IsNull(change); Assert.AreEqual(1, result.Events.OfType<SourceStatAtMinimumEvent>().Count()); }
            else { Assert.IsNotNull(change); Assert.AreEqual(stage, change.Before); Assert.AreEqual(Math.Max(0, stage - 2), change.After); }
            for (int i = 0; i < result.Events.Length; i++)
            {
                var wave = F.Wave(result.Events, i, single);
                if (result.Events[i] != change) { Assert.IsNull(wave, "Restoration and cap rows cannot replay the authored self drop."); continue; }
                Assert.IsNotNull(wave); Assert.AreEqual(49, wave.AnimationArgument); Assert.AreEqual(new BattlerSlot(0), wave.Affected);
            }
            if (whiteHerb)
            {
                Assert.IsTrue(result.Events.OfType<SourceHeldItemConsumedEvent>().Any(e => e.Item == "ITEM_WHITE_HERB"));
                Assert.AreEqual(0, result.State.Slots[0].Body!.SpecialAttackStageDelta);
            }
        }
    }
    [TestMethod]
    public void PsychoBoostSuccessfulHitStillDropsItsOwnStatWhenTheTargetFaints()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Body(F.Initial(354, single), 1, b => b with { Pokemon = b.Pokemon with { CurrentHp = 1 } });
            var result = F.Ordered(frame, 0, 1); var hit = result.Events.OfType<SourceHpChangedEvent>().Single(); Assert.AreEqual(0, hit.HpAfter);
            F.Check(result.Events, 354, single, result.Events.OfType<SourceMoveUsedEvent>().Single(), hit);
            var stat = result.Events.OfType<SourceStatChangedEvent>().First(e => e.Stat == WildBattleStat.SpecialAttack && e.After < e.Before);
            Assert.IsNotNull(F.Wave(result.Events, result.Events.IndexOf(stat), single));
        }
    }
    [TestMethod, DataRow(13), DataRow(130), DataRow(101), DataRow(102), DataRow(104), DataRow(354)]
    public void PhaseSourceMetadataAndCanonicalSupportRemainAuthoritative(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = F.Ordered(F.Initial(id, single), 0, id == 104 ? 0 : 1); bool setup = id is 13 or 130;
            int index = Array.FindIndex(result.Events.ToArray(), e => setup ? e is SourceMtMoonVolatileEvent { Volatile: "called-charge" } : e is SourceMoveUsedEvent);
            var definitions = Kernel.Mechanics.Catalog.Moves; var definition = definitions[F.Move(id)]; Assert.IsTrue(AnimationDispatchFixture.Supports(id, definition.Id.Value));
            var missing = definitions.Remove(F.Move(id)); var mismatched = definitions.SetItem(F.Move(id), definition with { SourceMoveId = id == 13 ? 130 : 13 });
            foreach (var catalog in new IReadOnlyDictionary<MoveId, PokemonMoveDefinition>?[] { null, missing, mismatched })
            {
                var decision = single ? setup ? AnimationDispatchFixture.AtResult(F.Single(result.Events, index), true, SourceBattleScene.On, catalog)
                    : AnimationDispatchFixture.AtAttackText(F.Single(result.Events, index), true, SourceBattleScene.On, catalog)
                    : setup ? AnimationDispatchFixture.AtResult(Envelope.Double(result.Events, index), SourceBattleScene.On, catalog)
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(result.Events, index), SourceBattleScene.On, catalog);
                Assert.IsNull(decision.Dispatch);
            }
            foreach (int cursor in new[] { -1, result.Events.Length }) { Assert.IsNull(F.Project(result.Events, cursor, single, false).Dispatch); Assert.IsNull(F.Project(result.Events, cursor, single, true).Dispatch); }
            if (single)
            {
                var b = F.Single(result.Events, index) with { Phase = CampaignBattlePhase.Results };
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(b, true, SourceBattleScene.On).Dispatch); Assert.IsNull(AnimationDispatchFixture.AtResult(b, true, SourceBattleScene.On).Dispatch);
            }
            else
            {
                var b = Envelope.Double(result.Events, index) with { Stage = CampaignDoubleBattleStage.Finished };
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(b, SourceBattleScene.On).Dispatch); Assert.IsNull(AnimationDispatchFixture.AtResult(b, SourceBattleScene.On).Dispatch);
            }
            var unsupported = single ? setup ? CampaignMoveAnimationPresentation.AtResult(F.Single(result.Events, index), true, SourceBattleScene.On, definitions, (_, _) => false)
                : CampaignMoveAnimationPresentation.AtAttackText(F.Single(result.Events, index), true, SourceBattleScene.On, definitions, (_, _) => false)
                : setup ? CampaignMoveAnimationPresentation.AtResult(Envelope.Double(result.Events, index), SourceBattleScene.On, definitions, (_, _) => false)
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(result.Events, index), SourceBattleScene.On, definitions, (_, _) => false);
            Assert.IsNull(unsupported.Dispatch);
        }
    }

    [TestMethod, DataRow("MIMIC"), DataRow("METRONOME"), DataRow("SKETCH"), DataRow("STRUGGLE")]
    public void MimicRejectsSourceForbiddenCopiedMovesEvenWithConsistentReplacementPp(string name)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = F.Ordered(F.Initial(102, single), 0, 1).Events;
            var copy = events.OfType<SourceMoveCopiedEvent>().Single();
            var forbidden = Kernel.Mechanics.Catalog.Moves[Kernel.Id(name)];
            var replacement = new PokemonMoveSlot(forbidden.Id, Math.Min(5, forbidden.Pp), forbidden.Pp, 0);
            var malformed = copy with { CopiedMove = forbidden.Id, Replacement = replacement };
            Assert.AreEqual(malformed.CopiedMove, malformed.Replacement.Move);
            F.Deny(events.SetItem(events.IndexOf(copy), malformed), single);
        }
    }
}
