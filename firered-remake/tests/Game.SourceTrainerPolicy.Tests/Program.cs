using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

if (args.Length != 1) return 2;
var root = Path.GetFullPath(args[0]);
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(root); var catalog = proof.Catalog;
var items = SourceBattleItemCatalogLoader.LoadAuthenticated(root);
var raw = PretRocketHideoutPokemon.Load(root).Catalog;
var extra = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules: true);
var prior = PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(raw with { Moves = raw.Moves.SetItems(extra.Moves) }, root, root).Catalog, root).Catalog, root).Catalog;
var route = PretFuchsiaRoutePokemon.Load(prior, root);
var legacyCatalog = PretMetronomeMoveCatalog.Load(root).Extend(route.Catalog);
var loaded = PretFuchsiaGymTrainers.Load(root);
var gym = new FuchsiaGymTrainerCatalog(loaded.SourceCommit, loaded.Profile, loaded.CompleteSourceRoster, loaded.Trainers, loaded.Battles);
int checks = 0, failed = 0; var observations = new List<object>();
void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
void Test(string name, Action test) { try { test(); observations.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; observations.Add(new { name, passed = false, error = ex.ToString() }); Console.WriteLine("FAIL " + name + " " + ex); } }
string Json<T>(T value) => JsonSerializer.Serialize(value);
WildBattleCombatant Player(int pid) => new(PokemonFactory.Create(legacyCatalog, new(new("pret:SPECIES_CLEFAIRY"), 50, 42,
    IvPolicy.Fixed(31), PersonalityPolicy.Fixed((uint)pid)), new(0)).Pokemon);
