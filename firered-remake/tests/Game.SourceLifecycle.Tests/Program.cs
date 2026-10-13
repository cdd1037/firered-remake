using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Capture;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length is < 1 or > 2 || args.Length == 2 && args[1] != "--four-parity") { Console.Error.WriteLine("Usage: Game.SourceLifecycle.Tests <authenticated-content-project> [--four-parity]"); return 2; }
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(Path.GetFullPath(args[0]));
var catalog = proof.Catalog;
int groups = 0, checks = 0, failures = 0;
var observations = new List<object>();
void Check(bool condition, string message) { checks++; if (!condition) throw new InvalidDataException(message); }
void Test(string name, Action run)
{
    groups++;
    try { run(); Console.WriteLine("PASS " + name); observations.Add(new { name, passed = true }); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + " " + ex); observations.Add(new { name, passed = false, error = ex.ToString() }); }
}
string Json<T>(T value) => JsonSerializer.Serialize(value);
MoveId Id(string name) => new("pret:MOVE_" + name);
WildBattleCombatant Mon(string species = "PIDGEY", int level = 50, string move = "TACKLE", int? hp = null)
{
    var pokemon = SourcePokemonFactory.Create(proof,
        new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
    var definition = catalog.Moves[Id(move)];
    return new(pokemon with { CurrentHp = hp ?? pokemon.CurrentHp, Moves = [new(definition.Id, definition.Pp, definition.Pp)] });
}
WildBattleCalledPartyMember Member(WildBattleCombatant body) => new(WildBattleTransformationRules.PersistentPokemon(body),
    body.HeldItem, WildBattleTransformationRules.PersistentStatus(body), KnockedOffItem: body.KnockedOffItem);
SourceFourRewardState Rewards(int players, SourceRewardBattleKind kind) => new()
{
    Context = new(kind, 42, "RED", "MAPSEC_ROUTE_1"),
    Metadata = Enumerable.Range(0, players).Select(i => new SourceRewardMetadata(new(BattleSide.Player, i),
        "RED", "MAPSEC_PALLET_TOWN", "ITEM_POKE_BALL", SourceRewardPokerusHistory.NeverInfected)).ToImmutableArray()
};
SourceBattleFrame Single(WildBattleCombatant? player = null, WildBattleCombatant? opponent = null,
    params WildBattleCombatant[] reserves)
{
    player ??= Mon(); opponent ??= Mon("RATTATA");
    var players = new[] { Member(player) }.Concat(reserves.Select(Member)).ToImmutableArray();
    return new()
    {
        Layout = SourceBattleLayout.Singles, KoReplacementTiming = KoReplacementTiming.SourceImmediate,
        SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Wild,
        Rng = new(31), SourceAbsentMask = 12, TurnStartAbsentMask = 12,
        Parties = [new(BattleSide.Player, players), new(BattleSide.Opponent, [Member(opponent)])],
        Slots = [new(new(0), new(BattleSide.Player, 0), 1, player), new(new(1), new(BattleSide.Opponent, 0), 1, opponent),
            new(new(2), null, 0, null), new(new(3), null, 0, null)],
        Rewards = Rewards(players.Length, SourceRewardBattleKind.OrdinaryWild)
    };
}
SourceBattleFrame Four(bool playerKo = false)
{
    WildBattleCombatant[] mons = playerKo
        ? [Mon(level: 5, move: "BARRIER", hp: 1), Mon("RATTATA", move: "QUICK_ATTACK"), Mon("SPEAROW", move: "BARRIER"), Mon("DODUO", move: "BARRIER")]
        : [Mon(), Mon("RATTATA"), Mon("SPEAROW", move: "PECK"), Mon("DODUO", move: "PECK")];
    var players = new[] { Member(mons[0]), Member(mons[2]) }.Concat(playerKo ? [Member(Mon(move: "BARRIER"))] : Array.Empty<WildBattleCalledPartyMember>()).ToImmutableArray();
    return new()
    {
        Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
        SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer,
        Rng = new(31), Parties = [new(BattleSide.Player, players), new(BattleSide.Opponent, [Member(mons[1]), Member(mons[3])])],
        Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, mons[i])).ToImmutableArray(),
        Rewards = Rewards(players.Length, SourceRewardBattleKind.OrdinaryTrainer)
    };
}
void Valid(SourceBattleFrame state)
{
    Check(SourceBattleIdentityRules.Validate(state) is null, "canonical output: " + SourceBattleIdentityRules.Validate(state));
    if (state.Layout != SourceBattleLayout.Singles) return;
    Check(typeof(SourceBattleFrame).GetProperty("WildTurn") is null && typeof(SourceBattleFrame).GetProperty("SinglesEncoding") is null && state.DeferredKoVacancyMask == 0, "retired owner and encoding absent; no doubles KO vacancy in singles");
    Check((state.SourceAbsentMask & 12) == 12 && (state.TurnStartAbsentMask & 12) == 12, "unused singles positions remain source absent");
    Check(state.Slots.Skip(2).All(s => s.Body is null && s.Member is null && s.EntrySerial == 0 && s.Registers == new SourceSlotRegisters() && s.Position == new SourcePositionRegisters()), "no phantom battlers, identities or registers");
    Check(state.Queue.IsEmpty || state.Queue.Length == 2 && state.Queue.All(a => a.Slot.Value < 2 && a.Target?.Value is not (>= 2)), "exact two-position queue");
}
SourceFourActionResult Ok(SourceFourActionResult result)
{
    Check(result.Succeeded, "accepted: " + result.Fault); Valid(result.State); return result;
}
SourceTurnContinuation Token(SourceBattleFrame state) => new(state.Revision, state.EventSequence, state.NextAction, state.FourTurn!.Phase);
ImmutableArray<SourceActionSelection> Choices(SourceBattleFrame state) => state.Slots.Where(s => state.CanAct(s.Slot)).Select(s =>
{
    var move = catalog.Moves[s.Body!.Pokemon.Moves[0].Move];
    return new SourceActionSelection(s.Occupant!.Value, SourceActionKind.Move, 0,
        move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? s.Slot : s.Slot.Opposite);
}).ToImmutableArray();
SourceFourActionResult Begin(SourceBattleFrame state, ImmutableArray<SourceActionSelection>? choices = null) =>
    Ok(SourceFourActionTurns.BeginActions(catalog, state, state.Revision, choices ?? Choices(state)));
