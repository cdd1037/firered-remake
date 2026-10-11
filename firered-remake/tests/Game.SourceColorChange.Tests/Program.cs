using System.Collections.Immutable;
using System.Text.Json;
using System.Reflection;
using System.Diagnostics;
using Game.Domain.Rules;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length != 3) return 2;
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(Path.GetFullPath(args[0]), Path.GetFullPath(args[1]));
var catalog = proof.Catalog;
int groups = 0, checks = 0, failures = 0;
var evidence = new List<object>();
const WildBattleSide P = WildBattleSide.Player, O = WildBattleSide.Opponent;
const string Ability = "ABILITY_COLOR_CHANGE";
MoveId Id(string name) => new("pret:MOVE_" + name);
void Check(bool yes, string reason) { checks++; if (!yes) throw new InvalidDataException(reason); }
void Test(string name, Action work) { groups++; try { work(); Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " " + e); } }
PokemonInstance Mon(string name, int level = 30, uint pid = 0, IvPolicy? ivs = null, uint seed = 0)
{
    var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + name), level, 42, ivs ?? IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(seed)).Pokemon;
    Check(SourcePokemonValidator.ValidatePermanent(proof, p) is null, "authentic permanent identity " + name); return p;
}
WildBattleCombatant Get(WildBattleState s, WildBattleSide side) => side == P ? s.Player : s.Opponent;
WildBattleSide Other(WildBattleSide side) => side == P ? O : P;
WildBattleState Pair(PokemonInstance actor, PokemonInstance holder, uint seed = 0, WildBattleSide side = P) =>
    new(new(side == P ? actor : holder) { FirstTurnCounter = 1 }, new(side == P ? holder : actor) { FirstTurnCounter = 1 },
        0, 0, 0, new(seed), 0, WildBattleOutcome.Ongoing, RulesVersion: WildBattleRules.FuchsiaRulesVersion)
    { Terrain = WildBattleTerrain.Plain, SourceMechanicsRulesVersion = BattleRulesProfile.CanonicalSourceSinglesVersion };
WildBattleState Party(WildBattleState s, params PokemonInstance[] reserve) => s with {
    CalledParty = new(new[] { s.Player.Pokemon }.Concat(reserve).Select(p => new WildBattleCalledPartyMember(p, "ITEM_NONE", 0)).ToImmutableArray(),
        [new(s.Opponent.Pokemon, "ITEM_NONE", 0)], 0, 0) };
