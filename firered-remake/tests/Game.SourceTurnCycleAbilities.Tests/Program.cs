using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length != 4) throw new ArgumentException("CANONICAL_ROOT MOVE_ROOT PINNED_SOURCE VECTOR_JSON");
var mechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0], args[1]);
var catalog = mechanics.Catalog;
string source = Path.GetFullPath(args[2]);
string tm = File.ReadAllText(Path.Combine(source, "src/data/pokemon/tmhm_learnsets.h"));
string tutor = File.ReadAllText(Path.Combine(source, "src/data/pokemon/tutor_learnsets.h"));
int groups = 0, checks = 0, failures = 0, turns = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
void Test(string name, Action action) { groups++; try { action(); Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " " + e); } }
MoveId Move(string name) => new("pret:MOVE_" + name);
WildBattleCombatant Mon(string species, string[] moves, int level = 50, uint pid = 0)
{
    var p = SourcePokemonFactory.Create(mechanics, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
    foreach (var name in moves)
    {
        bool learned = catalog.Species[p.Species].LevelUpMoves.Any(m => m.Move == Move(name) && m.Level <= level);
        string tmBlock = Regex.Match(tm, @"\[SPECIES_" + species + @"\].*?(?=\[SPECIES_|\z)", RegexOptions.Singleline).Value;
        string tutorBlock = Regex.Match(tutor, @"\[SPECIES_" + species + @"\].*?(?=\[SPECIES_|\z)", RegexOptions.Singleline).Value;
        Check(learned || Regex.IsMatch(tmBlock, @"TM\d\d_" + name + @"\)") || tutorBlock.Contains("TUTOR(MOVE_" + name + ")"), "authentic retained level/TM/tutor move " + species + "/" + name);
    }
    return new(p with { Moves = moves.Select(name => new PokemonMoveSlot(Move(name), catalog.Moves[Move(name)].Pp, catalog.Moves[Move(name)].Pp)).ToImmutableArray() });
}
WildBattleState Require(WildBattleResult r, string label)
{
    Check(r.Succeeded, label + ": " + r.Fault);
    Check(WildBattleRules.Validate(catalog, r.State) is null, label + " output valid: " + WildBattleRules.Validate(catalog, r.State));
    return r.State!;
}
WildBattleState Start(WildBattleCombatant player, WildBattleCombatant opponent, uint seed = 0, params WildBattleCombatant[] reserves) =>
    Require(WildBattleRules.StartSourceWild(mechanics, BattleRulesProfile.CanonicalSourceSingles, [player, .. reserves], 0, opponent, new(seed)), "entry");
WildBattleResult Turn(WildBattleState s, int slot = 0)
{
    var r = WildBattleRules.ResolveMove(catalog, s, slot);
    var next = Require(r, "full turn"); turns++;
    Check(next.Outcome != WildBattleOutcome.Ongoing || next.AwaitingReplacement || next.CalledParty?.PendingSwitch is not null || next.Turn == s.Turn + 1, "full turn owns readiness");
    return r;
}
WildBattleCombatant Get(WildBattleState s, WildBattleSide side) => side == WildBattleSide.Player ? s.Player : s.Opponent;
WildBattleState Sync(WildBattleState s) => s with { CalledParty = s.CalledParty! with {
    Player = s.CalledParty.Player.SetItem(s.CalledParty.PlayerActiveIndex, s.CalledParty.Player[s.CalledParty.PlayerActiveIndex] with { Pokemon = WildBattleTransformationRules.PersistentPokemon(s.Player), Status = WildBattleTransformationRules.PersistentStatus(s.Player) }),
    Opponent = s.CalledParty.Opponent.SetItem(0, s.CalledParty.Opponent[0] with { Pokemon = WildBattleTransformationRules.PersistentPokemon(s.Opponent), Status = WildBattleTransformationRules.PersistentStatus(s.Opponent) }) } };
WildBattleState Restore(WildBattleState s)
{
    string json = CanonicalJson.Text(s); var result = JsonSerializer.Deserialize<WildBattleState>(json)!;
    Check(CanonicalJson.Text(result) == json && WildBattleRules.Validate(catalog, result) is null, "canonical JSON round trip and validation");
    return result;
}
void Atomic(WildBattleState s, string label)
{
    string before = CanonicalJson.Hash(s); var r = WildBattleRules.ResolveMove(catalog, s, 0);
    Check(!r.Succeeded && ReferenceEquals(r.State, s) && r.Events.IsEmpty && r.Trace.IsEmpty && CanonicalJson.Hash(s) == before, label + " rollback");
}
bool Loaf(WildBattleResult r, WildBattleSide side) => r.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Actor == side && e.Reason == "truant");
WildBattleResult Action(WildBattleState s, WildBattleSide side = WildBattleSide.Player, int slot = 0)
{
    var r = WildBattleRules.ResolveSourceMoveAction(catalog, s, new(side, slot)).Battle;
    Require(r, "shared source action"); return r;
}