SourceFourActionResult Next(SourceFourActionResult result) => Ok(SourceFourActionTurns.Advance(catalog, result.State, Token(result.State)));
void Atomic(SourceBattleFrame state, SourceFourActionResult rejected, string message) =>
    Check(!rejected.Succeeded && ReferenceEquals(state, rejected.State) && rejected.Events.IsEmpty && rejected.Trace.IsEmpty, message + ": " + rejected.Fault);
SourceFourActionResult ExactNext(SourceFourActionResult result)
{
    var saved = JsonSerializer.Deserialize<SourceBattleFrame>(Json(result.State))!;
    var token = Token(result.State); var next = Next(result);
    Check(Json(next) == Json(SourceFourActionTurns.Advance(catalog, saved, token)), "exact JSON continuation state/events/RNG");
    Atomic(next.State, SourceFourActionTurns.Advance(catalog, next.State, token), "replayed continuation");
    return next;
}
SourceFourActionResult Replace(SourceFourActionResult result, int index)
{
    var pending = result.State.PendingSwitches[0];
    return Ok(SourceFourActionTurns.ChooseReplacement(catalog, result.State, Token(result.State), pending.Slot, new(pending.Slot.Side, index)));
}
SourceFourActionResult Drain(SourceFourActionResult result, List<SourceBattleEvent> events, List<RandomTrace> trace)
{
    for (int step = 0; step < 24 && result.State.FourTurn is not null && result.State.Outcome == WildBattleOutcome.Ongoing; step++)
    {
        Check(result.State.PendingSwitches.IsEmpty, "explicitly choose every replacement before draining");
        Check(result.State.Rewards?.Pending is null, "fixture has no unhandled learning decision");
        result = ExactNext(result); events.AddRange(result.Events); trace.AddRange(result.Trace);
    }
    Check(result.State.FourTurn is null || result.State.Outcome != WildBattleOutcome.Ongoing, "bounded lifecycle reached Ready or terminal");
    return result;
}
void TraceAndEvents(SourceBattleFrame start, SourceBattleFrame end, List<SourceBattleEvent> events, List<RandomTrace> trace)
{
    Check(events.Select(e => e.Sequence).SequenceEqual(Enumerable.Range(1, events.Count).Select(i => start.EventSequence + i)), "event sequence has no gaps or duplicate publication");
    Check(end.EventSequence == start.EventSequence + events.Count, "event cursor matches actual publication count");
    var rng = start.Rng;
    foreach (var draw in trace)
    {
        var expected = Gen3Rng.Next(rng, draw.Purpose, draw.Bound);
        Check(draw == expected.Trace, "trace is the exact next source LCG draw"); rng = expected.State;
    }
    Check(end.Rng == rng, "RNG cursor and seed account for all reported draws exactly once");
}

