using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Battle.Moves;

[TestClass, TestCategory("Game")]
public sealed class SourceBattleMoveOrderTests
{
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static PokemonCatalog Catalog => Mechanics.Catalog;
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);

    private static WildBattleCombatant Mon(string species, params string[] names)
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), 30, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(31)).Pokemon;
        return new(pokemon with { Moves = names.Select(name => new PokemonMoveSlot(Id(name),
            Catalog.Moves[Id(name)].Pp, Catalog.Moves[Id(name)].Pp)).ToImmutableArray() });
    }

    private static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
        new(WildBattleTransformationRules.PersistentPokemon(body), body.HeldItem,
            WildBattleTransformationRules.PersistentStatus(body), KnockedOffItem: body.KnockedOffItem);

    private static SourceBattleFrame Frame(bool doubles = false, WildBattleCombatant? player = null,
        WildBattleCombatant? opponent = null)
    {
        var bodies = new[] { player ?? Mon("RATTATA", "TACKLE", "GROWL", "TAIL_WHIP"),
            opponent ?? Mon("SNORLAX", "GROWL", "DEFENSE_CURL"),
            Mon("PIDGEY", "TACKLE", "GROWL"), Mon("SPEAROW", "GROWL", "TAIL_WHIP") };
        var frame = new SourceBattleFrame {
            Layout = doubles ? SourceBattleLayout.PresetDoubles : SourceBattleLayout.Singles,
            KoReplacementTiming = doubles ? KoReplacementTiming.EndOfTurn : KoReplacementTiming.SourceImmediate,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = doubles ? WildBattleCalledBattleKind.Trainer : WildBattleCalledBattleKind.Wild,
            Rng = new(31), SourceAbsentMask = doubles ? (byte)0 : (byte)12,
            TurnStartAbsentMask = doubles ? (byte)0 : (byte)12,
            Parties = [new(BattleSide.Player, doubles ? [Member(bodies[0]), Member(bodies[2])] : [Member(bodies[0])]),
                new(BattleSide.Opponent, doubles ? [Member(bodies[1]), Member(bodies[3])] : [Member(bodies[1])])],
            Slots = Enumerable.Range(0, 4).Select(i => doubles || i < 2
                ? new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])
                : new SourceBattleSlot(new(i), null, 0, null)).ToImmutableArray()
        };
        Assert.IsNull(SourceFourActionTurns.ValidateCapability(Catalog, frame));
        return frame;
    }

    private static SourceBattleFrame Put(SourceBattleFrame frame, int index, WildBattleCombatant body)
    {
        var cell = frame.Slots[index]; var member = cell.Member!.Value; var party = frame.Party(member.Side);
        return frame with { Slots = frame.Slots.SetItem(index, cell with { Body = body }),
            Parties = frame.Parties.SetItem((int)member.Side,
                party with { Members = party.Members.SetItem(member.Index, Member(body)) }) };
    }

    private static SourceBattleFrame Swap(SourceBattleFrame frame, int first = 0, int second = 1, int actor = 0)
    {
        var result = SourceBattleMoveOrderRules.Swap(Catalog, frame, frame.Revision,
            frame.Get(new(actor)).Occupant!.Value, first, second);
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsNull(SourceFourActionTurns.ValidateCapability(Catalog, result.State));
        return result.State;
    }

    private static SourceBattleFrame Act(SourceBattleFrame frame, int slot, int target = 1)
    {
        var result = SourceBattleKernel.ResolveAction(Catalog, frame,
            new(frame.Get(new(0)).Occupant!.Value, frame.Revision, slot, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault);
        return result.State;
    }

    [TestMethod]
    [DataRow(false, 0)]
    [DataRow(true, 0)]
    [DataRow(true, 2)]
    public void CompleteSlotsAndBoundPartySwapWithoutAnyBattleProgress(bool doubles, int actor)
    {
        var frame = Frame(doubles); var body = frame.Get(new(actor)).Body!;
        var first = body.Pokemon.Moves[0]; var second = body.Pokemon.Moves[1];
        first = first with { PpUps = 3, MaximumPp = first.MaximumPp * 8 / 5, CurrentPp = 7 };
        second = second with { PpUps = 1, MaximumPp = second.MaximumPp * 6 / 5, CurrentPp = 3 };
        frame = Put(frame, actor, body with { Pokemon = body.Pokemon with {
            Moves = body.Pokemon.Moves.SetItem(0, first).SetItem(1, second) } });
        var after = Swap(frame, actor: actor);
        Assert.AreEqual(second, after.Get(new(actor)).Body!.Pokemon.Moves[0]);
        Assert.AreEqual(first, after.Get(new(actor)).Body!.Pokemon.Moves[1]);
        Assert.AreEqual(Json(after.Get(new(actor)).Body!.Pokemon),
            Json(after.Party(BattleSide.Player).Members[actor / 2].Pokemon));
        Assert.AreEqual(Json(frame with { Revision = frame.Revision + 1, Slots = after.Slots, Parties = after.Parties }), Json(after));
        foreach (int index in Enumerable.Range(0, 4).Where(i => i != actor))
            Assert.AreSame(frame.Slots[index], after.Slots[index]);
    }

    [TestMethod]
    public void DisabledZeroPpSlotCanMoveWhileChoiceAndHistoryStayUntouched()
    {
        var frame = Frame(); var cell = frame.Get(new(0)); var body = cell.Body!;
        body = body with { HeldItem = "ITEM_CHOICE_BAND", SourceChoiceMove = Id("TACKLE"),
            DisabledMove = Id("GROWL"), DisableTurnsRemaining = 3, LastUsedMove = Id("TACKLE"),
            Pokemon = body.Pokemon with { Moves = body.Pokemon.Moves.SetItem(1, body.Pokemon.Moves[1] with { CurrentPp = 0 }) } };
        frame = Put(frame, 0, body);
        frame = frame with { Slots = frame.Slots.SetItem(0, frame.Slots[0] with { Registers = new() {
            LastMoveSlot = 1, ChosenMoveSlot = 2, History = new() { LastPrintedMove = Id("GROWL"), LastResultingMove = Id("TACKLE") } } }) };
        var after = Swap(frame, 1, 0);
        Assert.AreEqual(Json(body with { Pokemon = after.Slots[0].Body!.Pokemon }), Json(after.Slots[0].Body));
        Assert.AreSame(frame.Slots[0].Registers, after.Slots[0].Registers);
        Assert.AreEqual(0, after.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);
    }

    [TestMethod]
    public void SameSlotKeepsExactFrameAndNoRevisionIsConsumed()
    {
        var frame = Frame() with { Revision = long.MaxValue };
        Assert.AreSame(frame, Swap(frame, 1, 1));
    }

    [TestMethod]
    [DataRow("revision")]
    [DataRow("entry")]
    [DataRow("member")]
    [DataRow("opponent")]
    [DataRow("fainted")]
    [DataRow("absent")]
    [DataRow("empty-slot")]
    [DataRow("negative-slot")]
    [DataRow("one-move")]
    [DataRow("struggle")]
    [DataRow("encore")]
    [DataRow("lock")]
    [DataRow("queue")]
    [DataRow("prepared")]
    [DataRow("ended")]
    [DataRow("counter")]
    public void UnavailableOrStaleRequestsAreAtomic(string scenario)
    {
        var frame = Frame(); int first = 0, second = 1; long revision = frame.Revision;
        var actor = frame.Slots[0].Occupant!.Value;
        if (scenario == "revision") revision--;
        if (scenario == "entry") actor = actor with { EntrySerial = 2 };
        if (scenario == "member") actor = actor with { Member = new(BattleSide.Player, 1) };
        if (scenario == "opponent") actor = frame.Slots[1].Occupant!.Value;
        if (scenario == "fainted") frame = Put(frame, 0, frame.Slots[0].Body! with {
            Pokemon = frame.Slots[0].Body!.Pokemon with { CurrentHp = 0 } });
        if (scenario == "absent") frame = frame with { SourceAbsentMask = 13, TurnStartAbsentMask = 13 };
        if (scenario == "empty-slot") second = 3;
        if (scenario == "negative-slot") first = -1;
        if (scenario == "one-move") frame = Frame(player: Mon("RATTATA", "TACKLE"));
        if (scenario == "struggle") frame = Put(frame, 0, frame.Slots[0].Body! with { Pokemon = frame.Slots[0].Body!.Pokemon with {
            Moves = frame.Slots[0].Body!.Pokemon.Moves.Select(m => m with { CurrentPp = 0 }).ToImmutableArray() } });
        if (scenario == "encore") frame = Put(frame, 0, frame.Slots[0].Body! with {
            EncoredMove = Id("TACKLE"), EncoreMoveSlot = 0, EncoreTurnsRemaining = 3 });
        if (scenario == "lock")
        {
            frame = Frame(player: Mon("RATTATA", "ROLLOUT", "TACKLE"));
            frame = frame with { Slots = frame.Slots.SetItem(0, frame.Slots[0] with { Registers = new() {
                Turn = new(WildBattleRules.CalledMoveKernelRulesVersion,
                    new(WildBattleCalledLockKind.Rollout, Id("ROLLOUT"), 0, Id("ROLLOUT"), new(1), 3, 0, null, false), 0, null) } }) };
        }
        if (scenario == "queue")
        {
            var queued = SourceBattleIdentityRules.CommitSelections(frame, frame.Revision,
                [new(actor, SourceActionKind.Move, 0, new(1)), new(frame.Slots[1].Occupant!.Value, SourceActionKind.Move, 0, new(0))]);
            Assert.IsTrue(queued.Succeeded, queued.Fault); frame = queued.State; revision = frame.Revision;
        }
        if (scenario == "prepared") frame = frame with {
            PreparedMedicines = [new(actor, new("pret:ITEM_POTION"), actor.Member)] };
        if (scenario == "ended") frame = frame with { Outcome = WildBattleOutcome.Escaped };
        if (scenario == "counter") { frame = frame with { Revision = long.MaxValue }; revision = frame.Revision; }
        var result = SourceBattleMoveOrderRules.Swap(Catalog, frame, revision, actor, first, second);
        Assert.IsFalse(result.Succeeded, scenario);
        Assert.AreSame(frame, result.State, scenario);
    }

    [TestMethod]
    public void DoublesPartnerPreparedItemIsPreservedWhileCurrentActorReorders()
    {
        var frame = Frame(true); var partner = frame.Slots[0].Occupant!.Value;
        frame = frame with { PreparedMedicines = [new(partner, new("pret:ITEM_POTION"), partner.Member)] };
        var after = Swap(frame, actor: 2);
        CollectionAssert.AreEqual(frame.PreparedMedicines!.Value.ToArray(), after.PreparedMedicines!.Value.ToArray());
        Assert.AreSame(frame.Slots[0], after.Slots[0]);
    }

    [TestMethod]
    [DataRow(0, 1)]
    [DataRow(1, 0)]
    public void MimicBackupFollowsCopiedSlotInBothDirectionsAndRestoresCorrectly(int first, int second)
    {
        var opponent = Mon("SNORLAX", "GROWL", "DEFENSE_CURL") with { LastUsedMove = Id("GROWL") };
        var copied = Act(Frame(player: Mon("RATTATA", "MIMIC", "TACKLE"), opponent: opponent), 0);
        Assert.IsNotNull(copied.Slots[0].Body!.CopyMoves);
        var after = Swap(copied, first, second); var body = after.Slots[0].Body!;
        Assert.AreEqual(1, body.CopyMoves!.Slots.Single().Slot);
        Assert.AreEqual(Id("TACKLE"), body.Pokemon.Moves[0].Move);
        Assert.AreEqual(Id("GROWL"), body.Pokemon.Moves[1].Move);
        Assert.AreEqual(Id("MIMIC"), after.Party(BattleSide.Player).Members[0].Pokemon.Moves[1].Move);
        var persistent = WildBattleTransformationRules.PersistentPokemon(body);
        var usedCopy = Act(after, 1);
        Assert.AreEqual(body.Pokemon.Moves[1].CurrentPp - 1, usedCopy.Slots[0].Body!.Pokemon.Moves[1].CurrentPp);
        Assert.AreEqual(persistent.Moves[1], usedCopy.Party(BattleSide.Player).Members[0].Pokemon.Moves[1]);
        var usedNormal = Act(usedCopy, 0);
        Assert.AreEqual(persistent.Moves[0].CurrentPp - 1, usedNormal.Party(BattleSide.Player).Members[0].Pokemon.Moves[0].CurrentPp);
        Assert.AreEqual(Json(usedNormal.Party(BattleSide.Player).Members[0].Pokemon),
            Json(WildBattleTransformationRules.Restore(usedNormal.Slots[0].Body!).Pokemon));
        var restored = JsonSerializer.Deserialize<SourceBattleFrame>(Json(usedNormal))!;
        Assert.IsNull(SourceFourActionTurns.ValidateCapability(Catalog, restored));
        var back = Swap(after, 1, 0);
        CollectionAssert.AreEqual(copied.Slots[0].Body!.Pokemon.Moves.ToArray(), back.Slots[0].Body!.Pokemon.Moves.ToArray());
        CollectionAssert.AreEqual(copied.Slots[0].Body!.CopyMoves!.Slots.ToArray(), back.Slots[0].Body!.CopyMoves!.Slots.ToArray());
    }

    [TestMethod]
    public void TransformPermutationPreservesDonorAndPartyAndSurvivesPpUseAndJson()
    {
        var actor = Mon("DITTO", "TRANSFORM", "TACKLE");
        actor = actor with { Pokemon = actor.Pokemon with { Moves = actor.Pokemon.Moves.Select((m, i) =>
            m with { PpUps = i + 1, MaximumPp = m.MaximumPp + m.MaximumPp * 20 * (i + 1) / 100 }).ToImmutableArray() } };
        var transformed = Act(Frame(player: actor), 0);
        var before = transformed.Slots[0].Body!;
        Assert.IsNotNull(before.Transform);
        var after = Swap(transformed); var changed = after.Slots[0].Body!;
        Assert.AreSame(before.Transform!.CopiedPokemon, changed.Transform!.CopiedPokemon);
        Assert.AreSame(before.Transform.OriginalPokemon, changed.Transform.OriginalPokemon);
        CollectionAssert.AreEqual(new[] { 1, 0 }, changed.Transform.LiveMoveOrder!.Value.ToArray());
        Assert.AreEqual(before.Pokemon.Moves[1], changed.Pokemon.Moves[0]);
        Assert.AreEqual(before.Pokemon.Moves[0], changed.Pokemon.Moves[1]);
        Assert.AreEqual(Json(transformed.Party(BattleSide.Player)), Json(after.Party(BattleSide.Player)));
        var resumed = JsonSerializer.Deserialize<SourceBattleFrame>(Json(after))!;
        Assert.IsNull(SourceFourActionTurns.ValidateCapability(Catalog, resumed));
        var used = Act(resumed, 0, 0); // donor slot 1 is Defense Curl, targeting the transformed user.
        Assert.AreEqual(changed.Pokemon.Moves[0].CurrentPp - 1, used.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);
        Assert.AreEqual(Json(after.Party(BattleSide.Player)), Json(used.Party(BattleSide.Player)));
        Assert.AreEqual(Json(before.Transform.OriginalPokemon), Json(WildBattleTransformationRules.Restore(used.Slots[0].Body!).Pokemon));
        var back = Swap(after);
        Assert.IsNull(back.Slots[0].Body!.Transform!.LiveMoveOrder);
        CollectionAssert.AreEqual(before.Pokemon.Moves.ToArray(), back.Slots[0].Body!.Pokemon.Moves.ToArray());
    }

    [TestMethod]
    public void ForgedTransformPermutationIsRejectedWithoutMutation()
    {
        var frame = Act(Frame(player: Mon("DITTO", "TRANSFORM", "TACKLE")), 0);
        frame = frame with { Slots = frame.Slots.SetItem(0, frame.Slots[0] with {
            Body = frame.Slots[0].Body! with { Transform = frame.Slots[0].Body!.Transform! with { LiveMoveOrder = [0, 0] } } }) };
        var result = SourceBattleMoveOrderRules.Swap(Catalog, frame, frame.Revision, frame.Slots[0].Occupant!.Value, 0, 1);
        Assert.AreEqual("source_copy_transform_shape", result.Fault);
        Assert.AreSame(frame, result.State);
    }

    [TestMethod]
    public void TransformedFourMovePickerDoesNotIndexSinglePersistentTransformSlot()
    {
        var frame = Act(Frame(player: Mon("DITTO", "TRANSFORM"),
            opponent: Mon("SNORLAX", "GROWL", "DEFENSE_CURL", "TAIL_WHIP", "TACKLE")), 0);
        Assert.AreEqual(1, frame.Party(BattleSide.Player).Members[0].Pokemon.Moves.Length);
        var after = Swap(frame, 0, 3);
        Assert.AreEqual(Id("TACKLE"), after.Slots[0].Body!.Pokemon.Moves[0].Move);
        CollectionAssert.AreEqual(new[] { 3, 1, 2, 0 }, after.Slots[0].Body!.Transform!.LiveMoveOrder!.Value.ToArray());
        Assert.AreEqual(Json(frame.Party(BattleSide.Player)), Json(after.Party(BattleSide.Player)));
    }
}