int Slot(PokemonInstance p, string name) { int slot = Array.FindIndex(p.Moves.ToArray(), m => m.Move == Id(name)); Check(slot >= 0, p.Species + " naturally knows " + name); return slot; }
void Trace(WildBattleState before, WildBattleResult result)
{
    uint seed = before.Rng.Seed;
    foreach (var draw in result.Trace) { seed = unchecked(seed * 1103515245 + 24691); Check(draw.Raw == (ushort)(seed >> 16), "source LCG " + draw.Purpose); }
    Check(result.State!.Rng.Seed == seed && result.State.Rng.Cursor == before.Rng.Cursor + (ulong)result.Trace.Length, "terminal RNG");
    Check(!result.Trace.Any(t => t.Purpose.Contains("color-change")), "Color Change consumes no RNG");
}
WildBattleResult Act(WildBattleState s, string move, WildBattleSide side = P, bool called = false)
{
    var actor = Get(s, side);
    var result = called ? WildBattleRules.ResolveCalledMoveAction(catalog, s, new(side, Slot(actor.Pokemon, "METRONOME"), Id(move))).Battle :
        WildBattleRules.ResolveSourceMoveAction(catalog, s, new(side, Slot(actor.Pokemon, move))).Battle;
    Check(result.Succeeded, move + " accepted: " + result.Fault);
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!) is null, "valid result: " + WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!));
    Trace(s, result); return result;
}
WildBattleResult Branch(WildBattleState s, string move, Func<WildBattleResult, bool> predicate, WildBattleSide side = P, bool called = false)
{
    for (uint seed = 0; seed < 250; seed++) { var r = Act(s with { Rng = new(seed) }, move, side, called); if (predicate(r)) return r; }
    throw new InvalidDataException("No deterministic source branch: " + move);
}
WildBattleAbilityActivatedEvent[] Activations(WildBattleResult r) => r.Events.OfType<WildBattleAbilityActivatedEvent>().Where(e => e.Ability == Ability).ToArray();
void Changed(WildBattleResult r, WildBattleSide side, string type)
{
    var a = Activations(r).Single(); Check(a.Side == side, "correct holder");
    var overlay = r.Events.OfType<WildBattleCalledOverlayChangedEvent>().Single(e => e.Sequence == a.Sequence + 1);
    Check(overlay.Side == side && overlay.After?.Type1 == type && overlay.After.Type2 == type, "adjacent exact type overlay");
    Check(WildBattleEffective.Types(catalog, Get(r.State!, side)) == (type, type), "live effective type");
}
void Atomic(WildBattleState state, WildBattleResult result, string reason) => Check(!result.Succeeded && ReferenceEquals(state, result.State) && result.Events.IsEmpty && result.Trace.IsEmpty, reason + " atomic");
const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
var execution = typeof(WildBattleRules).GetNestedType("Execution", BindingFlags.NonPublic)!;
object Kernel(WildBattleState s) => execution.GetConstructors(flags).Single().Invoke([s, true, true]);
object? Call(object kernel, string method, params object?[] values) => execution.GetMethod(method, flags)!.Invoke(kernel, values);
WildBattleState State(object kernel) => (WildBattleState)execution.GetField("State", flags)!.GetValue(kernel)!;
WildBattleEvent[] Events(object kernel) => ((IEnumerable<WildBattleEvent>)execution.GetField("Events", flags)!.GetValue(kernel)!).ToArray();