SourceBattleFrame WithBody(SourceBattleFrame state, int slot, WildBattleCombatant body)
{
    var cell = state.Get(new(slot)); var key = cell.Member!.Value; var party = state.Party(key.Side);
    return state with { Slots = state.Slots.SetItem(slot, cell with { Body = body }),
        Parties = state.Parties.SetItem((int)key.Side, party with { Members = party.Members.SetItem(key.Index, Member(body)) }) };
}
if (args.Length == 2)
{
    var cases = new List<object>();
    var clean = Four(); var playerKo = Four(playerKo: true);
    var opponentKo = WithBody(WithBody(WithBody(WithBody(Four(), 0, Mon("RATTATA", move: "QUICK_ATTACK")),
        1, Mon(level: 5, move: "BARRIER", hp: 1)), 2, Mon("SPEAROW", move: "BARRIER")), 3, Mon("DODUO", move: "BARRIER"));
    var poison = WithBody(WithBody(Four(playerKo: true), 0, Mon(move: "BARRIER", hp: 1) with { Status = WildBattleStatus.Poison }),
        1, Mon("RATTATA", move: "BARRIER")) with { Field = new() { RainTurnsRemaining = 3 } };
    foreach (var (name, start) in new[] { ("clean-four", clean), ("deferred-player-ko", playerKo), ("opponent-ko-exp", opponentKo), ("poison-replacement", poison) })
    {
        var result = Begin(start); var events = result.Events.ToList(); var trace = result.Trace.ToList();
        for (int step = 0; step < 24 && result.State.FourTurn is not null && result.State.Outcome == WildBattleOutcome.Ongoing; step++)
        {
            result = result.State.PendingSwitches.IsEmpty ? ExactNext(result) : Replace(result, result.State.PendingSwitches[0].LegalPartyIndices[0]);
            events.AddRange(result.Events); trace.AddRange(result.Trace);
        }
        Check(result.CompletedCleanTurn, name + " completes a real four-position turn");
        TraceAndEvents(start, result.State, events, trace);
        cases.Add(new { name, state = result.State, events, trace });
    }
    Check(cases.Count == 4, "exactly four baseline scenarios");
    Console.WriteLine(Json(cases));
    return 0;
}

Test("authenticated fixture uses runtime10 and exactly two real occupants", () =>
{
    Check(Environment.Version.Major == 10, "actual .NET runtime10");
    Check(catalog.Species.Count == 386 && catalog.Moves.Count == 354, "complete authenticated mechanics catalog");
    var state = Single(); Valid(state);
    Check(state.BattlerCount == 2 && state.Parties.Sum(p => p.Members.Length) == 2, "one real member per side");
    Atomic(state, SourceFourActionTurns.BeginActions(catalog, state, state.Revision, Choices(state).RemoveAt(1)), "missing real actor rejected");
    var malformed = state with { Slots = state.Slots.SetItem(2, new(new(2), new(BattleSide.Player, 0), 1, state.Get(new(0)).Body)) };
    Atomic(malformed, SourceFourActionTurns.BeginActions(catalog, malformed, malformed.Revision, Choices(state)), "fabricated unused partner rejected");
});

Test("single normal moves finish one Ready boundary with exact continuation and PP", () =>
{
    var start = Single(); var result = Begin(start);
    Check(result.State.Queue.Length == 2, "two selections committed");
    var events = result.Events.ToList(); var trace = result.Trace.ToList();
    for (int i = 0; i < 2; i++) { result = ExactNext(result); events.AddRange(result.Events); trace.AddRange(result.Trace); }
    Check(result.State.FourTurn?.Phase == SourceFourActionPhase.EndTurn && result.State.NextAction == 2, "two actions then end turn");
    result = Drain(result, events, trace);
    Check(result.CompletedCleanTurn && result.State.Turn == 1 && result.State.Outcome == WildBattleOutcome.Ongoing, "one clean ongoing turn");
    Check(events.OfType<SourceMoveUsedEvent>().Select(e => e.Actor.Value).Order().SequenceEqual(new[] { 0, 1 }), "both actual actors move exactly once");
    Check(events.OfType<SourceTurnReadyEvent>().Count() == 1, "Ready published once");
    for (int slot = 0; slot < 2; slot++)
    {
        var body = result.State.Get(new(slot)).Body!;
        Check(body.Pokemon.CurrentHp < start.Get(new(slot)).Body!.Pokemon.CurrentHp && body.Pokemon.CurrentHp > 0, "both real targets took nonterminal damage");
        Check(body.Pokemon.Moves[0].CurrentPp == start.Get(new(slot)).Body!.Pokemon.Moves[0].CurrentPp - 1, "one PP command per actor");
        Check(Json(Member(body)) == Json(result.State.Party((BattleSide)slot).Members[0]), "permanent party remains sole Pokemon authority");
    }
    TraceAndEvents(start, result.State, events, trace);
});

