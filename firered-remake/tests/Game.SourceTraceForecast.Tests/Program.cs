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
void Atomic(WildBattleState s, string label)
{
    string before = CanonicalJson.Hash(s); var r = WildBattleRules.ResolveMove(catalog, s, 0);
    Check(!r.Succeeded && ReferenceEquals(r.State, s) && r.Events.IsEmpty && r.Trace.IsEmpty && CanonicalJson.Hash(s) == before, label + " rollback");
}
WildBattleResult Action(WildBattleState s, WildBattleSide side = WildBattleSide.Player, int slot = 0)
{
    var r = WildBattleRules.ResolveSourceMoveAction(catalog, s, new(side, slot)).Battle;
    Require(r, "shared source action"); Trace(s, r); return r;
}

const WildBattleSide P = WildBattleSide.Player, O = WildBattleSide.Opponent;
WildBattleResult Entry(WildBattleCombatant p, WildBattleCombatant o, uint seed=0, params WildBattleCombatant[] reserves)
{
    var r = WildBattleRules.StartSourceWild(mechanics, BattleRulesProfile.CanonicalSourceSingles, [p,.. reserves], 0, o, new(seed));
    Require(r,"entry"); return r;
}
void Trace(WildBattleState before, WildBattleResult r)
{
    uint seed=before.Rng.Seed;
    foreach(var draw in r.Trace) { seed=unchecked(seed*1103515245+24691); Check(draw.Raw==(ushort)(seed>>16),"raw LCG "+draw.Purpose); }
    Check(r.State!.Rng.Seed==seed && r.State.Rng.Cursor==before.Rng.Cursor+(ulong)r.Trace.Length,"terminal RNG");
}
WildBattleState Roundtrip(WildBattleResult r)
{
    var s=r.State!; var imported=SinglesBattleAdapter.Import(catalog,s);Check(imported.Succeeded,"frame import "+imported.Fault);
    var frame=JsonSerializer.Deserialize<SourceBattleFrame>(CanonicalJson.Text(imported.Frame))!;
    Check(SourceBattleIdentityRules.Validate(frame) is null,"frame valid");
    var exported=SinglesBattleAdapter.Export(frame);Check(exported.Succeeded,"frame export "+exported.Fault);
    Check(CanonicalJson.Text(s)==CanonicalJson.Text(exported.State),"sole weather authority lossless state");
    Check(frame.Field.PermanentWeather==s.PermanentWeather,"field weather retained");
    foreach(var e in r.Events) {
        var canonical=SourceBattleEventAdapter.Import(e);
        Check(SourceBattleEventHooks.ValidateRouting(frame,canonical) is null,"typed canonical routing");
        var saved=JsonSerializer.Deserialize<SourceBattleEvent>(JsonSerializer.Serialize<SourceBattleEvent>(canonical))!;
        Check(CanonicalJson.Text<WildBattleEvent>(e)==CanonicalJson.Text<WildBattleEvent>(SourceBattleEventAdapter.Export(saved)),"typed event roundtrip "+e.GetType().Name);
    }
    return exported.State!;
}
var vectors = JsonDocument.Parse(File.ReadAllText(args[3])).RootElement;
Test("authentic holders and unchanged campaign boundary", () => {
    Check(Environment.Version.Major == 10 && catalog.Species.Count == 386, "official runtime and catalog");
    foreach (var (name, pid, ability) in new[] { ("PORYGON", 0u, "ABILITY_TRACE"), ("PORYGON2", 0u, "ABILITY_TRACE"),
        ("RALTS", 1u, "ABILITY_TRACE"), ("KIRLIA", 1u, "ABILITY_TRACE"), ("GARDEVOIR", 1u, "ABILITY_TRACE"), ("CASTFORM", 0u, "ABILITY_FORECAST") }) {
        var m = Mon(name, ["PROTECT"], pid: pid); Check(m.Pokemon.AbilityId == ability, "authentic donor " + name);
        Check(SourcePokemonValidator.ValidatePermanent(mechanics, m.Pokemon) is null, "source PID ability slot");
        Check(!CampaignAdmissionPolicy.CurrentFuchsia.Admits(m.Pokemon.Species), "no new campaign species");
        Check(Entry(m, Mon("MAGIKARP", ["SPLASH"])).Succeeded, "real admission " + name);
    }
});
Test("original C Forecast branch corpus", () => {
    var castform = Mon("CASTFORM", ["PROTECT"]); var porygon = Mon("PORYGON", ["PROTECT"]);
    foreach (var row in vectors.GetProperty("forecastRows").EnumerateArray()) {
        // C branch inputs are battle-register projections, not fabricated permanent donors or admitted battles.
        var body = (row.GetProperty("castform").GetBoolean() ? castform : porygon);
        body = body with { Pokemon = body.Pokemon with { CurrentHp = row.GetProperty("hp").GetInt32() }, CalledOverlay = new() {
            Type1 = row.GetProperty("type1").GetString(), Type2 = row.GetProperty("type2").GetString(), Ability = row.GetProperty("ability").GetString() } };
        int w = row.GetProperty("weather").GetInt32(); var changed = SourceTraceForecastRules.ForecastChange(body,
            !row.GetProperty("suppressed").GetBoolean(), (w & 96) != 0, (w & 7) != 0, (w & 128) != 0);
        Check((changed is null ? 0 : (int)changed.Form + 1) == row.GetProperty("change").GetInt32(), "source form branch");
        Check((changed?.Type ?? body.CalledOverlay.Type1) == row.GetProperty("after1").GetString(), "source type1");
        Check((changed?.Type ?? body.CalledOverlay.Type2) == row.GetProperty("after2").GetString(), "source type2");
    }
});
Test("original C Trace position selection and exact draw count", () => {
    var donors = new Dictionary<string, WildBattleCombatant> {
        ["ABILITY_TRACE"] = Mon("PORYGON", ["PROTECT"]), ["ABILITY_FORECAST"] = Mon("CASTFORM", ["PROTECT"]),
        ["ABILITY_WONDER_GUARD"] = Mon("SHEDINJA", ["PROTECT"]), ["ABILITY_SWIFT_SWIM"] = Mon("MAGIKARP", ["SPLASH"]),
        ["ABILITY_DRIZZLE"] = Mon("KYOGRE", ["PROTECT"]) };
    WildBattleCombatant? Donor(string ability, int hp) => ability == "ABILITY_NONE" ? null : donors[ability] with { Pokemon = donors[ability].Pokemon with { CurrentHp = hp == 0 ? 0 : donors[ability].Pokemon.CurrentHp } };
    foreach (var row in vectors.GetProperty("traceRows").EnumerateArray()) {
        int count = row.GetProperty("count").GetInt32(), tracer = row.GetProperty("tracer").GetInt32(), calls = 0;
        var slots = new WildBattleCombatant?[count]; int first = (tracer ^ 1) & 1;
        slots[tracer] = donors["ABILITY_TRACE"]; slots[first] = Donor(row.GetProperty("a").GetString()!, row.GetProperty("ah").GetInt32());
        if (count == 4) slots[first + 2] = Donor(row.GetProperty("b").GetString()!, row.GetProperty("bh").GetInt32());
        int? selected = SourceTraceForecastRules.TraceTarget(slots, tracer, () => { calls++; return row.GetProperty("raw").GetUInt16(); });
        Check((selected ?? -1) == row.GetProperty("target").GetInt32(), "source trace target");
        Check(calls == row.GetProperty("draws").GetInt32(), "source trace draw consumption");
        if (selected is { } target) Check(WildBattleEffective.Ability(slots[target]!) == row.GetProperty("ability").GetString(), "source copies exact ability incl Trace/Wonder Guard");
    }
});
Test("Trace singles activates once, no ability RNG, authentic identity", () => {
    foreach (bool reverse in new[] { false, true }) {
        var porygon = Mon("PORYGON", ["PROTECT"]); var pika = Mon("PIKACHU", ["GROWL"]);
        var r = Entry(reverse ? pika : porygon, reverse ? porygon : pika); var side = reverse ? O : P;
        var s = Roundtrip(r); var m = Get(s, side);
        Check(WildBattleEffective.Ability(m) == "ABILITY_STATIC" && m.Pokemon.AbilityId == "ABILITY_TRACE", "Trace battle-only ability");
        Check(m.Pokemon.Personality == porygon.Pokemon.Personality && m.Pokemon.AbilitySlot == porygon.Pokemon.AbilitySlot, "permanent PID slot unchanged");
        Check(!m.SourceTracePending && !r.Trace.Any(t => t.Purpose.Contains("trace")), "one-shot no RNG");
        Check(r.Events.OfType<WildBattleAbilityActivatedEvent>().Count(e => e.Ability == "ABILITY_TRACE") == 1, "one activation");
        var t = Turn(s); Trace(s, t); Check(!t.Events.OfType<WildBattleAbilityActivatedEvent>().Any(e => e.Ability == "ABILITY_TRACE"), "no re-trace per turn"); Roundtrip(t);
    }
});
Test("Trace copies Trace and the admitted Wonder Guard donor", () => {
    var r = WildBattleRules.StartSourceWild(mechanics, BattleRulesProfile.CanonicalSourceSingles, [Mon("PORYGON", ["PROTECT"])], 0, Mon("SHEDINJA", ["PROTECT"]), new(0));
    Check(r.Succeeded && WildBattleEffective.Ability(r.State!.Player) == "ABILITY_WONDER_GUARD" &&
        r.State.Player.Pokemon.AbilityId == "ABILITY_TRACE", "real Wonder Guard donor now admitted; Trace keeps permanent identity");
    Roundtrip(r);
    r = Entry(Mon("PORYGON", ["PROTECT"]), Mon("PORYGON2", ["PROTECT"]));
    Check(r.Events.OfType<WildBattleAbilityActivatedEvent>().Count(e => e.Ability == "ABILITY_TRACE") == 2, "both slots copy Trace once");
    Check(!r.State!.Player.SourceTracePending && !r.State.Opponent.SourceTracePending, "both pending bits consumed"); Roundtrip(r);
});
Test("Trace does not replay copied switch-in scripts", () => {
    foreach (var donor in new[] { "GYARADOS", "KYOGRE", "GROUDON", "TYRANITAR" }) {
        var r = Entry(Mon("PORYGON", ["PROTECT"]), Mon(donor, ["PROTECT"])); var s = r.State!;
        string copied = s.Opponent.Pokemon.AbilityId;
        Check(WildBattleEffective.Ability(s.Player) == copied, "copied native " + copied);
        Check(r.Events.OfType<WildBattleAbilityActivatedEvent>().Count(e => e.Side == P && e.Ability == copied) == 0, "no copied ON_SWITCHIN");
        if (donor == "GYARADOS") Check(s.Player.AttackStage == 5 && s.Opponent.AttackStage == 6, "original Intimidate only");
        else Check(r.Events.OfType<WildBattlePermanentWeatherChangedEvent>().Count() == 1, "one original weather callback");
    }
});
Test("Forecast immediate weather scripts, exact form/types and round trips", () => {
    foreach (var (move, type, form) in new[] { ("RAIN_DANCE", "TYPE_WATER", SourceCastformForm.Rainy),
        ("SUNNY_DAY", "TYPE_FIRE", SourceCastformForm.Sunny), ("HAIL", "TYPE_ICE", SourceCastformForm.Snowy) }) {
        var s = Start(Mon("CASTFORM", [move, "PROTECT"]), Mon("MAGIKARP", ["SPLASH"]));
        var r = Turn(s); Trace(s, r); s = Roundtrip(r);
        Check(WildBattleEffective.Types(catalog, s.Player) == (type, type), "weather types " + move);
        Check(s.Player.CalledOverlay!.CastformForm == form && s.Player.Pokemon.Species.Value == "pret:SPECIES_CASTFORM", "battle form " + move);
        Check(r.Events.OfType<WildBattleAbilityActivatedEvent>().Count(e => e.Ability == "ABILITY_FORECAST") == 1, "one weather form event");
        for (int i = 0; i < 4; i++) { var next = Turn(s, 1); Trace(s, next); s = Roundtrip(next); }
        Check(WildBattleEffective.Types(catalog, s.Player) == ("TYPE_NORMAL", "TYPE_NORMAL") && s.Player.CalledOverlay?.CastformForm == SourceCastformForm.Normal, "expiry restores at fainted-action phase");
    }
});
Test("Forecast weather entry in both speed orders", () => {
    foreach (bool reverse in new[] { false, true }) foreach (var (name, type, form) in new[] {
        ("KYOGRE", "TYPE_WATER", SourceCastformForm.Rainy), ("GROUDON", "TYPE_FIRE", SourceCastformForm.Sunny),
        ("TYRANITAR", "TYPE_NORMAL", SourceCastformForm.Normal) }) {
        var cast = Mon("CASTFORM", ["PROTECT"], level: reverse ? 100 : 10); var weather = Mon(name, ["PROTECT"]);
        var r = Entry(cast, weather); var s = Roundtrip(r);
        Check(WildBattleEffective.Types(catalog, s.Player) == (type, type), "entry type " + name);
        Check((s.Player.CalledOverlay?.CastformForm ?? SourceCastformForm.Normal) == form, "entry form " + name);
    }
});
Test("suppression entry/departure, retained weather and form restoration", () => {
    var cast = Mon("CASTFORM", ["RAIN_DANCE", "PROTECT"]); var duck = Mon("PSYDUCK", ["PROTECT"], pid: 1);
    var s = Start(duck, cast, 0, Mon("MAGIKARP", ["SPLASH"]));
    var rain = Action(s, O, 0); s = Roundtrip(rain);
    Check(s.RainTurnsRemaining == 5 && s.Opponent.CalledOverlay?.CastformForm is null, "suppression keeps normal but stores rain");
    var left = WildBattleRules.ResolveCalledPartySwitch(catalog, s, new(P, 1)); s = Require(left, "suppressor leaves");
    Check(s.Opponent.CalledOverlay?.CastformForm == SourceCastformForm.Rainy, "departure enables forecast"); Roundtrip(left);
    var back = WildBattleRules.ResolveCalledPartySwitch(catalog, s, new(P, 0)); s = Require(back, "suppressor returns");
    Check(s.Opponent.CalledOverlay?.CastformForm == SourceCastformForm.Normal && s.RainTurnsRemaining == 5, "entry normalizes without clearing weather"); Roundtrip(back);
});
Test("ability loss retains form; reacquisition queries Forecast; acquired Trace is unarmed", () => {
    var s = Start(Mon("ALAKAZAM", ["SKILL_SWAP", "PROTECT"]), Mon("CASTFORM", ["RAIN_DANCE", "TACKLE"]));
    s = Require(Action(s, O), "rain"); Check(s.Opponent.CalledOverlay?.CastformForm == SourceCastformForm.Rainy, "rain form before swap");
    s = Require(Action(s), "swap Forecast away");
    Check(WildBattleEffective.Ability(s.Opponent) == "ABILITY_SYNCHRONIZE" && s.Opponent.CalledOverlay?.CastformForm == SourceCastformForm.Rainy, "loss retains rainy form");
    s = s with { RainTurnsRemaining = 0 }; var retained = Action(s, O, 1); s = Roundtrip(retained);
    Check(WildBattleEffective.Types(catalog, s.Opponent) == ("TYPE_WATER", "TYPE_WATER"), "loss retains types after expiry");
    var restored = Action(s); s = Roundtrip(restored);
    Check(WildBattleEffective.Ability(s.Opponent) == "ABILITY_FORECAST" && s.Opponent.CalledOverlay?.CastformForm == SourceCastformForm.Normal, "reacquired Forecast changes normal at phase");
    // Porygon has already copied Synchronize. Use a real Trace-vs-Trace entry to retain the donor's Trace.
    var donor = Start(Mon("PORYGON", ["PROTECT"]), Mon("PORYGON2", ["PROTECT"]), 0, Mon("KADABRA", ["ROLE_PLAY"]));
    donor = Require(WildBattleRules.ResolveCalledPartySwitch(catalog, donor, new(P, 1)), "Kadabra replacement");
    var gained = Action(donor); s = Roundtrip(gained);
    Check(WildBattleEffective.Ability(s.Player) == "ABILITY_TRACE" && !s.Player.SourceTracePending, "Role Play Trace not armed");
    Check(!gained.Events.OfType<WildBattleAbilityActivatedEvent>().Any(e => e.Ability == "ABILITY_TRACE"), "no copy chain after Role Play");
});
Test("Transform copies form and battle ability without rewriting PID/permanent identity", () => {
    var ditto = Mon("DITTO", ["TRANSFORM"], pid: 456); var cast = Mon("CASTFORM", ["RAIN_DANCE", "PROTECT"], pid: 789);
    var s = Start(ditto, cast); s = Require(Action(s, O), "rain before transform");
    var transformed = Action(s); s = Roundtrip(transformed);
    Check(s.Player.Transform is not null && s.Player.CalledOverlay?.CastformForm == SourceCastformForm.Rainy, "source form copied");
    Check(s.Player.Pokemon.Personality == ditto.Pokemon.Personality && s.Player.Transform!.CopiedPokemon.Personality == cast.Pokemon.Personality, "own PID/donor appearance PID");
    Check(WildBattleTransformationRules.PersistentPokemon(s.Player).Species == ditto.Pokemon.Species && WildBattleTransformationRules.PersistentPokemon(s.Player).AbilityId == ditto.Pokemon.AbilityId, "permanent Ditto identity");
    s = s with { RainTurnsRemaining = 0 }; var normal = Action(s, P, 1); s = Roundtrip(normal);
    Check(s.Player.CalledOverlay?.CastformForm == SourceCastformForm.Normal && s.Opponent.CalledOverlay?.CastformForm == SourceCastformForm.Normal, "transformed Forecast follows weather");
});
Test("Trace re-entry resets copied ability and re-copies", () => {
    var s = Start(Mon("PORYGON", ["PROTECT"]), Mon("PIKACHU", ["GROWL"]), 0, Mon("MAGIKARP", ["SPLASH"]));
    s = Require(WildBattleRules.ResolveCalledPartySwitch(catalog, s, new(P, 1)), "leave");
    var r = WildBattleRules.ResolveCalledPartySwitch(catalog, s, new(P, 0)); s = Roundtrip(r); Require(r, "return");
    Check(WildBattleEffective.Ability(s.Player) == "ABILITY_STATIC" && s.Player.Pokemon.AbilityId == "ABILITY_TRACE", "new entry copies native donor again");
    Check(r.Events.OfType<WildBattleAbilityActivatedEvent>().Count(e => e.Ability == "ABILITY_TRACE") == 1, "exactly one reentry event");
});
Test("malformed form, legacy register and overflow are atomic failures", () => {
    var s = Start(Mon("CASTFORM", ["RAIN_DANCE"]), Mon("MAGIKARP", ["SPLASH"]));
    Atomic(s with { Player = s.Player with { CalledOverlay = new() { Type1 = "TYPE_WATER", Type2 = "TYPE_WATER", CastformForm = (SourceCastformForm)99 } } }, "unknown form");
    Atomic(s with { Player = s.Player with { CalledOverlay = new() { CastformForm = SourceCastformForm.Rainy } } }, "form missing types");
    Atomic(s with { SourceMechanicsRulesVersion = null, Player = s.Player with { SourceTracePending = true } }, "legacy pending");
    Atomic(s with { Player = s.Player with { SourceTracePending = true } }, "non-Trace identity cannot arm pending bit");
    Atomic(s with { EventSequence = long.MaxValue }, "event overflow");
    var frame = SinglesBattleAdapter.Import(catalog, s).Frame!;
    var malformed = frame with { SourceMechanicsRulesVersion = null, Slots = frame.Slots.SetItem(0, frame.Slots[0] with { Body = frame.Slots[0].Body! with { SourceTracePending = true } }) };
    Check(SourceBattleIdentityRules.Validate(malformed) == "source_trace_forecast_profile", "M01 rejects historical slot extension");
    Check(!CanonicalJson.Text(Mon("MAGIKARP", ["SPLASH"])).Contains("sourceTracePending"), "default omission");
});
Test("Castform form remains independent of types after ability loss and real Mimic Conversion", () => {
    var s = Start(Mon("ALAKAZAM", ["SKILL_SWAP"]), Mon("CASTFORM", ["RAIN_DANCE", "MIMIC", "PROTECT"]), 0, Mon("PORYGON", ["CONVERSION"]));
    s = Require(Action(s, O), "rain before losing Forecast");
    s = Require(Action(s), "lose Forecast by Skill Swap");
    s = Require(WildBattleRules.ResolveCalledPartySwitch(catalog, s, new(P, 1)), "real Conversion donor enters");
    s = Require(Action(s), "donor uses Conversion");
    s = Require(Action(s, O, 1), "Mimic Conversion");
    Check(s.Opponent.Pokemon.Moves[1].Move == Move("CONVERSION"), "real move copied");
    var converted = Action(s, O, 1); s = Roundtrip(converted);
    Check(WildBattleEffective.Types(catalog, s.Opponent) == ("TYPE_NORMAL", "TYPE_NORMAL"), "Conversion changes types");
    Check(s.Opponent.CalledOverlay?.CastformForm == SourceCastformForm.Rainy, "visual form remains rainy independently");
});
Test("canonical source positions honor retained absent donors but full-turn gate remains closed", () => {
    var p = Mon("PORYGON", ["TACKLE"]); var cast = Mon("CASTFORM", ["TACKLE"]);
    var a = Mon("PIKACHU", ["GROWL"]); var b = Mon("PSYDUCK", ["SCRATCH"], pid: 1);
    WildBattleCalledPartyMember Member(WildBattleCombatant m) => new(m.Pokemon);
    var frame = new SourceBattleFrame { Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
        BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(0), SourceMechanicsRulesVersion = SourceBattleKernel.Version,
        Parties = [new(BattleSide.Player, [Member(p), Member(cast)]), new(BattleSide.Opponent, [Member(a), Member(b)])],
        Slots = [new(new(0), new(BattleSide.Player, 0), 1, p), new(new(1), new(BattleSide.Opponent, 0), 1, a),
            new(new(2), new(BattleSide.Player, 1), 1, cast), new(new(3), new(BattleSide.Opponent, 1), 1, b)], SourceAbsentMask = 8 };
    Check(SourceBattleIdentityRules.Validate(frame) is null, "authentic canonical four-slot fixture");
    int calls = 0; var donor = SourceTraceForecastRules.TraceTarget(frame, new(0), () => { calls++; return 1; });
    Check(donor?.Value == 3 && calls == 1, "source query includes retained living absent body");
    var denied = SourceBattleKernel.ResolveAction(catalog, frame, new(frame.Slots[0].Occupant!.Value, 0, 0, new(1)));
    Check(!denied.Succeeded && ReferenceEquals(denied.State, frame) && denied.Trace.IsEmpty && denied.Events.IsEmpty && denied.Fault == "source_kernel_ability_not_migrated", "no four-action support inferred");
});
Test("Spikes faint blocks incoming ability; real replacement resumes entry and full turn", () => {
    foreach (var species in new[] { "PORYGON", "CASTFORM" }) {
        var living = Mon(species, ["PROTECT"]); var dying = living with { Pokemon = living.Pokemon with { CurrentHp = 1 } };
        var s = Start(Mon("KYOGRE", ["CALM_MIND"]), Mon("MAGIKARP", ["SPLASH"]), 0, dying, living);
        s = s with { CalledParty = s.CalledParty! with { PlayerSpikes = 1 } };
        var fainted = WildBattleRules.SwitchSourceWildPlayer(catalog, s, 1); Require(fainted, "Spikes faint"); Trace(s, fainted); s = Roundtrip(fainted);
        Check(s.AwaitingReplacement && s.Player.Pokemon.CurrentHp == 0 && !s.Player.SourceTracePending, "real paused replacement and no pending Trace");
        Check(!fainted.Events.OfType<WildBattleAbilityActivatedEvent>().Any(e => e.Side == P), "fainted entry never activates");
        var replaced = WildBattleRules.ReplaceSourceWildPlayer(catalog, s, 2); Require(replaced, "replacement resume"); Trace(s, replaced); s = Roundtrip(replaced);
        Check(!s.AwaitingReplacement && s.Turn == 1, "same owner reaches readiness");
        if (species == "PORYGON") Check(WildBattleEffective.Ability(s.Player) == "ABILITY_SWIFT_SWIM", "replacement Trace copies opponent");
        else Check(s.Player.CalledOverlay?.CastformForm == SourceCastformForm.Rainy, "replacement Forecast sees retained rain");
    }
});
Test("entry Trace preserves preexisting status until the source immunity stage", () => {
    foreach (var (species, move, pid, status, copied) in new[] {
        ("DROWZEE", "POUND", 0u, 2u, "ABILITY_INSOMNIA"),
        ("MANKEY", "LEER", 0u, 2u, "ABILITY_VITAL_SPIRIT"),
        ("GOLDEEN", "TAIL_WHIP", 1u, WildBattleStatus.Burn, "ABILITY_WATER_VEIL") }) {
        var p = Mon("PORYGON", ["PROTECT"]) with { Status = status };
        var r = Entry(p, Mon(species, [move], pid: pid)); var s = Roundtrip(r);
        Check(WildBattleEffective.Ability(s.Player) == copied && s.Player.Status == status, "entry copy keeps original status until move end");
        var turn = Turn(s); Trace(s, turn); s = Roundtrip(turn);
        Check(s.Player.Status == 0 && s.CalledParty!.Player[0].Status == 0, "shared source immunity cures active and permanent party status");
    }
});
Test("pending Trace has one scalar slot authority and Transform does not copy donor pending bit", () => {
    var s = Start(Mon("DITTO", ["TRANSFORM"]), Mon("PORYGON", ["TACKLE"]));
    // Explicit source pause between arm and resolution: authenticate original Porygon identity,
    // preserve its existing slot, and remove the completed copy overlay before replaying that phase.
    s = s with { Opponent = s.Opponent with { CalledOverlay = null, SourceTracePending = true } };
    Check(WildBattleRules.Validate(catalog, s) is null, "pending native Trace register validates");
    var imported = SinglesBattleAdapter.Import(catalog, s); Check(imported.Succeeded, "pending import");
    Check(imported.Frame!.Slots[1].Body!.SourceTracePending && !imported.Frame.Slots[0].Body!.SourceTracePending, "bit belongs only to exact source occupant");
    var restored = SinglesBattleAdapter.Export(JsonSerializer.Deserialize<SourceBattleFrame>(CanonicalJson.Text(imported.Frame))!);
    Check(restored.Succeeded && CanonicalJson.Text(restored.State) == CanonicalJson.Text(s), "pending JSON/frame lossless");
    var transformed = Action(restored.State!); s = Roundtrip(transformed);
    Check(s.Player.Transform is not null && WildBattleEffective.Ability(s.Player) == "ABILITY_TRACE" && !s.Player.SourceTracePending, "Transform copies ability but not donor pending flag");
    Check(transformed.Events.OfType<WildBattleAbilityActivatedEvent>().All(e => e.Side != P || e.Ability != "ABILITY_TRACE"), "no phantom actor Trace activation");
});
Console.WriteLine($"RESULT groups={groups} checks={checks} turns={turns} failures={failures} runtime={Environment.Version}");
if (groups < 14 || checks < 3000) throw new Exception("test suite incomplete");
return failures == 0 ? 0 : 1;
