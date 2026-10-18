using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
using static FireRed.Tests.Application.WildBattles.ControlPowerAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class ControlPowerAnimationKernelTests
{
    [TestMethod, DataRow(175), DataRow(179), DataRow(182), DataRow(247), DataRow(269), DataRow(266), DataRow(213), DataRow(259)]
    public void SelectedNaturalLearnersOwnOneRootAcrossEverySlotSinglesAndSelectedAllies(int id)
    {
        var definition = Kernel.Mechanics.Catalog.Moves[Move(id)]; Assert.AreEqual(id, definition.SourceMoveId); Assert.AreEqual(Spec(id).Effect, definition.Effect);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in id is 182 or 266 ? new[] { actor } : single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var bodies = Bodies(id, actor); var frame = Kernel.Frame(bodies, single);
            if (id == 182)
            {
                var result = Kernel.Ordered(frame, actor, actor); var attempt = result.Events.OfType<SourceProtectAttemptEvent>().Single();
                Assert.IsTrue(attempt.Succeeded); Assert.AreEqual("protect", result.Events.OfType<SourceMtMoonVolatileEvent>().Single().Volatile);
                Shared.Check(result, id, single, attempt);
            }
            else
            {
                var result = Envelope.Resolve(frame, actor, target); Shared.Check(result, id, single, Marker(result.Events, id));
                if (id == 213)
                {
                    Assert.AreEqual(PokemonGender.Female, bodies[actor].Pokemon.Gender); Assert.AreEqual(PokemonGender.Male, bodies[target].Pokemon.Gender);
                    Assert.AreNotEqual("ABILITY_OBLIVIOUS", WildBattleEffective.Ability(bodies[target]));
                    var changed = (SourceInfatuationChangedEvent)Marker(result.Events, id); Assert.IsNull(changed.Before); Assert.AreEqual(new BattlerSlot(actor), changed.After);
                    Assert.AreEqual(new BattlerSlot(target), changed.Side);
                }
            }
        }
    }

    [TestMethod, DataRow(269), DataRow(259), DataRow(213)]
    public void SelectedControlBypassesSubstituteAndSafeguardButNotProtectOrMiss(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (string branch in new[] { "substitute", "safeguard", "protect", "miss" })
        {
            var bodies = Bodies(id); if (branch == "substitute") bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } };
            if (branch == "protect") bodies[1] = bodies[1] with { Protected = true };
            if (branch == "miss") bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Kernel.Frame(bodies, single);
            if (branch == "safeguard") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { SafeguardTurnsRemaining = 5 }) };
            var result = branch == "miss" ? Envelope.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any() ||
                r.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "accuracy"), "selected volatile accuracy failure") : Envelope.Resolve(frame);
            Shared.Check(result, id, single, branch is "substitute" or "safeguard" ? Marker(result.Events, id) : null);
        }
    }

    [TestMethod, DataRow(269), DataRow(259)]
    public void RepeatedTauntOrTormentSpendsPpWithoutRefreshingExistingState(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[1] = bodies[1] with { CalledVolatile = id == 269 ? new() { TauntTurnsRemaining = 1 } : new() { Tormented = true } };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single)); Assert.AreEqual(id == 269 ? "already-taunted" : "already-tormented", result.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            Assert.IsFalse(result.Events.OfType<SourceMtMoonVolatileEvent>().Any()); Assert.AreEqual(bodies[1].CalledVolatile, result.State.Slots[1].Body!.CalledVolatile);
            Shared.Check(result, id, single, null);
        }
    }

    [TestMethod, DataRow(269), DataRow(259), DataRow(266), DataRow(213)]
    public void NewControlMarkersRequireDirectOwnedPpUniqueImmediateAndExactTargetReceipt(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Resolve(id, single, target: id == 266 ? 0 : 1).Events; var marker = Marker(events, id); int at = events.IndexOf(marker);
            var used = events.OfType<SourceMoveUsedEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
            int pi = events.IndexOf(pp), ui = events.IndexOf(used); Assert.AreEqual(pi + 1, at); Assert.AreEqual(pp.Sequence + 1, marker.Sequence);
            Deny(events.RemoveAt(at), single); Deny(events.RemoveAt(pi), single); Deny(Shared.Number(events.Insert(at + 1, marker)), single);
            Deny(events.SetItem(at, marker with { Turn = marker.Turn + 1 }), single); Deny(events.SetItem(at, marker with { Sequence = marker.Sequence + 1 }), single);
            Deny(events.SetItem(pi, marker with { Sequence = pp.Sequence }).SetItem(at, pp with { Sequence = marker.Sequence }), single);
            Deny(Shared.Number(events.Insert(at, new SourceMtMoonVolatileEvent(0, used.Turn, used.Target, "unrelated", 0, 1, "move"))), single);
            Deny(events.SetItem(pi, pp with { PpOwner = new(1) }), single); Deny(events.SetItem(pi, pp with { PpSlot = 1 }), single);
            Deny(events.SetItem(pi, pp with { ChosenMove = Kernel.Id("METRONOME") }), single);
            Deny(events.SetItem(events.IndexOf(attack), attack with { ChosenMove = Kernel.Id("METRONOME") }), single);
            int wrongTarget = id == 266 ? 1 : 0;
            Deny(events.SetItem(ui, used with { Target = new(wrongTarget) }).SetItem(pi, pp with { Target = new(wrongTarget) }), single);
            if (marker is SourceMtMoonVolatileEvent state)
            {
                Assert.AreEqual(id == 266 ? used.Actor : used.Target, state.Side); Assert.AreEqual(0, state.Before);
                Assert.AreEqual(id == 269 ? 2 : 1, state.After); Assert.AreEqual(id == 266 && single ? "single-battle-no-redirection" : "move", state.Reason);
                foreach (var wrong in new[] { state with { Side = new(wrongTarget) }, state with { Before = id == 266 ? 2 : 1 },
                    state with { After = 0 }, state with { After = id == 269 ? 1 : 2 }, state with { Volatile = "identified" }, state with { Reason = "countdown" } }) Deny(events.SetItem(at, wrong), single);
                if (id == 266) Deny(events.SetItem(at, state with { Reason = single ? "move" : "single-battle-no-redirection" }), single);
            }
            else if (marker is SourceInfatuationChangedEvent love)
                foreach (var wrong in new[] { love with { Side = used.Actor }, love with { Before = used.Actor }, love with { After = null },
                    love with { After = used.Target }, love with { Reason = "cute-charm" }, love with { Reason = "mental-herb" } }) Deny(events.SetItem(at, wrong), single);
            var definitions = Kernel.Mechanics.Catalog.Moves; var definition = definitions[Move(id)];
            foreach (var wrong in new[] { definition with { Power = 1 }, definition with { Target = id == 266 ? "MOVE_TARGET_SELECTED" : "MOVE_TARGET_USER" } })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, ui), true, SourceBattleScene.On, definitions.SetItem(Move(id), wrong)).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, ui), SourceBattleScene.On, definitions.SetItem(Move(id), wrong)).Dispatch);
            if (single)
            {
                var battle = Envelope.Single(events, ui); Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(battle with { SourceEvents = [] }, true, SourceBattleScene.On).Dispatch);
                WildBattleEvent altered = battle.Events[at] switch { WildBattleInfatuationChangedEvent x => x with { After = null },
                    WildBattleMtMoonVolatileEvent x => x with { After = 0 }, _ => throw new InvalidOperationException() };
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with { Events = battle.Events.SetItem(at, altered) }, true, SourceBattleScene.On).Dispatch);
            }
        }
    }

    [TestMethod, DataRow("same-gender"), DataRow("genderless"), DataRow("oblivious"), DataRow("already-infatuated")]
    public void AttractRequiresPersistentOppositeGendersAndAvailableTarget(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(213); bodies[1] = branch switch {
                "same-gender" => Kernel.Mon("WAILORD", "SPLASH", 100), "genderless" => Kernel.Mon("MAGNEMITE", "THUNDER_SHOCK", 100),
                "oblivious" => Kernel.Mon("SLOWPOKE", "TACKLE", 100, personality: 254), _ => bodies[1] };
            if (branch == "oblivious") Assert.AreEqual("ABILITY_OBLIVIOUS", WildBattleEffective.Ability(bodies[1]));
            var frame = Kernel.Frame(bodies, single);
            if (branch == "already-infatuated") frame = frame with { Slots = frame.Slots.SetItem(1, frame.Slots[1] with { Registers = frame.Slots[1].Registers with { InfatuationSource = new(0) } }) };
            var result = Envelope.Resolve(frame); Assert.IsFalse(result.Events.OfType<SourceInfatuationChangedEvent>().Any(e => e.Reason == "attract"));
            if (branch == "oblivious") Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_OBLIVIOUS"));
            else Assert.AreEqual(branch, result.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            Shared.Check(result, 213, single, null);
        }
    }

    [TestMethod]
    public void AttractUsesPersistentGenderOnBothSidesOfAnActualTransform()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(213); bodies[1] = Kernel.Mon("DITTO", "TRANSFORM", 50);
            var transformed = Envelope.Resolve(Kernel.Frame(bodies, single), 1, 0);
            Assert.AreEqual(1, transformed.Events.OfType<SourceTransformedEvent>().Count());
            Assert.AreEqual(PokemonGender.Genderless, WildBattleTransformationRules.PersistentPokemon(transformed.State.Slots[1].Body!).Gender);
            foreach (int actor in new[] { 0, 1 })
            {
                var result = Envelope.Resolve(transformed.State, actor, actor ^ 1);
                Assert.AreEqual("genderless", result.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
                Shared.Check(result, 213, single, null);
            }
        }
    }

    [TestMethod]
    public void AttractMentalHerbCureCannotEraseOrReplayTheCommittedSetup()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(213); bodies[1] = bodies[1] with { HeldItem = "ITEM_MENTAL_HERB" };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single)); var setup = (SourceInfatuationChangedEvent)Marker(result.Events, 213);
            var cure = result.Events.OfType<SourceInfatuationChangedEvent>().Single(e => e.Reason == "mental-herb");
            Assert.AreEqual(setup.After, cure.Before); Assert.IsNull(cure.After); Assert.IsTrue(cure.Sequence > setup.Sequence);
            Assert.IsNull(result.State.Slots[1].Registers.InfatuationSource); Assert.IsTrue(result.Events.OfType<SourceHeldItemConsumedEvent>().Any());
            Shared.Check(result, 213, single, setup);
        }
    }

    [TestMethod]
    public void FollowMeOverwritesCurrentSideTargetAndSinglesStillOwnAnAnimation()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies(266, actor); bodies[actor] = bodies[actor] with { Protected = true, AccuracyStage = 0, CalledVolatile = new() { SubstituteHp = 1 } };
            var frame = Kernel.Frame(bodies, single);
            if (!single) frame = frame with { Sides = frame.Sides.SetItem(actor & 1, frame.Sides[actor & 1] with { FollowMeTimer = 1, FollowMeTarget = new(actor ^ 2) }) };
            var result = Envelope.Resolve(frame, actor, actor); var marker = (SourceMtMoonVolatileEvent)Marker(result.Events, 266);
            Assert.AreEqual(single ? 0 : 1, marker.Before); Assert.AreEqual(1, marker.After); Assert.AreEqual(frame.Rng, result.State.Rng);
            Assert.AreEqual(single ? 0 : 1, result.State.Sides[actor & 1].FollowMeTimer);
            Assert.AreEqual(single ? null : (BattlerSlot?)new(actor), result.State.Sides[actor & 1].FollowMeTarget); Shared.Check(result, 266, single, marker);
        }
    }

    [TestMethod, DataRow(269), DataRow(266)]
    public void ActualTurnCleanupNeverReplaysTauntOrFollowMeSetup(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Kernel.Ordered(Kernel.Frame(Bodies(id), single), target: id == 266 ? 0 : 1); int roots = 0, countdowns = 0;
            for (int step = 0; ; step++)
            {
                Assert.IsTrue(step < 32); string before = JsonSerializer.Serialize(result);
                for (int i = 0; i < result.Events.Length; i++)
                {
                    var cue = Project(result.Events, i, single).Dispatch; if (cue?.SourceMoveId == id) roots++;
                    if (result.Events[i] is SourceMtMoonVolatileEvent { Volatile: "taunt", Reason: "countdown" }) { countdowns++; Assert.IsNull(cue); }
                }
                Assert.AreEqual(before, JsonSerializer.Serialize(result)); if (result.CompletedCleanTurn) break;
                Assert.IsNotNull(result.Continuation); result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, result.State, result.Continuation.Value);
                Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            }
            Assert.AreEqual(1, roots); Assert.AreEqual(id == 269 ? 1 : 0, countdowns);
            if (id == 266) { Assert.AreEqual(0, result.State.Sides[0].FollowMeTimer); Assert.IsNull(result.State.Sides[0].FollowMeTarget); }
        }
    }

    [TestMethod, DataRow(175), DataRow(179), DataRow(247)]
    public void NewHitsUseActualSubstituteProtectImmunityAndMissReceipts(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (string branch in new[] { "substitute", "protect", "immune", "miss" })
        {
            var bodies = Bodies(id); bodies[1] = branch switch {
                "substitute" => bodies[1] with { CalledVolatile = new() { SubstituteHp = 100 } },
                "protect" => bodies[1] with { Protected = true },
                "immune" => id == 247 ? Kernel.Mon("SNORLAX", "DEFENSE_CURL", 100) : Kernel.Mon("GASTLY", "LICK", 100), _ => bodies[1] };
            if (branch == "miss") bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Kernel.Frame(bodies, single); var result = branch == "miss" ? Envelope.Find(frame, 0, 1,
                r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "new hit accuracy failure") : Envelope.Resolve(frame);
            Shared.Check(result, id, single, branch == "substitute" ? Envelope.Hits(result.Events).Single() : null);
            if (branch == "substitute") Assert.IsInstanceOfType<SourceSubstituteDamagedEvent>(Envelope.Hits(result.Events).Single());
        }
    }

    [TestMethod]
    public void FollowMeIsExcludedByTheSourceMetronomeRejectionLoop()
    {
        // battle_script_commands.c sMovesForbiddenToCopy includes FOLLOW_ME;
        // Cmd_metronome scans through METRONOME_FORBIDDEN_END. Seed377 first
        // proposes266, rejects it, then accepts244 without inventing a called leaf.
        Assert.IsFalse(WildBattleSourceCallers.IsDirectMetronomeChoice(266));
        var selected = WildBattleSourceCallers.Metronome(new(377));
        Assert.AreEqual(2, selected.Trace.Length);
        Assert.AreEqual(266, (selected.Trace[0].Raw & 511) + 1);
        Assert.AreEqual(244, (selected.Trace[1].Raw & 511) + 1);
        Assert.AreEqual(244, selected.SourceMoveId); Assert.AreEqual(2UL, selected.Rng.Cursor);
    }

    [TestMethod, DataRow(269), DataRow(213), DataRow(259)]
    public void ActualMetronomeControlLeavesOwnTheirCommittedAttackTextBoundary(int id)
    {
        uint? seed = null; for (uint candidate = 0; candidate < 65536; candidate++)
            if (WildBattleSourceCallers.Metronome(new(candidate)).SourceMoveId == id) { seed = candidate; break; }
        Assert.IsNotNull(seed);
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[0] = Kernel.Mon("TOGETIC", "METRONOME", 29) with { AccuracyStage = 12 };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single) with { Rng = new(seed.Value) });
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Caller == Kernel.Id("METRONOME") && e.Result == Move(id)));
            Assert.IsNotNull(Marker(result.Events, id)); CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), id, Marker(result.Events, id));
        }
    }
    // Cmd_remaininghptopower reads the six pairs in sFlailHpScaleToPowerTable
    // before attackcanceler. These integer boundary probes do not derive power from damage.
    private static int SourceFlailPower(int hp, int maximum)
    {
        int fraction = hp * 48 / maximum;
        return new[] { (1, 200), (4, 150), (9, 100), (16, 80), (32, 40), (48, 20) }.First(pair => fraction <= pair.Item1).Item2;
    }
    private static int? Prepared(SourceBattleEvent receipt) => receipt switch {
        SourceHpChangedEvent hit => hit.PreparedBasePower, SourceSubstituteDamagedEvent sub => sub.PreparedBasePower,
        _ => throw new InvalidOperationException("A prepared hit must remain its existing HP/substitute receipt.") };
    private static SourceBattleEvent WithPrepared(SourceBattleEvent receipt, int? power) => receipt switch {
        SourceHpChangedEvent hit => hit with { PreparedBasePower = power }, SourceSubstituteDamagedEvent sub => sub with { PreparedBasePower = power },
        _ => throw new InvalidOperationException() };

    [TestMethod, DataRow(175), DataRow(179)]
    public void FlailFamilyCarriesFrozenPowerAtEverySourceHpThresholdIncludingSubstitute(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
        {
            int maximum = Bodies(id)[0].Pokemon.Stats.MaxHp;
            var points = Enumerable.Range(1, maximum).Where(hp => hp == 1 || hp == maximum ||
                SourceFlailPower(hp, maximum) != SourceFlailPower(hp - 1, maximum) || SourceFlailPower(hp, maximum) != SourceFlailPower(hp + 1, maximum)).ToArray();
            CollectionAssert.AreEquivalent(new[] { 200, 150, 100, 80, 40, 20 }, points.Select(hp => SourceFlailPower(hp, maximum)).Distinct().ToArray());
            foreach (int hp in points)
            {
                var bodies = Bodies(id); bodies[0] = bodies[0] with { Pokemon = bodies[0].Pokemon with { CurrentHp = hp } };
                if (substitute) bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 100 } };
                var result = Envelope.Resolve(Kernel.Frame(bodies, single)); var hit = Envelope.Hits(result.Events).Single();
                Assert.AreEqual(SourceFlailPower(hp, maximum), Prepared(hit)); Assert.AreEqual(substitute, hit is SourceSubstituteDamagedEvent);
                Shared.Check(result, id, single, hit); int index = Array.FindIndex(result.Events.ToArray(), e => e is SourceMoveUsedEvent);
                Assert.AreEqual(Prepared(hit), (int?)Project(result.Events, index, single).Dispatch!.MovePower);
                var exported = SourceBattleEventAdapter.Export(hit);
                Assert.AreEqual(Prepared(hit), exported switch { WildBattleHpChangedEvent x => x.PreparedBasePower,
                    WildBattleSubstituteDamagedEvent x => x.PreparedBasePower, _ => throw new InvalidOperationException() });
            }
        }
    }

    [TestMethod, DataRow(175), DataRow(179)]
    public void FlailFamilyRequiresLegalCommittedPowerAndNeverRecomputesItAfterShellBellHealing(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[0] = bodies[0] with { HeldItem = "ITEM_SHELL_BELL", Pokemon = bodies[0].Pokemon with { CurrentHp = 1 } };
            if (substitute) bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 100 } };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single)); var events = result.Events; var hit = Envelope.Hits(events).Single();
            int at = events.IndexOf(hit), usedAt = Array.FindIndex(events.ToArray(), e => e is SourceMoveUsedEvent);
            Assert.AreEqual(200, Prepared(hit)); Shared.Check(result, id, single, hit);
            Assert.AreEqual(200, (int?)Project(events, usedAt, single).Dispatch!.MovePower);
            if (!substitute) Assert.IsTrue(result.State.Slots[0].Body!.Pokemon.CurrentHp > 1, "Shell Bell resolves after the prepared hit.");
            foreach (int? invalid in new int?[] { null, -1, 0, 1, 19, 21, 199, 201, 255 }) Deny(events.SetItem(at, WithPrepared(hit, invalid)), single);
            if (single)
            {
                var battle = Envelope.Single(events, usedAt); WildBattleEvent wrong = battle.Events[at] switch {
                    WildBattleHpChangedEvent x => x with { PreparedBasePower = null },
                    WildBattleSubstituteDamagedEvent x => x with { PreparedBasePower = null }, _ => throw new InvalidOperationException() };
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with { Events = battle.Events.SetItem(at, wrong) }, true, SourceBattleScene.On).Dispatch);
                Assert.AreEqual(200, (int?)AnimationDispatchFixture.AtAttackText(battle with { SourceEvents = [] }, true, SourceBattleScene.On).Dispatch!.MovePower);
            }
        }
    }

}