Test("authentic Kecleon admitted by shared action kernel; campaign stays closed", () => {
    Check(Environment.Version.Major == 10, "actual .NET 10"); var k = Mon("KECLEON", 1); Check(k.AbilityId == Ability, "official donor");
    Check(!CampaignAdmissionPolicy.CurrentFuchsia.Admits(k.Species), "no campaign expansion");
    var s = Pair(Mon("SQUIRTLE", 13), Mon("KECLEON", 100));
    var frame = SinglesBattleAdapter.Import(catalog, Party(s)).Frame!;
    var action = new SourceResolvedMoveAction(frame.Get(new(0)).Occupant!.Value, frame.Revision, Slot(s.Player.Pokemon, "WATER_GUN"), new(1));
    var shared = SourceBattleKernel.ResolveAction(catalog, frame, action);
    Check(shared.Succeeded && shared.Events.OfType<SourceAbilityActivatedEvent>().Any(e => e.Ability == Ability), "shared kernel Color Change admitted: " + shared.Fault);
});
Test("factory-built complete turns activate as opponent and player with normal actions", () => {
    foreach (var side in new[] { P, O }) {
        bool found = false;
        for (uint seed = 0; seed < 120; seed++) {
            var player = side == O ? Mon("SQUIRTLE", 13) : Mon("KECLEON", 24);
            var foe = side == O ? Mon("KECLEON", 100) : Mon("PIKACHU", 5);
            var start = WildBattleRules.StartSourceWild(proof, BattleRulesProfile.CanonicalSourceSingles, [new(player)], 0, new(foe), new(seed));
            Check(start.Succeeded, "canonical entry " + start.Fault);
            var r = WildBattleRules.ResolveMove(catalog, start.State!, Slot(player, side == O ? "WATER_GUN" : "SCREECH"));
            Check(r.Succeeded, "whole turn " + r.Fault); Trace(start.State!, r);
            Check(WildBattleRules.ValidateSourceWild(catalog, r.State!) is null, "whole turn remains valid");
            if (!Activations(r).Any(e => e.Side == side)) continue;
            Check(r.State!.Turn > start.State!.Turn || r.State.Outcome != WildBattleOutcome.Ongoing, "turn reached boundary");
            evidence.Add(new { side, seed, r.State.Turn, outcome = r.State.Outcome.ToString(), r.State.Rng.Cursor }); found = true; break;
        }
        Check(found, "complete ordinary turn activation " + side);
    }
});
Test("noncontact and contact damage activate with identity and permanent ability intact", () => {
    foreach (var side in new[] { P, O }) foreach (var pair in new[] { ("SQUIRTLE", 13, "WATER_GUN", "TYPE_WATER"), ("PIKACHU", 5, "THUNDER_SHOCK", "TYPE_ELECTRIC"), ("MANKEY", 6, "LOW_KICK", "TYPE_FIGHTING") }) {
        var s = Pair(Mon(pair.Item1, pair.Item2), Mon("KECLEON", 100), side: side); var r = Act(s, pair.Item3, side); Changed(r, Other(side), pair.Item4);
        Check(Get(s, Other(side)).Pokemon with { CurrentHp = 1 } == Get(r.State!, Other(side)).Pokemon with { CurrentHp = 1 }, "holder permanent identity unchanged");
    }
});
Test("both existing type slots independently suppress change", () => {
    foreach (bool first in new[] { false, true }) {
        var s = Pair(Mon("SQUIRTLE", 13), Mon("KECLEON", 100)); s = s with { Opponent = s.Opponent with { CalledOverlay = new() { Type1 = first ? "TYPE_WATER" : "TYPE_FIRE", Type2 = first ? "TYPE_FIRE" : "TYPE_WATER" } } };
        var r = Act(s, "WATER_GUN"); Check(Activations(r).Length == 0 && r.State!.Opponent.CalledOverlay == s.Opponent.CalledOverlay, "either type blocks");
    }
});
Test("Protect, Substitute break, no-effect and zero HP loss cannot trigger", () => {
    foreach (int gate in new[] { 0, 1, 2, 3 }) {
        var s = Pair(Mon("CLEFAIRY", 29), Mon("KECLEON", 100));
        s = s with { Opponent = gate switch {
            0 => s.Opponent with { Protected = true }, 1 => s.Opponent with { CalledVolatile = new() { SubstituteHp = 1 } },
            2 => s.Opponent with { CalledOverlay = new() { Type1 = "TYPE_GROUND", Type2 = "TYPE_GROUND" } },
            _ => s.Opponent with { Pokemon = s.Opponent.Pokemon with { CurrentHp = 1 }, CalledOverlay = new() { Type1 = "TYPE_WATER", Type2 = "TYPE_WATER" } } } };
        var r = Act(s, gate == 3 ? "FALSE_SWIPE" : "THUNDER_SHOCK", called: true); Check(Activations(r).Length == 0, "skip gate " + gate);
    }
});
Test("holder KO skips while attacker recoil KO still changes a living holder", () => {
    var s = Pair(Mon("CLEFAIRY", 29), Mon("KECLEON", 100) with { CurrentHp = 1 }); var r = Act(s, "WATER_GUN", called: true);
    Check(r.State!.Opponent.Pokemon.CurrentHp == 0 && Activations(r).Length == 0, "fainted holder skips");
    s = Pair(Mon("CLEFAIRY", 29) with { CurrentHp = 1 }, Mon("KECLEON", 100)); s = s with { Opponent = s.Opponent with { CalledOverlay = new() { Type1 = "TYPE_WATER", Type2 = "TYPE_WATER" } } };
    r = Branch(s, "TAKE_DOWN", r => Activations(r).Length == 1, called: true); Changed(r, O, "TYPE_NORMAL");
    Check(r.State!.Player.Pokemon.CurrentHp == 0 && r.Events.OfType<WildBattleFaintedEvent>().Single(e => e.Side == P).Sequence < Activations(r).Single().Sequence, "recoil KO precedes Color Change");
});
Test("shared Explosion callback survives dead attacker and occurs before attacker faint", () => {
    foreach (var move in new[] { "SELF_DESTRUCT", "EXPLOSION" }) {
        var s = Pair(Mon("CLEFAIRY", 29), Mon("KECLEON", 100)); s = s with { Opponent = s.Opponent with { CalledOverlay = new() { Type1 = "TYPE_WATER", Type2 = "TYPE_WATER" } } };
        var r = Act(s, move, called: true); Changed(r, O, "TYPE_NORMAL");
        Check(r.Events.OfType<WildBattleSelfDestructEvent>().Single().Sequence < Activations(r).Single().Sequence && Activations(r).Single().Sequence < r.Events.OfType<WildBattleFaintedEvent>().Single(e => e.Side == P).Sequence, "source selfdestruct order");
    }
});
Test("Hidden Power uses IV type and preserves source move metadata", () => {
    var actor = Mon("CLEFAIRY", 29); var s = Pair(actor, Mon("KECLEON", 100)); var r = Act(s, "HIDDEN_POWER", called: true);
    Changed(r, O, "TYPE_DARK"); Check(catalog.Moves[Id("HIDDEN_POWER")].Type == "TYPE_NORMAL", "static metadata retained");
    Check(r.State!.Opponent.SourceMoveHistory?.LastHitByType == "TYPE_DARK" && r.State.Opponent.CalledDamage is { PhysicalDamage: > 0, SpecialDamage: 0 }, "effective type history and source Normal physical damage classification");
});
Test("Weather Ball uses each effective weather type and ignores suppressed weather", () => {
    foreach (string weather in new[] { "rain", "sun", "sand", "hail" }) {
        var s = Pair(Mon("CLEFAIRY", 29), Mon("KECLEON", 100)); s = s with { RainTurnsRemaining = weather == "rain" ? 3 : 0, SandstormTurnsRemaining = weather == "sand" ? 3 : 0, CalledField = new() { SunTurnsRemaining = weather == "sun" ? 3 : 0, HailTurnsRemaining = weather == "hail" ? 3 : 0 } };
        var r = Act(s, "WEATHER_BALL", called: true); Changed(r, O, weather switch { "rain" => "TYPE_WATER", "sun" => "TYPE_FIRE", "sand" => "TYPE_ROCK", _ => "TYPE_ICE" });
    }
    var cloud = Pair(Mon("PSYDUCK", 5, 1), Mon("KECLEON", 100));
    Check(cloud.Player.Pokemon.AbilityId == "ABILITY_CLOUD_NINE", "authentic suppressor");
    // A source move slot is a legal mechanics fixture; acquisition provenance is outside the instance schema.
    var wb = catalog.Moves[Id("WEATHER_BALL")]; cloud = cloud with { RainTurnsRemaining = 3, Player = cloud.Player with { Pokemon = cloud.Player.Pokemon with { Moves = [new(wb.Id, wb.Pp, wb.Pp)] } } };
    Check(Activations(Act(cloud, "WEATHER_BALL")).Length == 0, "suppressed weather keeps Normal");
});
Test("SonicBoom old recipe and Beat Up per-hit move end run Color Change", () => {
    var s = Pair(Mon("VOLTORB", 15), Mon("KECLEON", 100)); s = s with { Opponent = s.Opponent with { CalledOverlay = new() { Type1 = "TYPE_WATER", Type2 = "TYPE_WATER" } } };
    var r = Branch(s, "SONIC_BOOM", r => Activations(r).Length == 1); Changed(r, O, "TYPE_NORMAL");
    s = Party(Pair(Mon("CLEFAIRY", 29), Mon("KECLEON", 100)), Mon("SQUIRTLE", 13), Mon("PIDGEY", 5));
    r = Act(s, "BEAT_UP", called: true); Changed(r, O, "TYPE_DARK");
    var hits = r.Events.OfType<WildBattleHpChangedEvent>().Where(e => e.Target == O).ToArray();
    Check(hits.Length == 3 && hits[0].Sequence < Activations(r).Single().Sequence && Activations(r).Single().Sequence < hits[1].Sequence, "first Beat Up hit changes before later contributors");
});
Test("multi-hit changes once between hits with new type affecting later damage", () => {
    var s = Pair(Mon("CLEFAIRY", 29), Mon("KECLEON", 100)); var r = Act(s, "DOUBLE_KICK", called: true); Changed(r, O, "TYPE_FIGHTING");
    var hits = r.Events.OfType<WildBattleHpChangedEvent>().Where(e => e.Target == O).ToArray();
    Check(hits.Length == 2 && hits[0].Sequence < Activations(r).Single().Sequence && Activations(r).Single().Sequence < hits[1].Sequence, "per-hit phase");
    Check(hits[0].Damage > hits[1].Damage, "first super effective then neutral damage");
});
Test("Pursuit interception has exact receipt without ordinary invocation", () => {
    var s = Pair(Mon("DODUO", 9), Mon("KECLEON", 100)); var kernel = Kernel(s);
    Check((bool)Call(kernel, "TryPursuitSwitch", catalog, O, Slot(s.Player.Pokemon, "PURSUIT"))!, "actual interception");
    Check(execution.GetField("activeInvocation", flags)!.GetValue(kernel) is null, "abbreviated script has no ordinary invocation");
    Check(WildBattleEffective.Types(catalog, State(kernel).Opponent) == ("TYPE_DARK", "TYPE_DARK"), "Pursuit changes type");
    Check(Events(kernel).OfType<WildBattleAbilityActivatedEvent>().Count(e => e.Ability == Ability) == 1, "one activation");
});
Test("acquired Role Play and Skill Swap use live ability and preserve permanent identity", () => {
    foreach (string move in new[] { "ROLE_PLAY", "SKILL_SWAP" }) {
        var s = Pair(Mon("CLEFAIRY", 29), Mon("KECLEON", 100)); s = s with { Opponent = new(Mon("KECLEON", 30)) { FirstTurnCounter = 1 } }; var acquired = Act(s, move, called: true).State!;
        Check(WildBattleEffective.Ability(acquired.Player) == Ability && acquired.Player.Pokemon.AbilityId == s.Player.Pokemon.AbilityId, "acquired overlay " + move);
        // Authentic Kecleon's naturally known Psybeam is a noncontact hit on the acquired holder.
        var hit = Act(acquired, "PSYBEAM", O); Changed(hit, P, "TYPE_PSYCHIC");
        Check(hit.State!.Player.CalledOverlay!.Ability == Ability && hit.State.Player.Pokemon.AbilityId == s.Player.Pokemon.AbilityId, "type write preserves acquired ability");
    }
});
Test("Transform inherits Color Change with original permanent identity retained", () => {
    var s = Pair(Mon("CLEFAIRY", 29), Mon("KECLEON", 30)); var transformed = Act(s, "TRANSFORM", called: true).State!;
    Check(transformed.Player.Transform is not null && WildBattleEffective.Ability(transformed.Player) == Ability, "actual transformed ability");
    var hit = Act(transformed, "PSYBEAM", O); Changed(hit, P, "TYPE_PSYCHIC");
    Check(WildBattleTransformationRules.PersistentPokemon(hit.State!.Player).Species == s.Player.Pokemon.Species && WildBattleTransformationRules.PersistentPokemon(hit.State.Player).AbilityId == s.Player.Pokemon.AbilityId, "source permanent species and ability retained");
});
Test("type overlay and acquired ability clear on real party switch", () => {
    var s = Party(Pair(Mon("CLEFAIRY", 29), Mon("KECLEON", 30)), Mon("SQUIRTLE", 13)); s = Act(s, "ROLE_PLAY", called: true).State!; s = Act(s, "PSYBEAM", O).State!;
    var away = WildBattleRules.ResolveCalledPartySwitch(catalog, s, new(P, 1)); Check(away.Succeeded, "switch away " + away.Fault);
    var back = WildBattleRules.ResolveCalledPartySwitch(catalog, away.State!, new(P, 0)); Check(back.Succeeded, "switch back " + back.Fault);
    Check(back.State!.Player.CalledOverlay is null && back.State.Player.Pokemon.AbilityId == "ABILITY_CUTE_CHARM", "battle overlay not stored");
    Check(back.State.CalledParty!.Player[0].Pokemon.AbilityId == "ABILITY_CUTE_CHARM", "party permanent ability");
});
Test("delayed Future Sight resolves HP without ordinary damage abilities", () => {
    var s = Pair(Mon("CLEFAIRY", 29), Mon("KECLEON", 100)); s = Act(s, "FUTURE_SIGHT", called: true).State!;
    var kernel = Kernel(s);
    for (int i = 0; i < 3; i++) Call(kernel, "SourceCalledFutureResidual", catalog, O);
    Check(State(kernel).Opponent.Pokemon.CurrentHp < s.Opponent.Pokemon.CurrentHp, "actual delayed damage resolved");
    Check(!Events(kernel).OfType<WildBattleAbilityActivatedEvent>().Any(e => e.Ability == Ability), "source delayed script skips Color Change");
});
Test("real Future Sight receipt makes zero-loss Focus Band special hit activate Color Change", () => {
    var s = Pair(Mon("CLEFAIRY", 29), Mon("KECLEON", 100));
    s = s with {Opponent = s.Opponent with {HeldItem="ITEM_FOCUS_BAND"}};
    s = Act(s, "FUTURE_SIGHT", called: true).State!;
    var fullHp = Kernel(s);
    for(int i=0;i<3;i++) Call(fullHp,"SourceCalledFutureResidual",catalog,O);
    int delayed = s.Opponent.Pokemon.CurrentHp-State(fullHp).Opponent.Pokemon.CurrentHp;
    Check(delayed>0,"actual delayed damage reference");
    s = s with {Opponent=s.Opponent with {Pokemon=s.Opponent.Pokemon with {CurrentHp=delayed+1}}};
    var kernel = Kernel(s);
    for (int i=0;i<3;i++) Call(kernel,"SourceCalledFutureResidual",catalog,O);
    var ready = State(kernel);
    Check(ready.Opponent.Pokemon.CurrentHp == 1 && ready.SourceOpponentHitReceipt is {Physical:>0},"actual delayed command leaves one HP and physical receipt");
    Check(!Events(kernel).OfType<WildBattleAbilityActivatedEvent>().Any(e=>e.Ability==Ability),"delayed script has no Color Change");
    var result = Branch(ready,"WATER_GUN", r=>r.Events.OfType<WildBattleHpChangedEvent>().Any(e=>e.Target==O && e.HpBefore==1 && e.HpAfter==1),called:true);
    Changed(result,O,"TYPE_WATER");
    Check(result.Events.OfType<WildBattleDamageAdjustedEvent>().Any(e=>e.HungOn && e.DamageAfter==0),"actual Focus Band zero-damage command");
    Check(result.State!.SourceOpponentHitReceipt is null,"ordinary action then clears receipts");
    evidence.Add(new {naturalSequence="Future Sight damage -> retained physical receipt -> Focus Band special hit at one HP -> Color Change",priorHp=ready.Opponent.Pokemon.CurrentHp,priorPhysical=ready.SourceOpponentHitReceipt!.Physical,actualHpLost=0});
});
Test("Struggle and source zero-power moves never change type", () => {
    var s = Pair(Mon("CLEFAIRY", 29), Mon("KECLEON", 100)); s = s with { Opponent = s.Opponent with { CalledOverlay = new() { Type1 = "TYPE_WATER", Type2 = "TYPE_WATER" } } };
    foreach (string move in new[] { "SPLASH", "PAIN_SPLIT" }) {
        var r = Act(s, move, called: true); Check(Activations(r).Length == 0, "zero metadata power " + move);
    }
    var exhausted = s with { Player = s.Player with { Pokemon = s.Player.Pokemon with { Moves = s.Player.Pokemon.Moves.Select(m => m with { CurrentPp = 0 }).ToImmutableArray() } } };
    var start = WildBattleRules.StartSourceWild(proof, BattleRulesProfile.CanonicalSourceSingles, [new(exhausted.Player.Pokemon)], 0, new(exhausted.Opponent.Pokemon), new(0));
    Check(start.Succeeded, "exhausted source entry");
    var before = start.State! with { Opponent = start.State!.Opponent with { CalledOverlay = exhausted.Opponent.CalledOverlay } };
    var struggle = WildBattleRules.ResolveMove(catalog, before, -1);
    Check(struggle.Succeeded && struggle.Events.OfType<WildBattleHpChangedEvent>().Any(e => e.Move == Id("STRUGGLE")) && Activations(struggle).Length == 0, "real whole-turn Struggle excludes Color Change: " + struggle.Fault);
    Trace(before, struggle);
});
Test("no retained receipt, wrong profile and delayed guard cannot mutate", () => {
    var s = Pair(Mon("SQUIRTLE", 13), Mon("KECLEON", 100)); var move = catalog.Moves[Id("WATER_GUN")];
    foreach (string gate in new[] { "none", "zero", "profile", "delayed", "forged" }) {
        var input = s with {SourceOpponentHitReceipt = gate is "none" or "zero" ? null : new(1,1)};
        var kernel = Kernel(gate == "profile" ? input with { SourceMechanicsRulesVersion = null } : input);
        if (gate != "none") execution.GetField("sourceContactDamage", flags)!.SetValue(kernel, (gate == "actor" ? O : P, gate == "target" ? P : O, gate == "move" ? Id("TACKLE") : move.Id, gate == "zero" ? 0 : 1));
        if (gate == "delayed") execution.GetField("calledPassiveDelayed", flags)!.SetValue(kernel, true);
        Call(kernel, "SourceColorChange", catalog, P, O, gate == "forged" ? move with { Power = move.Power + 1 } : move);
        Check(Events(kernel).Length == 0 && State(kernel).Opponent.CalledOverlay is null, "gate " + gate);
    }
});
Test("presentation emits pinned text once for adjacent holder pair", () => {
    var r = Act(Pair(Mon("SQUIRTLE", 13), Mon("KECLEON", 100)), "WATER_GUN");
    var activation = Activations(r).Single(); int index = r.Events.ToList().FindIndex(e => e == activation);
    var display = SourceTrainerPresentationModel.Describe(r.Events, index, s => s.ToString(), m => m.Value, (_, i) => i.ToString())!;
    Check(display.Texts.Single().Symbol == "sText_PkmnChangedTypeWith" && display.Texts.Single().Values.Contains(("B_BUFF1", "WATER")), "pinned ability text and type");
    Check(SourceTrainerPresentationModel.Describe(r.Events, index + 1, s => s.ToString(), m => m.Value, (_, i) => i.ToString())!.Silent, "paired overlay silent");
    WildBattleEvent[] orphan = [activation]; Check(SourceTrainerPresentationModel.Describe(orphan, 0, s => s.ToString(), m => m.Value, (_, i) => i.ToString())!.Unsupported, "orphan fails closed");
    foreach (var e in r.Events.Where(e => e is WildBattleAbilityActivatedEvent or WildBattleCalledOverlayChangedEvent)) Check(JsonSerializer.Deserialize<WildBattleEvent>(JsonSerializer.Serialize(e, typeof(WildBattleEvent))) == e, "typed event JSON roundtrip");
});
Test("forged permanent donor and altered move reject atomically", () => {
    var s = Pair(Mon("SQUIRTLE", 13), Mon("KECLEON", 100));
    var forged = s with { Opponent = s.Opponent with { Pokemon = Mon("RATTATA", 30) with { AbilityId = Ability } } };
    Atomic(forged, WildBattleRules.ResolveSourceMoveAction(catalog, forged, new(P, Slot(s.Player.Pokemon, "WATER_GUN"))).Battle, "forged donor");
    var changed = catalog with { Moves = catalog.Moves.SetItem(Id("WATER_GUN"), catalog.Moves[Id("WATER_GUN")] with { Type = "TYPE_FIRE" }) };
    Atomic(s, WildBattleRules.ResolveSourceMoveAction(changed, s, new(P, Slot(s.Player.Pokemon, "WATER_GUN"))).Battle, "altered authenticated move");
});
Test("unchanged original C branch matches live executor across type pairs and guard vectors", () => {
    var actor = Mon("CLEFAIRY", 29); var holder = Mon("KECLEON", 100);
    var types = Enum.GetValues<ElementType>().Where(t => t != ElementType.Mystery).ToArray();
    var rows = new List<(PokemonMoveDefinition Move, ElementType First, ElementType Second, bool NoEffect, bool Damaged, int Hp, int AttackerHp)>();
    foreach (var type in types) {
        var move = catalog.Moves.Values.Where(m => m.Type == "TYPE_" + type.ToString().ToUpperInvariant() && m.Power > 0 && m.Id != Id("STRUGGLE")).OrderBy(m => m.SourceMoveId).First();
        foreach (var first in types) foreach (var second in new[] { ElementType.Normal, type })
            rows.Add((move, first, second, false, true, holder.CurrentHp, actor.CurrentHp));
    }
    foreach (string move in new[] { "WATER_GUN", "SPLASH", "STRUGGLE", "SUPER_FANG" })
        foreach (bool noEffect in new[] { false, true }) foreach (bool damaged in new[] { false, true })
            foreach (int hp in new[] { 0, holder.CurrentHp }) foreach (int attackerHp in new[] { 0, actor.CurrentHp })
                rows.Add((catalog.Moves[Id(move)], ElementType.Fire, ElementType.Grass, noEffect, damaged, hp, attackerHp));
    using var process = Process.Start(new ProcessStartInfo(args[2]) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false })!;
    var output = process.StandardOutput.ReadToEndAsync();
    foreach (var row in rows) {
        int type = (int)Enum.Parse<ElementType>(row.Move.Type[5..], true);
        process.StandardInput.WriteLine($"{type} {(int)row.First} {(int)row.Second} {(row.NoEffect ? 1 : 0)} {(row.Move.Id == Id("STRUGGLE") ? 165 : 1)} {row.Move.Power} {(row.Damaged ? 1 : 0)} {row.Hp}");
    }
    process.StandardInput.Close(); process.WaitForExit(); var lines = output.GetAwaiter().GetResult().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    Check(process.ExitCode == 0 && lines.Length == rows.Count && lines.Length >= 600, "nonempty exact original C vector count");
    for (int i = 0; i < rows.Count; i++) {
        var row = rows[i]; var expected = lines[i].Split(' ').Select(int.Parse).ToArray();
        var s = Pair(actor with { CurrentHp = row.AttackerHp }, holder with { CurrentHp = row.Hp }) with {SourceOpponentHitReceipt = row.Damaged ? new(1,1) : null};
        s = s with { Opponent = s.Opponent with { CalledOverlay = new() { Type1 = "TYPE_" + row.First.ToString().ToUpperInvariant(), Type2 = "TYPE_" + row.Second.ToString().ToUpperInvariant() } } };
        var kernel = Kernel(s);
        execution.GetField("sourceContactDamage", flags)!.SetValue(kernel, (P, O, row.Move.Id, row.Damaged ? 1 : 0));
        execution.GetField("activeInvocation", flags)!.SetValue(kernel, new WildBattleMoveInvocation(P, 0, 0, row.Move.Id, row.Move.Id, row.Move.Id, null, O, WildBattleAttackCancellerPhase.ConditionsPassed, WildBattleInvocationFlags.Obeys));
        execution.GetField("invocationCommandNoEffect", flags)!.SetValue(kernel, row.NoEffect);
        Call(kernel, "SourceColorChange", catalog, P, O, row.Move);
        var result = State(kernel); var actual = WildBattleEffective.Types(catalog, result.Opponent);
        Check(Events(kernel).OfType<WildBattleAbilityActivatedEvent>().Count(e => e.Ability == Ability) == expected[0], "original C activation vector " + i);
        Check(actual.Type1 == "TYPE_" + ((ElementType)expected[1]).ToString().ToUpperInvariant() && actual.Type2 == "TYPE_" + ((ElementType)expected[2]).ToString().ToUpperInvariant(), "original C resulting types vector " + i);
        Check(result.Rng == s.Rng, "original C no RNG vector " + i);
    }
    evidence.Add(new { originalCVectors = rows.Count });
});
Console.WriteLine(JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(), groups, checks, failures, evidence }, new JsonSerializerOptions { WriteIndented = true }));
return failures == 0 && groups >= 19 && checks > 0 ? 0 : 1;