Test("single terminal KO runs residual cleanup before wild EXP and ends once without ready RNG", () =>
{
    var player = Mon("RATTATA", move: "QUICK_ATTACK") with { Status = WildBattleStatus.Poison };
    var start = Single(player, Mon(level: 5, move: "BARRIER", hp: 1)) with { Field = new() { RainTurnsRemaining = 3 } };
    var result = Begin(start); var events = result.Events.ToList(); var trace = result.Trace.ToList();
    result = ExactNext(result); events.AddRange(result.Events); trace.AddRange(result.Trace);
    Check(result.Events.OfType<SourceFaintedEvent>().Single().Side.Value == 1 && result.State.NextAction == 2, "wild KO cancels unexecuted selection");
    Check(result.State.FourTurn!.Phase == SourceFourActionPhase.EndTurn && result.State.Outcome == WildBattleOutcome.Ongoing, "source terminal residual barrier precedes rewards");
    Check(result.State.Party(BattleSide.Opponent).Members[0].Pokemon.Moves[0].CurrentPp == start.Party(BattleSide.Opponent).Members[0].Pokemon.Moves[0].CurrentPp, "KO opponent never spends PP");
    var defeat = result.State.FourTurn.Defeats.Single(d => d.RequiresExperience);
    var expected = SourceVictoryRewards.Calculate(proof, result.State.Party(BattleSide.Player), result.State.Rewards!.Metadata,
        new(defeat.Occupant, result.State.Get(new(1)).Body!, [new(BattleSide.Player, 0)]), result.State.Rewards.Context!);
    result = ExactNext(result); events.AddRange(result.Events); trace.AddRange(result.Trace);
    Check(result.State.FourTurn!.Phase == SourceFourActionPhase.RewardsPending && result.State.Field.RainTurnsRemaining == 2, "residual pass ticks timer then exposes EXP");
    Check(!events.OfType<SourceStatusDamageEvent>().Any() && result.State.Get(new(0)).Body!.Pokemon.CurrentHp == player.Pokemon.CurrentHp, "terminal pending suppresses residual damage");
    ulong beforeRewardRng = result.State.Rng.Cursor;
    var beforeReward = result.State; result = Ok(SourceFourRewards.Settle(catalog, beforeReward, Token(beforeReward)));
    events.AddRange(result.Events); trace.AddRange(result.Trace);
    Check(result.State.Outcome == WildBattleOutcome.Won && result.State.FourTurn!.Phase == SourceFourActionPhase.BattleEnded, "wild terminal after EXP");
    Check(result.State.Rewards!.CompletedOpponentMask == 1 && result.Events.OfType<SourceOpponentRewardCompletedEvent>().Count() == 1 && result.Events.OfType<SourceRewardAppliedEvent>().Count() == 1, "one defeat, one actual EXP recipient, one completion");
    Check(Json(result.State.Party(BattleSide.Player).Members[0].Pokemon) == Json(expected.Members.Single().Reward.Pokemon), "shared ordinary-wild calculator supplies exact Pokemon reward");
    Check(events.OfType<SourceEndedEvent>().Count() == 1 && !events.OfType<SourceTurnReadyEvent>().Any() && result.State.Rng.Cursor == beforeRewardRng, "one terminal publication, no final ready draw");
    Check(events.FindIndex(e => e is SourceOrderEvent o && o.Phase == WildBattleOrderPhase.EndTurn) < events.FindIndex(e => e is SourceRewardAppliedEvent) && events.FindIndex(e => e is SourceRewardAppliedEvent) < events.FindIndex(e => e is SourceEndedEvent), "source cleanup/reward/ended event order");
    Atomic(result.State, SourceFourRewards.Settle(catalog, result.State, Token(beforeReward)), "reward replay cannot duplicate EXP");
    Atomic(result.State, SourceFourActionTurns.Advance(catalog, result.State, Token(result.State)), "ended battle cannot run another action");
    TraceAndEvents(start, result.State, events, trace);
});

