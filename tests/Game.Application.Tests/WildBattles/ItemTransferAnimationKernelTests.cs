using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
using Body = FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class ItemTransferAnimationKernelTests
{
    private static MoveId Move(int id) => Kernel.Id(id == 271 ? "TRICK" : "RECYCLE");
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon("MR_MIME", id == 271 ? "TRICK" : "RECYCLE") with { SpeedStage = 12, AccuracyStage = 12 };
        return bodies;
    }
    private static SourceBattleFrame Used(SourceBattleFrame frame, int actor, string? item)
    {
        var slot = frame.Slots[actor];
        return frame with { Slots = frame.Slots.SetItem(actor, slot with { Position = slot.Position with { UsedHeldItem = item } }) };
    }
    private static SourceBattleFrame Initial(int id, bool single, int actor = 0, int target = 1,
        string actorItem = "ITEM_CHARCOAL", string targetItem = "ITEM_MYSTIC_WATER")
    {
        var bodies = Bodies(id, actor);
        if (id == 271)
        {
            bodies[actor] = bodies[actor] with { HeldItem = actorItem };
            bodies[target] = bodies[target] with { HeldItem = targetItem };
        }
        var frame = Kernel.Frame(bodies, single);
        return id == 278 ? Used(frame, actor, "ITEM_RAWST_BERRY") : frame;
    }
    private static SourceBattleEvent Owner(ImmutableArray<SourceBattleEvent> events, int id) => id == 271
        ? events.OfType<SourceHeldItemsSwappedEvent>().Single() : events.OfType<SourceHeldItemRecycledEvent>().Single();
    private static void Deny(ImmutableArray<SourceBattleEvent> events, bool single) =>
        Envelope.Deny(events, single, "Only the unique owned successful item command can authorize its animation.");
    private static void Check(SourceKernelResult result, int id, bool single, SourceBattleEvent? owner)
    { Shared.Check(result, id, single, owner); CheckExtra(result.Events, id, single); }
    private static void Check(SourceFourActionResult result, int id, bool single, SourceBattleEvent? owner)
    { Shared.Check(result, id, single, owner); CheckExtra(result.Events, id, single); }
    private static void CheckExtra(ImmutableArray<SourceBattleEvent> events, int id, bool single)
    {
        Assert.IsTrue(AnimationDispatchFixture.Supports(id, Move(id).Value), "Require real generated ID/label support.");
        foreach (var row in events)
        {
            int i = events.IndexOf(row); var cue = Kernel.Project(events, i, single).Dispatch;
            if (cue is not null) { Assert.IsNull(cue.MoveTurn); Assert.IsNull(cue.MovePower); Assert.IsNull(cue.MoveDamage); }
            if (single)
            {
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Envelope.Single(events, i), false, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(Envelope.Single(events, i), false, SourceBattleScene.On).Dispatch);
            }
        }
        string source = JsonSerializer.Serialize(events);
        Assert.AreEqual(source, JsonSerializer.Serialize(JsonSerializer.Deserialize<ImmutableArray<SourceBattleEvent>>(source)));
        if (single)
        {
            var exported = events; string legacy = JsonSerializer.Serialize(exported);
            Assert.AreEqual(legacy, JsonSerializer.Serialize(JsonSerializer.Deserialize<ImmutableArray<SourceBattleEvent>>(legacy)));
        }
    }

    [TestMethod, DataRow("ITEM_CHARCOAL", "ITEM_MYSTIC_WATER"), DataRow("ITEM_CHARCOAL", "ITEM_CHARCOAL"),
        DataRow("ITEM_CHARCOAL", "ITEM_NONE"), DataRow("ITEM_NONE", "ITEM_MYSTIC_WATER")]
    public void TrickAuthenticatesSuccessfulEqualDifferentAndOneSidedSwapsAtAllLegalSlots(string actorItem, string targetItem)
    {
        foreach (bool single in new[] { false, true }) foreach (int actor in single ? new[] { 0 } : new[] { 0, 2 })
        foreach (int target in single ? new[] { 1 } : Enumerable.Range(0, 4).Where(x => x != actor).ToArray())
        {
            var frame = Initial(271, single, actor, target, actorItem, targetItem);
            var result = Envelope.Resolve(frame, actor, target); var marker = result.Events.OfType<SourceHeldItemsSwappedEvent>().Single();
            Assert.AreEqual(new BattlerSlot(actor), marker.Actor); Assert.AreEqual(new BattlerSlot(target), marker.Target);
            Assert.AreEqual(Move(271), marker.Move); Assert.AreEqual(actorItem, marker.ActorItemBefore); Assert.AreEqual(targetItem, marker.TargetItemBefore);
            Assert.AreEqual(targetItem, result.State.Slots[actor].Body!.HeldItem); Assert.AreEqual(actorItem, result.State.Slots[target].Body!.HeldItem);
            Check(result, 271, single, marker);
            if (targetItem == "ITEM_NONE") Assert.IsFalse(result.Events.OfType<SourceCalledPartyChangedEvent>().Any(e => e.Reason == "trick-party-write"),
                "One-sided giving succeeds without a conditional party-write receipt.");
        }
    }

    [TestMethod]
    public void RecycleOwnsItsExplicitSuccessAtAllPositionsWithoutRequiringAPartyWrite()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var result = Envelope.Resolve(Initial(278, single, actor), actor, actor); var marker = result.Events.OfType<SourceHeldItemRecycledEvent>().Single();
            Assert.AreEqual(new BattlerSlot(actor), marker.Actor); Assert.AreEqual(Move(278), marker.Move); Assert.AreEqual("ITEM_RAWST_BERRY", marker.Item);
            Assert.AreEqual("ITEM_RAWST_BERRY", result.State.Slots[actor].Body!.HeldItem); Assert.IsNull(result.State.Slots[actor].Position.UsedHeldItem);
            Assert.IsFalse(result.Events.OfType<SourceCalledPartyChangedEvent>().Any(e => e.Reason == "recycle-party-write"));
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            Check(result, 278, single, marker);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void RecycleUsesAnActuallyConsumedItemAndRetainsSuccessWhenImmediatelyConsumedAgain(bool consumeAgain)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Body.Body(Kernel.Frame(Bodies(278), single), 0,
                b => b with { HeldItem = "ITEM_RAWST_BERRY", Status = WildBattleStatus.Burn });
            var first = Envelope.Resolve(frame, 1, 1);
            Assert.AreEqual("ITEM_RAWST_BERRY", first.Events.OfType<SourceHeldItemConsumedEvent>().Single().Item);
            Assert.AreEqual("ITEM_RAWST_BERRY", first.State.Slots[0].Position.UsedHeldItem);
            frame = consumeAgain ? Body.Body(first.State, 0, b => b with { Status = WildBattleStatus.Burn }) : first.State;
            var result = Envelope.Resolve(frame, 0, 0); var marker = result.Events.OfType<SourceHeldItemRecycledEvent>().Single();
            Assert.AreEqual("ITEM_RAWST_BERRY", marker.Item); Check(result, 278, single, marker);
            var consumed = result.Events.OfType<SourceHeldItemConsumedEvent>().SingleOrDefault();
            Assert.AreEqual(consumeAgain, consumed is not null);
            if (consumed is not null)
            {
                Assert.IsTrue(marker.Sequence < consumed.Sequence); Assert.AreEqual(marker.Item, consumed.Item);
                Assert.AreEqual("ITEM_NONE", result.State.Slots[0].Body!.HeldItem); Assert.AreEqual(marker.Item, result.State.Slots[0].Position.UsedHeldItem);
                Deny(result.Events.Remove(marker), single);
                Deny(result.Events.SetItem(result.Events.IndexOf(marker), marker with { Item = "ITEM_CHERI_BERRY" }), single);
                Deny(Shared.Number(result.Events.Remove(consumed)), single);
            }
            else { Assert.AreEqual(marker.Item, result.State.Slots[0].Body!.HeldItem); Assert.IsNull(result.State.Slots[0].Position.UsedHeldItem); }
        }
    }

    [TestMethod, DataRow("ITEM_CHARCOAL", true), DataRow("ITEM_ORANGE_MAIL", false)]
    public void RecyclePreservesKnockOffStateAndHonorsTheExistingMailSafeguard(string knocked, bool succeeds)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Body.Body(Initial(278, single), 0, b => b with { KnockedOffItem = knocked });
            var result = Envelope.Resolve(frame, 0, 0); Assert.AreEqual(knocked, result.State.Slots[0].Body!.KnockedOffItem);
            var marker = result.Events.OfType<SourceHeldItemRecycledEvent>().SingleOrDefault(); Assert.AreEqual(succeeds, marker is not null);
            Check(result, 278, single, marker);
            if (!succeeds) Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "no-recyclable-item"));
        }
    }

    [TestMethod, DataRow("both-empty"), DataRow("actor-knocked"), DataRow("target-knocked"), DataRow("actor-mail"),
        DataRow("target-mail"), DataRow("sticky-hold"), DataRow("substitute"), DataRow("protect"), DataRow("miss")]
    public void TrickFailureNeverCreatesAnItemSuccessReceiptOrAnimation(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(271, single);
            if (branch == "both-empty") { frame = Body.Body(frame, 0, b => b with { HeldItem = "ITEM_NONE" }); frame = Body.Body(frame, 1, b => b with { HeldItem = "ITEM_NONE" }); }
            if (branch == "actor-knocked") frame = Body.Body(frame, 0, b => b with { HeldItem = "ITEM_NONE", KnockedOffItem = "ITEM_CHARCOAL" });
            if (branch == "target-knocked") frame = Body.Body(frame, 1, b => b with { HeldItem = "ITEM_NONE", KnockedOffItem = "ITEM_MYSTIC_WATER" });
            if (branch == "actor-mail") frame = Body.Body(frame, 0, b => b with { HeldItem = "ITEM_ORANGE_MAIL" });
            if (branch == "target-mail") frame = Body.Body(frame, 1, b => b with { HeldItem = "ITEM_ORANGE_MAIL" });
            if (branch == "sticky-hold") frame = Body.Body(frame, 1, b => Kernel.Mon("GULPIN", "POUND", 100, personality: 1) with { HeldItem = b.HeldItem });
            if (branch == "sticky-hold") Assert.AreEqual("ABILITY_STICKY_HOLD", WildBattleEffective.Ability(frame.Slots[1].Body!));
            if (branch == "substitute") frame = Body.Body(frame, 1, b => b with { CalledVolatile = new() { SubstituteHp = 1 } });
            if (branch == "protect") frame = Body.Body(frame, 1, b => b with { Protected = true });
            if (branch == "miss") frame = Body.Body(frame, 0, b => b with { AccuracyStage = 0 });
            var result = branch == "miss" ? Envelope.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "missed"), "Trick accuracy miss") : Envelope.Resolve(frame);
            Assert.IsFalse(result.Events.OfType<SourceHeldItemsSwappedEvent>().Any());
            Assert.IsTrue(result.Events.Any(e => e is SourceMoveFailedEvent or SourceAbilityPreventedEvent)); Check(result, 271, single, null);
        }
    }

    [TestMethod]
    public void OrdinaryOpponentTrickAndMissingOrOccupiedRecycleAreDeniedByActualKernel()
    {
        foreach (bool single in new[] { false, true })
        {
            foreach (int actor in single ? new[] { 1 } : new[] { 1, 3 })
            {
                var result = Envelope.Resolve(Initial(271, single, actor, actor ^ 1), actor, actor ^ 1);
                Assert.IsFalse(result.Events.OfType<SourceHeldItemsSwappedEvent>().Any()); Check(result, 271, single, null);
            }
            foreach (bool occupied in new[] { false, true })
            {
                var frame = occupied ? Body.Body(Initial(278, single), 0, b => b with { HeldItem = "ITEM_CHARCOAL" }) : Used(Initial(278, single), 0, null);
                var result = Envelope.Resolve(frame, 0, 0); Assert.IsFalse(result.Events.OfType<SourceHeldItemRecycledEvent>().Any()); Check(result, 278, single, null);
            }
        }
    }

    [TestMethod, DataRow(271), DataRow(278)]
    public void MissingDuplicateForeignReorderedAndCalledCommandReceiptsFailClosed(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Initial(id, single), 0, id == 278 ? 0 : 1).Events;
            var marker = Owner(events, id); int at = events.IndexOf(marker);
            Deny(events.RemoveAt(at), single); Deny(Shared.Number(events.Insert(at + 1, marker)), single);
            Deny(events.SetItem(at, marker with { Turn = marker.Turn + 1 }), single);
            Deny(events.SetItem(at, marker with { Sequence = marker.Sequence + 1 }), single);
            Deny(Shared.Number(events.Insert(at, new SourceMtMoonVolatileEvent(0, marker.Turn, new(0), "charge", 0, 2, "move"))), single);
            SourceBattleEvent[] bad = marker is SourceHeldItemRecycledEvent recycle
                ? [recycle with { Actor = new(1) }, recycle with { Move = Move(271) }, recycle with { Item = "ITEM_NONE" }, recycle with { Item = "berry" }]
                : [((SourceHeldItemsSwappedEvent)marker) with { Actor = new(1) }, ((SourceHeldItemsSwappedEvent)marker) with { Target = new(0) },
                    ((SourceHeldItemsSwappedEvent)marker) with { Move = Move(278) }, ((SourceHeldItemsSwappedEvent)marker) with { ActorItemBefore = "ITEM_NONE", TargetItemBefore = "ITEM_NONE" },
                    ((SourceHeldItemsSwappedEvent)marker) with { TargetItemBefore = "ITEM_ORANGE_MAIL" }];
            foreach (var wrong in bad) Deny(events.SetItem(at, wrong), single);
            SupportStatusAnimationKernelFixture.RejectCommonEnvelopeMutations(events, single);
            foreach (var sync in events.OfType<SourceCalledPartyChangedEvent>().Where(e => e.Reason == "active-sync"))
            {
                Deny(events.SetItem(events.IndexOf(sync), sync with { SourceSlot = null }), single);
                Deny(events.SetItem(events.IndexOf(sync), sync with { SourceSlot = new(sync.SourceSlot!.Value.Value ^ (single ? 1 : 2)) }), single);
                Deny(events.SetItem(events.IndexOf(sync), sync with { After = sync.After with { Pokemon = sync.After.Pokemon with { CurrentHp = 1 } } }), single);
            }
        }
    }

    [TestMethod]
    public void TrickPartyWritesRequireExactCommandSlotAndCompleteOrderedInstallation()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Initial(271, single)).Events;
            var writes = events.OfType<SourceCalledPartyChangedEvent>().Where(e => e.Reason is "trick-party-write" or "changed-item-applied").ToArray();
            Assert.IsTrue(writes.Length > 0);
            foreach (var write in writes)
            {
                Assert.AreEqual(new BattlerSlot(0), write.SourceSlot); int at = events.IndexOf(write);
                Deny(events.SetItem(at, write with { SourceSlot = null }), single);
                Deny(events.SetItem(at, write with { SourceSlot = new(single ? 1 : 2) }), single);
                Deny(events.SetItem(at, write with { PartyIndex = 5 }), single);
                Deny(events.SetItem(at, write with { After = write.After with { Status = 1 } }), single);
                Deny(Shared.Number(events.Insert(at + 1, write)), single);
                if (write.Reason == "changed-item-applied") Deny(Shared.Number(events.RemoveAt(at)), single);
            }
        }
    }

    [TestMethod, DataRow(271), DataRow(278)]
    public void RealOtherSlotImmunityAndBerryTailsStayOwnedAndCannotBeReorderedAcrossItemInstallation(int id)
    {
        var frame = Initial(id, false);
        // Source move-end scans all active slots, not only the selected target.
        frame = Body.Body(frame, 2, b => b with { Status = WildBattleStatus.Burn });
        frame = Body.Body(frame, 3, _ => Kernel.Mon("SNORLAX", "DEFENSE_CURL", 100) with {
            Status = WildBattleStatus.Paralysis, HeldItem = "ITEM_CHERI_BERRY" });
        var result = Envelope.Resolve(frame, 0, id == 278 ? 0 : 1);
        var ability = result.Events.OfType<SourceAbilityActivatedEvent>().Single(e => e.Side.Value == 2);
        var consumed = result.Events.OfType<SourceHeldItemConsumedEvent>().Single(e => e.Side.Value == 3);
        Check(result, id, false, Owner(result.Events, id));
        Assert.IsTrue(ability.Sequence < consumed.Sequence);
        Deny(Shared.Number(result.Events.Remove(ability)), false);
        Deny(Shared.Number(result.Events.Remove(consumed)), false);
        foreach (var sync in result.Events.OfType<SourceCalledPartyChangedEvent>().Where(e => e.Reason == "active-sync" && e.SourceSlot!.Value.Value is 2 or 3))
            Deny(result.Events.SetItem(result.Events.IndexOf(sync), sync with { SourceSlot = new(sync.SourceSlot!.Value.Value ^ 2) }), false);
        if (id == 271)
        {
            var applied = result.Events.OfType<SourceCalledPartyChangedEvent>().Single(e => e.Reason == "changed-item-applied");
            var without = result.Events.Remove(applied);
            Deny(Shared.Number(without.Insert(without.IndexOf(ability) + 1, applied)), false);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void TrickReceivedBerryCanBeConsumedAfterItsExactCommandOwnedInstallation(bool actorConsumes)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(271, single, actorItem: actorConsumes ? "ITEM_CHARCOAL" : "ITEM_RAWST_BERRY",
                targetItem: actorConsumes ? "ITEM_RAWST_BERRY" : "ITEM_MYSTIC_WATER");
            int receiver = actorConsumes ? 0 : 1;
            frame = Body.Body(frame, receiver, b => actorConsumes ? b with { Status = WildBattleStatus.Burn } :
                Kernel.Mon("SNORLAX", "DEFENSE_CURL", 100) with { HeldItem = b.HeldItem, Status = WildBattleStatus.Burn });
            var result = Envelope.Resolve(frame);
            var marker = Owner(result.Events, 271);
            var consumed = result.Events.OfType<SourceHeldItemConsumedEvent>().Single();
            Assert.AreEqual(new BattlerSlot(receiver), consumed.Side); Assert.AreEqual("ITEM_RAWST_BERRY", consumed.Item);
            Assert.IsTrue(marker.Sequence < consumed.Sequence);
            Assert.AreEqual("ITEM_NONE", result.State.Slots[receiver].Body!.HeldItem);
            Check(result, 271, single, marker);
            Deny(Shared.Number(result.Events.Remove(consumed)), single);
            if (actorConsumes)
            {
                var used = result.Events.OfType<SourceMoveUsedEvent>().Single();
                WildBattleCalledPartyMember Refund(WildBattleCalledPartyMember member) => member with { Pokemon = member.Pokemon with {
                    Moves = member.Pokemon.Moves.SetItem(used.Slot, member.Pokemon.Moves[used.Slot] with { CurrentPp = used.PpBefore }) } };
                var forged = result.Events.Select(e => e is SourceCalledPartyChangedEvent { SourceSlot: {} slot } row && slot == used.Actor
                    ? row with { Before = Refund(row.Before), After = Refund(row.After) } : e).ToImmutableArray();
                Deny(forged, single);
            }
        }
    }

    [TestMethod, DataRow(271), DataRow(278)]
    public void ItemGainOrLossCannotHideARefundedPersistentPpDelta(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Initial(id, single, targetItem: "ITEM_NONE"), 0, id == 278 ? 0 : 1).Events;
            var sync = events.OfType<SourceCalledPartyChangedEvent>().Single(e => e.Reason == "active-sync" && e.SourceSlot == new BattlerSlot(0));
            Assert.IsTrue(sync.Before.Pokemon.Moves[0].CurrentPp > sync.After.Pokemon.Moves[0].CurrentPp);
            Deny(events.SetItem(events.IndexOf(sync), sync with { After = sync.After with {
                Pokemon = sync.After.Pokemon with { Moves = sync.Before.Pokemon.Moves } } }), single);
        }
    }

    [TestMethod]
    public void ActualTransformKeepsRecyclePersistentPpUnchangedWithoutInventingARefund()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(278);
            bodies[0] = Kernel.Mon("DITTO", "TRANSFORM"); bodies[1] = Kernel.Mon("MR_MIME", "RECYCLE");
            var transformed = Envelope.Resolve(Kernel.Frame(bodies, single));
            Assert.IsTrue(transformed.Events.OfType<SourceTransformedEvent>().Any());
            var frame = Used(transformed.State, 0, "ITEM_RAWST_BERRY");
            var result = Envelope.Resolve(frame, 0, 0);
            var sync = result.Events.OfType<SourceCalledPartyChangedEvent>().Single(e => e.Reason == "active-sync" && e.SourceSlot == new BattlerSlot(0));
            Assert.AreEqual((byte)15, sync.TransientMoveSlots);
            CollectionAssert.AreEqual(sync.Before.Pokemon.Moves.ToArray(), sync.After.Pokemon.Moves.ToArray());
            Check(result, 278, single, Owner(result.Events, 278));
            Deny(result.Events.SetItem(result.Events.IndexOf(sync), sync with { TransientMoveSlots = null }), single);
        }
    }

    [TestMethod, DataRow(278)]
    public void ActualMetronomeItemCommandsKeepTheirSuccessAndAuthenticateTheCalledLeaf(int id)
    {
        uint seed = CallerPreludeAnimationKernelFixture.MetronomeSeed(id);
        foreach (bool single in new[] { false, true })
        {
            var frame = Body.Body(Initial(id, single), 0, b => Kernel.Mon("TOGETIC", "METRONOME", 29) with { HeldItem = b.HeldItem, AccuracyStage = 12 });
            var result = Envelope.Resolve(frame with { Rng = new(seed) });
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Result == Move(id)));
            Assert.IsNotNull(Owner(result.Events, id));
            CalledLeafAnimationKernelFixture.Check(result, single, Kernel.Id("METRONOME"), id);
        }
    }

    [TestMethod]
    public void TrickIsExcludedFromMetronomeButActualMirrorMoveSuccessAuthenticatesTheCalledLeaf()
    {
        Assert.IsFalse(WildBattleSourceCallers.IsDirectMetronomeChoice(271),
            "Pinned sMovesForbiddenToCopy excludes Trick before METRONOME_FORBIDDEN_END.");
        var definition = Kernel.Mechanics.Catalog.Moves[Move(271)];
        Assert.IsTrue(definition.Flags.Contains("FLAG_MIRROR_MOVE_AFFECTED"));
        // A real same-side Trick first populates Pidgey's move-end mirror history.
        // Both source participants learn their chosen moves naturally.
        var frame = Body.Body(Initial(271, false, 2, 0), 0, b =>
            Kernel.Mon("PIDGEY", "MIRROR_MOVE", 50) with { HeldItem = b.HeldItem, AccuracyStage = 12 });
        var first = Envelope.Resolve(frame, 2, 0);
        Assert.IsNotNull(Owner(first.Events, 271));
        Assert.AreEqual(Move(271), first.State.Slots[0].Registers.History!.LastTakenMove);
        var called = Envelope.Resolve(first.State, 0, 1);
        Assert.IsTrue(called.Events.OfType<SourceCallerSelectionEvent>().Any(e =>
            e.Caller == Kernel.Id("MIRROR_MOVE") && e.Result == Move(271)));
        Assert.IsNotNull(Owner(called.Events, 271));
        CalledLeafAnimationKernelFixture.Check(called, false, Kernel.Id("MIRROR_MOVE"), 271);
    }

    [TestMethod, DataRow(271), DataRow(278)]
    public void CanonicalScriptMetadataAndExportedItemReceiptsRemainTheOnlyAdmissionAuthority(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Initial(id, single), 0, id == 278 ? 0 : 1).Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); int ui = events.IndexOf(used);
            var definitions = Kernel.Mechanics.Catalog.Moves; var definition = definitions[Move(id)];
            foreach (var wrong in new[] { definition with { Power = 1 }, definition with { Target = id == 271 ? "MOVE_TARGET_USER" : "MOVE_TARGET_SELECTED" },
                definition with { SourceMoveId = 33 }, definition with { Effect = id == 271 ? "EFFECT_RECYCLE" : "EFFECT_TRICK" } })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, ui), true, SourceBattleScene.On, definitions.SetItem(Move(id), wrong)).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, ui), SourceBattleScene.On, definitions.SetItem(Move(id), wrong)).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, ui), true, SourceBattleScene.On, null).Dispatch
                : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, ui), SourceBattleScene.On, null).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(events, ui), true, SourceBattleScene.On, definitions, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, ui), SourceBattleScene.On, definitions, (_, _) => false).Dispatch);
            if (!single) continue;
            var battle = Envelope.Single(events, ui);
            Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(battle, true, SourceBattleScene.On).Dispatch);
            int at = events.IndexOf(Owner(events, id)); SourceBattleEvent corrupted = battle.Events[at] switch {
                SourceHeldItemRecycledEvent x => x with { Item = "ITEM_NONE" },
                SourceHeldItemsSwappedEvent x => x with { Actor = new BattlerSlot(1) }, _ => throw new InvalidOperationException() };
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with { Events = battle.Events.SetItem(at, corrupted) }, true, SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod, DataRow(271), DataRow(278)]
    public void PublicTurnDrainAndForwardDisplayPreserveCommandAndReconsumptionOrder(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Initial(id, single);
            if (id == 278) frame = Body.Body(frame, 0, b => b with { Status = WildBattleStatus.Burn });
            var result = Kernel.Ordered(frame, 0, id == 278 ? 0 : 1); Check(result, id, single, Owner(result.Events, id));
            var display = CampaignBattleDisplayBodies.FromSlots(frame.Slots); int steps = 0, markers = 0;
            while (true)
            {
                foreach (var row in result.Events)
                {
                    display = display.Apply(row);
                    if (row is SourceHeldItemsSwappedEvent swap)
                    {
                        markers++; Assert.AreEqual("ITEM_NONE", display.Bodies[swap.Actor.Value].HeldItem);
                        Assert.AreEqual(swap.ActorItemBefore, display.Bodies[swap.Target.Value].HeldItem);
                    }
                    if (row is SourceHeldItemRecycledEvent recycled) { markers++; Assert.AreEqual(recycled.Item, display.Bodies[recycled.Actor.Value].HeldItem); }
                    if (row is SourceHeldItemConsumedEvent consumed) Assert.AreEqual("ITEM_NONE", display.Bodies[consumed.Side.Value].HeldItem);
                }
                if (result.CompletedCleanTurn) break;
                Assert.IsTrue(++steps <= 32); Assert.IsNotNull(result.Continuation);
                result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, result.State, result.Continuation.Value);
                Assert.IsTrue(result.Succeeded, result.Fault);
            }
            Assert.AreEqual(1, markers); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            for (int i = 0; i < frame.BattlerCount; i++) Assert.AreEqual(result.State.Slots[i].Body!.HeldItem, display.Bodies[i].HeldItem);
        }
    }
}
