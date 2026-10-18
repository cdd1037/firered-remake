using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Previous = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using static FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class SelectedWeatherHitAnimationKernelTests
{
    [TestMethod, DataRow(345), DataRow(327), DataRow(237), DataRow(310)]
    public void NaturalSelectedHitsUseAllActorSlotsSinglesAndSelectedAllies(int id)
    {
        var definition = Mechanics.Catalog.Moves[Move(id)];
        Assert.AreEqual(id, definition.SourceMoveId); Assert.AreEqual(Spec(id).Effect, definition.Effect);
        Assert.AreEqual("MOVE_TARGET_SELECTED", definition.Target); Assert.IsTrue(definition.Power > 0);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Ordered(Frame(Bodies(id, actor), single), actor, target);
            Check(result, id, single, Hits(result.Events).Single());
        }
    }

    [TestMethod, DataRow(345), DataRow(327), DataRow(237), DataRow(310)]
    public void SubstituteBreakKoAndTrueZeroLossEndureKeepExactlyOneSelectedRoot(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (string branch in new[] { "substitute", "ko", "endure" })
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor);
            bodies[target] = branch == "substitute" ? bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } }
                : bodies[target] with { Enduring = branch == "endure", Pokemon = bodies[target].Pokemon with { CurrentHp = 1 } };
            var result = Ordered(Frame(bodies, single), actor, target); var hit = Hits(result.Events).Single();
            if (branch == "substitute")
            {
                Assert.AreEqual(0, result.Events.OfType<SourceSubstituteDamagedEvent>().Single().HpAfter);
                Assert.AreEqual(bodies[target].Pokemon.CurrentHp, result.State.Slots[target].Body!.Pokemon.CurrentHp);
                Assert.IsFalse(result.Events.OfType<SourceFlinchEvent>().Any(e => e.Applied));
            }
            else if (branch == "ko")
            {
                Assert.AreEqual(0, result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
                Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e => e.Side.Value == target));
            }
            else
            {
                var hp = result.Events.OfType<SourceHpChangedEvent>().Single(); Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(1, hp.HpAfter);
                var adjustment = result.Events.OfType<SourceDamageAdjustedEvent>().Single();
                Assert.IsTrue(adjustment.Endured); Assert.IsTrue(adjustment.DamageBefore > 0); Assert.AreEqual(0, adjustment.DamageAfter);
            }
            Check(result, id, single, hit);
        }
    }

    [TestMethod, DataRow(345), DataRow(327), DataRow(237), DataRow(310)]
    public void ActualProtectAndAccuracyMissesCannotBorrowASelectedHitRoot(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(id, actor); bodies[target] = bodies[target] with { Protected = true };
            var protectedResult = Ordered(Frame(bodies, single), actor, target);
            Assert.IsTrue(protectedResult.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "protected"));
            Check(protectedResult, id, single, null);
            if (id == 345) continue; // EFFECT_ALWAYS_HIT has no ordinary accuracy-miss branch.
            bodies[target] = bodies[target] with { Protected = false, EvasionStage = 12 };
            bodies[actor] = bodies[actor] with { AccuracyStage = 0 };
            var missed = Find(Frame(bodies, single), actor, target,
                r => r.Events.OfType<SourceMoveMissedEvent>().Any(), Spec(id).Name + " miss");
            Check(missed, id, single, null);
        }
    }

    [TestMethod, DataRow(345, "SHEDINJA", "HARDEN"), DataRow(327, "GASTLY", "LICK"), DataRow(310, "SNORLAX", "DEFENSE_CURL")]
    public void SourceTypeOrWonderGuardPreventionDeniesEvenZeroLossHpReceipts(int id, string species, string move)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[1] = Mon(species, move, 100);
            var result = Ordered(Frame(bodies, single));
            Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect) ||
                result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_WONDER_GUARD"));
            Assert.AreEqual(bodies[1].Pokemon.CurrentHp, result.State.Slots[1].Body!.Pokemon.CurrentHp);
            Check(result, id, single, null);
        }
    }

    [TestMethod]
    public void MagicalLeafIgnoresAccuracyStagesWithoutInventingAccuracyRng()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(345); bodies[0] = bodies[0] with { AccuracyStage = 0 }; bodies[1] = bodies[1] with { EvasionStage = 12 };
            var result = Ordered(Frame(bodies, single));
            Assert.IsFalse(result.Events.OfType<SourceMoveMissedEvent>().Any());
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            Check(result, 345, single, Hits(result.Events).Single());
        }
    }

    [TestMethod, DataRow("FLY", "RAYQUAZA", true), DataRow("BOUNCE", "PONYTA", true), DataRow("DIG", "DIGLETT", false)]
    public void SkyUppercutUsesAuthenticChargeStateAndDoesNotDoubleAirborneDamage(string charge, string species, bool hits)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(327); bodies[1] = Mon(species, charge, 100);
            var frame = Frame(bodies, single); var charged = Direct(frame, 1, 0);
            Assert.AreEqual(Id(charge), charged.State.Slots[1].Registers.Turn!.Lock!.Move);
            Assert.IsTrue(charged.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile == "called-charge" && e.After == 1));
            var result = Direct(charged.State with { Rng = frame.Rng });
            Check(result, 327, single, hits ? Hits(result.Events).Single() : null);
            if (!hits) { Assert.IsTrue(result.Events.OfType<SourceMoveMissedEvent>().Any()); continue; }
            var ground = Direct(frame); var a = ground.Events.OfType<SourceHpChangedEvent>().Single(); var b = result.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.AreEqual(a.Damage, b.Damage); Assert.AreEqual(a.Critical, b.Critical); Assert.AreEqual(a.RandomPercent, b.RandomPercent);
            Check(ground, 327, single, a);
        }
    }

    [TestMethod, DataRow("FLY", "RAYQUAZA"), DataRow("BOUNCE", "PONYTA"), DataRow("DIG", "DIGLETT")]
    public void AlwaysHitMagicalLeafStillRespectsAuthenticSemiInvulnerability(string charge, string species)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(345); bodies[1] = Mon(species, charge, 100);
            var charged = Direct(Frame(bodies, single), 1, 0); var result = Direct(charged.State);
            Assert.IsTrue(result.Events.OfType<SourceMoveMissedEvent>().Any()); Check(result, 345, single, null);
        }
    }

    [TestMethod, DataRow(0, false), DataRow(31, true)]
    public void HiddenPowerUsesActualIvDerivedTypeWithoutMutatingTheNormalMetadata(int iv, bool hits)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(237); bodies[0] = Mon("UNOWN", "HIDDEN_POWER", 50, iv) with { AccuracyStage = 12, SpeedStage = 12 };
            bodies[1] = Mon("GASTLY", "LICK", 100);
            var definition = Mechanics.Catalog.Moves[Move(237)]; string before = JsonSerializer.Serialize(definition);
            var result = Ordered(Frame(bodies, single));
            // All-zero IVs yield Fighting/30 and all-31 yield Dark/70. Ghost
            // immunity must follow that committed dynamic type, not TYPE_NORMAL.
            Assert.AreEqual(!hits, result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
            Check(result, 237, single, hits ? Hits(result.Events).Single() : null);
            Assert.AreEqual("TYPE_NORMAL", definition.Type); Assert.AreEqual(1, definition.Power);
            Assert.AreEqual(before, JsonSerializer.Serialize(Mechanics.Catalog.Moves[Move(237)]));
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void AstonishOwnsItsHitWhetherTheActualFlinchChanceSucceedsOrFails(bool flinch)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Find(Frame(Bodies(310), single), 0, 1,
                r => r.Events.OfType<SourceFlinchEvent>().Any(e => e.Applied) == flinch, "Astonish flinch=" + flinch);
            Assert.AreEqual(30, Mechanics.Catalog.Moves[Move(310)].SecondaryEffectChance);
            Check(result, 310, single, Hits(result.Events).Single());
        }
    }

    [TestMethod, DataRow("inner-focus"), DataRow("shield-dust")]
    public void PreventedAstonishFlinchCannotSuppressItsRealHit(string prevention)
    {
        var bodies = Bodies(310); bodies[1] = prevention == "inner-focus" ? Mon("ZUBAT", "LEECH_LIFE", 100) : Mon("CATERPIE", "STRING_SHOT", 100);
        var result = Find(Frame(bodies), 0, 1, r => r.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal) && t.Raw % 100 <= 30),
            "Astonish qualifying but prevented flinch");
        Assert.IsFalse(result.Events.OfType<SourceFlinchEvent>().Any(e => e.Applied)); Check(result, 310, false, Hits(result.Events).Single());
    }

    [TestMethod]
    public void AstonishMinimizeBonusChangesDamageWithoutAnExtraRootOrDifferentRandomDraws()
    {
        var bodies = Bodies(310); var plain = Ordered(Frame(bodies));
        bodies[1] = bodies[1] with { Minimized = true }; var minimized = Ordered(Frame(bodies));
        var first = plain.Events.OfType<SourceHpChangedEvent>().Single(); var second = minimized.Events.OfType<SourceHpChangedEvent>().Single();
        Assert.IsTrue(second.Damage > first.Damage); Assert.AreEqual(first.Critical, second.Critical); Assert.AreEqual(first.RandomPercent, second.RandomPercent);
        Assert.AreEqual(plain.State.Rng, minimized.State.Rng); Assert.AreEqual(JsonSerializer.Serialize(plain.Trace), JsonSerializer.Serialize(minimized.Trace));
        Check(plain, 310, false, first); Check(minimized, 310, false, second);
    }

    [TestMethod, DataRow(201), DataRow(240), DataRow(241), DataRow(258), DataRow(257), DataRow(345), DataRow(327), DataRow(237), DataRow(310)]
    public void EveryRootRequiresAuthenticAttackPpGeneratedScriptAndMatchingMetadata(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            bool weather = Spec(id).Weather is not null; int target = weather ? 0 : 1;
            var events = weather ? Direct(Frame(Bodies(id), single), 0, target).Events : Ordered(Frame(Bodies(id), single), 0, target).Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); int at = events.IndexOf(used);
            var attack = events.OfType<SourceAttackStringEvent>().Single(); int ai = events.IndexOf(attack);
            var pp = events.OfType<SourcePpCommandEvent>().First(); int pi = events.IndexOf(pp);
            foreach (var bad in new[] { attack with { Actor = new(1) }, attack with { CurrentMove = Id("TACKLE") },
                attack with { ChosenMove = Id("METRONOME") }, attack with { Turn = attack.Turn + 1 }, attack with { Sequence = used.Sequence } })
                Deny(events.SetItem(ai, bad), single);
            foreach (var bad in new[] { pp with { PpOwner = new(1) }, pp with { Actor = new(1) }, pp with { CurrentMove = Id("TACKLE") },
                pp with { ChosenMove = Id("METRONOME") }, pp with { CommandCost = 0 }, pp with { PpSlot = 3 }, pp with { Turn = pp.Turn + 1 } })
                Deny(events.SetItem(pi, bad), single);
            Deny(events.RemoveAt(ai), single); Deny(events.RemoveAt(pi), single);
            var moves = Mechanics.Catalog.Moves; var definition = moves[Move(id)];
            foreach (var metadata in new[] { null, moves.Remove(Move(id)), moves.SetItem(Move(id), definition with { SourceMoveId = id == 201 ? 240 : 201 }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Previous.Single(events, at), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Previous.Double(events, at), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Previous.Single(events, at), true, SourceBattleScene.On, moves, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Previous.Double(events, at), SourceBattleScene.On, moves, (_, _) => false).Dispatch);
        }
    }
}