Test("single action KO pauses immediately for reserve and rejects stale continuations", () =>
{
    var start = Single(Mon(level: 5, move: "BARRIER", hp: 1), Mon("RATTATA", move: "QUICK_ATTACK"), Mon(move: "BARRIER")) with { Field = new() { RainTurnsRemaining = 3 } };
    var result = Begin(start); var events = result.Events.ToList(); var trace = result.Trace.ToList();
    result = ExactNext(result); events.AddRange(result.Events); trace.AddRange(result.Trace);
    Check(result.State.FourTurn!.Phase == SourceFourActionPhase.ReplacementsPending && result.State.FourTurn.EndTurn is null && result.State.NextAction == 2, "action-time immediate replacement with no residual cursor");
    Check(result.State.PendingSwitches.Single().LegalPartyIndices.SequenceEqual(new[] { 1 }) && result.State.Get(new(0)).EntrySerial == 1, "only actual reserve offered; no early binding");
    Check(result.State.Field.RainTurnsRemaining == 3 && result.State.Turn == 0, "no residuals or turn increment before choice");
    Atomic(result.State, SourceFourActionTurns.Advance(catalog, result.State, Token(result.State)), "replacement requires explicit choice");
    var paused = result.State; var stale = Token(paused) with { EventSequence = paused.EventSequence - 1 };
    Atomic(paused, SourceFourActionTurns.ChooseReplacement(catalog, paused, stale, new(0), new(BattleSide.Player, 1)), "stale replacement token");
    result = Replace(result, 1); events.AddRange(result.Events); trace.AddRange(result.Trace);
    Check(result.State.Get(new(0)).Member == new PartyMemberKey(BattleSide.Player, 1) && result.State.Get(new(0)).EntrySerial == 2 && result.State.Turn == 0, "replacement binds actual reserve and advances identity only");
    Check(result.State.FourTurn!.Phase == SourceFourActionPhase.EndTurn && result.State.FourTurn.EndTurn is null, "action replacement continues fresh end turn");
    Atomic(result.State, SourceFourActionTurns.ChooseReplacement(catalog, result.State, Token(paused), new(0), new(BattleSide.Player, 1)), "old replacement cursor cannot be reused");
    result = Drain(result, events, trace);
    Check(result.CompletedCleanTurn && result.State.Field.RainTurnsRemaining == 2 && result.State.Turn == 1, "one residual pass and Ready after replacement");
    Check(events.OfType<SourceMoveUsedEvent>().Count() == 1 && events.OfType<SourceMoveUsedEvent>().Single().Actor.Value == 1, "incoming reserve cannot inherit fainted selection");
    Check(result.State.Party(BattleSide.Player).Members.All(m => m.Pokemon.Moves[0].CurrentPp == m.Pokemon.Moves[0].MaximumPp), "fainted and incoming Pokemon never spend player PP");
    TraceAndEvents(start, result.State, events, trace);
});

Test("single poison KO preserves residual cursor and does not replay weather or poison", () =>
{
    var start = Single(Mon(move: "BARRIER", hp: 1) with { Status = WildBattleStatus.Poison }, Mon("RATTATA", move: "BARRIER"), Mon(move: "BARRIER")) with { Field = new() { RainTurnsRemaining = 3 } };
    var result = Begin(start); var events = result.Events.ToList(); var trace = result.Trace.ToList();
    for (int i = 0; i < 3; i++) { result = ExactNext(result); events.AddRange(result.Events); trace.AddRange(result.Trace); }
    Check(result.State.FourTurn!.Phase == SourceFourActionPhase.ReplacementsPending && result.State.FourTurn.EndTurn is not null, "poison creates actual retained residual boundary");
    var cursor = result.State.FourTurn.EndTurn;
    Check(result.State.Field.RainTurnsRemaining == 2 && events.OfType<SourceStatusDamageEvent>().Count() == 1, "initial residuals run once before interruption");
    result = Replace(result, 1); events.AddRange(result.Events); trace.AddRange(result.Trace);
    Check(result.State.FourTurn!.Phase == SourceFourActionPhase.EndTurnResume && result.State.FourTurn.EndTurn == cursor, "replacement retains exact residual cursor");
    result = Drain(result, events, trace);
    Check(result.CompletedCleanTurn && result.State.Field.RainTurnsRemaining == 2 && events.OfType<SourceStatusDamageEvent>().Count() == 1 && events.OfType<SourceTurnReadyEvent>().Count() == 1, "no poison, weather or Ready replay");
    Check(result.State.Get(new(0)).Body!.Pokemon.CurrentHp == result.State.Get(new(0)).Body!.Pokemon.Stats.MaxHp, "clean reserve is unaffected by outgoing poison");
    TraceAndEvents(start, result.State, events, trace);
});