(ForestTrainerCombatState Legacy, TrainerPartyCreationResult Made) Fixture(string name, uint seed = 42)
{
    TrainerId id = new("pret:TRAINER_" + name);
    var start = ForestTrainerCombatRules.Start(gym, legacyCatalog, id, [Player(0), Player(3)], 0, new(seed));
    Check(start.Succeeded, "baseline fixture entry " + start.Fault);
    var rng = new RngState(seed);
    for (int i = 0; i < 4; i++) rng = Gen3Rng.Next(rng, "fixture-entry-ai", 16).State;
    var made = FuchsiaGymTrainerPartyFactory.Create(gym, legacyCatalog, id, rng);
    Check(made.Party.Select(x => x.Pokemon.Personality).SequenceEqual(start.State!.OpponentParty.Select(x => x.Pokemon.Personality)), "authentic generated roster agrees");
    return (start.State, made);
}
SourceBattleFrame Frame(ForestTrainerCombatState legacy, TrainerPartyCreationResult made)
{
    var imported = SinglesBattleAdapter.Import(legacyCatalog, legacy.Battle);
    Check(imported.Succeeded, "comparison-only import " + imported.Fault);
    var context = SourceTrainerEncounterPolicy.CreateContext(catalog, made, legacy.BattleStyle);
    Check(context.Succeeded, "authentic creation receipt " + context.Fault);
    var frame = imported.Frame! with { SourceMechanicsRulesVersion = SourceBattleKernel.Version, SinglesEncoding = null,
        TrainerEncounter = context.Context! with { StaleCurrentMove = legacy.StaleCurrentMove } };
    Check(SourceFourActionTurns.ValidateCapability(catalog, frame) is null, "canonical capability " + SourceFourActionTurns.ValidateCapability(catalog, frame));
    Check(SourceTrainerEncounterPolicy.Validate(catalog, frame) is null, "trainer policy " + SourceTrainerEncounterPolicy.Validate(catalog, frame));
    return frame;
}
ForestTrainerCombatState Enemy(ForestTrainerCombatState state, Func<WildBattleCombatant, WildBattleCombatant> edit)
{
    var mon = edit(state.Battle.Opponent); var p = state.Battle.CalledParty!;
    return state with { OpponentParty = state.OpponentParty.SetItem(state.OpponentIndex, mon), Battle = state.Battle with {
        Opponent = mon, CalledParty = p with { Opponent = p.Opponent.SetItem(state.OpponentIndex,
            new(mon.Pokemon, mon.HeldItem, mon.Status)) } } };
}
Test("flags1/7 scores, RNG and controller bypasses", () =>
{
    foreach (string name in new[] { "JUGGLER_KIRK", "LEADER_KOGA" })
    foreach (uint seed in new uint[] { 1, 42, 913 })
    {
        var (legacy, made) = Fixture(name, seed); var frame = Frame(legacy, made);
        var setup = SourceTrainerEncounterEntry.Prepare(new(seed), "fuchsia-gym-trainer");
        var entered = SourceTrainerEncounterEntry.Begin(catalog, setup, made, [Player(0), Player(3)], 0,
            terrain: WildBattleTerrain.Building);
        var originalEntry = ForestTrainerCombatRules.Start(gym, legacyCatalog, made.Trainer.Id, [Player(0), Player(3)], 0, new(seed));
        Check(entered.Succeeded, "canonical entry " + entered.Fault);
        Check(entered.Rng == legacy.Battle.Rng && Json(entered.Trace) == Json(originalEntry.Trace), "exact setup/factory/order/ready entry RNG trace");
        Check(Json(entered.State!.Parties) == Json(frame.Parties) && Json(entered.State.Slots) == Json(frame.Slots), "same actual entry party bodies and registers");
        Check(Json(entered.Events.Skip(1).Select(SourceBattleEventAdapter.Export).ToImmutableArray()) == Json(originalEntry.Events), "same ordered battle entry events");
        Check(entered.Events[0] is SourceTrainerStartedEvent { OpponentCount: > 0 } started && started.Trainer == made.Trainer.Id,
            "typed trainer-start provenance precedes battle-start");
        var before = SourceTrainerAi.Choose(legacyCatalog, legacy);
        var after = SourceFourTrainerAi.ChooseMove(catalog, made.Trainer, frame, frame.Revision, new(1));
        Check(before.Succeeded && after.Succeeded, "pure move selection " + before.Diagnostic + " / " + after.Fault);
        Check(before.MoveSlot == after.Choices[0].Selection.MoveSlot && before.Scores.SequenceEqual(after.Choices[0].Scores), "exact selected move and scores");
        Check(before.Rng == after.State.Rng && Json(before.Trace) == Json(after.Trace), "exact selection RNG trace");
        Check(before.SimulatedDamagePercent.SequenceEqual(after.Choices[0].SimulatedDamagePercent) && Json(before.AbilityReads) == Json(after.Choices[0].AbilityReads), "same simulated damage and ability knowledge");
    }
    var (initial, created) = Fixture("JUGGLER_KIRK");
    var encored = Enemy(initial, m => m with { EncoreTurnsRemaining = 2, EncoreMoveSlot = 0, EncoredMove = m.Pokemon.Moves[0].Move });
    var f = Frame(encored, created);
    var forced = SourceFourTrainerAi.ChooseMove(catalog, created.Trainer, f, f.Revision, new(1));
    Check(forced.Succeeded && forced.Selections[0].MoveSlot == 0 && forced.Trace.IsEmpty && forced.State.Rng == f.Rng, "Encore bypasses setup and scoring RNG");
    var exhausted = Enemy(encored, m => m with { Pokemon = m.Pokemon with { Moves = m.Pokemon.Moves.Select(x => x with { CurrentPp = 0 }).ToImmutableArray() } });
    f = Frame(exhausted, created);
    var struggle = SourceFourTrainerAi.ChooseMove(catalog, created.Trainer, f, f.Revision, new(1));
    Check(struggle.Succeeded && struggle.Selections[0].IsStruggle && struggle.Selections[0].Target == new BattlerSlot(0) && struggle.Trace.IsEmpty, "Struggle precedes Encore without a fabricated target draw");
});
Test("authentic receipt and item transaction survive JSON", () =>
{
    var (initial, made) = Fixture("LEADER_KOGA");
    var legacy = Enemy(initial, m => m with { Pokemon = m.Pokemon with { CurrentHp = 1 } });
    var f = Frame(legacy, made);
    var beforeSwitch = SourceTrainerAi.DecideSwitch(legacyCatalog, legacy);
    var beforeItem = SourceTrainerAi.DecideItem(legacyCatalog, legacy);
    var after = SourceTrainerEncounterPolicy.PrepareActions(items, catalog, f, f.Revision);
    Check(beforeSwitch.Succeeded && beforeSwitch.PartyIndex is null && beforeItem.Succeeded && beforeItem.ItemSlot is not null, "baseline chooses medicine");
    Check(after.Succeeded && after.Actions.Single().Selection.Kind == SourceActionKind.Item && after.Actions[0].ItemChoice!.InventorySlot == beforeItem.ItemSlot, "same item choice " + after.Fault);
    Check(after.State.TrainerEncounter!.StaleCurrentMove == f.TrainerEncounter!.StaleCurrentMove, "singles scorer does not overwrite action stale-move register");
    Check(after.Inventory.ConsumedMask == 1 && after.State.TrainerEncounter!.Inventory == after.Inventory && f.TrainerEncounter!.Inventory.ConsumedMask == 0, "inventory commits with prepared receipt only");
    Check(after.State.Get(new(1)).Body!.Pokemon.CurrentHp == 1 && after.State.PreparedMedicines?.Length == 1, "trainer effect deferred until existing queue action");
    var begin = SourceFourActionTurns.BeginActions(catalog, after.State, after.State.Revision,
        [new(after.State.Get(new(0)).Occupant!.Value, SourceActionKind.Move, 0, new BattlerSlot(1)), after.Selections.Single()]);
    Check(begin.Succeeded, "existing turn owner accepts prepared single trainer item " + begin.Fault);
    var effect = SourceFourActionTurns.Advance(catalog, begin.State, begin.Continuation!.Value);
    Check(effect.Succeeded && effect.State.Get(new(1)).Body!.Pokemon.CurrentHp == beforeItem.HpAfter,
        "one canonical action applies exactly the source trainer medicine effect " + effect.Fault);
    Check(effect.State.TrainerEncounter!.Inventory.ConsumedMask == 1, "action does not debit trainer inventory again");
    var replay = SourceFourActionTurns.Advance(catalog, effect.State, begin.Continuation.Value);
    Check(!replay.Succeeded && ReferenceEquals(replay.State, effect.State) && replay.Trace.IsEmpty, "stale action cannot heal or consume again");
    var retry = SourceTrainerEncounterPolicy.PrepareActions(items, catalog, after.State, after.State.Revision);
    Check(!retry.Succeeded && ReferenceEquals(retry.State, after.State) && retry.Trace.IsEmpty && retry.Events.IsEmpty && retry.Inventory == after.Inventory, "duplicate item preparation rolls back");
    var restored = JsonSerializer.Deserialize<SourceBattleFrame>(Json(f))!;
    Check(SourceTrainerEncounterPolicy.Validate(catalog, restored) is null, "same-process JSON retains authentic proof");
    Check(Json(after) == Json(SourceTrainerEncounterPolicy.PrepareActions(items, catalog, restored, restored.Revision)), "same JSON-resumed prepared result");
    var setup = SourceTrainerEncounterEntry.Prepare(new(999), "fuchsia-gym-trainer");
    var mismatched = SourceTrainerEncounterEntry.Begin(catalog, setup, made, [Player(0)], 0);
    Check(!mismatched.Succeeded && mismatched.Rng == setup.InitialRng && mismatched.Events.IsEmpty && mismatched.Trace.IsEmpty,
        "entry refuses a discontinuous setup/factory RNG chain atomically");
    var fabricated = new TrainerPartyCreationResult(made.Trainer, made.Party, made.Rng, made.Trace);
    Check(!SourceTrainerEncounterPolicy.CreateContext(catalog, fabricated).Succeeded, "fabricated receipt refused");
    Check(!SourceTrainerEncounterPolicy.CreateContext(catalog, made with { Trainer = made.Trainer with { AiFlags = 1 } }).Succeeded, "changed AI metadata refused");
    Check(!SourceTrainerEncounterPolicy.CreateContext(catalog, made with { Party = made.Party.SetItem(0, made.Party[0] with { Pokemon = made.Party[0].Pokemon with { OriginalTrainerId = 17 } }) }).Succeeded, "changed generated identity refused");
    Check(SourceTrainerEncounterPolicy.Validate(catalog, restored with { TrainerEncounter = restored.TrainerEncounter! with { RosterProof = "fabricated" } }) is not null, "unknown proof explicitly refused");
});
Test("single switch/replacement baseline and Shift policy", () =>
{
    var (initial, made) = Fixture("JUGGLER_KIRK");
    var perish = Enemy(initial, m => m with { CalledVolatile = new() { PerishSongTimer = 0 } });
    var f = Frame(perish, made);
    var before = SourceTrainerAi.DecideSwitch(legacyCatalog, perish);
    var after = SourceFourTrainerAi.DecideSwitch(catalog, f, new(1), ForestTrainerCombatRules.NoMove);
    Check(before.Succeeded && after.Succeeded && before.PartyIndex == after.PartyIndex && after.PartyIndex is not null, "same Perish Song reserve selection");
    Check(before.Rng == after.Rng && Json(before.Trace) == Json(after.Trace), "replacement target does not consume doubles RNG");
    var fainted = Enemy(initial, m => m with { Pokemon = m.Pokemon with { CurrentHp = 0 }, FirstTurnCounter = 0 });
    var beforeReplacement = SourceTrainerAi.ChooseReplacement(legacyCatalog, fainted);
    var readyFrame = Frame(initial, made); var cell = readyFrame.Get(new(1)); var roster = readyFrame.Party(BattleSide.Opponent);
    var faintFrame = readyFrame with { Slots = readyFrame.Slots.SetItem(1, cell with { Body = fainted.Battle.Opponent }),
        Parties = readyFrame.Parties.SetItem(1, roster with { Members = roster.Members.SetItem(0,
            new(fainted.Battle.Opponent.Pokemon, fainted.Battle.Opponent.HeldItem)) }) };
    var replacement = SourceTrainerEncounterPolicy.ChooseReplacement(catalog, faintFrame, new(1));
    Check(beforeReplacement.Succeeded && replacement.Succeeded && beforeReplacement.PartyIndex == replacement.PartyIndex &&
        beforeReplacement.Scores.SequenceEqual(replacement.Scores), "same faint replacement index and scores " + beforeReplacement.Diagnostic + "/" + replacement.Fault);
    Check(replacement.Rng == faintFrame.Rng && replacement.Trace.IsEmpty, "single faint replacement consumes no doubles target draw");
    Check(SourceTrainerEncounterPolicy.CanOfferShift(f), "live Shift style with reserve offers choice");
    Check(!SourceTrainerEncounterPolicy.CanOfferShift(f with { TrainerEncounter = f.TrainerEncounter! with { BattleStyle = ForestTrainerBattleStyle.Set } }), "Set suppresses choice");
    var observed = SourceTrainerEncounterPolicy.Observe(f, [new SourceFaintedEvent(1, f.Turn, new(0))]);
    Check(!SourceTrainerEncounterPolicy.CanOfferShift(observed), "player faint this turn suppresses choice");
    var selected = SourceTrainerEncounterPolicy.PrepareActions(items, catalog, f, f.Revision);
    Check(selected.Succeeded && selected.Selections.Single().Kind == SourceActionKind.Switch && selected.Selections[0].Incoming!.Value.Index == before.PartyIndex, "canonical preparation preserves source nonmove priority");
});

