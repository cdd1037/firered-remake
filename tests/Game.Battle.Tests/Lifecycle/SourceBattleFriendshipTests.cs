using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using FireRed.Tests.Battle.Encounters;
using FireRed.Tests.Battle.FieldEffects;

namespace FireRed.Tests.Battle.Lifecycle;

// Bounded source arithmetic and real entry/action/medicine/residual owners. Controlled
// HP/EXP/stages are integration inputs, never evidence of a played campaign or victory.
[TestClass, TestCategory("Game")]
public sealed class SourceBattleFriendshipTests : FieldEffectFixture
{
    private static GamePokemonCatalog Owned => RegionalWildDefinitionTests.Owned;
    private static SourceFourRewardState Facts(int count, bool bonuses = false) => new() {
        Context = new(SourceRewardBattleKind.OrdinaryTrainer, 42, "RED", "MAPSEC_FUCHSIA_CITY"),
        Metadata = Enumerable.Range(0, count).Select(i => new SourceRewardMetadata(new(BattleSide.Player, i),
            "RED", bonuses ? "MAPSEC_FUCHSIA_CITY" : "MAPSEC_PALLET_TOWN",
            bonuses ? "ITEM_LUXURY_BALL" : "ITEM_POKE_BALL", SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()
    };
    private SourceTrainerEncounterEntryResult Enter(string trainer, ImmutableArray<WildBattleCombatant> party,
        SourceFourRewardState? facts, uint seed = 31, GamePokemonCatalog? ownership = null)
    {
        var owned = ownership ?? Owned;
        TrainerId id = new("pret:TRAINER_" + trainer);
        var setup = SourceTrainerEncounterEntry.Prepare(new(seed), "friendship-test");
        Assert.IsTrue(setup.Succeeded, setup.Fault);
        var made = TrainerPartyFactory.Create(SharedTrainerCatalog.LoadProject(TestAssets.Root).Select([id]),
            owned.Opponents, id, setup.Rng);
        return SourceTrainerEncounterEntry.Begin(owned.Mechanics.Catalog, setup, made, party, 0, owned: owned, rewards: facts);
    }
    private WildBattleCombatant Friendly(int friendship, bool bonuses = false, uint personality = 0) {
        var mon = Mon("RATICATE", "QUICK_ATTACK", personality, 50);
        return mon with { Pokemon = mon.Pokemon with { Friendship = friendship }, HeldItem = bonuses ? "ITEM_SOOTHE_BELL" : "ITEM_NONE" };
    }

    [TestMethod]
    [DataRow("LEADER_KOGA", false)] [DataRow("LEADER_KOGA", true)]
    [DataRow("ELITE_FOUR_LORELEI", false)] [DataRow("ELITE_FOUR_LORELEI", true)]
    [DataRow("CHAMPION_FIRST_CHARMANDER", false)] [DataRow("CHAMPION_FIRST_CHARMANDER", true)]
    public void LeagueEntryAppliesSourceBandsToActiveAndFaintedReservesBeforeBodies(string trainer, bool bonuses)
    {
        int[] values = [0, 99, 100, 199, 200, 254], deltas = [3, 3, 2, 2, 1, 1];
        var party = values.Select((v, i) => Friendly(v, bonuses, (uint)i)).ToImmutableArray();
        party = party.SetItem(1, party[1] with { Pokemon = party[1].Pokemon with { CurrentHp = 0 } });
        string before = JsonSerializer.Serialize(party);
        var result = Enter(trainer, party, Facts(6, bonuses)); Assert.IsTrue(result.Succeeded, result.Fault);
        for (int i = 0; i < values.Length; i++) {
            int expected = Math.Min(255, values[i] + (bonuses ? deltas[i] * 150 / 100 + 2 : deltas[i]));
            Assert.AreEqual(expected, result.State!.Party(BattleSide.Player).Members[i].Pokemon.Friendship);
        }
        Assert.AreEqual(result.State!.Party(BattleSide.Player).Members[0].Pokemon.Friendship, result.EntrySlots[0].Body!.Pokemon.Friendship);
        Assert.AreEqual(0, result.State.Party(BattleSide.Player).Members[1].Pokemon.CurrentHp);
        Assert.AreEqual(before, JsonSerializer.Serialize(party));
        var repeated = Enter(trainer, party, Facts(6, bonuses));
        Assert.AreEqual(JsonSerializer.Serialize(result), JsonSerializer.Serialize(repeated), "same entry inputs replay deterministically without mutating the party");
        var plain = Enter(trainer, values.Select((v, i) => Friendly(v, false, (uint)i)).ToImmutableArray(), Facts(6));
        Assert.IsTrue(plain.Succeeded, plain.Fault);
        Assert.AreEqual(plain.Rng, result.Rng); CollectionAssert.AreEqual(plain.Trace.ToArray(), result.Trace.ToArray());
    }

    [TestMethod]
    public void OrdinaryTrainerDoesNotChangeFriendshipAndLeagueEntryRejectsMissingBonusFacts()
    {
        ImmutableArray<WildBattleCombatant> party = [Friendly(99, true)];
        var ordinary = Enter("JUGGLER_KIRK", party, Facts(1, true)); Assert.IsTrue(ordinary.Succeeded, ordinary.Fault);
        Assert.AreEqual(99, ordinary.State!.Party(BattleSide.Player).Members[0].Pokemon.Friendship);
        var incomplete = Enter("LEADER_KOGA", party, null);
        Assert.IsFalse(incomplete.Succeeded); Assert.AreEqual("source_trainer_entry_friendship_context", incomplete.Fault);
        Assert.IsNull(incomplete.State); Assert.IsTrue(incomplete.Events.IsEmpty); Assert.IsTrue(incomplete.Trace.IsEmpty);
        Assert.AreEqual(99, party[0].Pokemon.Friendship);
    }

    [TestMethod]
    public void LeagueEntryPreservesActualGiftEggCycleCounter()
    {
        var content = TestAssets.Cached(() => PretCampaignCatalog.LoadCurrent(TestAssets.Root)).PokemonContent!;
        var egg = SourceGiftEggRules.GiveEgg(content, content.Catalog.BreedingData!, SourceGiftEggRules.Togepi,
            100, new(42, "RED", 0), 4, new(31)).Egg;
        ImmutableArray<WildBattleCombatant> party = [Friendly(99), new(egg.Pokemon) { IsEgg = true }];
        var facts = Facts(2) with { Metadata = Facts(2).Metadata.SetItem(1,
            new(new(BattleSide.Player, 1), egg.OriginalTrainerName, egg.MetLocation, egg.Ball, SourceRewardPokerusHistory.NeverInfected)) };
        // A carried egg requires this exact campaign's authenticated breeding metadata.
        var result = Enter("LEADER_KOGA", party, facts, ownership: content.Pokemon); Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.AreEqual(102, result.State!.Party(BattleSide.Player).Members[0].Pokemon.Friendship);
        Assert.AreEqual(egg.Pokemon, result.State.Party(BattleSide.Player).Members[1].Pokemon);
    }

    private SourceBattleFrame FaintFrame(bool single, int friendship, int leftLevel = 49, int rightLevel = 50)
    {
        var player = Mon("WAILMER", "SPLASH", level: 20);
        player = player with { Pokemon = player.Pokemon with { CurrentHp = 1, Friendship = friendship } };
        var left = Mon("RATICATE", "HYPER_FANG", level: leftLevel) with { SpeedStage = 12, AccuracyStage = 12 };
        var ally = Mon("WAILORD", "SPLASH", personality: 3, level: 100);
        var right = Mon("WAILORD", "SPLASH", level: rightLevel);
        WildBattleCombatant[] bodies = [player, left, ally, right];
        var frame = new SourceBattleFrame {
            Layout = single ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = single ? KoReplacementTiming.SourceImmediate : KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = single ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer,
            SourceAbsentMask = (byte)(single ? 12 : 0), TurnStartAbsentMask = (byte)(single ? 12 : 0), Rng = new(31),
            Parties = [new(BattleSide.Player, [Member(player), Member(ally)]),
                new(BattleSide.Opponent, single ? [Member(left)] : [Member(left), Member(right)])],
            Slots = Enumerable.Range(0, 4).Select(i => single && i >= 2 ? new SourceBattleSlot(new(i), null, 0, null)
                : new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault); return entry.State;
    }
    private SourceFourActionResult Begin(SourceBattleFrame frame, SourceActionSelection? item = null)
    {
        var choices = frame.Slots.Take(frame.BattlerCount).Where(c => frame.CanAct(c.Slot))
            .Where(c => item is null || c.Occupant != item.Actor && c.Slot.Value != 0)
            .Select(c => new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0,
                c.Slot.Value == 1 ? new(0) : catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"
                    ? c.Slot : new(c.Slot.Value ^ 1))).ToImmutableArray();
        if (item is not null) choices = choices.Add(item);
        return Good(SourceFourActionTurns.BeginActions(catalog, frame, frame.Revision, choices));
    }
    private SourceFourActionResult KnockOut(SourceBattleFrame frame)
    {
        var started = Begin(frame); Assert.AreEqual(1, started.State.Queue[0].Slot.Value);
        var result = Next(started);
        Assert.AreEqual(1, result.Events.OfType<SourceFaintedEvent>().Count(e => e.Side.Value == 0));
        var replay = SourceFourActionTurns.Advance(catalog, result.State, started.Continuation!.Value);
        Assert.IsFalse(replay.Succeeded); Assert.AreSame(result.State, replay.State);
        Assert.IsTrue(replay.Events.IsEmpty); Assert.IsTrue(replay.Trace.IsEmpty);
        return result;
    }

    [TestMethod]
    [DataRow(true, 49, 49, 1)] [DataRow(true, 50, 49, 5)]
    [DataRow(false, 49, 49, 1)] [DataRow(false, 49, 50, 5)]
    public void RealFaintUsesThirtyLevelThresholdAndHigherOpposingFlank(bool single, int left, int right, int lowPenalty)
    {
        foreach (int friendship in new[] { 0, 1, 99, 100, 199, 200, 255 }) {
            var frame = FaintFrame(single, friendship, left, right); string before = JsonSerializer.Serialize(frame);
            var result = KnockOut(frame);
            int penalty = lowPenalty == 5 && friendship >= 200 ? 10 : lowPenalty;
            int expected = Math.Max(0, friendship - penalty);
            Assert.AreEqual(expected, result.State.Party(BattleSide.Player).Members[0].Pokemon.Friendship);
            Assert.AreEqual(42u, result.State.Party(BattleSide.Player).Members[0].Pokemon.OriginalTrainerId);
            Assert.AreEqual(frame.Party(BattleSide.Player).Members[1].Pokemon.Friendship,
                result.State.Party(BattleSide.Player).Members[1].Pokemon.Friendship);
            foreach (int i in Enumerable.Range(0, frame.Party(BattleSide.Opponent).Members.Length))
                Assert.AreEqual(frame.Party(BattleSide.Opponent).Members[i].Pokemon.Friendship, result.State.Party(BattleSide.Opponent).Members[i].Pokemon.Friendship);
            var receipt = result.Events.OfType<SourceCalledPartyChangedEvent>().Single(e => e.Side == BattleSide.Player && e.PartyIndex == 0);
            Assert.AreEqual(expected, receipt.After.Pokemon.Friendship);
            Assert.AreEqual(before, JsonSerializer.Serialize(frame));
            var differentFriendship = KnockOut(FaintFrame(single, friendship == 0 ? 255 : 0, left, right));
            Assert.AreEqual(result.State.Rng, differentFriendship.State.Rng);
            CollectionAssert.AreEqual(result.Trace.ToArray(), differentFriendship.Trace.ToArray());
        }
    }

    [TestMethod, DataRow(1), DataRow(3)]
    public void StandaloneKernelRejectsMissingOpposingLevelWithoutPartialFaintMutation(int missing)
    {
        var frame = FaintFrame(false, 200);
        int attacker = missing == 1 ? 3 : 1;
        frame = Rebind(frame, attacker, Mon("RATICATE", "TACKLE", level: 50) with { AccuracyStage = 12 });
        frame = frame with {
            Slots = frame.Slots.SetItem(missing, new(new(missing), null, 0, null)),
            SourceAbsentMask = (byte)(1 << missing), TurnStartAbsentMask = (byte)(1 << missing),
            EntryAbilities = frame.EntryAbilities! with { AppliedEntries = frame.EntryAbilities.AppliedEntries.SetItem(missing, null) }
        };
        var action = new SourceResolvedMoveAction(frame.Get(new(attacker)).Occupant!.Value, frame.Revision, 0, new(0));
        Assert.IsNull(SourceBattleKernel.ValidateAction(catalog, frame, action), "standalone action admits its otherwise valid partial layout");
        var result = SourceBattleKernel.ResolveAction(catalog, frame, action);
        Assert.IsFalse(result.Succeeded); Assert.AreEqual("source_friendship_opposing_battle_body_required", result.Fault);
        Assert.AreSame(frame, result.State); Assert.IsTrue(result.Events.IsEmpty); Assert.IsTrue(result.Trace.IsEmpty);
        Assert.AreEqual(200, frame.Party(BattleSide.Player).Members[0].Pokemon.Friendship);
    }

    [TestMethod]
    public void ResidualFaintUsesSameOwnerAndRetainedFaintedHigherOpponent()
    {
        var frame = FaintFrame(false, 200, 49, 50);
        // Both opponents use non-damaging source moves; the player reaches poison at one HP.
        frame = Rebind(frame, 1, Mon("WAILORD", "SPLASH", level: 49));
        frame = Rebind(frame, 3, frame.Get(new(3)).Body! with { Pokemon = frame.Get(new(3)).Body!.Pokemon with { CurrentHp = 0 } });
        frame = frame with { SourceAbsentMask = 8, TurnStartAbsentMask = 8 };
        var player = frame.Get(new(0)).Body!; frame = Rebind(frame, 0, player with { Status = WildBattleStatus.Poison });
        var result = Begin(frame);
        for (int i = 0; result.State.FourTurn?.Phase == SourceFourActionPhase.Actions && i < 5; i++) result = Next(result);
        Assert.AreEqual(SourceFourActionPhase.EndTurn, result.State.FourTurn!.Phase);
        result = Next(result);
        Assert.AreEqual(1, result.Events.OfType<SourceFaintedEvent>().Count(e => e.Side.Value == 0));
        Assert.AreEqual(190, result.State.Party(BattleSide.Player).Members[0].Pokemon.Friendship);
        Assert.AreEqual(WildBattleStatus.None, result.State.Party(BattleSide.Player).Members[0].Status);
    }

    [TestMethod]
    public void RealRevivalHerbThenSecondFaintUsesCurrentBandAndEachReceiptOnlyOnce()
    {
        var result = KnockOut(FaintFrame(false, 205));
        Assert.AreEqual(195, result.State.Party(BattleSide.Player).Members[0].Pokemon.Friendship);
        for (int i = 0; result.State.FourTurn is not null && i < 8; i++) result = Next(result);
        Assert.IsNull(result.State.FourTurn); Assert.IsTrue(result.State.IsSourceAbsent(new(0)));
        var frame = result.State;
        var party = frame.Party(BattleSide.Player).Members.Select((m, i) => new PartyPokemon(i + 1, m.Pokemon,
            i == 0 ? "WAILMER" : "WAILORD", "RED", 0, 2, "MAPSEC_ROUTE_1", 20, 4, "ITEM_POKE_BALL", m.HeldItem) { Status = m.Status }).ToImmutableArray();
        ItemId herb = new("pret:ITEM_REVIVAL_HERB");
        var items = SourceBattleItemCatalogLoader.LoadAuthenticated(TestAssets.Root);
        var selected = SourceFourMedicineRules.Select(items, proof, frame, frame.Revision,
            frame.Get(new(2)).Occupant!.Value, new([new(herb, 1)], [], SourceBattleItemCatalog.Limits), party,
            new(herb, 1), "MAPSEC_FUCHSIA_CITY");
        Assert.IsTrue(selected.Succeeded, selected.Fault);
        Assert.AreEqual(180, selected.Party[0].Pokemon.Friendship, "195 chooses the source -15 Revival Herb band after the first faint");
        Assert.IsTrue(selected.Party[0].Pokemon.CurrentHp > 0); Assert.IsTrue(selected.Inventory.BagItems.IsEmpty);
        var turn = Begin(selected.Battle.State, selected.Selection);
        var events = turn.Events;
        for (int i = 0; turn.State.Party(BattleSide.Player).Members[0].Pokemon.CurrentHp > 0 && i < 5; i++) {
            turn = Next(turn); events = events.AddRange(turn.Events);
        }
        Assert.AreEqual(1, events.OfType<SourceFaintedEvent>().Count(e => e.Side.Value == 0));
        Assert.AreEqual(175, turn.State.Party(BattleSide.Player).Members[0].Pokemon.Friendship,
            "revived same party identity incurs a fresh large-faint -5 at its current friendship");
    }

    [TestMethod]
    public void TransformedFaintChangesOriginalPartyOwnerAndNeverTheDonor()
    {
        var frame = FaintFrame(false, 200);
        var ditto = Mon("DITTO", "TRANSFORM", level: 20);
        ditto = ditto with { Pokemon = ditto.Pokemon with { Friendship = 200 } };
        frame = Rebind(frame, 0, ditto);
        var copied = SourceBattleKernel.ResolveAction(catalog, frame,
            new(frame.Get(new(0)).Occupant!.Value, frame.Revision, 0, new(1)));
        Assert.IsTrue(copied.Succeeded, copied.Fault);
        frame = copied.State; var cell = frame.Get(new(0));
        Assert.IsNotNull(cell.Body!.Transform);
        var donor = cell.Body.Transform.CopiedPokemon;
        var original = WildBattleTransformationRules.PersistentPokemon(cell.Body) with { CurrentHp = 1 };
        var party = frame.Party(BattleSide.Player);
        frame = frame with {
            Slots = frame.Slots.SetItem(0, cell with { Body = WildBattleTransformationRules.ApplyPersistentPokemon(cell.Body, original) }),
            Parties = frame.Parties.SetItem(0, party with { Members = party.Members.SetItem(0, party.Members[0] with { Pokemon = original }) })
        };
        var result = KnockOut(frame); var after = result.State.Get(new(0)).Body!;
        Assert.AreEqual(190, result.State.Party(BattleSide.Player).Members[0].Pokemon.Friendship);
        Assert.AreEqual(ditto.Pokemon.Species, result.State.Party(BattleSide.Player).Members[0].Pokemon.Species);
        Assert.IsNotNull(after.Transform); Assert.AreEqual(190, after.Transform.OriginalPokemon.Friendship);
        Assert.AreEqual(donor, after.Transform.CopiedPokemon);
        Assert.AreEqual(frame.Party(BattleSide.Opponent).Members[0].Pokemon.Friendship,
            result.State.Party(BattleSide.Opponent).Members[0].Pokemon.Friendship);
    }

    [TestMethod]
    public void EntryGainPrecedesActualKnockoutLevelUpBandSelection()
    {
        var player = Mon("RATICATE", "QUICK_ATTACK", level: 99);
        player = player with { Pokemon = player.Pokemon with { Friendship = 96,
            Experience = GrowthRules.ExperienceAtLevel(catalog.Species[player.Pokemon.Species].GrowthRate, 100) - 1 } };
        var entered = Enter("LEADER_KOGA", [player], Facts(1)); Assert.IsTrue(entered.Succeeded, entered.Fault);
        var frame = entered.State!; Assert.AreEqual(99, frame.Party(BattleSide.Player).Members[0].Pokemon.Friendship);
        var foe = frame.Get(new(1)).Body!; frame = Rebind(frame, 1, foe with { Pokemon = foe.Pokemon with { CurrentHp = 1 } });
        var start = Good(SourceFourActionTurns.BeginActions(catalog, frame, frame.Revision, [
            new(frame.Get(new(0)).Occupant!.Value, SourceActionKind.Move, 0, new(1)),
            new(frame.Get(new(1)).Occupant!.Value, SourceActionKind.Move, 0, new(0))]));
        var defeated = Next(start); Assert.AreEqual(SourceFourActionPhase.RewardsPending, defeated.State.FourTurn!.Phase);
        var settled = Good(SourceFourRewards.Settle(catalog, defeated.State, defeated.Continuation!.Value));
        Assert.AreEqual(100, settled.State.Party(BattleSide.Player).Members[0].Pokemon.Level);
        Assert.AreEqual(104, settled.State.Party(BattleSide.Player).Members[0].Pokemon.Friendship);
        var replay = SourceFourRewards.Settle(catalog, settled.State, defeated.Continuation.Value);
        Assert.IsFalse(replay.Succeeded); Assert.AreSame(settled.State, replay.State); Assert.IsTrue(replay.Events.IsEmpty);
    }
}