Test("single delayed KO resumes after its exact position with no repeated hit", () =>
{
    var start = Single(Mon(move: "BARRIER"), Mon("RATTATA", move: "BARRIER"), Mon(move: "BARRIER"));
    start = start with { Field = new() { RainTurnsRemaining = 3 }, Slots = start.Slots.SetItem(0, start.Get(new(0)) with { Position = new() { FutureAttack = new(1, Id("FUTURE_SIGHT"), new(1), 10000) } }) };
    var result = Begin(start); var events = result.Events.ToList(); var trace = result.Trace.ToList();
    for (int i = 0; i < 3; i++) { result = ExactNext(result); events.AddRange(result.Events); trace.AddRange(result.Trace); }
    Check(result.State.FourTurn!.Phase == SourceFourActionPhase.ReplacementsPending && result.State.FourTurn.EndTurn is { Stage: WildBattleSourceEndTurnStage.FutureAttacks, NextBattler: 1 }, "delayed callback retains numeric position after target0");
    Check(result.State.Get(new(0)).Position.FutureAttack is null && events.OfType<SourceCalledDelayedEvent>().Count() == 1, "stored attack consumed once");
    var cursor = result.State.FourTurn.EndTurn;
    var copied = JsonSerializer.Deserialize<SourceBattleFrame>(Json(result.State))!;
    var direct = Replace(result, 1);
    var restored = SourceFourActionTurns.ChooseReplacement(catalog, copied, Token(copied), new(0), new(BattleSide.Player, 1));
    Check(Json(direct) == Json(restored), "replacement from serialized delayed cursor is exact");
    result = direct; events.AddRange(result.Events); trace.AddRange(result.Trace);
    Check(result.State.FourTurn!.EndTurn == cursor && result.State.FourTurn.Phase == SourceFourActionPhase.EndTurnResume, "delayed cursor survives entry unchanged");
    result = Drain(result, events, trace);
    Check(result.CompletedCleanTurn && result.State.Field.RainTurnsRemaining == 2 && events.OfType<SourceCalledDelayedEvent>().Count() == 1 && events.OfType<SourceFaintedEvent>().Count() == 1, "no repeated delayed damage, faint or weather");
    Check(result.State.Get(new(0)).Body!.Pokemon.CurrentHp == result.State.Get(new(0)).Body!.Pokemon.Stats.MaxHp, "new occupant never receives old due attack");
    TraceAndEvents(start, result.State, events, trace);
});

Test("single voluntary switch consumes selected Pursuit once before real reserve entry", () =>
{
    var start = Single(Mon(move: "BARRIER"), Mon("SPEAROW", move: "PURSUIT"), Mon("DODUO", move: "BARRIER"));
    var choices = Choices(start).SetItem(0, new(start.Get(new(0)).Occupant!.Value, SourceActionKind.Switch, Incoming: new(BattleSide.Player, 1)));
    var result = Begin(start, choices); var events = result.Events.ToList(); var trace = result.Trace.ToList();
    result = ExactNext(result); events.AddRange(result.Events); trace.AddRange(result.Trace);
    Check(result.Events.OfType<SourcePursuitInterceptedEvent>().Count() == 1 && result.State.Get(new(0)).Member!.Value.Index == 1, "actual Pursuit interception followed by selected reserve");
    Check(result.State.Party(BattleSide.Player).Members[0].Pokemon.CurrentHp < start.Party(BattleSide.Player).Members[0].Pokemon.CurrentHp, "Pursuit hits outgoing Pokemon");
    Check(result.State.Party(BattleSide.Player).Members[1].Pokemon.CurrentHp == start.Party(BattleSide.Player).Members[1].Pokemon.CurrentHp, "Pursuit cannot hit incoming Pokemon");
    Check(events.FindIndex(e => e is SourcePursuitInterceptedEvent) < events.FindIndex(e => e is SourceCalledPartySwitchedEvent), "interception precedes switch publication");
    result = Drain(result, events, trace);
    Check(result.CompletedCleanTurn && events.OfType<SourcePursuitInterceptedEvent>().Count() == 1 && events.OfType<SourceCalledPartySwitchedEvent>().Count() == 1, "one interception and one switch in complete turn");
    Check(result.State.Party(BattleSide.Opponent).Members[0].Pokemon.Moves[0].CurrentPp == start.Party(BattleSide.Opponent).Members[0].Pokemon.Moves[0].CurrentPp - 1, "Pursuit spends PP exactly once");
    Check(result.State.Party(BattleSide.Player).Members.All(m => m.Pokemon.Moves[0].CurrentPp == m.Pokemon.Moves[0].MaximumPp), "switching and incoming player have no borrowed move");
    TraceAndEvents(start, result.State, events, trace);
});

Test("real four-position turn retains four actions and four simultaneous occupants", () =>
{
    var start = Four(); var result = Begin(start); var events = result.Events.ToList(); var trace = result.Trace.ToList();
    Check(result.State.Queue.Length == 4 && result.State.Slots.All(s => s.Occupant is not null), "real four-position queue and occupancy");
    result = Drain(result, events, trace);
    Check(result.CompletedCleanTurn && result.State.Turn == 1 && events.OfType<SourceMoveUsedEvent>().Select(e => e.Actor.Value).Order().SequenceEqual(new[] { 0, 1, 2, 3 }), "all four real actors execute");
    Check(events.OfType<SourceHpChangedEvent>().Select(e => e.Target.Value).Distinct().Count() == 4, "all four real targets receive damage");
    Check(result.State.Slots.All(s => s.Body!.Pokemon.Moves[0].CurrentPp == start.Get(s.Slot).Body!.Pokemon.Moves[0].CurrentPp - 1), "four separate PP owners");
    TraceAndEvents(start, result.State, events, trace);
});

