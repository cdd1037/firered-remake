using System.Collections.Immutable;
using System.Text.Json;
using System.Reflection;
using System.Security.Cryptography;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length != 1) throw new ArgumentException("Pass project root.");
string root = Path.GetFullPath(args[0]); int groups = 0, checks = 0, failures = 0;
void Check(bool ok, string why) { checks++; if (!ok) throw new Exception(why); }
void Test(string name, Action action) { groups++; try { action(); Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " " + e); } }
MoveId Id(string move) => new("pret:MOVE_" + move);
var metadata = PretMetronomeMoveCatalog.Load(root);
var rocket = PretRocketHideoutPokemon.Load(root).Catalog;
var supplement = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules: true);
var tower = PretPokemonTowerPokemon.Load(rocket with { Moves = rocket.Moves.SetItems(supplement.Moves) }, root, root).Catalog;
var catalog = metadata.Extend(PretFuchsiaRoutePokemon.Load(PretEeveePokemon.Load(PretSnorlaxPokemon.Load(tower, root).Catalog, root).Catalog, root).Catalog);
PokemonInstance Mon(string species, int level, params string[] moves) {
    var mon = PokemonFactory.Create(catalog, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(0), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
    return mon with { Moves = moves.Select(m => new PokemonMoveSlot(Id(m), catalog.Moves[Id(m)].Pp, catalog.Moves[Id(m)].Pp)).ToImmutableArray() };
}
WildBattleState Pair(uint seed = 7) => new(new(Mon("CLEFABLE", 20, "GROWL", "METRONOME")) { FirstTurnCounter = 1 },
    new(Mon("SNORLAX", 100, "GROWL", "METRONOME")) { FirstTurnCounter = 1 }, 0, 0, 0, new(seed), 0,
    WildBattleOutcome.Ongoing, RulesVersion: WildBattleRules.FuchsiaRulesVersion) { Terrain = WildBattleTerrain.Plain };
WildBattleCalledMoveResult Act(WildBattleState state, string move, WildBattleSide side = WildBattleSide.Player) =>
    WildBattleRules.ResolveCalledMoveAction(catalog, state, new(side, 1, Id(move)));
WildBattleCalledMoveResult Continue(WildBattleState state, WildBattleSide side = WildBattleSide.Player) => WildBattleRules.ResolveCalledMoveContinuation(catalog, state, side);
WildBattleState Accepted(WildBattleCalledMoveResult result) {
    Check(result.Succeeded, result.Fault ?? "success");
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!) is null, WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!) ?? "valid state"); return result.State!;
}
WildBattleState Tick(WildBattleState state) { var r = WildBattleRules.ResolveCalledTurnLockEndTurn(catalog, state); Check(r.Succeeded, r.Fault ?? "residual"); return r.State!; }
WildBattleState Pp(WildBattleState state, int pp) => state with { Player = state.Player with { Pokemon = state.Player.Pokemon with { Moves = state.Player.Pokemon.Moves.SetItem(1, state.Player.Pokemon.Moves[1] with { CurrentPp = pp }) } } };
WildBattleState Locked(string name) {
    for (uint seed = 0; seed < 100; seed++) { var r = Act(Pair(seed), name); if (r.Succeeded && r.State!.Player.CalledTurnState?.Lock is not null) return r.State; }
    throw new Exception("No lock created for " + name);
}
int Damage(WildBattleCalledMoveResult r) => r.Battle.Events.OfType<WildBattleHpChangedEvent>().Where(e => e.Actor == WildBattleSide.Player && e.Target == WildBattleSide.Opponent).Sum(e => e.Damage);
using var vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "source-vectors.json")));
string[] chargeMoves = ["RAZOR_WIND", "FLY", "SOLAR_BEAM", "SKULL_BASH", "SKY_ATTACK", "DIVE", "BOUNCE"];
string[] allMoves = ["RAZOR_WIND", "FLY", "HYPER_BEAM", "SOLAR_BEAM", "PETAL_DANCE", "EARTHQUAKE", "BIDE", "SKULL_BASH", "SKY_ATTACK", "MIND_READER", "LOCK_ON", "OUTRAGE", "FURY_CUTTER", "UPROAR", "DIVE", "ICE_BALL", "BLAST_BURN", "HYDRO_CANNON", "SKY_UPPERCUT", "FRENZY_PLANT", "BOUNCE"];
Test("runtime, all 21 exact source definitions, historical admission and omitted state", () => {
    using var sourceLock = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "source-lock.json")));
    string sourceRoot = Environment.GetEnvironmentVariable("FIRERED_B06_SOURCE_ROOT") ?? Path.GetFullPath(Path.Combine(root, "../../firered-reference-20261010/pokefirered"));
    int sourceFiles = 0;
    foreach (var file in sourceLock.RootElement.GetProperty("files").EnumerateArray()) {
        string path = Path.Combine(sourceRoot, file.GetProperty("path").GetString()!);
        Check(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant() == file.GetProperty("sha256").GetString(), "pinned source authentication " + path); sourceFiles++;
    }
    Check(sourceFiles == 9, "complete nonempty source file set");
    Check(Environment.Version.Major == 10, "runtime10"); Check(vectors.RootElement.GetProperty("sourceCommit").GetString() == PokemonFactory.SourceCommit, "source pin");
    foreach (var name in allMoves) { Check(WildBattleRules.IsCalledMoveEffectSupported(catalog.Moves[Id(name)]), "registered " + name); Check(!Pair().Player.Pokemon.Moves.Any(m => m.Move == Id(name)), "not learned " + name); }
    Check(Pair().Player.Pokemon.Stats.Attack == 33 && Pair().Player.Pokemon.Stats.SpecialAttack == 39 && Pair().Opponent.Pokemon.Stats.Defense == 135 && Pair().Opponent.Pokemon.Stats.SpecialDefense == 225, "source oracle authenticated fixture stats");
    Check(!JsonSerializer.Serialize(Pair()).Contains("CalledTurnState"), "default omitted");
    Check(!WildBattleRules.IsMoveSupported(Id("METRONOME"), WildBattleRules.FuchsiaRulesVersion), "historical gate unchanged");
    Check(!WildBattleRules.ResolveMove(catalog, Pair(), 1).Succeeded, "ordinary Metronome stays gated");
});
Test("296 independent source first/continuation RNG and PP vectors", () => {
    int count = 0;
    foreach (var v in vectors.RootElement.GetProperty("actions").EnumerateArray()) {
        string name = v.GetProperty("move").GetString()!; uint seed = v.GetProperty("seed").GetUInt32(); bool continuation = v.GetProperty("continuation").GetBoolean();
        Check(catalog.Moves[Id(name)].SourceMoveId == v.GetProperty("id").GetInt32(), "exact id");
        WildBattleState state = Pair(seed);
        if (continuation) {
            state = Locked(name); var locked = state.Player.CalledTurnState!.Lock!;
            state = Pp(state with { Rng = new(seed), Player = state.Player with { CalledTurnState = state.Player.CalledTurnState with {
                Lock = locked with { TurnsRemaining = locked.Kind is WildBattleCalledLockKind.Rampage or WildBattleCalledLockKind.Uproar ? 2 : locked.TurnsRemaining } } } }, 10);
        }
        var r = continuation ? Continue(state) : Act(state, name); var after = Accepted(r); string label = name + "/" + seed + "/" + continuation;
        Check(after.Rng.Seed == v.GetProperty("finalSeed").GetUInt32() && after.Rng.Cursor == v.GetProperty("draws").GetUInt64(), label + " source RNG " + string.Join(',', r.Battle.Trace.Select(t => t.Purpose)));
        var ppEvents = r.Battle.Events.OfType<WildBattlePpCommandEvent>().ToArray();
        Check(ppEvents.Length == v.GetProperty("commands").GetInt32(), label + " PP commands");
        Check(after.Player.Pokemon.Moves[1].CurrentPp == v.GetProperty("ppAfter").GetInt32(), label + " PP amount");
        if (ppEvents.Length > 0) Check(ppEvents.Last().RngCursor == v.GetProperty("ppCursor").GetUInt64(), label + " PP cursor");
        Check((after.Player.CalledTurnState?.Lock?.TurnsRemaining ?? 0) == v.GetProperty("remaining").GetInt32(), label + " source timer");
        Check(Damage(r) == v.GetProperty("damage").GetInt32(), label + " exact source damage");
        Check(Damage(r) > 0 == v.GetProperty("hit").GetBoolean() && name is not ("MIND_READER" or "LOCK_ON") || name is "MIND_READER" or "LOCK_ON", label + " damage branch");
        Check(r.Invocation!.PpSlot == 1 && r.Invocation.ChosenSlot == 1 && r.Invocation.CurrentMove == Id(name), label + " source registers");
        if (continuation) { Check(!r.Battle.Trace.Any(t => t.Purpose.EndsWith("called-target")), "no repeated target selection"); Check(r.Invocation.ChosenMove == Id(name), "locked chosen identity"); }
        Check(after.Player.Pokemon.Moves[0] == state.Player.Pokemon.Moves[0] && after.Player.Pokemon.Moves[1].Move == Id("METRONOME"), label + " learned slots preserved");
        count++;
    }
    Check(count == 296, "all vectors");
});
Test("every charging move ignores first-turn Protect and release consumes no PP", () => {
    foreach (string name in chargeMoves) {
        var first = Act(Pair() with { Opponent = Pair().Opponent with { Protected = true } }, name); var s = Accepted(first);
        Check(s.Player.CalledTurnState?.Lock is { Kind: WildBattleCalledLockKind.Charge }, name + " charge on Protect");
        Check(!first.Battle.Trace.Any(t => t.Purpose.EndsWith(".accuracy")), "charge no accuracy");
        Check(s.Player.Pokemon.Moves[1].CurrentPp == 9 && Damage(first) == 0, "charge PP only");
        Check(first.Battle.Events.OfType<WildBattleAttackStringEvent>().Count() == 1, "charging script only caller attackstring");
        var second = Continue(s); s = Accepted(second);
        Check(s.Player.CalledTurnState?.Lock is null && s.Player.Pokemon.Moves[1].CurrentPp == 9, name + " protected release unlocked with no PP");
        Check(second.Battle.Events.OfType<WildBattlePpCommandEvent>().Single().Before == 9 && second.Battle.Trace.IsEmpty, "release protection no RNG");
    }
});
Test("all immediate B06 scripts fail under Protect at source PP and RNG boundary", () => {
    foreach (var name in allMoves.Except(chargeMoves).Except(new[] { "BIDE" })) {
        var state = Pair() with { Opponent = Pair().Opponent with { Protected = true } }; var r = Act(state, name); var after = Accepted(r);
        Check(Damage(r) == 0 && after.Player.CalledTurnState?.Lock is null, name + " blocked without lock");
        Check(after.Player.Pokemon.Moves[1].CurrentPp == 9 && r.Battle.Events.OfType<WildBattlePpCommandEvent>().Count() == 1, name + " source PP on Protect");
        Check(!r.Battle.Trace.Any(t => !t.Purpose.EndsWith("called-target")), name + " no accuracy/damage RNG on Protect");
    }
});
Test("zero PP survives all charged and repeated continuations without fake slots", () => {
    foreach (var name in chargeMoves.Concat(new[] { "PETAL_DANCE", "OUTRAGE", "ICE_BALL", "UPROAR", "BIDE", "HYPER_BEAM" })) {
        var before = Pp(Locked(name), 0); var r = Continue(before); var s = Accepted(r);
        Check(s.Player.Pokemon.Moves[1].CurrentPp == 0, name + " zero stable"); Check(!r.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "no-pp"), "forced zero PP permitted");
        Check(!Act(before, "TACKLE").Succeeded, "new caller blocked while locked");
    }
});
Test("sun charging branches and PP commands follow source even on a later release", () => {
    var sunny = Pair() with { CalledField = new() { SunTurnsRemaining = 5 } };
    var immediate = Act(sunny, "SOLAR_BEAM"); var s = Accepted(immediate);
    Check(s.Player.CalledTurnState?.Lock is null && Damage(immediate) > 0, "sun immediate hit");
    var pp = immediate.Battle.Events.OfType<WildBattlePpCommandEvent>().ToArray(); Check(pp.Length == 2 && pp[0].Before == 10 && pp[0].After == 9 && pp[1].Before == 9 && pp[1].After == 9, "sun command pair");
    var charged = Locked("SOLAR_BEAM"); int initialPp = charged.Player.Pokemon.Moves[1].CurrentPp;
    var release = Continue(charged with { CalledField = new() { SunTurnsRemaining = 5 } }); s = Accepted(release);
    Check(s.Player.Pokemon.Moves[1].CurrentPp == initialPp - 1 && release.Battle.Events.OfType<WildBattlePpCommandEvent>().Count() == 2, "sun appears on release spends again");
    var rain = charged with { RainTurnsRemaining = 5, Rng = new(7) };
    var ordinary = Continue(charged with { Rng = new(7) }); var wet = Continue(rain); Accepted(ordinary); Accepted(wet);
    Check(Damage(wet) < Damage(ordinary), "non-sun weather halves base damage before plus-two");
});
Test("Skull Bash defense, source charge cancellation, bounded other-lock parity", () => {
    var bash = Accepted(Act(Pair(), "SKULL_BASH")); Check(bash.Player.DefenseStage == 7, "charging defense rise");
    var capped = Accepted(Act(Pair() with { Player = Pair().Player with { DefenseStage = 12 } }, "SKULL_BASH")); Check(capped.Player.CalledTurnState?.Lock is not null, "cap does not block charge");
    foreach (string name in chargeMoves.Concat(new[] { "PETAL_DANCE", "OUTRAGE", "UPROAR", "ICE_BALL", "BIDE" })) {
        var locked = Locked(name); var flinched = Accepted(Continue(locked with { Player = locked.Player with { Flinched = true } }));
        Check(flinched.Player.CalledTurnState?.Lock is null && flinched.Player.Pokemon.Moves[1].CurrentPp == locked.Player.Pokemon.Moves[1].CurrentPp, name + " flinch clears locked action without PP");
        var paralyzed = Continue(locked with { Rng = new(0), Player = locked.Player with { Status = WildBattleStatus.Paralysis } });
        // Find one source full-paralysis seed, then inspect the source residual distinction.
        for (uint seed = 0; seed < 100 && !paralyzed.Battle.Events.OfType<WildBattleActionBlockedEvent>().Any(); seed++) paralyzed = Continue(locked with { Rng = new(seed), Player = locked.Player with { Status = WildBattleStatus.Paralysis } });
        var s = Accepted(paralyzed); Check(paralyzed.Battle.Events.OfType<WildBattleActionBlockedEvent>().Any(), "full paralysis found");
        bool charge = chargeMoves.Contains(name);
        Check((s.Player.CalledTurnState?.Lock is null) == charge, name + " charge paralysis script cancels lock and semi bits");
        var tick = Tick(s); bool residualCancels = charge || name is "PETAL_DANCE" or "OUTRAGE" or "UPROAR";
        Check((tick.Player.CalledTurnState?.Lock is null) == residualCancels, name + " residual interruption policy");
    }
});
Test("recharge is consumed before flinch and after sleep/freeze, without PP or RNG", () => {
    foreach (string name in new[] { "HYPER_BEAM", "BLAST_BURN", "HYDRO_CANNON", "FRENZY_PLANT" }) {
        var locked = Locked(name); var r = Continue(locked with { Player = locked.Player with { Flinched = true } }); var s = Accepted(r);
        Check(r.Battle.Trace.IsEmpty && r.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "must-recharge"), name + " must recharge");
        Check(s.Player.CalledTurnState?.Lock is null && s.Player.Flinched, "recharge consumes before flinch");
        var sleeping = Accepted(Continue(locked with { Player = locked.Player with { Status = 2 } })); Check(sleeping.Player.CalledTurnState?.Lock?.Kind == WildBattleCalledLockKind.Recharge, "sleep retains recharge");
        var ghost = Pair() with { Opponent = new(Mon("GASTLY", 60, "GROWL", "METRONOME")) { FirstTurnCounter = 1 } };
        if (name == "HYPER_BEAM") { var immune = Accepted(Act(ghost, name)); Check(immune.Player.CalledTurnState?.Lock is null, "type immunity causes no recharge"); }
    }
});
Test("Rampage and Uproar actual continuation miss PP plus source end counters", () => {
    foreach (string name in new[] { "PETAL_DANCE", "OUTRAGE", "UPROAR" }) {
        var locked = Locked(name); int before = locked.Player.Pokemon.Moves[1].CurrentPp;
        WildBattleCalledMoveResult r = Continue(locked);
        for (uint seed = 0; seed < 100; seed++) { r = Continue(locked with { Rng = new(seed), Player = locked.Player with { AccuracyStage = 0 }, Opponent = locked.Opponent with { EvasionStage = 12 } }); if (r.Battle.Events.OfType<WildBattleMoveMissedEvent>().Any()) break; }
        var s = Accepted(r); Check(r.Battle.Events.OfType<WildBattleMoveMissedEvent>().Any(), "miss found");
        Check(s.Player.Pokemon.Moves[1].CurrentPp == before - 1 && s.Player.CalledTurnState?.Lock is not null, "miss spends PP and keeps existing timer");
        var state = locked with { Player = locked.Player with { CalledTurnState = locked.Player.CalledTurnState! with { Lock = locked.Player.CalledTurnState!.Lock! with { TurnsRemaining = 1 } } } };
        state = Tick(state); Check(state.Player.CalledTurnState?.Lock is null, "timer expires");
        Check((state.Player.ConfusionTurnsRemaining > 0) == (name != "UPROAR"), "only rampage end confuses");
    }
});
Test("Ice Ball and existing Rollout execute five real escalating hits, cancel on miss", () => {
    foreach (string name in new[] { "ICE_BALL", "ROLLOUT" }) {
        var first = Act(Pair() with { Player = Pair().Player with { AccuracyStage = 12 } }, name); var s = Accepted(first);
        int pp = s.Player.Pokemon.Moves[1].CurrentPp;
        for (int i = 0; i < 4; i++) { s = s with { Opponent = s.Opponent with { Pokemon = s.Opponent.Pokemon with { CurrentHp = s.Opponent.Pokemon.Stats.MaxHp } }, Outcome = WildBattleOutcome.Ongoing }; var r = Continue(s); s = Accepted(r); Check(s.Player.Pokemon.Moves[1].CurrentPp == pp, "continued PP unchanged"); Check((s.Player.CalledTurnState?.Lock?.TurnsRemaining ?? 0) == 3-i, "five-hit counter"); }
        var locked = Locked(name); WildBattleCalledMoveResult miss = Continue(locked);
        for (uint seed = 0; seed < 100; seed++) { miss = Continue(locked with { Rng = new(seed), Player = locked.Player with { AccuracyStage = 0 }, Opponent = locked.Opponent with { EvasionStage = 12 } }); if (miss.Battle.Events.OfType<WildBattleMoveMissedEvent>().Any()) break; }
        s = Accepted(miss); Check(s.Player.CalledTurnState?.Lock is null && s.Player.Pokemon.Moves[1].CurrentPp == locked.Player.Pokemon.Moves[1].CurrentPp, "non-Protect miss cancels without PP");
        var protect = Accepted(Continue(locked with { Opponent = locked.Opponent with { Protected = true } })); Check(protect.Player.CalledTurnState?.Lock is null && protect.Player.Pokemon.Moves[1].CurrentPp == locked.Player.Pokemon.Moves[1].CurrentPp - 1, "Protect cancels before skip-PP test");
    }
});
Test("Bide stores actual damage across enemy actions and releases twice total", () => {
    var s = Accepted(Act(Pair(), "BIDE", WildBattleSide.Opponent));
    var attack1 = Act(s, "TACKLE"); s = Accepted(attack1); int first = Damage(attack1);
    Check(s.Opponent.CalledTurnState?.Lock?.StoredDamage == first && first > 0, "first actual damage stored");
    var store = Continue(s, WildBattleSide.Opponent); s = Accepted(store); Check(s.Opponent.CalledTurnState?.Lock?.TurnsRemaining == 1 && store.Battle.Trace.IsEmpty, "Bide storage continuation no RNG");
    var attack2 = Act(s, "TACKLE"); s = Accepted(attack2); int total = first + Damage(attack2);
    Check(s.Opponent.CalledTurnState?.Lock?.StoredDamage == total, "second actual damage stored");
    int pp = s.Opponent.Pokemon.Moves[1].CurrentPp; var release = Continue(s, WildBattleSide.Opponent); s = Accepted(release);
    Check(release.Battle.Events.OfType<WildBattleHpChangedEvent>().Single().Damage == total * 2, "twice source accumulated damage");
    Check(s.Opponent.CalledTurnState?.Lock is null && s.Opponent.Pokemon.Moves[1].CurrentPp == pp && !release.Battle.Events.OfType<WildBattlePpCommandEvent>().Any(), "Bide release keeps original PP");
    var empty = Locked("BIDE"); empty = Accepted(Continue(empty)); var failed = Continue(empty); s = Accepted(failed);
    Check(failed.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "bide-no-energy") && s.Player.CalledTurnState?.Lock is null, "zero stored damage fails and clears lock");
});
Test("Bide release checks Protect/type immunity; fixed-damage item command has exact RNG", () => {
    var stored = Locked("BIDE"); stored = stored with { Player = stored.Player with { FuryCutterCounter = 3, CalledTurnState = stored.Player.CalledTurnState! with {
        Lock = stored.Player.CalledTurnState!.Lock! with { TurnsRemaining = 1, StoredDamage = 20, StoredDamageSource = WildBattleSide.Opponent } } } };
    var protectedRelease = Continue(stored with { Opponent = stored.Opponent with { Protected = true } }); var protectedAfter = Accepted(protectedRelease);
    Check(Damage(protectedRelease) == 0 && protectedAfter.Player.CalledTurnState?.Lock is null && protectedAfter.Player.FuryCutterCounter == 0, "Protect cancels complete multi-turn/Fury state");
    Check(protectedRelease.Battle.Trace.IsEmpty && !protectedRelease.Battle.Events.OfType<WildBattlePpCommandEvent>().Any(), "protected Bide release no accuracy or PP");
    var immune = stored with { Opponent = new(Mon("GASTLY", 60, "TACKLE")) { FirstTurnCounter = 1 } };
    var ghost = Continue(immune); Accepted(ghost); Check(Damage(ghost) == 0 && ghost.Battle.Trace.Count(t => t.Purpose.EndsWith(".accuracy")) == 1, "Bide remains Normal and checks ordinary accuracy");
    Check(ghost.Battle.Trace.Length == 1, "no crit/damage/secondary RNG");
    // Focus Band is an authenticated source item but is outside the current legal held-item overlay.
    // Check the real source command primitive; do not pretend public action admission accepted it.
    var band = immune with { Opponent = immune.Opponent with { HeldItem = "ITEM_FOCUS_BAND" } };
    Check(!Continue(band).Succeeded, "unsupported item is still rejected by public contract");
    var executionType = typeof(WildBattleRules).GetNestedType("Execution", BindingFlags.NonPublic)!;
    var execution = Activator.CreateInstance(executionType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [band, false, false], null)!;
    executionType.GetMethod("ScalarAdjust", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(execution, [WildBattleSide.Opponent, catalog.Moves[Id("BIDE")], 40, "bide-source-command"]);
    var trace = ((IEnumerable<RandomTrace>)executionType.GetField("Trace")!.GetValue(execution)!).ToArray();
    Check(trace.Length == 1 && trace[0].Purpose.EndsWith(".focus-band"), "adjustsetdamage command draws regardless of type immunity");
});
Test("air, underground and underwater miss/hit exceptions and damage multipliers", () => {
    foreach (var item in new[] { (Charge:"FLY", Hit:"GUST"), (Charge:"BOUNCE", Hit:"TWISTER"), (Charge:"DIG", Hit:"EARTHQUAKE"), (Charge:"DIVE", Hit:"SURF"), (Charge:"DIVE", Hit:"WHIRLPOOL") }) {
        var baseState = Pair() with { Player = Pair().Player with { AccuracyStage = 12 } };
        var hidden = Accepted(Act(baseState, item.Charge, WildBattleSide.Opponent));
        var miss = Act(hidden, "TACKLE"); Accepted(miss); Check(miss.Battle.Events.OfType<WildBattleMoveMissedEvent>().Any() && !miss.Battle.Trace.Any(t => t.Purpose.EndsWith(".accuracy")), item.Charge + " evades before accuracy draw");
        var hit = Act(hidden with { Rng = new(7) }, item.Hit); Accepted(hit);
        var visible = hidden with { Opponent = hidden.Opponent with { CalledTurnState = null }, Rng = new(7) }; var normal = Act(visible, item.Hit); Accepted(normal);
        Check(Damage(hit) > Damage(normal), item.Hit + " authentic doubled source modifier");
    }
    var fly = Accepted(Act(Pair(), "FLY", WildBattleSide.Opponent));
    foreach (var move in new[] { "THUNDER", "SKY_UPPERCUT" }) { var r = Act(fly with { Player = fly.Player with { AccuracyStage = 12 } }, move); Accepted(r); Check(Damage(r) > 0, move + " hits airborne"); }
    var transform = Act(fly, "TRANSFORM"); var transformed = Accepted(transform); Check(transformed.Player.Transform is null, "Transform fails against semi-invulnerable target");
});
Test("Lock-On and Mind Reader persist for two residuals and bypass semi-invulnerability", () => {
    foreach (string move in new[] { "LOCK_ON", "MIND_READER" }) {
        var s = Accepted(Act(Pair(), move)); Check(s.Opponent.CalledTurnState is { SureHitTurnsRemaining: 2, SureHitSource: WildBattleSide.Player }, "sure-hit source recorded");
        s = Accepted(Act(s, "FLY", WildBattleSide.Opponent)); var hit = Act(s with { Player = s.Player with { AccuracyStage = 0 } }, "TACKLE"); Accepted(hit);
        Check(Damage(hit) > 0 && !hit.Battle.Trace.Any(t => t.Purpose.EndsWith(".accuracy")), "sure hit precedes semi and accuracy");
        s = Tick(s); Check(s.Opponent.CalledTurnState?.SureHitTurnsRemaining == 1, "first residual"); s = Tick(s); Check((s.Opponent.CalledTurnState?.SureHitTurnsRemaining ?? 0) == 0, "second residual expires");
        var blocked = Act(Accepted(Act(Pair(), move)) with { Opponent = Accepted(Act(Pair(), move)).Opponent with { Protected = true } }, "TACKLE"); Accepted(blocked); Check(Damage(blocked) == 0, "Protect precedes sure-hit");
    }
});
Test("Uproar wakes both battlers and stops sleep, Rest and Yawn with Soundproof exception", () => {
    var locked = Locked("UPROAR"); var sleep = Tick(locked with { Player = locked.Player with { Status = 3, Nightmare = true }, Opponent = locked.Opponent with { Status = 3, Nightmare = true } });
    Check(sleep.Player.Status == 0 && sleep.Opponent.Status == 0 && !sleep.Player.Nightmare && !sleep.Opponent.Nightmare, "wake active sleepers and remove nightmare");
    var quiet = Pair() with { Player = new(Mon("CLEFABLE", 100, "GROWL", "METRONOME")) { FirstTurnCounter = 1 }, Opponent = new(Mon("SNORLAX", 20, "GROWL", "METRONOME")) { FirstTurnCounter = 1 } };
    var opponentUproar = Accepted(Act(quiet, "UPROAR", WildBattleSide.Opponent));
    // Reset the caller's HP after the first Uproar hit, keeping valid source state.
    opponentUproar = opponentUproar with { Outcome = WildBattleOutcome.Ongoing, Player = opponentUproar.Player with { Pokemon = opponentUproar.Player.Pokemon with { CurrentHp = Math.Max(1, opponentUproar.Player.Pokemon.Stats.MaxHp - 5) } } };
    var rest = Act(opponentUproar, "REST"); Accepted(rest); Check(rest.State!.Player.Status == 0 && rest.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "uproar"), "Rest cannot sleep");
    var yawn = Act(opponentUproar, "YAWN"); Accepted(yawn); Check(yawn.State!.Opponent.YawnTurnsRemaining == 0, "Yawn blocked");
    var sleepMove = Act(opponentUproar with { Player = opponentUproar.Player with { AccuracyStage = 12 } }, "SPORE"); Accepted(sleepMove); Check(sleepMove.State!.Opponent.Status == 0, "primary sleep blocked");
    var proof = locked with { Opponent = new(Mon("VOLTORB", 60, "TACKLE")) { FirstTurnCounter = 1, Status = 3 } };
    Check(proof.Opponent.Pokemon.AbilityId == "ABILITY_SOUNDPROOF", "authentic Soundproof"); var after = Tick(proof); Check(after.Opponent.Status == 3, "Soundproof asleep unaffected");
    var continued = Continue(proof); after = Accepted(continued); Check(after.Player.Pokemon.Moves[1].CurrentPp == proof.Player.Pokemon.Moves[1].CurrentPp, "Soundproof on locked Uproar suppresses PP");
});
Test("sure-hit fails against hidden targets before setup and authentic OHKO observes level guard", () => {
    var hidden = Accepted(Act(Pair(), "DIVE", WildBattleSide.Opponent));
    foreach (var name in new[] { "LOCK_ON", "MIND_READER" }) {
        var r = Act(hidden, name); var s = Accepted(r); Check((s.Opponent.CalledTurnState?.SureHitTurnsRemaining ?? 0) == 0, name + " cannot initially aim at hidden target");
        Check(r.Battle.Events.OfType<WildBattleMoveMissedEvent>().Any() && !r.Battle.Trace.Any(t => t.Purpose.EndsWith(".accuracy")), "hidden miss skips ordinary accuracy");
    }
    var basis = Pair() with { Player = new(Mon("CLEFABLE", 100, "GROWL", "METRONOME")) { FirstTurnCounter = 1 } };
    var aimed = Accepted(Act(basis, "LOCK_ON")); var ohko = Act(aimed, "FISSURE"); Accepted(ohko);
    Check(!ohko.Battle.Trace.Any(t => t.Purpose.EndsWith(".ohko")) && ohko.State!.Opponent.Pokemon.CurrentHp == 0, "sure hit bypasses tryKO chance only at permitted level");
    var low = Accepted(Act(Pair(), "LOCK_ON")); var denied = Act(low, "FISSURE"); Accepted(denied);
    Check(denied.Battle.Trace.Any(t => t.Purpose.EndsWith(".ohko")) && denied.State!.Opponent.Pokemon.CurrentHp > 0, "lower level still draws and fails");
});
Test("Fury Cutter uses real called power chain, miss and Protect reset", () => {
    var state = Pair() with { Player = Pair().Player with { AccuracyStage = 12 } }; int before = 0;
    for (int i = 0; i < 6; i++) { state = state with { Rng = new(7), Opponent = state.Opponent with { Pokemon = state.Opponent.Pokemon with { CurrentHp = state.Opponent.Pokemon.Stats.MaxHp } }, Outcome = WildBattleOutcome.Ongoing }; var r = Act(state, "FURY_CUTTER"); state = Accepted(r); Check(state.Player.FuryCutterCounter == Math.Min(5, i+1), "source counter"); Check(Damage(r) >= before, "power nondecreasing"); before = Damage(r); }
    var protect = Act(state with { Opponent = state.Opponent with { Protected = true } }, "FURY_CUTTER"); var s = Accepted(protect); Check(s.Player.FuryCutterCounter == 0, "Protect reset");
    Check(s.Player.CalledTurnState?.Lock is null, "Fury Cutter never forces next move");
});
Test("shared weather and departure consumers observe complete semi-invulnerable state", () => {
    var executionType = typeof(WildBattleRules).GetNestedType("Execution", BindingFlags.NonPublic)!;
    (WildBattleState State, WildBattleEvent[] Events) Run(WildBattleState state, string method, params object[] arguments) {
        var execution = Activator.CreateInstance(executionType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [state, false, false], null)!;
        executionType.GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(execution, arguments);
        return ((WildBattleState)executionType.GetField("State")!.GetValue(execution)!, ((IEnumerable<WildBattleEvent>)executionType.GetField("Events")!.GetValue(execution)!).ToArray());
    }
    foreach (var name in new[] { "FLY", "DIG", "DIVE" }) {
        var s = Locked(name); var hail = Run(s with { CalledField = new() { HailTurnsRemaining = 5 } }, "CalledFieldHailDamage", catalog, WildBattleSide.Player);
        var sand = Run(s with { SandstormTurnsRemaining = 5 }, "RockTunnelSandstormDamage", catalog, WildBattleSide.Player);
        Check(hail.Events.OfType<WildBattleWeatherDamageEvent>().Any() == (name == "FLY"), name + " hail exposure");
        Check(sand.Events.OfType<WildBattleWeatherDamageEvent>().Any() == (name == "FLY"), name + " sand exposure");
        var departed = Run(s, "ClearRoute3Volatiles", WildBattleSide.Player, "switch"); Check(departed.State.Player.CalledTurnState is null, "departure clears lock");
    }
    var aimed = Accepted(Act(Pair(), "LOCK_ON")); var removed = Run(aimed, "ClearRoute3Volatiles", WildBattleSide.Player, "switch");
    Check(removed.State.Opponent.CalledTurnState?.SureHitSource is null, "caster departure clears sure-hit targeting");
});
Test("state validation, source mutation, serialization and atomic failure", () => {
    var s = Locked("FLY"); var roundtrip = JsonSerializer.Deserialize<WildBattleState>(JsonSerializer.Serialize(s))!; Check(roundtrip.Player.CalledTurnState == s.Player.CalledTurnState, "roundtrip lock identity");
    Check(WildBattleRules.Validate(catalog, s) is not null, "ordinary validator rejects called state");
    var locked = s.Player.CalledTurnState!.Lock!;
    foreach (var bad in new[] { locked with { PpSlot = 8 }, locked with { PpSlotMove = Id("FLY") }, locked with { TurnsRemaining = 5 }, locked with { Move = Id("TACKLE") }, locked with { StoredDamage = 1 } }) {
        var corrupt = s with { Player = s.Player with { CalledTurnState = s.Player.CalledTurnState with { Lock = bad } } }; var r = Continue(corrupt); Check(!r.Succeeded && ReferenceEquals(corrupt, r.State) && r.Battle.Trace.IsEmpty && r.Battle.Events.IsEmpty, "invalid lock atomic");
    }
    foreach (var overflow in new[] { s with { Revision = long.MaxValue }, s with { EventSequence = long.MaxValue }, s with { Rng = new(7, ulong.MaxValue) } }) { var r = Continue(overflow); Check(!r.Succeeded && ReferenceEquals(overflow, r.State) && r.Invocation is null && r.Battle.Trace.IsEmpty && r.Battle.Events.IsEmpty, "overflow atomic"); }
    var badCatalog = catalog with { Moves = catalog.Moves.SetItem(Id("FLY"), catalog.Moves[Id("FLY")] with { Power = 999 }) }; var malformed = WildBattleRules.ResolveCalledMoveContinuation(badCatalog, s, WildBattleSide.Player); Check(!malformed.Succeeded && malformed.Battle.Trace.IsEmpty, "metadata authentication");
});
Console.WriteLine($"{groups} groups, {checks} assertions, {failures} failures"); if (groups == 0 || checks == 0 || failures != 0) Environment.Exit(1);
