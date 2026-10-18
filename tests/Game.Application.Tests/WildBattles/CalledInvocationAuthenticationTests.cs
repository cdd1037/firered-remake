using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Caller = FireRed.Tests.Application.WildBattles.CallerPreludeAnimationKernelFixture;
using Numbering = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class CalledInvocationAuthenticationTests
{
    private static object? Read(ImmutableArray<SourceBattleEvent> events, bool single,
        IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? definitions = null, int? usedIndex = null)
    {
        var type = typeof(CampaignMoveAnimationPresentation).Assembly.GetType("Game.Application.CampaignAnimationInvocation", true)!;
        var method = type.GetMethod("TryRead", BindingFlags.NonPublic | BindingFlags.Static)!;
        int index = usedIndex ?? events.IndexOf(events.OfType<SourceMoveUsedEvent>().Last());
        return method.Invoke(null, [events.ToArray(), index, single ? 2 : 4, definitions ?? Kernel.Mechanics.Catalog.Moves]);
    }
    private static object? ReadCharge(ImmutableArray<SourceBattleEvent> events, bool single, int? chargeIndex = null)
    {
        var type = typeof(CampaignMoveAnimationPresentation).Assembly.GetType("Game.Application.CampaignAnimationInvocation", true)!;
        var method = type.GetMethod("TryReadChargeSetup", BindingFlags.NonPublic | BindingFlags.Static)!;
        int index = chargeIndex ?? events.IndexOf(events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-charge" && e.After == 1));
        return method.Invoke(null, [events.ToArray(), index, single ? 2 : 4, Kernel.Mechanics.Catalog.Moves]);
    }
    private static T Value<T>(object value, string property) => (T)value.GetType().GetProperty(property)!.GetValue(value)!;
    private static object[] Ledger(object value) => ((IEnumerable)value.GetType().GetProperty("CallerPpLedger")!.GetValue(value)!).Cast<object>().ToArray();
    private static string Name(int id) => id switch { 118 => "METRONOME", 119 => "MIRROR_MOVE", 214 => "SLEEP_TALK", 267 => "NATURE_POWER", 274 => "ASSIST", _ => throw new ArgumentOutOfRangeException(nameof(id)) };

    private static SourceBattleFrame Frame(int caller, bool single, int actor = 0, bool natureDonor = false)
    {
        if (caller is 118 or 214) return Caller.Frame(caller, single, actor);
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100)).ToArray();
        bodies[actor] = Kernel.Mon(caller == 119 ? "PIDGEY" : caller == 267 ? "LOTAD" : "SKITTY", Name(caller), caller == 119 ? 100 : 50) with { AccuracyStage = 12 };
        if (caller == 119) bodies[actor ^ 1] = Kernel.Mon("SNORLAX", "TACKLE", 100) with { AccuracyStage = 12 };
        var donor = natureDonor ? Kernel.Mon("LOTAD", "NATURE_POWER") : Kernel.Mon("PIDGEY", "TACKLE");
        if (caller == 274 && !single) bodies[actor ^ 2] = donor;
        var frame = Kernel.Frame(bodies, single);
        frame = frame with { Field = frame.Field with { Terrain = WildBattleTerrain.Plain } };
        if (caller == 274 && single)
        {
            var party = frame.Parties[actor & 1];
            frame = frame with { Parties = frame.Parties.SetItem(actor & 1, party with { Members = party.Members.Add(new(donor.Pokemon)) }) };
        }
        // Execute a real prior hit to populate source mirror history.
        return caller == 119 ? Envelope.Resolve(frame, actor ^ 1, actor).State : frame;
    }

    [TestMethod, DataRow(118), DataRow(119), DataRow(214), DataRow(267), DataRow(274)]
    public void FiveNaturalCallersRetainActualLeafAndLearnedPpIdentityAtAllBattleSlots(int caller)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var result = Envelope.Resolve(Frame(caller, single, actor), actor, actor ^ 1);
            string before = JsonSerializer.Serialize(result); var events = result.Events;
            var used = events.OfType<SourceMoveUsedEvent>().Last(); var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single();
            var invocation = Read(events, single); Assert.IsNotNull(invocation);
            Assert.IsTrue(Value<bool>(invocation, "IsCalled")); Assert.AreEqual(used.Actor, Value<BattlerSlot>(invocation, "Actor"));
            Assert.AreEqual(used.Target, Value<BattlerSlot>(invocation, "Target")); Assert.AreEqual(used.Move, Value<MoveId>(invocation, "CurrentMove"));
            Assert.AreEqual(Kernel.Id(Name(caller)), Value<MoveId>(invocation, "ChosenMove"));
            Assert.AreEqual(Kernel.Id(Name(caller)), Value<MoveId>(invocation, "OriginalChosenMove"));
            Assert.AreEqual(new BattlerSlot(actor), Value<BattlerSlot>(invocation, "PpOwner"));
            Assert.AreEqual(0, Value<int>(invocation, "ChosenSlot")); Assert.AreEqual(caller == 214 ? 1 : 0, Value<int>(invocation, "PpSlot"));
            Assert.AreEqual(caller == 214 ? Kernel.Id("TACKLE") : Kernel.Id(Name(caller)), Value<MoveId>(invocation, "LearnedPpMove"));
            Assert.AreEqual(caller == 214, Value<bool>(invocation, "NoPpDeduct"));
            Assert.AreEqual(events.IndexOf(used), Value<int>(invocation, "UsedIndex"));
            Assert.AreEqual(events.IndexOf(used) - 1, Value<int>(invocation, "AttackIndex"));
            Assert.IsTrue(Value<int>(invocation, "ChainStartIndex") < events.IndexOf(transition));
            Assert.AreEqual(caller == 214 ? 1 : 0, Ledger(invocation).Length);
            Assert.AreEqual(before, JsonSerializer.Serialize(result));
        }
    }

    [TestMethod, DataRow(118), DataRow(274)]
    public void NestedNaturePowerKeepsOuterChosenSlotAndTheInheritedCalledMoveRegister(int caller)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Frame(caller, single, natureDonor: true);
            frame = frame with { Field = frame.Field with { Terrain = WildBattleTerrain.Plain } };
            if (caller == 118) frame = frame with { Rng = new(Caller.MetronomeSeed(267)) };
            var events = Envelope.Resolve(frame).Events; var transitions = events.OfType<SourceCalledMoveTransitionEvent>().ToArray();
            Assert.AreEqual(2, transitions.Length); Assert.AreEqual(Kernel.Id("NATURE_POWER"), transitions[0].Invocation.CurrentMove);
            Assert.AreEqual(Kernel.Id("SWIFT"), transitions[1].Invocation.CurrentMove);
            Assert.AreEqual(caller == 274 ? Kernel.Id("NATURE_POWER") : (MoveId?)null, transitions[1].Invocation.CalledMove);
            var invocation = Read(events, single); Assert.IsNotNull(invocation);
            Assert.IsTrue(Value<bool>(invocation, "IsCalled")); Assert.AreEqual(Kernel.Id("SWIFT"), Value<MoveId>(invocation, "CurrentMove"));
            Assert.AreEqual(Kernel.Id(Name(caller)), Value<MoveId>(invocation, "ChosenMove"));
            Assert.AreEqual(Kernel.Id(Name(caller)), Value<MoveId>(invocation, "LearnedPpMove"));
            Assert.AreEqual(0, Value<int>(invocation, "PpSlot")); Assert.IsFalse(Value<bool>(invocation, "NoPpDeduct"));
            int at = events.IndexOf(transitions[1]);
            Assert.IsNull(Read(events.SetItem(at, transitions[1] with { Invocation = transitions[1].Invocation with { CalledMove = Kernel.Id("SWIFT") } }), single));
        }
    }

    [TestMethod]
    public void SleepTalkLedgerKeepsTheActualCallerCommandAndActiveSyncChargesOnlyItsLearnedSlot()
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Envelope.Resolve(Frame(214, single)); var events = result.Events;
            var invocation = Read(events, single); Assert.IsNotNull(invocation); var entry = Ledger(invocation).Single();
            var caller = events.OfType<SourcePpCommandEvent>().Single(e => e.CurrentMove == Kernel.Id("SLEEP_TALK"));
            var leaf = events.OfType<SourcePpCommandEvent>().Single(e => e.CurrentMove == Kernel.Id("TACKLE"));
            Assert.AreSame(caller, Value<SourcePpCommandEvent>(entry, "Command"));
            Assert.AreEqual(Kernel.Id("SLEEP_TALK"), Value<MoveId>(entry, "LearnedMove")); Assert.IsFalse(Value<bool>(entry, "NoPpDeduct"));
            Assert.IsTrue(caller.Before > caller.After); Assert.AreEqual(leaf.Before, leaf.After); Assert.IsTrue(leaf.CommandCost > 0);
            var sync = events.OfType<SourceCalledPartyChangedEvent>().Single(e => e.Reason == "active-sync" && e.SourceSlot == new BattlerSlot(0));
            Assert.AreEqual(caller.Before, sync.Before.Pokemon.Moves[0].CurrentPp); Assert.AreEqual(caller.After, sync.After.Pokemon.Moves[0].CurrentPp);
            Assert.AreEqual(Kernel.Id("SLEEP_TALK"), sync.After.Pokemon.Moves[0].Move);
            Assert.AreEqual(sync.Before.Pokemon.Moves[1], sync.After.Pokemon.Moves[1]);
            int at = events.IndexOf(caller);
            Assert.IsNull(Read(events.SetItem(at, caller with { After = caller.Before }), single));
            Assert.IsNull(Read(events.SetItem(at, caller with { PpOwner = new(1) }), single));
        }
    }

    [TestMethod]
    public void MissingForeignDuplicateAndReorderedCallProvenanceCannotBecomeADirectInvocation()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Frame(118, single)).Events;
            var selection = events.OfType<SourceCallerSelectionEvent>().Single(); var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single();
            int s = events.IndexOf(selection), t = events.IndexOf(transition); var invocation = transition.Invocation;
            Assert.IsNotNull(Read(events, single));
            Assert.IsNull(Read(events.RemoveAt(s), single)); Assert.IsNull(Read(events.RemoveAt(t), single));
            Assert.IsNull(Read(Numbering.Number(events.Insert(t + 1, transition)), single));
            Assert.IsNull(Read(Numbering.Number(events.Insert(s + 1, selection)), single));
            var callerAttack = events.OfType<SourceAttackStringEvent>().First(); int a = events.IndexOf(callerAttack);
            Assert.IsNull(Read(Numbering.Number(events.Insert(a + 1, callerAttack)), single));
            Assert.IsNull(Read(events.SetItem(s, transition with { Sequence = selection.Sequence }).SetItem(t, selection with { Sequence = transition.Sequence }), single));
            foreach (var wrong in new[] { selection with { Actor = new(1) }, selection with { Result = Kernel.Id("SWIFT") },
                selection with { Caller = Kernel.Id("ASSIST") }, selection with { Turn = selection.Turn + 1 }, selection with { CandidateSourceMoveIds = [33] } })
                Assert.IsNull(Read(events.SetItem(s, wrong), single));
            foreach (var wrong in new[] { invocation with { Actor = new(1) }, invocation with { PpOwner = new(1) },
                invocation with { Target = new(0) }, invocation with { OriginalChosenMove = Kernel.Id("ASSIST") },
                invocation with { ChosenMove = Kernel.Id("ASSIST") }, invocation with { CurrentMove = Kernel.Id("SWIFT") },
                invocation with { ChosenSlot = 1 }, invocation with { PpSlot = 1 }, invocation with { CalledMove = Kernel.Id("TACKLE") },
                invocation with { Flags = WildBattleInvocationFlags.None }, invocation with { Flags = invocation.Flags | WildBattleInvocationFlags.NoPpDeduct },
                invocation with { CancellerPhase = WildBattleAttackCancellerPhase.Start } })
                Assert.IsNull(Read(events.SetItem(t, transition with { Invocation = wrong }), single));
            var used = events.OfType<SourceMoveUsedEvent>().Single(); int u = events.IndexOf(used);
            var withoutCall = events[(u - 1)..]; Assert.IsNull(Read(withoutCall, single));
            Assert.IsNull(Read(events, single, Kernel.Mechanics.Catalog.Moves.SetItem(Kernel.Id("METRONOME"),
                Kernel.Mechanics.Catalog.Moves[Kernel.Id("METRONOME")] with { Target = "MOVE_TARGET_USER" })));
        }
    }

    [TestMethod]
    public void CandidateMetadataRejectsImpossibleSourceRelationshipsAndAcceptsPreferredMirrorOutsideFallbackPool()
    {
        foreach (bool single in new[] { false, true })
        {
            foreach (int caller in new[] { 214, 267, 274 })
            {
                var events = Envelope.Resolve(Frame(caller, single)).Events; var selection = events.OfType<SourceCallerSelectionEvent>().Single(); int at = events.IndexOf(selection);
                Assert.IsNull(Read(events.SetItem(at, selection with { CandidateSourceMoveIds = caller == 267 ? [129] : [] }), single));
            }
            var mirror = Envelope.Resolve(Frame(119, single)).Events;
            var chosen = mirror.OfType<SourceCallerSelectionEvent>().Single(); int s = mirror.IndexOf(chosen);
            Assert.IsNotNull(Read(mirror.SetItem(s, chosen with { CandidateSourceMoveIds = [] }), single),
                "The source preferred LastTakenMove succeeds independently of fallback membership.");
            var meta = Envelope.Resolve(Frame(118, single)).Events;
            // Relabel a whole otherwise coherent call to Trick. Metronome's source
            // forbidden-choice authority still rejects it; no new ID list is used.
            var trick = Kernel.Id("TRICK");
            var forged = meta.Select(e => e switch {
                SourceCallerSelectionEvent x => x with { Result = trick },
                SourceCalledMoveTransitionEvent x => x with { Invocation = x.Invocation with { CurrentMove = trick } },
                SourceAttackStringEvent x when x.CurrentMove == Kernel.Id("TACKLE") => x with { CurrentMove = trick },
                SourceMoveUsedEvent x => x with { Move = trick }, _ => e }).ToImmutableArray();
            Assert.IsNull(Read(forged, single));
        }
    }

    [TestMethod]
    public void ActualCalledSubstituteKeepsItsPreAttackPpWithoutRewritingSourceOrder()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Frame(118, single) with { Rng = new(Caller.MetronomeSeed(164)) };
            var result = Envelope.Resolve(frame); string before = JsonSerializer.Serialize(result); var events = result.Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); int at = events.IndexOf(used);
            Assert.IsInstanceOfType<SourcePpCommandEvent>(events[at - 2]); Assert.IsInstanceOfType<SourceAttackStringEvent>(events[at - 1]);
            var invocation = Read(events, single); Assert.IsNotNull(invocation); Assert.IsTrue(Value<bool>(invocation, "IsCalled"));
            Assert.AreEqual(Kernel.Id("SUBSTITUTE"), Value<MoveId>(invocation, "CurrentMove"));
            Assert.AreEqual(Kernel.Id("METRONOME"), Value<MoveId>(invocation, "LearnedPpMove"));
            Assert.IsNull(Read(events.RemoveAt(at - 2), single)); Assert.AreEqual(before, JsonSerializer.Serialize(result));
        }
    }

    [TestMethod]
    public void DirectIdentityLeavesConsumptionDownstreamAndForcedStruggleUsesItsDedicatedBoundary()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100)).ToArray();
            bodies[0] = Kernel.Mon("RATTATA", "TACKLE"); var events = Envelope.Resolve(Kernel.Frame(bodies, single)).Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); int u = events.IndexOf(used); var attack = (SourceAttackStringEvent)events[u - 1];
            var invocation = Read(events, single); Assert.IsNotNull(invocation); Assert.IsFalse(Value<bool>(invocation, "IsCalled"));
            Assert.AreEqual(0, Ledger(invocation).Length); Assert.AreEqual(used.Move, Value<MoveId>(invocation, "LearnedPpMove"));
            var pp = events.OfType<SourcePpCommandEvent>().Single();
            Assert.IsNotNull(Read(events.SetItem(events.IndexOf(pp), pp with { CommandCost = 0 }), single), "This descriptor does not replace downstream leaf PP authentication.");
            Assert.IsNull(Read(events.SetItem(u - 1, attack with { ChosenMove = Kernel.Id("METRONOME") }), single));
            Assert.IsNull(Read(events.SetItem(u - 1, attack with { ChosenMove = WildBattleRules.StruggleMove, CurrentMove = WildBattleRules.StruggleMove })
                .SetItem(u, used with { Move = WildBattleRules.StruggleMove, Slot = -1, PpBefore = 0, PpAfter = 0 }), single));
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void ActualHeaderlessChargeUsesTheSameCallerAuthorityAndRealPpBoundary(bool called)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = called ? Caller.Frame(118, single, actor) with { Rng = new(Caller.MetronomeSeed(130)) }
                : Kernel.Frame(ChargeCopyAnimationKernelFixture.Bodies(130, actor), single);
            var result = Envelope.Resolve(frame, actor, actor ^ 1); var events = result.Events;
            string before = JsonSerializer.Serialize(result);
            Assert.AreEqual(0, events.OfType<SourceMoveUsedEvent>().Count());
            Assert.AreEqual(called ? 1 : 0, events.OfType<SourceAttackStringEvent>().Count());
            var pp = events.OfType<SourcePpCommandEvent>().Single(); int p = events.IndexOf(pp);
            var charge = events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-charge");
            Assert.AreEqual(p + 1, events.IndexOf(charge));
            var invocation = ReadCharge(events, single); Assert.IsNotNull(invocation);
            Assert.AreEqual(called, Value<bool>(invocation, "IsCalled"));
            Assert.AreEqual(new BattlerSlot(actor), Value<BattlerSlot>(invocation, "Actor"));
            Assert.AreEqual(pp.Target, Value<BattlerSlot>(invocation, "Target"));
            Assert.AreEqual(Kernel.Id("SKULL_BASH"), Value<MoveId>(invocation, "CurrentMove"));
            Assert.AreEqual(Kernel.Id(called ? "METRONOME" : "SKULL_BASH"), Value<MoveId>(invocation, "ChosenMove"));
            Assert.AreEqual(Value<MoveId>(invocation, "ChosenMove"), Value<MoveId>(invocation, "LearnedPpMove"));
            Assert.AreEqual(-1, Value<int>(invocation, "AttackIndex")); Assert.AreEqual(-1, Value<int>(invocation, "UsedIndex"));
            Assert.AreEqual((int?)p, Value<int?>(invocation, "PpBoundaryIndex"));
            Assert.AreEqual(p, Value<int>(invocation, "PrefixStartIndex"));
            Assert.IsFalse(Value<bool>(invocation, "NoPpDeduct")); Assert.AreEqual(0, Ledger(invocation).Length);
            Assert.IsNull(ReadCharge(events.SetItem(p, pp with { PpOwner = new(actor ^ 1) }), single));
            Assert.IsNull(ReadCharge(events.SetItem(p + 1, charge with { Side = new(actor ^ 1) }), single));
            Assert.IsNull(ReadCharge(events.SetItem(p + 1, charge with { Before = 1 }), single, p + 1));
            Assert.IsNull(ReadCharge(events.RemoveAt(p), single));
            if (called)
            {
                var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single(); int t = events.IndexOf(transition);
                var selection = events.OfType<SourceCallerSelectionEvent>().Single();
                Assert.IsNull(ReadCharge(events.RemoveAt(events.IndexOf(selection)), single));
                Assert.IsNull(ReadCharge(events.RemoveAt(t), single));
                Assert.IsNull(ReadCharge(events.SetItem(t, transition with { Invocation = transition.Invocation with { PpSlot = 1 } }), single));
                var attack = events.OfType<SourceAttackStringEvent>().Single(); int a = events.IndexOf(attack);
                Assert.IsNull(ReadCharge(Numbering.Number(events.Insert(a + 1, attack)), single));
            }
            // PP consumption and the Defense result are authenticated by ChargeSetup.
            Assert.IsNotNull(ReadCharge(events.SetItem(p, pp with { CommandCost = 0 }), single));
            Assert.AreEqual(before, JsonSerializer.Serialize(result));
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void ActualSunnySolarKeepsItsEarlyPpReceiptAndReleaseSuppression(bool called)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100)).ToArray();
            bodies[0] = Kernel.Mon("BULBASAUR", "SOLAR_BEAM", 50) with { AccuracyStage = 12 };
            var frame = called ? Caller.Frame(118, single) with { Rng = new(Caller.MetronomeSeed(76)) } : Kernel.Frame(bodies, single);
            frame = frame with { Field = frame.Field with { PermanentWeather = null, SunTurnsRemaining = 5,
                RainTurnsRemaining = 0, SandstormTurnsRemaining = 0, HailTurnsRemaining = 0 } };
            var result = Envelope.Resolve(frame); var events = result.Events; string before = JsonSerializer.Serialize(result);
            var used = events.OfType<SourceMoveUsedEvent>().Single(); int u = events.IndexOf(used);
            var prepared = (SourceMtMoonVolatileEvent)events[u - 4];
            var early = (SourcePpCommandEvent)events[u - 3];
            var released = (SourceMtMoonVolatileEvent)events[u - 2];
            var later = events.OfType<SourcePpCommandEvent>().Last();
            var invocation = Read(events, single); Assert.IsNotNull(invocation);
            Assert.AreEqual(called, Value<bool>(invocation, "IsCalled")); Assert.IsTrue(Value<bool>(invocation, "NoPpDeduct"));
            Assert.AreEqual((int?)(u - 3), Value<int?>(invocation, "EarlyLeafPpIndex"));
            Assert.IsNull(Value<int?>(invocation, "PpBoundaryIndex")); Assert.AreEqual(u - 4, Value<int>(invocation, "PrefixStartIndex"));
            Assert.AreEqual(early.After, used.PpBefore); Assert.AreEqual(later.Before, later.After); Assert.IsTrue(later.CommandCost > 0);
            var entry = Ledger(invocation).Single(); Assert.AreSame(early, Value<SourcePpCommandEvent>(entry, "Command"));
            Assert.AreEqual(Kernel.Id(called ? "METRONOME" : "SOLAR_BEAM"), Value<MoveId>(entry, "LearnedMove"));
            Assert.IsFalse(Value<bool>(entry, "NoPpDeduct")); Assert.AreEqual(Math.Max(0, early.Before - early.CommandCost), early.After);
            Assert.IsNull(Read(events.SetItem(u - 4, prepared with { Side = new(1) }), single));
            Assert.IsNull(Read(events.SetItem(u - 3, early with { PpOwner = new(1) }), single));
            Assert.IsNull(Read(events.SetItem(u - 3, early with { After = early.Before }), single));
            Assert.IsNull(Read(events.SetItem(u - 2, released with { Turn = released.Turn + 1 }), single));
            if (called)
            {
                Assert.IsNull(Read(events.RemoveAt(u - 3), single));
                Assert.IsNull(Read(events.RemoveAt(u - 2), single));
                var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single(); int t = events.IndexOf(transition);
                Assert.IsNull(Read(events.SetItem(t, transition with { Invocation = transition.Invocation with {
                    Flags = transition.Invocation.Flags | WildBattleInvocationFlags.NoPpDeduct } }), single));
            }
            Assert.AreEqual(before, JsonSerializer.Serialize(result));
        }
    }
}