Test("official runtime, real donor identities and campaign boundary", () => {
    Check(Environment.Version.Major == 10 && catalog.Species.Count == 386 && mechanics.Abilities.Count == 76, "runtime/source inventory");
    foreach (string name in new[] { "SLAKOTH", "SLAKING", "NINJASK" }) {
        var mon = Mon(name, [name == "NINJASK" ? "HARDEN" : "AMNESIA"]);
        Check(mon.Pokemon.AbilityId == (name == "NINJASK" ? "ABILITY_SPEED_BOOST" : "ABILITY_TRUANT"), "authentic ability " + name);
        Check(SourcePokemonValidator.ValidatePermanent(mechanics, mon.Pokemon) is null, "permanent identity validates");
        Check(CampaignAdmissionPolicy.CurrentFuchsia.ValidatePokemon(mon.Pokemon) == "campaign_species_unsupported", "no campaign admission " + name);
    }
});
Test("72 original C end-turn vectors match shared ability executor", () => {
    using var doc = JsonDocument.Parse(File.ReadAllBytes(args[3]));
    Check(doc.RootElement.GetProperty("sourceCommit").GetString() == catalog.SourceCommit, "pinned original C");
    var rows = doc.RootElement.GetProperty("rows"); Check(rows.GetArrayLength() == 72, "nonempty complete vector grid");
    var type = typeof(WildBattleRules).GetNestedType("Execution", BindingFlags.NonPublic)!;
    foreach (var row in rows.EnumerateArray()) {
        int N(string key) => row.GetProperty(key).GetInt32();
        string species = N("ability") == 3 ? "NINJASK" : "SLAKOTH";
        var s = Start(Mon(species, [species == "NINJASK" ? "HARDEN" : "AMNESIA"]), Mon("MAGIKARP", ["SPLASH"]));
        s = s with { Player = s.Player with { Pokemon = s.Player.Pokemon with { CurrentHp = N("hp") }, SpeedStage = N("speed"), FirstTurnCounter = N("first"), TruantLoafing = N("truant") != 0 } };
        var executor = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [s, false, true], null)!;
        type.GetMethod("SourceEndTurnAbility", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(executor, [WildBattleSide.Player]);
        var after = (WildBattleState)type.GetField("State")!.GetValue(executor)!;
        var events = (ImmutableArray<WildBattleEvent>.Builder)type.GetField("Events")!.GetValue(executor)!;
        var trace = (ImmutableArray<RandomTrace>.Builder)type.GetField("Trace")!.GetValue(executor)!;
        Check(after.Player.SpeedStage == N("afterSpeed") && after.Player.TruantLoafing == (N("afterTruant") != 0), "source exact state grid");
        Check(events.OfType<WildBattleAbilityActivatedEvent>().Count() == N("callbacks") && trace.Count == 0 && after.Rng == s.Rng, "source exact event/RNG grid");
    }
});
Test("original C cleanup preserves/expires real canonical recharge counters", () => {
    using var doc = JsonDocument.Parse(File.ReadAllBytes(args[3]));
    var type = typeof(WildBattleRules).GetNestedType("Execution", BindingFlags.NonPublic)!;
    foreach (var row in doc.RootElement.GetProperty("rechargeRows").EnumerateArray()) {
        var s = Start(Mon("SLAKING", ["HYPER_BEAM"]), Mon("MAGIKARP", ["SPLASH"]));
        s = s with { Player = s.Player with { CalledTurnState = new() { Lock = new(WildBattleCalledLockKind.Recharge, Move("HYPER_BEAM"), 0, Move("HYPER_BEAM"), WildBattleSide.Opponent, row.GetProperty("before").GetInt32()) } } };
        Check(WildBattleRules.Validate(catalog, s) is null, "canonical recharge range validates");
        var executor = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [s, false, true], null)!;
        type.GetMethod("SourceTurnCycleReady", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(executor, []);
        var after = (WildBattleState)type.GetField("State")!.GetValue(executor)!;
        Check((after.Player.CalledTurnState?.Lock?.TurnsRemaining ?? 0) == row.GetProperty("after").GetInt32(), "source cleanup timer");
        Check((after.Player.CalledTurnState?.Lock is not null) == row.GetProperty("recharging").GetBoolean(), "source cleanup recharge flag");
    }
});
foreach (string species in new[] { "SLAKOTH", "SLAKING" }) foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
Test(species + " alternates real complete turns on " + side, () => {
    var donor = Mon(species, ["AMNESIA"]); var foe = Mon("MAGIKARP", ["SPLASH"]);
    var s = side == WildBattleSide.Player ? Start(donor, foe) : Start(foe, donor);
    Check(!Get(s, side).TruantLoafing && Get(s, side).FirstTurnCounter == 1, "initial entry can act");
    for (int i = 0; i < 4; i++) {
        int pp = Get(s, side).Pokemon.Moves[0].CurrentPp; var r = Turn(s); var next = r.State!;
        Check(Loaf(r, side) == (i % 2 == 1), "alternating loaf branch");
        Check(Get(next, side).Pokemon.Moves[0].CurrentPp == pp - (i % 2 == 0 ? 1 : 0), "no PP on loaf turn");
        Check(Get(next, side).TruantLoafing == (i % 2 == 0), "end-turn toggle");
        Check(r.Events.OfType<WildBattleMoveUsedEvent>().Any(e => e.Actor != side), "opposing action completes");
        if (i % 2 == 1) {
            Check(!r.Trace.Any(t => t.Purpose.StartsWith("wild-battle." + side.ToString().ToLowerInvariant() + ".") && t.Purpose != "wild-battle.opponent.choose-slot"), "Truant consumes no action RNG");
            Check(Get(next, side).LastUsedMove == Move("UNAVAILABLE") && Get(next, side).SourceMoveHistory!.LastResultingMove == Move("UNAVAILABLE"), "blocked source move registers unavailable");
            Check(Get(next, side).SourceMoveHistory!.LastPrintedMove == Move("AMNESIA"), "last printed move survives loafing");
        }
        s = Restore(next);
    }
});
Test("canonical resume produces identical full state, events and RNG", () => {
    var s = Turn(Start(Mon("SLAKING", ["SCRATCH"]), Mon("SNORLAX", ["AMNESIA"], 100))).State!;
    Check(CanonicalJson.Text(s).Contains("\"TruantLoafing\":true"), "active register serialized");
    var restored = Restore(s); var left = Turn(s); var right = Turn(restored);
    Check(CanonicalJson.Text(left) == CanonicalJson.Text(right), "identical next whole transition");
    Check(!CanonicalJson.Text(left.State).Contains("TruantLoafing"), "false register omitted");
});
Test("sleep and freeze run before Truant; later immobilizers consume no RNG", () => {
    var baseState = Turn(Start(Mon("SLAKOTH", ["AMNESIA"]), Mon("MAGIKARP", ["SPLASH"]))).State!;
    foreach (uint sleep in new uint[] { 2, 1 }) {
        var s = Sync(baseState with { Player = baseState.Player with { Status = sleep } }); var r = Action(s);
        Check(Loaf(r, WildBattleSide.Player) == (sleep == 1) && r.State!.Player.Status == sleep - 1, "sleep countdown precedes loaf");
        Check(r.Trace.IsEmpty && r.State!.Player.Pokemon.Moves[0].CurrentPp == s.Player.Pokemon.Moves[0].CurrentPp, "sleep/wake no RNG or PP");
    }
    foreach (uint seed in new uint[] { 0, 1, 2, 3, 4 }) {
        var s = Sync(baseState with { Rng = new(seed), Player = baseState.Player with { Status = WildBattleStatus.Freeze } }); var r = Action(s);
        Check(r.Trace.Length == 1 && r.Trace[0].Purpose.EndsWith("freeze-thaw"), "freeze owns sole preceding RNG");
        Check(Loaf(r, WildBattleSide.Player) == (r.State!.Player.Status == 0), "only thawed actor reaches Truant");
    }
    var confused = Sync(baseState with { Player = baseState.Player with { Status = WildBattleStatus.Paralysis, ConfusionTurnsRemaining = 3, Flinched = true } });
    var block = Action(confused);
    Check(Loaf(block, WildBattleSide.Player) && block.Trace.IsEmpty, "Truant precedes flinch/confusion/paralysis");
    Check(block.State!.Player.Flinched && block.State.Player.ConfusionTurnsRemaining == 3, "later canceller registers untouched");
});
Test("Truant still cycles when asleep and cannot cancel nonmove switching", () => {
    var s = Start(Mon("SLAKOTH", ["AMNESIA"]) with { Status = 3 }, Mon("MAGIKARP", ["SPLASH"]), 0, Mon("NINJASK", ["HARDEN"]));
    var a = Turn(s); Check(a.State!.Player.TruantLoafing && a.State.Player.Status == 2, "blocked initial move still cycles");
    var b = Turn(a.State); Check(!b.State!.Player.TruantLoafing && !Loaf(b, WildBattleSide.Player), "sleep blocks before silent next toggle");
    s = Turn(b.State).State!; Check(s.Player.TruantLoafing, "waking action resumes cycle");
    var switched = WildBattleRules.SwitchSourceWildPlayer(catalog, s, 1); var next = Require(switched, "switch while loafing");
    Check(!next.Player.TruantLoafing && next.Player.SpeedStage == 6 && next.Player.FirstTurnCounter == 1, "fresh Ninjask skips entry boost");
    var back = WildBattleRules.SwitchSourceWildPlayer(catalog, next, 0); next = Require(back, "switch back to Truant");
    Check(!next.Player.TruantLoafing, "switch-in one toggled to zero before next action");
    Check(!Loaf(Turn(next), WildBattleSide.Player), "returning Slakoth acts first turn");
});
foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
Test("Ninjask complete turns boost speed on " + side, () => {
    var p = Mon("NINJASK", ["HARDEN"]); var o = Mon("MAGIKARP", ["SPLASH"]);
    var s = side == WildBattleSide.Player ? Start(p, o) : Start(o, p);
    for (int i = 0; i < 7; i++) {
        var r = Turn(s); s = r.State!;
        Check(Get(s, side).SpeedStage == Math.Min(12, 7 + i), "one boost per full turn capped +6");
        Check(r.Events.OfType<WildBattleAbilityActivatedEvent>().Count(e => e.Side == side && e.Ability == "ABILITY_SPEED_BOOST") == (i < 6 ? 1 : 0), "no activation at stat cap");
        Check(!r.Trace.Any(t => t.Purpose.Contains("speed-boost")), "Speed Boost no RNG");
    }
});
Test("Speed Boost executes after Ingrain and before poison and held-item phases", () => {
    var s = Start(Mon("NINJASK", ["HARDEN"]) with { Status = WildBattleStatus.Poison }, Mon("MAGIKARP", ["SPLASH"]));
    s = Sync(s with { Player = s.Player with { Rooted = true, Pokemon = s.Player.Pokemon with { CurrentHp = 10 } } });
    var r = Turn(s);
    var heal = r.Events.OfType<WildBattleIngrainHealEvent>().Single(e => e.Side == WildBattleSide.Player);
    var boost = r.Events.OfType<WildBattleAbilityActivatedEvent>().Single(e => e.Ability == "ABILITY_SPEED_BOOST");
    var poison = r.Events.OfType<WildBattleStatusDamageEvent>().Single(e => e.Side == WildBattleSide.Player);
    Check(heal.Sequence < boost.Sequence && boost.Sequence < poison.Sequence, "source Ingrain/ability/poison ordering");
});
Test("Speed Boost works while asleep and resets on ordinary switch", () => {
    var s = Start(Mon("NINJASK", ["HARDEN"]) with { Status = 3 }, Mon("MAGIKARP", ["SPLASH"]), 0, Mon("SLAKING", ["AMNESIA"]));
    s = Turn(s).State!; Check(s.Player.Status == 2 && s.Player.SpeedStage == 7, "sleep does not block ability");
    s = Require(WildBattleRules.SwitchSourceWildPlayer(catalog, s, 1), "switch out");
    s = Require(WildBattleRules.SwitchSourceWildPlayer(catalog, s, 0), "switch in");
    Check(s.Player.SpeedStage == 6, "fresh speed stages and skipped switch turn");
});
Test("Baton Pass carries Speed stages but never the incoming first-turn or Truant register", () => {
    var s = Start(Mon("NINJASK", ["HARDEN", "BATON_PASS"]), Mon("MAGIKARP", ["SPLASH"]), 0,
        Mon("NINJASK", ["HARDEN"], 50, 1), Mon("SLAKING", ["AMNESIA"]));
    s = Turn(s).State!; Check(s.Player.SpeedStage == 7, "outgoing genuine Speed Boost");
    s = Turn(s, 1).State!; Check(s.CalledParty?.PendingSwitch?.Kind == WildBattleCalledSwitchKind.BatonPass, "actual suspended Baton choice");
    s = Require(WildBattleRules.ChooseSourceWildParty(catalog, Restore(s), 1), "Baton resume");
    Check(s.Player.SpeedStage == 7 && s.Player.FirstTurnCounter == 1, "passed speed retained with no arrival boost");
    s = Turn(s).State!; Check(s.Player.SpeedStage == 8, "incoming boosts next complete turn");
    s = Require(WildBattleRules.SwitchSourceWildPlayer(catalog, s, 2), "incoming Truant");
    Check(!s.Player.TruantLoafing && s.Player.SpeedStage == 6, "ordinary switch register seeded then toggled");
});
Test("real Skill Swap preserves raw counters through ability removal and reacquisition", () => {
    var s = Start(Mon("ALAKAZAM", ["RECOVER", "SKILL_SWAP"], 100), Mon("SLAKING", ["AMNESIA"], 40));
    s = Turn(s, 0).State!; Check(s.Opponent.TruantLoafing, "donor acted before swap");
    var swapped = Turn(s, 1); s = swapped.State!;
    Check(WildBattleEffective.Ability(s.Player) == "ABILITY_TRUANT" && s.Player.TruantLoafing, "receiver toggles its own raw zero");
    Check(WildBattleEffective.Ability(s.Opponent) != "ABILITY_TRUANT" && s.Opponent.TruantLoafing && !Loaf(swapped, WildBattleSide.Opponent), "removed ability keeps register without canceling");
    s = Turn(s, 0).State!;
    var restored = Turn(s, 1); s = restored.State!;
    Check(Loaf(restored, WildBattleSide.Opponent), "reacquired Truant observes old raw one before action");
    Check(!s.Opponent.TruantLoafing, "restored ability cycles old register");
    Check(s.Player.Pokemon.AbilityId == "ABILITY_SYNCHRONIZE" && s.Opponent.Pokemon.AbilityId == "ABILITY_TRUANT", "permanent authentic abilities unchanged");
});
Test("called Metronome shares Truant cancellation without PP, RNG or call transition", () => {
    var s = Start(Mon("KADABRA", ["ROLE_PLAY", "METRONOME"], 100), Mon("SLAKING", ["AMNESIA"]));
    s = Turn(s, 0).State!; Check(s.Player.TruantLoafing, "real Role Play acquired Truant");
    var called = WildBattleRules.ResolveCalledMoveAction(catalog, s, new(WildBattleSide.Player, 1, Move("SCRATCH"))).Battle;
    Require(called, "called canceled action");
    Check(Loaf(called, WildBattleSide.Player) && called.Trace.IsEmpty && !called.Events.OfType<WildBattleCalledMoveTransitionEvent>().Any(), "caller blocked before target/selection RNG");
    Check(called.State!.Player.Pokemon.Moves[1].CurrentPp == s.Player.Pokemon.Moves[1].CurrentPp, "caller PP untouched");
    var full = Turn(s, 1); Check(Loaf(full, WildBattleSide.Player) && !full.Events.OfType<WildBattleCallerSelectionEvent>().Any(e => e.Actor == WildBattleSide.Player), "ordinary random caller also blocked");
});
Test("Transform preserves actor register and then uses copied turn-cycle ability", () => {
    var s = Start(Mon("DITTO", ["TRANSFORM"]), Mon("SLAKOTH", ["AMNESIA"]));
    s = Turn(s).State!;
    Check(s.Player.Transform is not null && s.Player.TruantLoafing && s.Opponent.TruantLoafing, "actual copied Truant toggles after Transform");
    var r = Turn(Restore(s)); Check(Loaf(r, WildBattleSide.Player) && Loaf(r, WildBattleSide.Opponent), "both real/converted holders loaf");
    var speed = Turn(Start(Mon("DITTO", ["TRANSFORM"]), Mon("NINJASK", ["HARDEN"]))).State!;
    Check(speed.Player.Transform is not null && speed.Player.SpeedStage == 7 && speed.Opponent.SpeedStage == 7, "copied Speed Boost executes on Transform turn");
});
Test("two-turn Solar Beam is canceled by Truant before continuation PP or damage", () => {
    var s = Start(Mon("SLAKING", ["SOLAR_BEAM"]), Mon("SNORLAX", ["AMNESIA"], 100));
    s = Turn(s).State!; Check(s.Player.CalledTurnState?.Lock?.Kind == WildBattleCalledLockKind.Charge && s.Player.TruantLoafing, "genuine TM charge begun");
    int pp = s.Player.Pokemon.Moves[0].CurrentPp; var r = Turn(Restore(s));
    Check(Loaf(r, WildBattleSide.Player) && r.State!.Player.CalledTurnState?.Lock is null, "CancelMultiTurnMoves clears charge");
    Check(r.State!.Player.Pokemon.Moves[0].CurrentPp == pp && !r.Events.OfType<WildBattleHpChangedEvent>().Any(e => e.Actor == WildBattleSide.Player), "no continuation damage/PP");
});
Test("Hyper Beam recharge expires on loafing turn and cannot leak another forced turn", () => {
    var s = Start(Mon("SLAKING", ["HYPER_BEAM", "AMNESIA"], 40), Mon("SNORLAX", ["AMNESIA"], 100), 2);
    var beam = Turn(s); s = beam.State!; Check(s.Player.CalledTurnState?.Lock is { Kind: WildBattleCalledLockKind.Recharge, TurnsRemaining: 1 } && s.Player.TruantLoafing, "source timer two reaches one at cleanup: " + JsonSerializer.Serialize(new { s.Player.CalledTurnState, s.Player.TruantLoafing, misses = beam.Events.OfType<WildBattleMoveMissedEvent>().Count(), beam.Trace }));
    var r = Turn(Restore(s)); s = r.State!;
    Check(Loaf(r, WildBattleSide.Player) && !r.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "must-recharge"), "loafing takes precedence over recharge");
    Check(s.Player.CalledTurnState?.Lock is null && !s.Player.TruantLoafing, "cleanup expires recharge after blocked turn");
    Check(Turn(s, 1).Events.OfType<WildBattleMoveUsedEvent>().Any(e => e.Actor == WildBattleSide.Player && e.Move == Move("AMNESIA")), "third turn is freely selectable");
});
Test("Mimic supplies real Pursuit; loafing prevents switch interception without PP or hit RNG", () => {
    var s = Start(Mon("DODRIO", ["PURSUIT"], 50), Mon("SLAKOTH", ["MIMIC"], 100), 0, Mon("SNORLAX", ["AMNESIA"], 100));
    s = Turn(s).State!;
    Check(s.Opponent.Pokemon.Moves[0].Move == Move("PURSUIT") && s.Opponent.CopyMoves is not null && s.Opponent.TruantLoafing, "actual slower Mimic copied learned Pursuit");
    int pp = s.Opponent.Pokemon.Moves[0].CurrentPp;
    var blocked = WildBattleRules.SwitchSourceWildPlayer(catalog, Restore(s), 1); s = Require(blocked, "switch on loaf turn");
    Check(!blocked.Events.OfType<WildBattlePursuitInterceptedEvent>().Any() && Loaf(blocked, WildBattleSide.Opponent), "interception skipped then normal queued action loafs");
    Check(s.Opponent.Pokemon.Moves[0].CurrentPp == pp && !blocked.Trace.Any(t => t.Purpose is "wild-battle.opponent.critical" or "wild-battle.opponent.damage"), "no Pursuit PP or hit RNG");
    var intercept = WildBattleRules.SwitchSourceWildPlayer(catalog, s, 0); s = Require(intercept, "switch on active turn");
    Check(intercept.Events.OfType<WildBattlePursuitInterceptedEvent>().Count() == 1 && s.Opponent.Pokemon.Moves[0].CurrentPp == pp - 1, "next source counter permits exactly one intercept");
});
Test("sleep and freeze before recharge still expire at source cleanup", () => {
    var s = Turn(Start(Mon("SLAKING", ["HYPER_BEAM", "AMNESIA"], 40), Mon("SNORLAX", ["AMNESIA"], 100), 2)).State!;
    foreach (uint status in new uint[] { 2, WildBattleStatus.Freeze }) {
        var blocked = Turn(Sync(s with { Rng = new(0), Player = s.Player with { Status = status } }));
        Check(!Loaf(blocked, WildBattleSide.Player) && !blocked.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "must-recharge"), "earlier status consumes turn before either canceller");
        Check(blocked.State!.Player.CalledTurnState?.Lock is null, "source timer expires through earlier immobilizer");
    }
});
Test("loafing forced Struggle preserves only source pre-canceller target draws", () => {
    var mon = Mon("SLAKOTH", ["AMNESIA"]);
    mon = mon with { Pokemon = mon.Pokemon with { Moves = mon.Pokemon.Moves.SetItem(0, mon.Pokemon.Moves[0] with { CurrentPp = 0 }) } };
    var s = Turn(Start(mon, Mon("MAGIKARP", ["SPLASH"], 100)), WildBattleRules.StruggleSlot).State!;
    var r = Turn(s, WildBattleRules.StruggleSlot);
    Check(Loaf(r, WildBattleSide.Player) && r.State!.Player.Pokemon.Moves[0].CurrentPp == 0, "forced selection reaches Truant without PP");
    var own = r.Trace.Where(t => t.Purpose.StartsWith("wild-battle.player.")).ToArray();
    Check(own.Length > 0 && own.All(t => t.Purpose == "wild-battle.player.struggle-target"), "source target chosen before attack canceller; no damage/accuracy draws after");
    Check(!r.Events.OfType<WildBattleHpChangedEvent>().Any(e => e.Actor == WildBattleSide.Player), "no Struggle hit or recoil");
});
Test("faint clears Truant; residual replacement retains source switch timing", () => {
    var s = Start(Mon("SLAKOTH", ["AMNESIA"]) with { Status = WildBattleStatus.Poison }, Mon("MAGIKARP", ["SPLASH"]), 0, Mon("SLAKING", ["AMNESIA"]));
    s = Sync(s with { Player = s.Player with { Pokemon = s.Player.Pokemon with { CurrentHp = 1 } } });
    s = Turn(s).State!;
    Check(s.AwaitingReplacement && !s.Player.TruantLoafing && s.Player.Pokemon.CurrentHp == 0, "residual faint clears raw register");
    s = Require(WildBattleRules.ReplaceSourceWildPlayer(catalog, Restore(s), 1), "residual replacement");
    Check(s.Player.TruantLoafing, "FRLG post-ability replacement retains raw switch-in one until next residual");
    Check(Loaf(Turn(s), WildBattleSide.Player), "post-residual replacement loaf matches source lifetime");
});
foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
Test("real Slaking Focus Punch preparation obeys Truant on " + side, () => {
    var donor = Mon("SLAKING", ["FOCUS_PUNCH"], 40); var foe = Mon("SNORLAX", ["AMNESIA"], 100);
    var s = side == WildBattleSide.Player ? Start(donor, foe, 2) : Start(foe, donor, 2);
    var first = Turn(s); s = first.State!;
    Check(first.Events.OfType<WildBattleFocusPunchPreparedEvent>().Count(e => e.Actor == side) == 1, "initial source preparation emitted");
    int pp = Get(s, side).Pokemon.Moves[0].CurrentPp;
    var loaf = Turn(Restore(s)); s = loaf.State!;
    Check(!loaf.Events.OfType<WildBattleFocusPunchPreparedEvent>().Any(e => e.Actor == side) && Loaf(loaf, side), "loaf turn suppresses setup and move");
    Check(Get(s, side).Pokemon.Moves[0].CurrentPp == pp, "loaf Focus Punch costs no PP");
    Check(Turn(s).Events.OfType<WildBattleFocusPunchPreparedEvent>().Any(e => e.Actor == side), "setup returns with next active turn");
});
Test("Focus Punch checks stale raw Truant register after actual Skill Swap", () => {
    var s = Start(Mon("ALAKAZAM", ["RECOVER", "SKILL_SWAP"], 100), Mon("SLAKING", ["FOCUS_PUNCH"], 40), 2);
    s = Turn(s, 0).State!;
    var swapped = Turn(s, 1); s = swapped.State!;
    Check(s.Opponent.TruantLoafing && WildBattleEffective.Ability(s.Opponent) != "ABILITY_TRUANT", "removed ability retains raw source bit");
    Check(!swapped.Events.OfType<WildBattleFocusPunchPreparedEvent>().Any(e => e.Actor == WildBattleSide.Opponent), "preparation suppressed before ability swap");
    var stale = Turn(s, 0);
    Check(!stale.Events.OfType<WildBattleFocusPunchPreparedEvent>().Any(e => e.Actor == WildBattleSide.Opponent), "next setup still tests raw bit independent of ability");
    Check(stale.Events.OfType<WildBattleMoveUsedEvent>().Any(e => e.Actor == WildBattleSide.Opponent && e.Move == Move("FOCUS_PUNCH")), "source action remains permitted after Truant removed");
});
Test("real Snatch with acquired Truant cannot arm on loaf turn", () => {
    var s = Start(Mon("ALAKAZAM", ["SKILL_SWAP", "SNATCH"], 100), Mon("SLAKING", ["AMNESIA"], 40));
    s = Turn(s).State!; Check(s.Player.TruantLoafing, "real Skill Swap source ability");
    int pp = s.Player.Pokemon.Moves[1].CurrentPp; var blocked = Turn(s, 1); s = blocked.State!;
    Check(Loaf(blocked, WildBattleSide.Player) && !blocked.Events.OfType<WildBattleSnatchReadyEvent>().Any(), "Truant before Snatch arming");
    Check(s.Player.Pokemon.Moves[1].CurrentPp == pp && !blocked.Events.OfType<WildBattleMoveSnatchedEvent>().Any(), "no PP or stolen effect on loaf turn");
    var active = Turn(s, 1);
    Check(active.Events.OfType<WildBattleSnatchReadyEvent>().Any() && active.Events.OfType<WildBattleMoveSnatchedEvent>().Any(), "next active turn arms and steals real Amnesia");
});
Test("invalid register/profile and failed counter overflow are atomic", () => {
    var s = Turn(Start(Mon("SLAKOTH", ["AMNESIA"]), Mon("MAGIKARP", ["SPLASH"]))).State!;
    Atomic(s with { SourceMechanicsRulesVersion = null }, "unversioned counter");
    Atomic(s with { Revision = long.MaxValue }, "post-execution revision overflow");
    Atomic(s with { Player = s.Player with { Pokemon = s.Player.Pokemon with { CurrentHp = 0 } } }, "fainted register");
    var badEntry = WildBattleRules.StartSourceWild(mechanics, BattleRulesProfile.CanonicalSourceSingles, [Mon("SLAKOTH", ["AMNESIA"]) with { TruantLoafing = true }], 0, Mon("MAGIKARP", ["SPLASH"]), new(0));
    Check(!badEntry.Succeeded && badEntry.State is null && badEntry.Trace.IsEmpty, "entry refuses preloaded counter");
});
Console.WriteLine(JsonSerializer.Serialize(new { groups, checks, failures, completeTurns = turns, runtime = Environment.Version.ToString(), sourceCommit = catalog.SourceCommit }));
return failures == 0 && groups > 15 && turns > 25 ? 0 : 1;
