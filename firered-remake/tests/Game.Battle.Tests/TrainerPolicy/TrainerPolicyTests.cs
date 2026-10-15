using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Battle.TrainerPolicy;
[TestClass, TestCategory("Game")]
public sealed class TrainerPolicyTests
{
    private string root = default !;
    private global::Game.Domain.Pokemon.SourceMechanicsCatalog proof = default !;
    private global::Game.Domain.Pokemon.PokemonCatalog catalog = default !;
    private global::Game.Domain.Items.SourceBattleItemCatalog items = default !;
    private global::Game.Domain.Pokemon.PokemonCatalog raw = default !;
    private global::Game.Domain.Pokemon.PokemonCatalog extra = default !;
    private global::Game.Domain.Pokemon.PokemonCatalog prior = default !;
    private global::Game.Content.PretFuchsiaRoutePokemon route = default !;
    private global::Game.Domain.Pokemon.PokemonCatalog trainerCatalog = default !;
    private global::Game.Content.FuchsiaGymTrainerCatalogContent loaded = default !;
    private global::Game.Domain.Trainers.FuchsiaGymTrainerCatalog gym = default !;
    [TestInitialize]
    public void SetUp()
    {
        root = Path.GetFullPath(TestAssets.Root);
        proof = TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(root));
        catalog = proof.Catalog;
        items = SourceBattleItemCatalogLoader.LoadAuthenticated(root);
        raw = PretRocketHideoutPokemon.Load(root).Catalog;
        extra = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules: true);
        prior = PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(raw with { Moves = raw.Moves.SetItems(extra.Moves) }, root, root).Catalog, root).Catalog, root).Catalog;
        route = PretFuchsiaRoutePokemon.Load(prior, root);
        trainerCatalog = PretMetronomeMoveCatalog.Load(root).Extend(route.Catalog);
        loaded = PretFuchsiaGymTrainers.Load(root);
        gym = new FuchsiaGymTrainerCatalog(loaded.SourceCommit, loaded.Profile, loaded.CompleteSourceRoster, loaded.Trainers, loaded.Battles);
    }

    private string Json<T>(T value) => JsonSerializer.Serialize(value);
    private WildBattleCombatant Player(int pid) => new(PokemonFactory.Create(trainerCatalog, new(new("pret:SPECIES_CLEFAIRY"), 50, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed((uint)pid)), new(0)).Pokemon);
    private (SourceBattleFrame Frame, TrainerPartyCreationResult Made) Fixture(string name, uint seed = 42)
    {
        var setup = SourceTrainerEncounterEntry.Prepare(new(seed), "fuchsia-gym-trainer");
        Assert.IsTrue(setup.Succeeded && setup.Trace.Length == 4 && setup.Trace.All(t => t.Bound == 16), "four source entry AI setup draws");
        var made = FuchsiaGymTrainerPartyFactory.Create(gym, trainerCatalog, new("pret:TRAINER_" + name), setup.Rng);
        var entered = SourceTrainerEncounterEntry.Begin(catalog, setup, made, [Player(0), Player(3)], 0, terrain: WildBattleTerrain.Building);
        Assert.IsTrue(entered.Succeeded, "authentic canonical entry " + entered.Fault);
        var frame = entered.State!;
        Assert.IsTrue(SourceFourActionTurns.ValidateCapability(catalog, frame)is null, "canonical capability");
        Assert.IsTrue(SourceTrainerEncounterPolicy.Validate(catalog, frame)is null, "authenticated trainer policy");
        Assert.IsTrue(frame.Layout == SourceBattleLayout.Singles && frame.BattlerCount == 2 && frame.Queue.IsEmpty && frame.Slots.Skip(2).All(c => c.Body is null && c.Member is null), "two actual positions at the first player choice");
        Assert.IsTrue(frame.Party(BattleSide.Opponent).Members.Select(x => x.Pokemon.Personality).SequenceEqual(made.Party.Select(x => x.Pokemon.Personality)), "factory identities enter without regeneration");
        Assert.IsTrue(entered.Events[0] is SourceTrainerStartedEvent started && started.Trainer == made.Trainer.Id && started.OpponentCount == made.Party.Length && entered.Events[1] is SourceStartedEvent && entered.Events[^1] is SourceTurnReadyEvent, "trainer provenance precedes start and ready events");
        Assert.IsTrue(entered.Trace.Take(4).SequenceEqual(setup.Trace) && entered.Trace.Skip(4).Take(made.Trace.Length).SequenceEqual(made.Trace) && entered.Trace[^1].Purpose == "wild-battle.entry.random-turn-number", "setup, factory and ready trace order");
        TraceChain(new(seed), frame.Rng, entered.Trace);
        return (frame, made);
    }

    private void TraceChain(RngState initial, RngState final, ImmutableArray<RandomTrace> trace)
    {
        uint seed = initial.Seed;
        ulong cursor = initial.Cursor;
        foreach (var draw in trace)
        {
            uint next = unchecked(seed * 0x41C64E6Du + 0x6073u);
            Assert.IsTrue(draw.Before == seed && draw.After == next && draw.Cursor == ++cursor && draw.Raw == (ushort)(next >> 16) && draw.Result == draw.Raw % draw.Bound, "contiguous source LCG trace");
            seed = next;
        }

        Assert.IsTrue(final == new RngState(seed, cursor), "trace accounts for every committed RNG draw");
    }

    private SourceBattleFrame Enemy(SourceBattleFrame frame, Func<WildBattleCombatant, WildBattleCombatant> edit)
    {
        var cell = frame.Get(new(1));
        var mon = edit(cell.Body!);
        var party = frame.Party(BattleSide.Opponent);
        return frame with
        {
            Slots = frame.Slots.SetItem(1, cell with { Body = mon }),
            Parties = frame.Parties.SetItem(1, party with { Members = party.Members.SetItem(cell.Member!.Value.Index, new(mon.Pokemon, mon.HeldItem, mon.Status)) })
        };
    }

    private SourceFourActionResult Ok(SourceFourActionResult result)
    {
        Assert.IsTrue(result.Succeeded, "canonical lifecycle " + result.Fault);
        Assert.IsTrue(SourceBattleIdentityRules.Validate(result.State)is null, "canonical identity after lifecycle step");
        return result;
    }

    private SourceTurnContinuation Token(SourceBattleFrame state) => new(state.Revision, state.EventSequence, state.NextAction, state.FourTurn!.Phase);
    private SourceBattleFrame Entry(string trainer, string move, bool reserve = true)
    {
        var pokemon = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_CLEFAIRY"), 100, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        MoveId id = new("pret:MOVE_" + move);
        var definition = catalog.Moves[id];
        var player = new WildBattleCombatant(pokemon with { Moves = [new(id, definition.Pp, definition.Pp)] });
        ImmutableArray<WildBattleCombatant> players = reserve ? [player, Player(3)] : [player];
        var rewards = new SourceFourRewardState
        {
            Context = new(SourceRewardBattleKind.OrdinaryTrainer, 42, "RED", "MAPSEC_FUCHSIA_CITY"),
            Metadata = players.Select((_, i) => new SourceRewardMetadata(new(BattleSide.Player, i), "RED", "MAPSEC_PALLET_TOWN", "ITEM_POKE_BALL", SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()
        };
        var setup = SourceTrainerEncounterEntry.Prepare(new(17), "fuchsia-gym-trainer");
        var made = FuchsiaGymTrainerPartyFactory.Create(gym, trainerCatalog, new("pret:TRAINER_" + trainer), setup.Rng);
        var entered = SourceTrainerEncounterEntry.Begin(catalog, setup, made, players, 0, rewards: rewards);
        Assert.IsTrue(entered.Succeeded, "authentic lifecycle entry " + entered.Fault);
        return entered.State!;
    }

    private SourceFourActionResult Start(SourceBattleFrame frame)
    {
        var enemy = SourceTrainerEncounterPolicy.PrepareActions(items, catalog, frame, frame.Revision);
        Assert.IsTrue(enemy.Succeeded, "authentic controller " + enemy.Fault);
        return Ok(SourceFourActionTurns.BeginActions(catalog, enemy.State, enemy.State.Revision, [new(enemy.State.Get(new(0)).Occupant!.Value, SourceActionKind.Move, 0, new BattlerSlot(1)), ..enemy.Selections]));
    }

    private SourceFourActionResult RewardBarrier(SourceBattleFrame frame)
    {
        var result = Start(frame);
        for (int i = 0; i < 8 && result.State.FourTurn!.Phase == SourceFourActionPhase.Actions; i++)
            result = Ok(SourceFourActionTurns.Advance(catalog, result.State, Token(result.State)));
        Assert.IsTrue(result.State.FourTurn!.Phase == SourceFourActionPhase.RewardsPending, "KO pauses for EXP before entries");
        return result;
    }

    private SourceFourActionResult Settle(SourceFourActionResult pending)
    {
        var result = pending;
        for (int i = 0; i < 8 && result.State.FourTurn!.Phase == SourceFourActionPhase.RewardsPending; i++)
            result = Ok(SourceFourRewards.Settle(catalog, result.State, Token(result.State)));
        Assert.IsTrue(result.State.FourTurn!.Phase != SourceFourActionPhase.RewardsPending, "bounded recipient settlement completed");
        return result;
    }

    [TestMethod]
    public void authentic_flags1_7_legal_scoring_RNG_and_controller_bypasses()
    {
        foreach (string name in new[]
        {
            "JUGGLER_KIRK",
            "LEADER_KOGA"
        }

        )
        {
            var(frame, made) = Fixture(name);
            Assert.IsTrue(made.Trainer.AiFlags == (name == "LEADER_KOGA" ? 7u : 1u), "authentic source AI flags");
            var selected = SourceFourTrainerAi.ChooseMove(catalog, made.Trainer, frame, frame.Revision, new(1));
            Assert.IsTrue(selected.Succeeded && selected.Choices.Length == 1, "one actual enemy choice " + selected.Fault);
            var choice = selected.Choices.Single();
            var moves = frame.Get(new(1)).Body!.Pokemon.Moves;
            int slot = choice.Selection.MoveSlot!.Value;
            Assert.IsTrue(choice.Actor == new BattlerSlot(1) && choice.ScoredTarget == new BattlerSlot(0) && choice.Selection.Actor == frame.Get(new(1)).Occupant && choice.Selection.Kind == SourceActionKind.Move, "selection retains actual actor and scored opponent");
            Assert.IsTrue(slot >= 0 && slot < moves.Length && moves[slot].CurrentPp > 0 && choice.Scores.Length == 4 && choice.Scores.All(x => x >= 0) && choice.Scores[slot] == choice.Scores.Max(), "source controller selects a usable maximum-score move");
            Assert.IsTrue(choice.SimulatedDamagePercent.Length == 4 && choice.SimulatedDamagePercent.All(x => x is >= 85 and <= 100) && selected.Trace.Take(4).Select(t => 100 - t.Result).SequenceEqual(choice.SimulatedDamagePercent), "four original 85-to-100 damage simulations precede scoring");
            Assert.IsTrue(selected.Trace.All(t => !t.Purpose.Contains("target-flank")), "singles scoring has no doubles target draw");
            TraceChain(frame.Rng, selected.State.Rng, selected.Trace);
            var depleted = Enemy(frame, m => m with { Pokemon = m.Pokemon with { Moves = m.Pokemon.Moves.SetItem(0, m.Pokemon.Moves[0] with { CurrentPp = 0 }) } });
            var usable = SourceFourTrainerAi.ChooseMove(catalog, made.Trainer, depleted, depleted.Revision, new(1));
            Assert.IsTrue(usable.Succeeded && usable.Choices[0].Scores[0] == 0 && usable.Selections[0].MoveSlot != 0, "zero-PP move cannot win scoring");
            var stale = SourceFourTrainerAi.ChooseMove(catalog, made.Trainer, frame, frame.Revision + 1, new(1));
            Assert.IsTrue(!stale.Succeeded && ReferenceEquals(stale.State, frame) && stale.Trace.IsEmpty && stale.Choices.IsEmpty, "stale controller revision rolls back before RNG");
        }

        var(initial, created) = Fixture("JUGGLER_KIRK");
        var encored = Enemy(initial, m => m with { EncoreTurnsRemaining = 2, EncoreMoveSlot = 0, EncoredMove = m.Pokemon.Moves[0].Move });
        var forced = SourceFourTrainerAi.ChooseMove(catalog, created.Trainer, encored, encored.Revision, new(1));
        Assert.IsTrue(forced.Succeeded && forced.Selections[0].MoveSlot == 0 && forced.Trace.IsEmpty && forced.State.Rng == encored.Rng, "Encore bypasses setup and scoring RNG");
        var exhausted = Enemy(encored, m => m with { Pokemon = m.Pokemon with { Moves = m.Pokemon.Moves.Select(x => x with { CurrentPp = 0 }).ToImmutableArray() } });
        var struggle = SourceFourTrainerAi.ChooseMove(catalog, created.Trainer, exhausted, exhausted.Revision, new(1));
        Assert.IsTrue(struggle.Succeeded && struggle.Selections[0].IsStruggle && struggle.Selections[0].Target == new BattlerSlot(0) && struggle.Trace.IsEmpty && struggle.State.Rng == exhausted.Rng, "Struggle precedes Encore without a fabricated target draw");
    }

    [TestMethod]
    public void authentic_receipt_and_item_transaction_survive_JSON()
    {
        var(initial, made) = Fixture("LEADER_KOGA");
        var f = Enemy(initial, m => m with { Pokemon = m.Pokemon with { CurrentHp = 1 } });
        var after = SourceTrainerEncounterPolicy.PrepareActions(items, catalog, f, f.Revision);
        Assert.IsTrue(after.Succeeded && after.Actions.Single().Selection.Kind == SourceActionKind.Item && after.Actions[0].ItemChoice is { InventorySlot: 0, Kind: SourceTrainerItemKind.HealHp } item && item.Item == new ItemId("pret:ITEM_HYPER_POTION"), "Koga chooses his first Hyper Potion at 1 HP " + after.Fault);
        Assert.IsTrue(after.State.TrainerEncounter!.StaleCurrentMove == f.TrainerEncounter!.StaleCurrentMove, "singles scorer does not overwrite action stale-move register");
        Assert.IsTrue(after.Inventory.ConsumedMask == 1 && after.State.TrainerEncounter!.Inventory == after.Inventory && f.TrainerEncounter!.Inventory.ConsumedMask == 0, "inventory commits with prepared receipt only");
        Assert.IsTrue(after.State.Get(new(1)).Body!.Pokemon.CurrentHp == 1 && after.State.PreparedMedicines?.Length == 1, "trainer effect deferred until existing queue action");
        var begin = SourceFourActionTurns.BeginActions(catalog, after.State, after.State.Revision, [new(after.State.Get(new(0)).Occupant!.Value, SourceActionKind.Move, 0, new BattlerSlot(1)), after.Selections.Single()]);
        Assert.IsTrue(begin.Succeeded, "existing turn owner accepts prepared single trainer item " + begin.Fault);
        var effect = SourceFourActionTurns.Advance(catalog, begin.State, begin.Continuation!.Value);
        Assert.IsTrue(effect.Succeeded && effect.State.Get(new(1)).Body!.Pokemon.CurrentHp == Math.Min(f.Get(new(1)).Body!.Pokemon.Stats.MaxHp, 201), "one canonical action applies Hyper Potion's source 200 HP recovery " + effect.Fault);
        Assert.IsTrue(effect.State.TrainerEncounter!.Inventory.ConsumedMask == 1, "action does not debit trainer inventory again");
        var replay = SourceFourActionTurns.Advance(catalog, effect.State, begin.Continuation.Value);
        Assert.IsTrue(!replay.Succeeded && ReferenceEquals(replay.State, effect.State) && replay.Trace.IsEmpty && replay.Events.IsEmpty, "stale action cannot heal or consume again");
        var retry = SourceTrainerEncounterPolicy.PrepareActions(items, catalog, after.State, after.State.Revision);
        Assert.IsTrue(!retry.Succeeded && ReferenceEquals(retry.State, after.State) && retry.Trace.IsEmpty && retry.Events.IsEmpty && retry.Inventory == after.Inventory, "duplicate item preparation rolls back");
        var stalePrepare = SourceTrainerEncounterPolicy.PrepareActions(items, catalog, f, f.Revision + 1);
        Assert.IsTrue(!stalePrepare.Succeeded && ReferenceEquals(stalePrepare.State, f) && stalePrepare.Trace.IsEmpty && stalePrepare.Events.IsEmpty && stalePrepare.Inventory.ConsumedMask == 0, "stale preparation cannot pay for an item");
        var restored = JsonSerializer.Deserialize<SourceBattleFrame>(Json(f))!;
        Assert.IsTrue(SourceTrainerEncounterPolicy.Validate(catalog, restored)is null, "same-process JSON retains authentic proof");
        Assert.IsTrue(Json(after) == Json(SourceTrainerEncounterPolicy.PrepareActions(items, catalog, restored, restored.Revision)), "same JSON-resumed prepared result");
        var setup = SourceTrainerEncounterEntry.Prepare(new(999), "fuchsia-gym-trainer");
        var mismatched = SourceTrainerEncounterEntry.Begin(catalog, setup, made, [Player(0)], 0);
        Assert.IsTrue(!mismatched.Succeeded && mismatched.Rng == setup.InitialRng && mismatched.Events.IsEmpty && mismatched.Trace.IsEmpty, "entry refuses a discontinuous setup/factory RNG chain atomically");
        var fabricated = new TrainerPartyCreationResult(made.Trainer, made.Party, made.Rng, made.Trace);
        Assert.IsTrue(!SourceTrainerEncounterPolicy.CreateContext(catalog, fabricated).Succeeded, "fabricated receipt refused");
        Assert.IsTrue(!SourceTrainerEncounterPolicy.CreateContext(catalog, made with { Trainer = made.Trainer with { AiFlags = 1 } }).Succeeded, "changed AI metadata refused");
        Assert.IsTrue(!SourceTrainerEncounterPolicy.CreateContext(catalog, made with { Party = made.Party.SetItem(0, made.Party[0] with { Pokemon = made.Party[0].Pokemon with { OriginalTrainerId = 17 } }) }).Succeeded, "changed generated identity refused");
        var unknownProof = JsonSerializer.Deserialize<SourceBattleFrame>(Json(restored with { TrainerEncounter = restored.TrainerEncounter!with { RosterProof = "fabricated" } }))!;
        Assert.IsTrue(SourceTrainerEncounterPolicy.Validate(catalog, unknownProof)is not null, "unknown JSON proof explicitly refused");
    }

    [TestMethod]
    public void canonical_reserve_selection_and_Set_Shift_policy()
    {
        var(initial, _) = Fixture("JUGGLER_KIRK");
        var perish = Enemy(initial, m => m with { CalledVolatile = new() { PerishSongTimer = 0 } });
        var switching = SourceFourTrainerAi.DecideSwitch(catalog, perish, new(1), SourceTrainerEncounterPolicy.NoMove);
        Assert.IsTrue(switching.Succeeded && switching.PartyIndex is { } incoming && SourceBattleIdentityRules.LegalReserveIndices(perish, BattleSide.Opponent).Contains(incoming), "Perish Song expiry chooses a living source reserve");
        Assert.IsTrue(switching.Rng == perish.Rng && switching.Trace.IsEmpty, "single Perish Song switch needs no doubles target draw");
        var fainted = Enemy(initial, m => m with { Pokemon = m.Pokemon with { CurrentHp = 0 }, FirstTurnCounter = 0 });
        var replacement = SourceTrainerEncounterPolicy.ChooseReplacement(catalog, fainted, new(1));
        Assert.IsTrue(replacement.Succeeded && replacement.PartyIndex is { } index && SourceBattleIdentityRules.LegalReserveIndices(fainted, BattleSide.Opponent).Contains(index), "faint replacement chooses a legal real party member " + replacement.Fault);
        Assert.IsTrue(replacement.Rng == fainted.Rng && replacement.Trace.IsEmpty, "single faint replacement consumes no doubles target draw");
        var living = SourceTrainerEncounterPolicy.ChooseReplacement(catalog, initial, new(1));
        Assert.IsTrue(!living.Succeeded && living.PartyIndex is null && living.Rng == initial.Rng && living.Trace.IsEmpty, "living opponent cannot request a faint replacement");
        Assert.IsTrue(SourceTrainerEncounterPolicy.CanOfferShift(initial), "live Shift style with reserve offers choice");
        Assert.IsTrue(!SourceTrainerEncounterPolicy.CanOfferShift(initial with { TrainerEncounter = initial.TrainerEncounter!with { BattleStyle = ForestTrainerBattleStyle.Set } }), "Set suppresses choice");
        var noReserve = initial with
        {
            Parties = initial.Parties.SetItem(0, initial.Party(BattleSide.Player)with { Members = [initial.Party(BattleSide.Player).Members[0]] })
        };
        Assert.IsTrue(!SourceTrainerEncounterPolicy.CanOfferShift(noReserve), "Shift requires a real player reserve");
        var observed = SourceTrainerEncounterPolicy.Observe(initial, [new SourceFaintedEvent(1, initial.Turn, new(0))]);
        Assert.IsTrue(!SourceTrainerEncounterPolicy.CanOfferShift(observed), "player faint this turn suppresses choice");
        var selected = SourceTrainerEncounterPolicy.PrepareActions(items, catalog, perish, perish.Revision);
        Assert.IsTrue(selected.Succeeded && selected.Selections.Single().Kind == SourceActionKind.Switch && selected.Selections[0].Incoming!.Value.Index == switching.PartyIndex && selected.Inventory.ConsumedMask == 0, "canonical preparation preserves source switch-before-item-or-move priority");
    }

    [TestMethod]
    public void authentic_KO_EXP_Shift_decline_and_accept()
    {
        var barrier = RewardBarrier(Entry("LEADER_KOGA", "PSYCHIC"));
        Assert.IsTrue(barrier.State.Get(new(1)).Member!.Value.Index == 0 && barrier.State.NextAction == 2, "faint cancels queued trainer action before entry");
        var rewarded = Settle(barrier);
        Assert.IsTrue(rewarded.State.FourTurn!.Phase == SourceFourActionPhase.ReplacementsPending && rewarded.State.Rewards!.CompletedOpponentMask == 1, "EXP completes once before trainer selection");
        var prompt = Ok(SourceFourActionTurns.ContinueTrainerReplacement(catalog, rewarded.State, Token(rewarded.State)));
        Assert.IsTrue(prompt.State.FourTurn!.Phase == SourceFourActionPhase.ShiftPending && prompt.State.FourTurn.TrainerReplacement is not null, "Shift retains chosen opponent");
        int selected = prompt.State.FourTurn.TrainerReplacement!.Incoming.Index;
        var rooted = prompt.State with
        {
            Slots = prompt.State.Slots.SetItem(0, prompt.State.Get(new(0))with { Body = prompt.State.Get(new(0)).Body!with { Rooted = true } })
        };
        var blockedShift = SourceFourActionTurns.ChooseShift(catalog, rooted, Token(rooted), new(BattleSide.Player, 1));
        Assert.IsTrue(blockedShift.Fault == "source_trainer_shift_rooted" && ReferenceEquals(blockedShift.State, rooted) && blockedShift.Events.IsEmpty && blockedShift.Trace.IsEmpty, "rooted player cannot accept Shift");
        var rootedDecline = Ok(SourceFourActionTurns.ChooseShift(catalog, rooted, Token(rooted), null));
        Assert.IsTrue(rootedDecline.State.Get(new(1)).Member!.Value.Index == selected, "rooted player may decline Shift");
        var restored = JsonSerializer.Deserialize<SourceBattleFrame>(Json(prompt.State))!;
        var declined = Ok(SourceFourActionTurns.ChooseShift(catalog, restored, Token(restored), null));
        Assert.IsTrue(declined.State.Get(new(0)).Member!.Value.Index == 0 && declined.State.Get(new(1)).Member!.Value.Index == selected, "decline installs retained trainer selection");
        var accepted = Ok(SourceFourActionTurns.ChooseShift(catalog, prompt.State, Token(prompt.State), new(BattleSide.Player, 1)));
        Assert.IsTrue(accepted.State.Get(new(0)).Member!.Value.Index == 1 && accepted.State.Get(new(1)).Member!.Value.Index == selected, "accept installs actual player then retained trainer");
        var switched = accepted.Events.OfType<SourceCalledPartySwitchedEvent>().ToArray();
        Assert.IsTrue(switched.Length == 2 && switched[0].Side.Side == BattleSide.Player && switched[1].Side.Side == BattleSide.Opponent, "Shift entry event order");
        Assert.IsTrue(accepted.Trace.IsEmpty && declined.Trace.IsEmpty && accepted.State.Rng == prompt.State.Rng, "no reselection or fabricated target RNG");
        var playerParty = prompt.State.Party(BattleSide.Player);
        var fragile = playerParty.Members[1] with
        {
            Pokemon = playerParty.Members[1].Pokemon with
            {
                CurrentHp = 1
            }
        };
        var spikes = prompt.State with
        {
            Parties = prompt.State.Parties.SetItem(0, playerParty with { Members = playerParty.Members.SetItem(1, fragile) }),
            Sides = prompt.State.Sides.SetItem(0, prompt.State.Sides[0] with { Spikes = 1 })
        };
        var hazard = Ok(SourceFourActionTurns.ChooseShift(catalog, spikes, Token(spikes), new(BattleSide.Player, 1)));
        Assert.IsTrue(hazard.State.Get(new(0)).Body!.Pokemon.CurrentHp == 0 && hazard.State.Get(new(1)).Member!.Value.Index == selected, "Spikes KO of the Shift reserve still binds the cached opponent");
        Assert.IsTrue(hazard.State.FourTurn!.Phase == SourceFourActionPhase.ReplacementsPending && hazard.State.PendingSwitches.Length == 1 && hazard.State.PendingSwitches[0].Slot == new BattlerSlot(0) && hazard.State.FourTurn.TrainerReplacement is null, "only the next player replacement remains after Shift hazard KO");
        var hazardEntries = hazard.Events.OfType<SourceCalledPartySwitchedEvent>().ToArray();
        Assert.IsTrue(hazardEntries.Length == 2 && hazardEntries[0].Side.Side == BattleSide.Player && hazardEntries[1].Side.Side == BattleSide.Opponent && hazard.Trace.IsEmpty && hazard.State.Rng == spikes.Rng, "hazard continuation preserves entry order without trainer reselection RNG");
        var hazardReplacement = Ok(SourceFourActionTurns.ChooseReplacement(catalog, hazard.State, Token(hazard.State), new(0), new(BattleSide.Player, 0)));
        Assert.IsTrue(hazardReplacement.State.Get(new(1)).Member!.Value.Index == selected && hazardReplacement.Events.All(e => e is not SourceTrainerShiftOfferedEvent), "next player replacement retains the entered opponent");
        var stale = SourceFourActionTurns.ChooseShift(catalog, accepted.State, Token(prompt.State), null);
        Assert.IsTrue(!stale.Succeeded && ReferenceEquals(stale.State, accepted.State) && stale.Events.IsEmpty && stale.Trace.IsEmpty, "consumed Shift continuation is atomic");
        var ready = accepted;
        for (int i = 0; i < 4 && ready.State.FourTurn is not null; i++)
            ready = Ok(SourceFourActionTurns.Advance(catalog, ready.State, Token(ready.State)));
        Assert.IsTrue(ready.CompletedCleanTurn && ready.State.Turn == 1 && !ready.State.TrainerEncounter!.PlayerFaintedThisTurn, "one residual completion and next ready");
    }

    [TestMethod]
    public void authentic_mutual_KO_requires_player_replacement_before_trainer()
    {
        var barrier = RewardBarrier(Entry("LEADER_KOGA", "EXPLOSION"));
        Assert.IsTrue(barrier.State.Get(new(0)).Body!.Pokemon.CurrentHp == 0 && barrier.State.Get(new(1)).Body!.Pokemon.CurrentHp == 0, "both actual occupants fainted");
        var rewarded = Settle(barrier);
        Assert.IsTrue(rewarded.State.PendingSwitches[0].Slot == new BattlerSlot(0), "player replacement first after EXP");
        var early = SourceFourActionTurns.ContinueTrainerReplacement(catalog, rewarded.State, Token(rewarded.State));
        Assert.IsTrue(!early.Succeeded && ReferenceEquals(early.State, rewarded.State) && early.Trace.IsEmpty, "cannot skip player choice");
        var player = Ok(SourceFourActionTurns.ChooseReplacement(catalog, rewarded.State, Token(rewarded.State), new(0), new(BattleSide.Player, 1)));
        Assert.IsTrue(player.State.PendingSwitches[0].Slot == new BattlerSlot(1) && player.State.TrainerEncounter!.PlayerFaintedThisTurn, "mutual-faint suppression survives player entry");
        var trainer = Ok(SourceFourActionTurns.ContinueTrainerReplacement(catalog, player.State, Token(player.State)));
        Assert.IsTrue(trainer.State.FourTurn!.Phase != SourceFourActionPhase.ShiftPending && trainer.Events.All(e => e is not SourceTrainerShiftOfferedEvent), "no Shift after mutual faint");
        Assert.IsTrue(trainer.State.Get(new(0)).Member!.Value.Index == 1 && trainer.State.Get(new(1)).Member!.Value.Index != 0, "both real reserve identities entered");
    }

    [TestMethod]
    public void authentic_terminal_trainer_KO_preserves_action_and_residual_timing()
    {
        var barrier = RewardBarrier(Entry("JUGGLER_KAYDEN", "SHADOW_BALL"));
        var before = barrier.State.Rng;
        var finished = Settle(barrier);
        Assert.IsTrue(finished.State.Outcome == WildBattleOutcome.Won && finished.State.FourTurn!.Phase == SourceFourActionPhase.BattleEnded, "terminal trainer win published after EXP");
        Assert.IsTrue(finished.State.Rng == before && finished.Trace.IsEmpty && finished.Events.All(e => e is not SourceTurnReadyEvent && e is not SourceOrderEvent), "terminal action skips residual/ready RNG");
        Assert.IsTrue(finished.State.Rewards!.CompletedOpponentMask == 1 && finished.State.PendingSwitches.IsEmpty, "terminal reward is settled once without replacement");
        var residualFrame = Entry("JUGGLER_KAYDEN", "SPLASH");
        var cell = residualFrame.Get(new(1));
        var poisoned = cell.Body!with
        {
            Pokemon = cell.Body!.Pokemon with
            {
                CurrentHp = 1
            },
            Status = WildBattleStatus.Poison
        };
        var party = residualFrame.Party(BattleSide.Opponent);
        residualFrame = residualFrame with
        {
            Slots = residualFrame.Slots.SetItem(1, cell with { Body = poisoned }),
            Parties = residualFrame.Parties.SetItem(1, party with { Members = party.Members.SetItem(0, new(poisoned.Pokemon, poisoned.HeldItem, poisoned.Status)) })
        };
        var residual = Start(residualFrame);
        for (int i = 0; i < 8 && residual.State.FourTurn!.Phase is SourceFourActionPhase.Actions or SourceFourActionPhase.EndTurn or SourceFourActionPhase.EndTurnResume; i++)
            residual = Ok(SourceFourActionTurns.Advance(catalog, residual.State, Token(residual.State)));
        Assert.IsTrue(residual.State.FourTurn!.Phase == SourceFourActionPhase.RewardsPending && residual.State.FourTurn.EndTurn is not null && residual.State.Get(new(1)).Body!.Pokemon.CurrentHp == 0, "residual KO pauses EXP with its saved end-turn cursor");
        var settledResidual = Settle(residual);
        Assert.IsTrue(settledResidual.State.FourTurn!.Phase == SourceFourActionPhase.EndTurnResume && settledResidual.State.Outcome == WildBattleOutcome.Ongoing, "residual terminal EXP returns to retained cursor before publishing outcome");
        var ended = Ok(SourceFourActionTurns.Advance(catalog, settledResidual.State, Token(settledResidual.State)));
        Assert.IsTrue(ended.State.Outcome == WildBattleOutcome.Won && ended.State.FourTurn!.Phase == SourceFourActionPhase.BattleEnded && ended.Events.All(e => e is not SourceTurnReadyEvent && e is not SourceFaintedEvent), "terminal residual resumes without duplicate faint or new ready");
    }
}