Test("real four-position KO still defers reserve entry until end turn", () =>
{
    var start = Four(playerKo: true); var result = Begin(start); var events = result.Events.ToList(); var trace = result.Trace.ToList();
    result = ExactNext(result); events.AddRange(result.Events); trace.AddRange(result.Trace);
    Check(result.State.IsDeferredKoVacant(new(0)) && !result.State.IsSourceAbsent(new(0)) && result.State.PendingSwitches.IsEmpty && result.State.NextAction == 1, "four-slot action KO remains deferred with next original actor");
    Check(result.State.Get(new(0)).EntrySerial == 1 && result.State.Queue.Single(a => a.Slot.Value == 0).Kind == SourceActionKind.Cancelled, "outgoing selection canceled without reserve entry");
    while (result.State.FourTurn!.Phase == SourceFourActionPhase.Actions) { result = ExactNext(result); events.AddRange(result.Events); trace.AddRange(result.Trace); }
    Check(result.State.FourTurn.Phase == SourceFourActionPhase.EndTurn && events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == 2), "living partner executes before end turn");
    result = ExactNext(result); events.AddRange(result.Events); trace.AddRange(result.Trace);
    Check(result.State.FourTurn!.Phase == SourceFourActionPhase.ReplacementsPending && result.State.FourTurn.EndTurn is not null && result.State.PendingSwitches.Single().LegalPartyIndices.SequenceEqual(new[] { 2 }), "deferred KO offers actual bench only after residuals");
    result = Replace(result, 2); events.AddRange(result.Events); trace.AddRange(result.Trace);
    result = Drain(result, events, trace);
    Check(result.CompletedCleanTurn && result.State.Get(new(0)).Member!.Value.Index == 2 && result.State.Get(new(0)).EntrySerial == 2 && result.State.DeferredKoVacancyMask == 0, "actual four-slot reserve binds at retained boundary");
    Check(result.State.Get(new(0)).Body!.Pokemon.CurrentHp == start.Party(BattleSide.Player).Members[2].Pokemon.CurrentHp && result.State.Get(new(0)).Body!.Pokemon.Moves[0].CurrentPp == start.Party(BattleSide.Player).Members[2].Pokemon.Moves[0].CurrentPp, "deferred entrant inherits no earlier hit or action");
    TraceAndEvents(start, result.State, events, trace);
});

foreach (bool struggle in new[] { false, true })
Test(struggle ? "single forced Struggle matches frozen source party and exact RNG trace" : "single normal turn matches frozen source party and exact RNG trace", () =>
{
    WildBattleCombatant Exhaust(WildBattleCombatant body) => body with
    { Pokemon = body.Pokemon with { Moves = body.Pokemon.Moves.Select(m => m with { CurrentPp = 0 }).ToImmutableArray() } };
    var player = Mon(); var opponent = Mon("RATTATA");
    if (struggle) { player = Exhaust(player); opponent = Exhaust(opponent); }
    var entry = SourceWildBattlePolicy.Start(proof, [player], 0, opponent, new(31));
    Check(entry.Succeeded, "canonical mechanics entry " + entry.Fault);
    var start = entry.State! with { Rewards = Rewards(1, SourceRewardBattleKind.OrdinaryWild) };
    var choice = struggle ? SourceActionSelection.Struggle(start.Get(new(0)).Occupant!.Value)
        : new SourceActionSelection(start.Get(new(0)).Occupant!.Value, SourceActionKind.Move, 0, new(1));
    var result = Ok(SourceWildBattlePolicy.BeginAction(catalog, start, start.Revision, choice));
    var events = result.Events.ToList(); var trace = result.Trace.ToList();
    result = Drain(result, events, trace);
    // Frozen before owner retirement, using the exact same authenticated fixture and source
    // assembly recorded beside these files. The old executor is never linked by this test.
    using var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures",
        struggle ? "source-singles-struggle.json" : "source-singles-normal.json")));
    string Expected(string field) => Json(golden.RootElement.GetProperty(field));
    Check(result.CompletedCleanTurn && Json(result.State.Turn) == Expected("Turn") && Json(result.State.Outcome) == Expected("Outcome"), "same complete ongoing turn as fixed source oracle");
    string PartyHash(BattleSide side) => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Json(result.State.Party(side).Members))));
    Check(Json(PartyHash(BattleSide.Player)) == Expected("PlayerSha256") &&
        Json(PartyHash(BattleSide.Opponent)) == Expected("OpponentSha256"), "exact fixed permanent party including HP PP status and identity");
    Check(Json(result.State.Get(new(0)).Body!.Status) == Expected("PlayerStatus") && Json(result.State.Get(new(1)).Body!.Status) == Expected("OpponentStatus"), "same fixed active battle statuses");
    Check(Json(result.State.Rng) == Expected("Rng") && Json(result.State.RandomTurnNumber) == Expected("RandomTurnNumber"), "same fixed terminal RNG state and ready number");
    Check(Json(trace) == Expected("Trace"), "exact fixed source RNG trace including draw order and purpose");
    TraceAndEvents(start, result.State, events, trace);
});

