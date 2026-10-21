using System.Collections.Immutable;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class ItemTransferReceiptPresentationTests
{
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() =>
        SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static MoveId Move(string name) => new("pret:MOVE_" + name);

    [TestMethod]
    [DataRow("ITEM_FOCUS_BAND", "ITEM_KINGS_ROCK")]
    [DataRow("ITEM_FOCUS_BAND", "ITEM_FOCUS_BAND")]
    [DataRow("ITEM_FOCUS_BAND", "ITEM_NONE")]
    [DataRow("ITEM_NONE", "ITEM_KINGS_ROCK")]
    public void SuccessfulTrickPublicTurnPrintsSourceSuccessExactlyOnce(
        string actorItem, string targetItem)
    {
        var actor = Natural("MR_MIME", 50, "TRICK") with { HeldItem = actorItem, SpeedStage = 12, AccuracyStage = 12 };
        var target = Natural("WAILORD", 100, "SPLASH") with { HeldItem = targetItem, SpeedStage = 0 };
        var start = Initial(actor, target);
        var result = CompleteTurn(start, 0);
        var receipt = result.Events.OfType<SourceHeldItemsSwappedEvent>().Single();
        Assert.AreEqual(new BattlerSlot(0), receipt.Actor);
        Assert.AreEqual(new BattlerSlot(1), receipt.Target);
        Assert.AreEqual(Move("TRICK"), receipt.Move);
        Assert.AreEqual(actorItem, receipt.ActorItemBefore);
        Assert.AreEqual(targetItem, receipt.TargetItemBefore);
        Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == receipt.Move));
        Assert.AreEqual(targetItem, result.State.Slots[0].Body!.HeldItem);
        Assert.AreEqual(actorItem, result.State.Slots[1].Body!.HeldItem);
        Assert.AreEqual(targetItem, WildBattleSourcePartyEffects.PersistentHeldItem(result.State.Parties[0].Members[0]));
        Assert.AreEqual(actorItem, WildBattleSourcePartyEffects.PersistentHeldItem(result.State.Parties[1].Members[0]));
        Assert.IsTrue(result.Events.OfType<SourcePpCommandEvent>().Single(p => p.CurrentMove == receipt.Move).Sequence < receipt.Sequence);

        var exported = result.Events;
        var single = exported.OfType<SourceHeldItemsSwappedEvent>().Single();
        Assert.AreEqual(receipt.Sequence, single.Sequence);
        Assert.AreEqual(receipt.Turn, single.Turn);
        Assert.AreEqual(new BattlerSlot(0), single.Actor);
        Assert.AreEqual(new BattlerSlot(1), single.Target);
        Assert.AreEqual(receipt.Move, single.Move);
        Assert.AreEqual(actorItem, single.ActorItemBefore);
        Assert.AreEqual(targetItem, single.TargetItemBefore);
        var expected = ExpectedSwap("Player", "Opponent", actorItem, targetItem);
        AssertNarrationAt(exported, single, expected);
        AssertSuccessNarrationExactlyOnce(exported, expected);
        AssertBookkeepingIsSilent(exported);
        if (targetItem == "ITEM_NONE")
            Assert.IsFalse(result.Events.OfType<SourceCalledPartyChangedEvent>().Any(e => e.Reason == "trick-party-write"),
                "The public one-sided giving branch must prove success text without an optional party-write receipt.");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RecycleAfterActualConsumptionDrainsRestorationAndReconsumptionMessages(bool reconsume)
    {
        // The prior turn really consumes the berry. No synthetic UsedHeldItem,
        // fabricated result receipt, or direct call to an internal recipe is used.
        var actor = Natural("MR_MIME", 50, "RECYCLE", "BARRIER") with {
            HeldItem = "ITEM_ORAN_BERRY", SpeedStage = 12
        };
        actor = actor with { Pokemon = actor.Pokemon with {
            CurrentHp = reconsume ? 1 : actor.Pokemon.Stats.MaxHp / 2
        } };
        var initial = Initial(actor, Natural("WAILORD", 100, "SPLASH") with { SpeedStage = 0 });
        Assert.AreEqual("ITEM_ORAN_BERRY", initial.Slots[0].Body!.HeldItem);
        Assert.IsNull(initial.Slots[0].Position.UsedHeldItem);
        var consumed = CompleteTurn(initial, 1);
        var firstConsumption = consumed.Events.OfType<SourceHeldItemConsumedEvent>().Single();
        Assert.AreEqual(new BattlerSlot(0), firstConsumption.Side);
        Assert.AreEqual("ITEM_ORAN_BERRY", firstConsumption.Item);
        Assert.AreEqual("ITEM_NONE", consumed.State.Slots[0].Body!.HeldItem);
        Assert.AreEqual("ITEM_ORAN_BERRY", consumed.State.Slots[0].Position.UsedHeldItem);
        DrainSharedMessages(consumed.Events);

        var recycled = CompleteTurn(consumed.State, 0);
        var receipt = recycled.Events.OfType<SourceHeldItemRecycledEvent>().Single();
        Assert.AreEqual(new BattlerSlot(0), receipt.Actor);
        Assert.AreEqual(Move("RECYCLE"), receipt.Move);
        Assert.AreEqual("ITEM_ORAN_BERRY", receipt.Item);
        Assert.IsFalse(recycled.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == receipt.Move));
        Assert.IsTrue(recycled.Events.OfType<SourcePpCommandEvent>().Single(p => p.CurrentMove == receipt.Move).Sequence < receipt.Sequence);
        var laterConsumption = recycled.Events.OfType<SourceHeldItemConsumedEvent>().ToArray();
        Assert.AreEqual(reconsume ? 1 : 0, laterConsumption.Length);
        if (reconsume)
        {
            Assert.AreEqual(receipt.Actor, laterConsumption[0].Side);
            Assert.AreEqual(receipt.Item, laterConsumption[0].Item);
            Assert.IsTrue(receipt.Sequence < laterConsumption[0].Sequence);
            Assert.AreEqual("ITEM_NONE", recycled.State.Slots[0].Body!.HeldItem);
            Assert.AreEqual(receipt.Item, recycled.State.Slots[0].Position.UsedHeldItem);
        }
        else
        {
            Assert.AreEqual(receipt.Item, recycled.State.Slots[0].Body!.HeldItem);
            Assert.IsNull(recycled.State.Slots[0].Position.UsedHeldItem);
        }
        var exported = recycled.Events;
        var single = exported.OfType<SourceHeldItemRecycledEvent>().Single();
        Assert.AreEqual(receipt.Sequence, single.Sequence);
        Assert.AreEqual(receipt.Turn, single.Turn);
        Assert.AreEqual(new BattlerSlot(0), single.Actor);
        Assert.AreEqual(receipt.Move, single.Move);
        Assert.AreEqual(receipt.Item, single.Item);
        SourceTrainerText[] expected = [new("sText_XFoundOneY", ("B_ATK_NAME_WITH_PREFIX", "Player"), ("B_LAST_ITEM", receipt.Item))];
        AssertNarrationAt(exported, single, expected);
        AssertSuccessNarrationExactlyOnce(exported, expected);
        AssertBookkeepingIsSilent(exported);
        Assert.IsFalse(recycled.Events.OfType<SourceCalledPartyChangedEvent>().Any(e => e.Reason == "recycle-party-write"),
            "Real consumption and restoration must not rely on an optional permanent-party-write receipt.");
        if (reconsume)
        {
            int markerIndex = exported.IndexOf(single);
            int consumedIndex = Enumerable.Range(0, exported.Length).Single(i => exported[i] is SourceHeldItemConsumedEvent);
            Assert.IsTrue(markerIndex < consumedIndex, "Found-one narration belongs before later consumption.");
        }
    }

    [TestMethod]
    [DataRow(WildBattleSide.Player)]
    [DataRow(WildBattleSide.Opponent)]
    public void TypedItemTransferReceiptsOwnSuccessForEitherExportedActor(WildBattleSide actor)
    {
        var slot = new BattlerSlot((int)actor); var target = slot.Opposite;
        SourceBattleEvent[] events = [
            new SourceHeldItemsSwappedEvent(1, 1, slot, target, Move("TRICK"), "ITEM_FOCUS_BAND", "ITEM_NONE"),
            new SourceHeldItemRecycledEvent(2, 1, slot, Move("RECYCLE"), "ITEM_ORAN_BERRY")
        ];
        AssertNarrationAt(events, events[0], ExpectedSwap(actor.ToString(), target.Side.ToString(), "ITEM_FOCUS_BAND", "ITEM_NONE"));
        AssertNarrationAt(events, events[1], [new("sText_XFoundOneY",
            ("B_ATK_NAME_WITH_PREFIX", actor.ToString()), ("B_LAST_ITEM", "ITEM_ORAN_BERRY"))]);
        Assert.AreEqual(3, DrainSharedMessages(events).Length);
    }

    [TestMethod]
    [DataRow("ITEM_FOCUS_BAND", "ITEM_KINGS_ROCK")]
    [DataRow("ITEM_FOCUS_BAND", "ITEM_FOCUS_BAND")]
    [DataRow("ITEM_FOCUS_BAND", "ITEM_NONE")]
    [DataRow("ITEM_NONE", "ITEM_KINGS_ROCK")]
    public void SharedSelectorPreservesPositionNamesAndSourceItemBufferOrder(string actorItem, string targetItem)
    {
        // The same pure selector is called by both UI paths. Position names are
        // already prefixed by their actual view; never collapse partner slots.
        for (int actor = 0; actor < 4; actor++) for (int target = 0; target < 4; target++)
        {
            if (actor == target) continue;
            string actorName = $"actor prefix slot {actor}", targetName = $"target prefix slot {target}";
            var display = SourceTrainerPresentationModel.SwappedItems(actorName, targetName, actorItem, targetItem, item => item);
            Assert.IsFalse(display.Unsupported);
            Assert.AreEqual(PageSnapshot(ExpectedSwap(actorName, targetName, actorItem, targetItem)), PageSnapshot(display.Texts));
            var recycled = SourceTrainerPresentationModel.RecycledItem(actorName, "ITEM_ORAN_BERRY", item => "display " + item);
            Assert.AreEqual("sText_XFoundOneY", recycled.Texts.Single().Symbol);
            Assert.AreEqual(actorName, recycled.Texts.Single().Values.Single(v => v.Key == "B_ATK_NAME_WITH_PREFIX").Value);
            Assert.AreEqual("display ITEM_ORAN_BERRY", recycled.Texts.Single().Values.Single(v => v.Key == "B_LAST_ITEM").Value);
        }
    }

    [TestMethod]
    public void MissingItemFormattingAndImpossibleEmptySuccessStayExplicitlyUnsupported()
    {
        Assert.IsTrue(SourceTrainerPresentationModel.RecycledItem("actor", "ITEM_NONE", item => item).Unsupported);
        Assert.IsTrue(SourceTrainerPresentationModel.RecycledItem("actor", "ITEM_ORAN_BERRY", null).Unsupported);
        Assert.IsTrue(SourceTrainerPresentationModel.SwappedItems("actor", "target", "ITEM_NONE", "ITEM_NONE", item => item).Unsupported);
        Assert.IsTrue(SourceTrainerPresentationModel.SwappedItems("actor", "target", "ITEM_FOCUS_BAND", "ITEM_NONE", null).Unsupported);
    }

    [TestMethod]
    public void DoublesRoutesTypedSuccessThroughTheSharedSelectorBeforeItsFallback()
    {
        // CampaignView requires Godot and is intentionally not linked into this
        // pure Presentation project. This is a source-routing guard, not a claim
        // to have executed the native doubles view or its ACK loop.
        string source = File.ReadAllText(Path.Combine(TestAssets.Root, "src/Game.Godot/Battle/Views/CampaignDoubleEventMessages.cs"));
        string compact = Regex.Replace(Regex.Replace(source, @"//[^\r\n]*", ""), @"\s+", "");
        Assert.IsTrue(compact.Contains("caseSourceHeldItemRecycledEventx:returnSourceTrainerPresentationModel.RecycledItem(DoublePrefixed(x.Actor),x.Item,DoubleItem);", StringComparison.Ordinal));
        Assert.IsTrue(compact.Contains("caseSourceHeldItemsSwappedEventx:returnSourceTrainerPresentationModel.SwappedItems(DoublePrefixed(x.Actor),DoublePrefixed(x.Target),x.ActorItemBefore,x.TargetItemBefore,DoubleItem);", StringComparison.Ordinal));
        Assert.IsTrue(compact.Contains("caseSourceCalledPartyChangedEvent:returnDoubleSilent;", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("DoublePartyMessage", StringComparison.Ordinal), "Party writes must not retain a second success narrator.");
    }

    private static SourceTrainerText[] ExpectedSwap(string actor, string target, string actorItem, string targetItem)
    {
        SourceTrainerText obtained = actorItem == "ITEM_NONE"
            ? new("sText_PkmnObtainedX", ("B_ATK_NAME_WITH_PREFIX", actor), ("B_BUFF1", targetItem))
            : targetItem == "ITEM_NONE"
                ? new("sText_PkmnObtainedX2", ("B_DEF_NAME_WITH_PREFIX", target), ("B_BUFF2", actorItem))
                : new("sText_PkmnObtainedXYObtainedZ", ("B_ATK_NAME_WITH_PREFIX", actor), ("B_BUFF1", targetItem),
                    ("B_DEF_NAME_WITH_PREFIX", target), ("B_BUFF2", actorItem));
        return [new("sText_PkmnSwitchedItems", ("B_ATK_NAME_WITH_PREFIX", actor)), obtained];
    }

    private static SourceTrainerEventDisplay? Describe(IReadOnlyList<SourceBattleEvent> events, int index) =>
        SourceTrainerPresentationModel.Describe(events, index, side => side.Side.ToString(), move => move.Value,
            (side, member) => $"{side}:{member}", item => item);

    private static void AssertNarrationAt(IReadOnlyList<SourceBattleEvent> events, SourceBattleEvent marker,
        SourceTrainerText[] expected)
    {
        int index = Enumerable.Range(0, events.Count).Single(i => events[i] == marker);
        var display = Describe(events, index);
        Assert.IsNotNull(display, $"{marker.GetType().Name} must own its source success narration.");
        Assert.IsFalse(display.Unsupported);
        Assert.IsFalse(display.Silent);
        Assert.AreEqual(PageSnapshot(expected), PageSnapshot(display.Texts));
    }

    private static bool IsSuccessText(SourceTrainerText text) => text.Symbol is
        "sText_XFoundOneY" or "sText_PkmnSwitchedItems" or "sText_PkmnObtainedX" or
        "sText_PkmnObtainedX2" or "sText_PkmnObtainedXYObtainedZ";

    private static void AssertSuccessNarrationExactlyOnce(ImmutableArray<SourceBattleEvent> events,
        SourceTrainerText[] expected)
    {
        var pages = DrainSharedMessages(events).Where(IsSuccessText);
        Assert.AreEqual(PageSnapshot(expected), PageSnapshot(pages),
            "A committed success must retain all source text exactly once, including equal items and no-write branches.");
        var withoutMarkers = events.Where(e => e is not (SourceHeldItemsSwappedEvent or SourceHeldItemRecycledEvent)).ToImmutableArray();
        Assert.IsTrue(withoutMarkers.Length < events.Length);
        Assert.IsFalse(DrainSharedMessages(withoutMarkers).Any(IsSuccessText),
            "Optional party bookkeeping must never fabricate or repeat success narration.");
    }

    private static ImmutableArray<SourceTrainerText> DrainSharedMessages(IReadOnlyList<SourceBattleEvent> events)
    {
        Assert.IsTrue(events.Count > 0, "Never pass a message-drain test with an empty event batch.");
        string before = JsonSerializer.Serialize(events);
        var pages = ImmutableArray.CreateBuilder<SourceTrainerText>();
        for (int i = 0; i < events.Count; i++)
        {
            var display = Describe(events, i);
            // Null intentionally delegates a family to the existing view. Only
            // Unsupported explicitly requests the unsupported-event notice.
            Assert.IsFalse(display?.Unsupported == true,
                $"Shared message routing rejected {events[i].GetType().Name} at {events[i].Sequence}.");
            if (display is not null) pages.AddRange(display.Texts);
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(events), "Projection must leave committed receipts untouched.");
        return pages.ToImmutable();
    }

    private static void AssertBookkeepingIsSilent(ImmutableArray<SourceBattleEvent> events)
    {
        var partyWrites = events.OfType<SourceCalledPartyChangedEvent>().ToArray();
        Assert.IsTrue(partyWrites.Length > 0, "The full public turn must include its real party synchronization receipts.");
        foreach (var write in partyWrites)
        {
            var display = Describe(events, events.IndexOf(write));
            Assert.IsNotNull(display);
            Assert.IsTrue(display.Silent, $"{write.Reason} must not print a duplicate success page.");
        }
        var withoutWrites = events.Where(e => e is not SourceCalledPartyChangedEvent).ToImmutableArray();
        Assert.AreEqual(PageSnapshot(DrainSharedMessages(events)), PageSnapshot(DrainSharedMessages(withoutWrites)));
    }

    private static string PageSnapshot(IEnumerable<SourceTrainerText> pages) =>
        JsonSerializer.Serialize(pages.Select(page => new { page.Symbol,
            Values = page.Values.Select(value => new { value.Key, value.Value }).ToArray() }));

    private static WildBattleCombatant Natural(string species, int level, params string[] names)
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var moves = names.Select(name => {
            var move = Move(name);
            Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
                $"Require an actual source level-up learner: {species}/{level}/{name}.");
            int pp = Mechanics.Catalog.Moves[move].Pp;
            return new PokemonMoveSlot(move, pp, pp);
        }).ToImmutableArray();
        return new(pokemon with { Moves = moves });
    }

    private static SourceBattleFrame Initial(WildBattleCombatant actor, WildBattleCombatant target)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = SourceBattleLayout.Singles,
            KoReplacementTiming = KoReplacementTiming.SourceImmediate,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = WildBattleCalledBattleKind.Wild,
            SourceAbsentMask = 12, TurnStartAbsentMask = 12, Rng = new(31),
            Parties = [new(BattleSide.Player, [Member(actor)]), new(BattleSide.Opponent, [Member(target)])],
            Slots = [new(new(0), new(BattleSide.Player, 0), 1, actor), new(new(1), new(BattleSide.Opponent, 0), 1, target),
                new(new(2), null, 0, null), new(new(3), null, 0, null)]
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State;
    }

    private sealed record TurnResult(SourceBattleFrame State, ImmutableArray<SourceBattleEvent> Events);
    private static TurnResult CompleteTurn(SourceBattleFrame frame, int actorMoveSlot)
    {
        string before = JsonSerializer.Serialize(frame);
        var choices = frame.Slots.Take(frame.BattlerCount).Select(cell => {
            int slot = cell.Slot.Value == 0 ? actorMoveSlot : 0;
            var move = Mechanics.Catalog.Moves[cell.Body!.Pokemon.Moves[slot].Move];
            var target = move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? cell.Slot : cell.Slot.Opposite;
            return new SourceActionSelection(cell.Occupant!.Value, SourceActionKind.Move, slot, target);
        }).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.AreEqual(0, result.State.Queue[0].Slot.Value, "Controlled speed must place the tested actor first.");
        var events = ImmutableArray.CreateBuilder<SourceBattleEvent>();
        events.AddRange(result.Events);
        int steps = 0;
        while (!result.CompletedCleanTurn)
        {
            Assert.IsTrue(++steps <= 32, "A nonfainting turn must reach its public completion boundary.");
            Assert.IsNotNull(result.Continuation);
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
            Assert.IsTrue(result.Succeeded, result.Fault);
            Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            events.AddRange(result.Events);
        }
        Assert.IsTrue(events.Count > 0);
        Assert.IsTrue(events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == 0));
        Assert.AreEqual(before, JsonSerializer.Serialize(frame), "Public turn commands must not mutate the input frame.");
        return new(result.State, events.ToImmutable());
    }
}