SourceFourActionResult Ok(SourceFourActionResult result)
{
    Check(result.Succeeded, "canonical lifecycle " + result.Fault);
    Check(SourceBattleIdentityRules.Validate(result.State) is null, "canonical identity after lifecycle step");
    return result;
}
SourceTurnContinuation Token(SourceBattleFrame state) => new(state.Revision, state.EventSequence, state.NextAction, state.FourTurn!.Phase);
SourceBattleFrame Entry(string trainer, string move, bool reserve = true)
{
    var pokemon = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_CLEFAIRY"), 100, 42,
        IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
    MoveId id = new("pret:MOVE_" + move); var definition = catalog.Moves[id];
    var player = new WildBattleCombatant(pokemon with { Moves = [new(id, definition.Pp, definition.Pp)] });
    ImmutableArray<WildBattleCombatant> players = reserve ? [player, Player(3)] : [player];
    var rewards = new SourceFourRewardState { Context = new(SourceRewardBattleKind.OrdinaryTrainer, 42, "RED", "MAPSEC_FUCHSIA_CITY"),
        Metadata = players.Select((_, i) => new SourceRewardMetadata(new(BattleSide.Player, i), "RED", "MAPSEC_PALLET_TOWN", "ITEM_POKE_BALL", SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray() };
    var setup = SourceTrainerEncounterEntry.Prepare(new(17), "fuchsia-gym-trainer");
    var made = FuchsiaGymTrainerPartyFactory.Create(gym, legacyCatalog, new("pret:TRAINER_" + trainer), setup.Rng);
    var entered = SourceTrainerEncounterEntry.Begin(catalog, setup, made, players, 0, rewards: rewards);
    Check(entered.Succeeded, "authentic lifecycle entry " + entered.Fault);
    return entered.State!;
}
SourceFourActionResult Start(SourceBattleFrame frame)
{
    var enemy = SourceTrainerEncounterPolicy.PrepareActions(items, catalog, frame, frame.Revision);
    Check(enemy.Succeeded, "authentic controller " + enemy.Fault);
    return Ok(SourceFourActionTurns.BeginActions(catalog, enemy.State, enemy.State.Revision,
        [new(enemy.State.Get(new(0)).Occupant!.Value, SourceActionKind.Move, 0, new BattlerSlot(1)), .. enemy.Selections]));
}
SourceFourActionResult RewardBarrier(SourceBattleFrame frame)
{
    var result = Start(frame);
    for (int i = 0; i < 8 && result.State.FourTurn!.Phase == SourceFourActionPhase.Actions; i++)
        result = Ok(SourceFourActionTurns.Advance(catalog, result.State, Token(result.State)));
    Check(result.State.FourTurn!.Phase == SourceFourActionPhase.RewardsPending, "KO pauses for EXP before entries");
    return result;
}
SourceFourActionResult Settle(SourceFourActionResult pending)
{
    var result = pending;
    for (int i = 0; i < 8 && result.State.FourTurn!.Phase == SourceFourActionPhase.RewardsPending; i++)
        result = Ok(SourceFourRewards.Settle(catalog, result.State, Token(result.State)));
    Check(result.State.FourTurn!.Phase != SourceFourActionPhase.RewardsPending, "bounded recipient settlement completed");
    return result;
}
Test("authentic KO EXP Shift decline and accept", () =>
{
    var barrier = RewardBarrier(Entry("LEADER_KOGA", "PSYCHIC"));
    Check(barrier.State.Get(new(1)).Member!.Value.Index == 0 && barrier.State.NextAction == 2, "faint cancels queued trainer action before entry");
    var rewarded = Settle(barrier);
    Check(rewarded.State.FourTurn!.Phase == SourceFourActionPhase.ReplacementsPending && rewarded.State.Rewards!.CompletedOpponentMask == 1, "EXP completes once before trainer selection");
    var prompt = Ok(SourceFourActionTurns.ContinueTrainerReplacement(catalog, rewarded.State, Token(rewarded.State)));
    Check(prompt.State.FourTurn!.Phase == SourceFourActionPhase.ShiftPending && prompt.State.FourTurn.TrainerReplacement is not null, "Shift retains chosen opponent");
    int selected = prompt.State.FourTurn.TrainerReplacement!.Incoming.Index;
    var rooted = prompt.State with { Slots = prompt.State.Slots.SetItem(0, prompt.State.Get(new(0)) with {
        Body = prompt.State.Get(new(0)).Body! with { Rooted = true } }) };
    var blockedShift = SourceFourActionTurns.ChooseShift(catalog, rooted, Token(rooted), new(BattleSide.Player, 1));
    Check(blockedShift.Fault == "source_trainer_shift_rooted" && ReferenceEquals(blockedShift.State, rooted) &&
        blockedShift.Events.IsEmpty && blockedShift.Trace.IsEmpty, "rooted player cannot accept Shift");
    var rootedDecline = Ok(SourceFourActionTurns.ChooseShift(catalog, rooted, Token(rooted), null));
    Check(rootedDecline.State.Get(new(1)).Member!.Value.Index == selected, "rooted player may decline Shift");
    var restored = JsonSerializer.Deserialize<SourceBattleFrame>(Json(prompt.State))!;
    var declined = Ok(SourceFourActionTurns.ChooseShift(catalog, restored, Token(restored), null));
    Check(declined.State.Get(new(0)).Member!.Value.Index == 0 && declined.State.Get(new(1)).Member!.Value.Index == selected, "decline installs retained trainer selection");
    var accepted = Ok(SourceFourActionTurns.ChooseShift(catalog, prompt.State, Token(prompt.State), new(BattleSide.Player, 1)));
    Check(accepted.State.Get(new(0)).Member!.Value.Index == 1 && accepted.State.Get(new(1)).Member!.Value.Index == selected, "accept installs actual player then retained trainer");
    var switched = accepted.Events.OfType<SourceCalledPartySwitchedEvent>().ToArray();
    Check(switched.Length == 2 && switched[0].Side.Side == BattleSide.Player && switched[1].Side.Side == BattleSide.Opponent, "Shift entry event order");
    Check(accepted.Trace.IsEmpty && declined.Trace.IsEmpty && accepted.State.Rng == prompt.State.Rng, "no reselection or fabricated target RNG");
    var stale = SourceFourActionTurns.ChooseShift(catalog, accepted.State, Token(prompt.State), null);
    Check(!stale.Succeeded && ReferenceEquals(stale.State, accepted.State) && stale.Events.IsEmpty && stale.Trace.IsEmpty, "consumed Shift continuation is atomic");
    var ready = accepted;
    for (int i = 0; i < 4 && ready.State.FourTurn is not null; i++) ready = Ok(SourceFourActionTurns.Advance(catalog, ready.State, Token(ready.State)));
    Check(ready.CompletedCleanTurn && ready.State.Turn == 1 && !ready.State.TrainerEncounter!.PlayerFaintedThisTurn, "one residual completion and next ready");
});
Test("authentic mutual KO requires player replacement before trainer", () =>
{
    var barrier = RewardBarrier(Entry("LEADER_KOGA", "EXPLOSION"));
    Check(barrier.State.Get(new(0)).Body!.Pokemon.CurrentHp == 0 && barrier.State.Get(new(1)).Body!.Pokemon.CurrentHp == 0, "both actual occupants fainted");
    var rewarded = Settle(barrier);
    Check(rewarded.State.PendingSwitches[0].Slot == new BattlerSlot(0), "player replacement first after EXP");
    var early = SourceFourActionTurns.ContinueTrainerReplacement(catalog, rewarded.State, Token(rewarded.State));
    Check(!early.Succeeded && ReferenceEquals(early.State, rewarded.State) && early.Trace.IsEmpty, "cannot skip player choice");
    var player = Ok(SourceFourActionTurns.ChooseReplacement(catalog, rewarded.State, Token(rewarded.State), new(0), new(BattleSide.Player, 1)));
    Check(player.State.PendingSwitches[0].Slot == new BattlerSlot(1) && player.State.TrainerEncounter!.PlayerFaintedThisTurn, "mutual-faint suppression survives player entry");
    var trainer = Ok(SourceFourActionTurns.ContinueTrainerReplacement(catalog, player.State, Token(player.State)));
    Check(trainer.State.FourTurn!.Phase != SourceFourActionPhase.ShiftPending && trainer.Events.All(e => e is not SourceTrainerShiftOfferedEvent), "no Shift after mutual faint");
    Check(trainer.State.Get(new(0)).Member!.Value.Index == 1 && trainer.State.Get(new(1)).Member!.Value.Index != 0, "both real reserve identities entered");
});
Test("authentic terminal trainer KO preserves action and residual timing", () =>
{
    var barrier = RewardBarrier(Entry("JUGGLER_KAYDEN", "SHADOW_BALL"));
    var before = barrier.State.Rng; var finished = Settle(barrier);
    Check(finished.State.Outcome == WildBattleOutcome.Won && finished.State.FourTurn!.Phase == SourceFourActionPhase.BattleEnded, "terminal trainer win published after EXP");
    Check(finished.State.Rng == before && finished.Trace.IsEmpty && finished.Events.All(e => e is not SourceTurnReadyEvent && e is not SourceOrderEvent), "terminal action skips residual/ready RNG");
    Check(finished.State.Rewards!.CompletedOpponentMask == 1 && finished.State.PendingSwitches.IsEmpty, "terminal reward is settled once without replacement");
    var residualFrame = Entry("JUGGLER_KAYDEN", "SPLASH"); var cell = residualFrame.Get(new(1));
    var poisoned = cell.Body! with { Pokemon = cell.Body!.Pokemon with { CurrentHp = 1 }, Status = WildBattleStatus.Poison };
    var party = residualFrame.Party(BattleSide.Opponent);
    residualFrame = residualFrame with { Slots = residualFrame.Slots.SetItem(1, cell with { Body = poisoned }),
        Parties = residualFrame.Parties.SetItem(1, party with { Members = party.Members.SetItem(0, new(poisoned.Pokemon, poisoned.HeldItem, poisoned.Status)) }) };
    var residual = Start(residualFrame);
    for (int i = 0; i < 8 && residual.State.FourTurn!.Phase is SourceFourActionPhase.Actions or SourceFourActionPhase.EndTurn or SourceFourActionPhase.EndTurnResume; i++)
        residual = Ok(SourceFourActionTurns.Advance(catalog, residual.State, Token(residual.State)));
    Check(residual.State.FourTurn!.Phase == SourceFourActionPhase.RewardsPending && residual.State.FourTurn.EndTurn is not null &&
        residual.State.Get(new(1)).Body!.Pokemon.CurrentHp == 0, "residual KO pauses EXP with its saved end-turn cursor");
    var settledResidual = Settle(residual);
    Check(settledResidual.State.FourTurn!.Phase == SourceFourActionPhase.EndTurnResume && settledResidual.State.Outcome == WildBattleOutcome.Ongoing,
        "residual terminal EXP returns to retained cursor before publishing outcome");
    var ended = Ok(SourceFourActionTurns.Advance(catalog, settledResidual.State, Token(settledResidual.State)));
    Check(ended.State.Outcome == WildBattleOutcome.Won && ended.State.FourTurn!.Phase == SourceFourActionPhase.BattleEnded &&
        ended.Events.All(e => e is not SourceTurnReadyEvent && e is not SourceFaintedEvent), "terminal residual resumes without duplicate faint or new ready");
});
Console.WriteLine(Json(new { runtime = Environment.Version.ToString(), cases = observations.Count, checks, failures = failed, observations }));
return failed == 0 && observations.Count == 6 && checks > 180 ? 0 : 1;