Test("single entry Intimidate Protect and Leftovers respect null unused positions", () =>
{
    var player = Mon("GYARADOS", move: "PROTECT", hp: 100) with { HeldItem = "ITEM_LEFTOVERS" };
    var start = Single(player, Mon("RATTATA"));
    var entry = Ok(SourceFourEntryAbilities.InitializeEntry(catalog, start, start.Revision));
    Check(entry.State.Get(new(1)).Body!.AttackStage == 5 && entry.State.Get(new(0)).Body!.AttackStage == 6, "Intimidate touches only actual opposing body");
    Check(entry.State.EntryAbilities!.AppliedEntries.Skip(2).All(t => t is null), "entry lifecycle has no unused occupants");
    var result = Begin(entry.State); var events = result.Events.ToList(); var trace = result.Trace.ToList();
    result = Drain(result, events, trace);
    Check(result.CompletedCleanTurn && !events.OfType<SourceHpChangedEvent>().Any(e => e.Target.Value == 0 && e.HpAfter < e.HpBefore), "Protect prevents selected opponent damage");
    Check(result.State.Get(new(0)).Body!.Pokemon.CurrentHp == Math.Min(player.Pokemon.Stats.MaxHp, player.Pokemon.CurrentHp + Math.Max(1, player.Pokemon.Stats.MaxHp / 16)), "Leftovers heals the actual holder once");
    Check(!result.State.Get(new(0)).Body!.Protected, "Ready clears Protect after one turn");
    TraceAndEvents(entry.State, result.State, events, trace);
});

Test("disguised ghost Master Ball dodge precedes capture RNG", () =>
{
    var start = Single(Mon(move: "SPLASH"), Mon("GASTLY", 30, "LICK")) with
        { WildEncounter = new() { TowerBattleKind = PokemonTowerBattleKind.DisguisedWild } };
    var ball = new SourceActionSelection(start.Get(new(0)).Occupant!.Value, SourceActionKind.Capture)
        { Capture = new(CaptureBall.Master, new(false, [])) };
    var result = Ok(SourceWildBattlePolicy.BeginAction(catalog, start, start.Revision, ball));
    var events = result.Events.ToList(); var trace = result.Trace.ToList();
    result = Drain(result, events, trace);
    Check(events.OfType<SourceBallThrownEvent>().Count() == 1 && events.OfType<SourceTowerBallDodgedEvent>().Count() == 1,
        "one spent ball and one ghost dodge");
    Check(!events.OfType<SourceCaptureResultEvent>().Any() && result.State.Outcome == WildBattleOutcome.Ongoing,
        "Master Ball does not override ghost policy");
    Check(!trace.Any(t => t.Purpose.StartsWith("capture:", StringComparison.Ordinal)), "ghost dodge consumes zero capture draws");
    TraceAndEvents(start, result.State, events, trace);
});

Test("disguised ghost run guarantees escape before foe and Ready", () =>
{
    var start = Single(Mon("MAGIKARP", 5, "SPLASH"), Mon("GASTLY", 30, "LICK")) with
        { WildEncounter = new() { TowerBattleKind = PokemonTowerBattleKind.DisguisedWild } };
    Check(start.Get(new(0)).Body!.Pokemon.Stats.Speed < start.Get(new(1)).Body!.Pokemon.Stats.Speed, "player is slower");
    var run = new SourceActionSelection(start.Get(new(0)).Occupant!.Value, SourceActionKind.Run);
    var result = Ok(SourceWildBattlePolicy.BeginAction(catalog, start, start.Revision, run));
    var events = result.Events.ToList(); var trace = result.Trace.ToList();
    result = Drain(result, events, trace);
    Check(result.State.Outcome == WildBattleOutcome.Escaped && result.State.RunAttempts == start.RunAttempts,
        "guaranteed escape preserves attempts");
    Check(!trace.Any(t => t.Purpose == "wild-battle.run" || t.Purpose == "wild-battle.turn-end.random-turn-number") &&
        !events.OfType<SourceTowerGhostActionEvent>().Any(), "escape consumes no run draw and ends before foe and Ready");
    TraceAndEvents(start, result.State, events, trace);
});

Console.WriteLine(Json(new
{
    groups, checks, failures, runtime = Environment.Version.ToString(),
    mechanicsVersion = SourceBattleKernel.Version, lifecycleVersion = SourceFourActionTurns.Version,
    domainSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(SourceBattleFrame).Assembly.Location))),
    observations
}));
return groups == 14 && checks > 0 && failures == 0 ? 0 : 1;
