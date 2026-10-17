using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class WishHealingAnimationDispatchTests
{
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static MoveId Wish => Id("WISH");
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index = 0) =>
        new("wish-heal", new("test"), "test", new(), CampaignDoubleBattleStage.Combat, events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index = 0) =>
        new("wish-heal", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };
    private static CampaignWishHealingAnimationDispatch? At(ImmutableArray<SourceBattleEvent> events, int index = 0,
        bool single = false, MoveId?[]? entry = null, SourceBattleScene scene = SourceBattleScene.On, bool substitute = false) => single
        ? CampaignWishHealingAnimationPresentation.AtResult(Single(events, index), true, scene, entry ?? new MoveId?[2], substitute)
        : CampaignWishHealingAnimationPresentation.AtResult(Double(events, index), scene, entry ?? new MoveId?[4], substitute);
    private static SourceCalledDelayedEvent Resolved(int slot = 0, long sequence = 10, int caster = 0) =>
        new(sequence, 2, new(slot), Wish, 1, 0, "resolved", caster);
    private static SourceRecoveredEvent Recovery(int slot = 0, long sequence = 11) =>
        new(sequence, 2, new(slot), Wish, 7, 27, 20);
    private static SourceMoveFailedEvent Full(int slot = 0, long sequence = 11) =>
        new(sequence, 2, new(slot), new(slot), Wish, "already-full-hp");
    private static SourcePpCommandEvent Pp(int slot, string move, long sequence, int turn = 2) =>
        new(sequence, turn, new(slot), new(slot ^ 1), 0, Id("METRONOME"), Id(move), 10, 9, 1, 0);
    private static SourceMtMoonVolatileEvent Charge(int slot, long sequence, int after, string reason = "move") =>
        new(sequence, 2, new(slot), "called-charge", after == 0 ? 1 : 0, after, reason);

    [TestMethod]
    public void AdjacentRecoveryOwnsExactlyOneGeneralAnimationAtEveryRecipient()
    {
        foreach (bool single in new[] { true, false }) for (int slot = 0; slot < (single ? 2 : 4); slot++)
        {
            ImmutableArray<SourceBattleEvent> events = [Resolved(slot, caster: 2), Recovery(slot)];
            string before = JsonSerializer.Serialize(events);
            var kind = CampaignWishResolutionPresentation.At(events, 0); Assert.IsNotNull(kind);
            Assert.AreEqual(CampaignWishResolutionKind.Recovery, kind.Kind);
            Assert.AreEqual(slot, kind.Affected.Value); Assert.AreEqual(2, kind.CasterPartyIndex); Assert.AreEqual(11L, kind.ReceiptSequence);
            var dispatch = At(events, single: single); Assert.IsNotNull(dispatch);
            Assert.AreEqual("wish-heal", dispatch.BattleId); Assert.AreEqual(10L, dispatch.Sequence);
            Assert.AreEqual(11L, dispatch.ReceiptSequence); Assert.AreEqual(slot, dispatch.Affected.Value);
            Assert.AreEqual(dispatch, At(events, single: single)); Assert.IsNull(At(events, 1, single));
            if (single)
            {
                var exported = events.Select(SourceBattleEventAdapter.Export).ToImmutableArray();
                var projected = CampaignWishResolutionPresentation.At(exported, 0); Assert.IsNotNull(projected);
                Assert.AreEqual(kind, projected);
            }
            Assert.AreEqual(before, JsonSerializer.Serialize(events));
        }
    }

    [TestMethod]
    public void FullHpAndZeroHpExpiryClassifyWithoutAdmittingHealingAnimation()
    {
        for (int slot = 0; slot < 4; slot++)
        {
            foreach (var events in new ImmutableArray<SourceBattleEvent>[] {
                [Resolved(slot), Full(slot)], [Resolved(slot)],
                [Resolved(slot), new SourceTurnReadyEvent(11, 2, 31)] })
            {
                var classified = CampaignWishResolutionPresentation.At(events, 0); Assert.IsNotNull(classified);
                Assert.AreEqual(events.Length > 1 && events[1] is SourceMoveFailedEvent
                    ? CampaignWishResolutionKind.FullHp : CampaignWishResolutionKind.Expired, classified.Kind);
                for (int index = 0; index < events.Length; index++) Assert.IsNull(At(events, index));
                if (slot < 2) Assert.IsNull(At(events, single: true));
            }
        }
    }

    [TestMethod]
    public void SetupCountdownOtherMovesAndNoncurrentReceiptsNeverBecomeDelayedHealing()
    {
        var resolved = Resolved();
        foreach (var first in new[] { resolved with { Before = 0, After = 2, Reason = "scheduled" },
            resolved with { Before = 2, After = 1, Reason = "countdown" }, resolved with { Move = Id("FUTURE_SIGHT") },
            resolved with { Move = Id("DOOM_DESIRE") } })
        {
            ImmutableArray<SourceBattleEvent> events = [first, Recovery()];
            Assert.IsNull(CampaignWishResolutionPresentation.At(events, 0));
            Assert.IsNull(At(events)); Assert.IsNull(At(events, 1));
        }
        ImmutableArray<SourceBattleEvent> prior = [resolved, Recovery(), new SourceTurnReadyEvent(12, 2, 31)];
        Assert.IsNotNull(At(prior)); Assert.IsNull(At(prior, 1)); Assert.IsNull(At(prior, 2));
        Assert.IsNull(CampaignWishResolutionPresentation.At(prior, -1));
        Assert.IsNull(CampaignWishResolutionPresentation.At(prior, prior.Length));
    }

    [TestMethod]
    public void MissingOrMalformedCasterAndResolutionMetadataFailClosed()
    {
        var good = Resolved();
        foreach (var malformed in new[] { good with { SourcePartyIndex = null }, good with { SourcePartyIndex = -1 },
            good with { SourcePartyIndex = 6 }, good with { Before = 0 }, good with { Before = 2 }, good with { After = 1 } })
        {
            ImmutableArray<SourceBattleEvent> events = [malformed, Recovery()];
            var classified = CampaignWishResolutionPresentation.At(events, 0); Assert.IsNotNull(classified);
            Assert.AreEqual(CampaignWishResolutionKind.Invalid, classified.Kind);
            Assert.IsNull(At(events)); Assert.IsNull(At(events, single: true));
        }
    }

    [TestMethod]
    public void RecoveryMustMatchTheImmediateTurnSequenceRecipientAndPositiveHpDelta()
    {
        var good = Recovery();
        foreach (var malformed in new[] { good with { Sequence = 10 }, good with { Sequence = 12 }, good with { Turn = 3 },
            good with { Side = new(1) }, good with { Move = Id("RECOVER") }, good with { HpBefore = 0 },
            good with { HpAfter = 7, Recovered = 0 }, good with { Recovered = 19 } })
        {
            ImmutableArray<SourceBattleEvent> events = [Resolved(), malformed];
            var classified = CampaignWishResolutionPresentation.At(events, 0); Assert.IsNotNull(classified);
            Assert.AreEqual(CampaignWishResolutionKind.Invalid, classified.Kind);
            Assert.IsNull(At(events)); Assert.IsNull(At(events, single: true));
        }
        ImmutableArray<SourceBattleEvent> interrupted = [Resolved(), new SourceTurnReadyEvent(11, 2, 31), Recovery(sequence: 12)];
        Assert.IsNull(At(interrupted)); Assert.IsNull(At(interrupted, 2));
    }

    [TestMethod]
    public void FullHpFailureMustBeTheAdjacentSelfTargetWishFailure()
    {
        var good = Full();
        foreach (var malformed in new[] { good with { Sequence = 12 }, good with { Turn = 3 }, good with { Actor = new(1) },
            good with { Target = new(1) }, good with { Move = Id("RECOVER") }, good with { Reason = "wish-slot-occupied" } })
        {
            ImmutableArray<SourceBattleEvent> events = [Resolved(), malformed];
            var classified = CampaignWishResolutionPresentation.At(events, 0); Assert.IsNotNull(classified);
            Assert.AreEqual(CampaignWishResolutionKind.Invalid, classified.Kind); Assert.IsNull(At(events));
        }
    }

    [TestMethod]
    public void SceneSubstituteSourceRulesAndPhaseGatesLeaveTheRecoveryAndTextReceiptIntact()
    {
        ImmutableArray<SourceBattleEvent> events = [Resolved(), Recovery()]; string before = JsonSerializer.Serialize(events);
        foreach (bool single in new[] { true, false })
        {
            Assert.IsNull(At(events, single: single, scene: SourceBattleScene.Off));
            Assert.IsNull(At(events, single: single, substitute: true));
            Assert.AreEqual(CampaignWishResolutionKind.Recovery, CampaignWishResolutionPresentation.At(events, 0)!.Kind);
        }
        Assert.IsNull(CampaignWishHealingAnimationPresentation.AtResult(Single(events), false, SourceBattleScene.On, new MoveId?[2]));
        Assert.IsNull(CampaignWishHealingAnimationPresentation.AtResult(Double(events) with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On, new MoveId?[4]));
        Assert.AreEqual(before, JsonSerializer.Serialize(events));
    }

    [TestMethod]
    public void EntryChargeSnapshotAndEarlierChargeReceiptsGateTheCurrentRecipientOnly()
    {
        foreach (string move in new[] { "FLY", "BOUNCE", "DIG", "DIVE" }) for (int slot = 0; slot < 4; slot++)
        {
            MoveId?[] entry = new MoveId?[4]; entry[slot] = Id(move); var saved = entry.ToArray();
            ImmutableArray<SourceBattleEvent> events = [Resolved(slot), Recovery(slot)];
            Assert.IsNull(At(events, entry: entry));
            Assert.IsNotNull(At([Charge(slot, 9, 0, "released"), .. events], 1, entry: entry));
            Assert.IsNotNull(At([new SourceCalledPartySwitchedEvent(9, 2, new(slot), 0, 2, WildBattleCalledSwitchKind.Voluntary), .. events], 1, entry: entry));
            Assert.IsNull(At([.. events, Charge(slot, 12, 0, "released")], entry: entry));
            Assert.IsNull(At([Pp(slot, move, 8), Charge(slot, 9, 1), .. events], 2));
            Assert.IsNotNull(At([.. events, Pp(slot, move, 12), Charge(slot, 13, 1)]));
            CollectionAssert.AreEqual(saved, entry);
        }
        foreach (string move in new[] { "RAZOR_WIND", "SOLAR_BEAM", "SKULL_BASH", "SKY_ATTACK" })
            Assert.IsNotNull(At([Resolved(), Recovery()], entry: [Id(move), null, null, null]));
        Assert.IsNull(At([Resolved(), Recovery()], single: true, entry: [Id("DIG"), null]));
        Assert.IsNull(At([Charge(0, 9, 1), Resolved(), Recovery()], 1));
        Assert.IsNull(At([Pp(0, "FLY", 8, turn: 1), Charge(0, 9, 1), Resolved(), Recovery()], 2));
    }

    [TestMethod]
    public void EntryChargeShapeMustMatchTheActualBattleLayout()
    {
        ImmutableArray<SourceBattleEvent> events = [Resolved(), Recovery()];
        Assert.Throws<ArgumentException>(() => CampaignWishHealingAnimationPresentation.AtResult(Double(events), SourceBattleScene.On, new MoveId?[2]));
        Assert.Throws<ArgumentNullException>(() => CampaignWishHealingAnimationPresentation.AtResult(Double(events), SourceBattleScene.On, null!));
        Assert.Throws<ArgumentException>(() => CampaignWishHealingAnimationPresentation.AtResult(Single(events), true, SourceBattleScene.On, new MoveId?[4]));
    }

    [TestMethod]
    public void LaterItemAndStatReceiptsCannotLendOrRevokeWishRecoveryOwnership()
    {
        var berry=new SourceHeldItemConsumedEvent(12,2,new(0),"ITEM_SITRUS_BERRY",27,57,30);
        var stat=new SourceStatChangedEvent(13,2,new(0),new(0),WildBattleStat.Attack,6,7);
        ImmutableArray<SourceBattleEvent> valid=[Resolved(),Recovery(),berry,stat];
        Assert.IsNotNull(At(valid));for(int i=1;i<valid.Length;i++)Assert.IsNull(At(valid,i));
        ImmutableArray<SourceBattleEvent> borrowed=[Resolved(),berry with{Sequence=11},Recovery(sequence:12)];
        Assert.IsNull(At(borrowed));Assert.IsNull(At(borrowed,2));
        Assert.AreEqual(CampaignWishResolutionKind.Expired,CampaignWishResolutionPresentation.At(borrowed,0)!.Kind);
        ImmutableArray<SourceBattleEvent> duplicate=[Resolved(),Recovery(),Resolved(sequence:12),Recovery(sequence:13)];
        Assert.IsNotNull(At(duplicate));Assert.IsNull(At(duplicate,2));
    }

    [TestMethod]
    public void MissingCasterPartyMemberFailsClosedWithoutReadingFutureBodyState()
    {
        var frame=Frame(Bodies(0,true));
        for(int slot=0;slot<4;slot++)
        {
            Assert.IsTrue(CampaignWishResolutionPresentation.CasterAvailable(frame,new(slot),0));
            Assert.IsTrue(CampaignWishResolutionPresentation.CasterAvailable(frame,new(slot),1));
            foreach(int? member in new int?[]{null,-1,2,6})Assert.IsFalse(CampaignWishResolutionPresentation.CasterAvailable(frame,new(slot),member));
        }
    }

    private static WildBattleCombatant Mon(string species, int level, params string[] names)
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var moves = names.Select(name => {
            var move = Id(name);
            Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
                $"Natural source learnset required: {species}/{level}/{name}.");
            int pp = Mechanics.Catalog.Moves[move].Pp; return new PokemonMoveSlot(move, pp, pp);
        }).ToImmutableArray();
        return new(pokemon with { Moves = moves });
    }
    private static WildBattleCombatant[] Bodies(int actor, bool injured)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Mon("SNORLAX", 100, "DEFENSE_CURL")).ToArray();
        var caster = Mon("TOGETIC", 29, "WISH");
        bodies[actor] = caster with { SpeedStage = 12, Pokemon = caster.Pokemon with { CurrentHp = injured ? 1 : caster.Pokemon.CurrentHp } };
        return bodies;
    }
    private static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
        new(WildBattleTransformationRules.PersistentPokemon(body), body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single = false, int? reserveSide = null, WildBattleCombatant? reserve = null)
    {
        ImmutableArray<WildBattleCalledPartyMember> players = single ? [Member(bodies[0])] : [Member(bodies[0]), Member(bodies[2])];
        ImmutableArray<WildBattleCalledPartyMember> opponents = single ? [Member(bodies[1])] : [Member(bodies[1]), Member(bodies[3])];
        if (reserveSide == 0) players = players.Add(Member(reserve!));
        if (reserveSide == 1) opponents = opponents.Add(Member(reserve!));
        SourceBattleFrame frame = new() {
            Layout = single ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = single ? KoReplacementTiming.SourceImmediate : KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = single ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer,
            SourceAbsentMask = (byte)(single ? 12 : 0), TurnStartAbsentMask = (byte)(single ? 12 : 0), Rng = new(31),
            Parties = [new(BattleSide.Player, players), new(BattleSide.Opponent, opponents)],
            Slots = Enumerable.Range(0, 4).Select(i => single && i >= 2 ? new SourceBattleSlot(new(i), null, 0, null)
                : new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(31) };
    }
    private static ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame frame, int selected = -1, int moveSlot = 0, int? target = null) =>
        frame.Slots.Take(frame.BattlerCount).Where(c => frame.CanAct(c.Slot)).Select(c => {
            int position = c.Slot.Value == selected ? moveSlot : 0;
            var move = Mechanics.Catalog.Moves[c.Body!.Pokemon.Moves[position].Move];
            var recipient = c.Slot.Value == selected && target is { } value ? new BattlerSlot(value)
                : move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot : c.Slot.Opposite;
            return new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, position, recipient);
        }).ToImmutableArray();
    private static List<(SourceBattleFrame Before, SourceFourActionResult Result)> Turn(SourceBattleFrame frame, ImmutableArray<SourceActionSelection>? choices = null)
    {
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices ?? Choices(frame));
        List<(SourceBattleFrame Before, SourceFourActionResult Result)> batches = [];
        for (int step = 0; step < 32; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            batches.Add((frame, result));
            if (result.CompletedCleanTurn) return batches;
            Assert.IsNotNull(result.Continuation); frame = result.State;
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, frame, result.Continuation.Value);
        }
        Assert.Fail("The public action/end-turn lifecycle did not complete."); return batches;
    }
    private static MoveId?[] EntryCharges(SourceBattleFrame frame) => frame.Slots.Take(frame.BattlerCount).Select(c =>
        c.Registers.Turn?.Lock is { Kind: WildBattleCalledLockKind.Charge } charge ? (MoveId?)charge.Move : null).ToArray();
    private static void CheckBatch((SourceBattleFrame Before, SourceFourActionResult Result) batch, int actor, bool single, bool injured)
    {
        var result = batch.Result; string original = JsonSerializer.Serialize(result); var rng = result.State.Rng;
        var events = result.Events; var resolved = events.OfType<SourceCalledDelayedEvent>().Single(e => e.Move == Wish && e.Reason == "resolved");
        int index = events.IndexOf(resolved); var kind = CampaignWishResolutionPresentation.At(events, index); Assert.IsNotNull(kind);
        Assert.AreEqual(injured ? CampaignWishResolutionKind.Recovery : CampaignWishResolutionKind.FullHp, kind.Kind);
        Assert.AreEqual(actor, resolved.Target.Value); Assert.AreEqual(1, resolved.Before); Assert.AreEqual(0, resolved.After);
        int count = 0;
        for (int i = 0; i < events.Length; i++)
        {
            var dispatch = At(events, i, single, EntryCharges(batch.Before));
            Assert.AreEqual(dispatch, At(events, i, single, EntryCharges(batch.Before)));
            Assert.IsNull(At(events, i, single, EntryCharges(batch.Before), SourceBattleScene.Off));
            if (dispatch is null) continue;
            count++; Assert.AreEqual(index, i); Assert.AreEqual(actor, dispatch.Affected.Value);
            Assert.AreEqual(resolved.Sequence, dispatch.Sequence); Assert.AreEqual(resolved.Sequence + 1, dispatch.ReceiptSequence);
        }
        Assert.AreEqual(injured ? 1 : 0, count);
        Assert.AreEqual(original, JsonSerializer.Serialize(result)); Assert.AreEqual(rng, result.State.Rng);
    }

    [TestMethod]
    public void OrdinaryPublicTurnsProduceRealRecoveryAndFullHpReceiptsAtEveryPosition()
    {
        foreach (bool single in new[] { true, false }) foreach (bool injured in new[] { true, false })
        for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var first = Turn(Frame(Bodies(actor, injured), single));
            Assert.AreEqual(1, first.SelectMany(b => b.Result.Events).OfType<SourceCalledDelayedEvent>().Count(e => e.Move == Wish && e.Reason == "countdown"));
            var second = Turn(first[^1].Result.State);
            var batch = second.Single(b => b.Result.Events.OfType<SourceCalledDelayedEvent>().Any(e => e.Move == Wish && e.Reason == "resolved"));
            CheckBatch(batch, actor, single, injured);
            Assert.IsNull(second[^1].Result.State.Slots[actor].Position.Wish);
        }
    }

    [TestMethod]
    public void RealVoluntarySwitchHealsTheNewOccupantUsingItsMaxHpAndKeepsTheOriginalCaster()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            var reserve = Mon("SNORLAX", 100, "DEFENSE_CURL"); reserve = reserve with { Pokemon = reserve.Pokemon with { CurrentHp = 1 } };
            var start = Frame(Bodies(actor, false), reserveSide: actor & 1, reserve: reserve);
            var first = Turn(start); var frame = first[^1].Result.State;
            var selection = Choices(frame).Select(c => c.Actor.Slot.Value == actor
                ? new SourceActionSelection(c.Actor, SourceActionKind.Switch, Incoming: new((BattleSide)(actor & 1), 2)) : c).ToImmutableArray();
            var second = Turn(frame, selection);
            Assert.IsTrue(second.SelectMany(b => b.Result.Events).OfType<SourceCalledPartySwitchedEvent>().Any(e => e.Side.Value == actor));
            var batch = second.Single(b => b.Result.Events.OfType<SourceCalledDelayedEvent>().Any(e => e.Move == Wish && e.Reason == "resolved"));
            CheckBatch(batch, actor, false, true);
            var resolved = batch.Result.Events.OfType<SourceCalledDelayedEvent>().Single(e => e.Move == Wish && e.Reason == "resolved");
            Assert.AreEqual(actor / 2, resolved.SourcePartyIndex);
            var recovered = batch.Result.Events.OfType<SourceRecoveredEvent>().Single(e => e.Move == Wish);
            Assert.AreEqual(1, recovered.HpBefore); Assert.AreEqual(reserve.Pokemon.Stats.MaxHp / 2, recovered.Recovered);
            Assert.IsTrue(recovered.Recovered > start.Slots[actor].Body!.Pokemon.Stats.MaxHp / 2);
            Assert.AreEqual(2, second[^1].Result.State.Slots[actor].Member!.Value.Index);
        }
    }

    [TestMethod]
    public void ActualSecondTurnKoExpiresWishSilentlyWithoutHealingTheZeroHpRecipient()
    {
        foreach (int actor in new[] { 0, 2 })
        {
            var bodies = Bodies(actor, true); bodies[1] = Mon("UMBREON", 100, "TAIL_WHIP", "FAINT_ATTACK");
            var first = Turn(Frame(bodies)); var frame = first[^1].Result.State;
            var second = Turn(frame, Choices(frame, selected: 1, moveSlot: 1, target: actor));
            Assert.IsTrue(second.SelectMany(b => b.Result.Events).OfType<SourceFaintedEvent>().Any(e => e.Side.Value == actor));
            var batch = second.Single(b => b.Result.Events.OfType<SourceCalledDelayedEvent>().Any(e => e.Move == Wish && e.Reason == "resolved"));
            string original = JsonSerializer.Serialize(batch.Result);
            var resolved = batch.Result.Events.OfType<SourceCalledDelayedEvent>().Single(e => e.Move == Wish && e.Reason == "resolved");
            int index = batch.Result.Events.IndexOf(resolved);
            Assert.AreEqual(CampaignWishResolutionKind.Expired, CampaignWishResolutionPresentation.At(batch.Result.Events, index)!.Kind);
            Assert.IsNull(At(batch.Result.Events, index, entry: EntryCharges(batch.Before)));
            Assert.IsFalse(batch.Result.Events.OfType<SourceRecoveredEvent>().Any(e => e.Move == Wish));
            Assert.IsFalse(batch.Result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == Wish && e.Reason == "already-full-hp"));
            Assert.AreEqual(0, second[^1].Result.State.Slots[actor].Body!.Pokemon.CurrentHp);
            Assert.IsNull(second[^1].Result.State.Slots[actor].Position.Wish);
            Assert.AreEqual(original, JsonSerializer.Serialize(batch.Result));
        }
    }
}
