using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

if (args.Length != 1) throw new ArgumentException("Pass the project content root.");
string root = Path.GetFullPath(args[0]); int groups = 0, checks = 0, failures = 0;
void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
void Test(string title, Action work) { groups++; try { work(); Console.WriteLine("PASS " + title); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + title + " " + e); } }
MoveId Id(string name) => new("pret:MOVE_" + name);
var original = PretRocketHideoutPokemon.Load(root).Catalog;
var supplement = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules: true);
var prior = original with { Moves = original.Moves.SetItems(supplement.Moves) };
var old = PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior, root, root).Catalog, root).Catalog, root).Catalog;
var predecessor = PretFuchsiaRoutePokemon.Load(old, root).Catalog;
var catalog = PretMetronomeMoveCatalog.Load(root).Extend(predecessor);
PokemonInstance Mon(string species, int level = 40, params string[] moves)
{
    var mon = PokemonFactory.Create(catalog, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
    return moves.Length == 0 ? mon : mon with { Moves = moves.Select(name => new PokemonMoveSlot(Id(name), catalog.Moves[Id(name)].Pp, catalog.Moves[Id(name)].Pp)).ToImmutableArray() };
}
WildBattleState Pair(bool badge = true, string player = "CLEFABLE", string opponent = "RATTATA", uint seed = 7) => new(
    new(Mon(player, 60, "METRONOME")) { FirstTurnCounter = 1, DefenseBadgeBoost = badge },
    new(Mon(opponent, 40, "METRONOME")) { FirstTurnCounter = 1 },
    0, 0, 0, new(seed), 0, WildBattleOutcome.Ongoing, RulesVersion: WildBattleRules.FuchsiaRulesVersion) { Terrain = WildBattleTerrain.Plain };
WildBattleCalledPartyMember Member(WildBattleCombatant mon) => new(WildBattleTransformationRules.PersistentPokemon(mon), mon.HeldItem, WildBattleTransformationRules.PersistentStatus(mon));
WildBattleState Party(WildBattleState state) => state with { CalledParty = new(
    [Member(state.Player), new(Mon("DITTO", 60, "METRONOME"))],
    [Member(state.Opponent), new(Mon("CLEFAIRY", 40, "METRONOME"))], 0, 0, WildBattleCalledBattleKind.Trainer) };
WildBattleState Accepted(WildBattleResult result)
{
    Check(result.Succeeded, result.Fault ?? "accepted");
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!) is null, "output: " + WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!));
    return result.State!;
}
WildBattleCalledMoveResult Called(WildBattleState state, string move, WildBattleSide actor = WildBattleSide.Player)
{
    var result = WildBattleRules.ResolveCalledMoveAction(catalog, state, new(actor, 0, Id(move)));
    Accepted(result.Battle); return result;
}
WildBattleCalledMoveResult Source(WildBattleState state, WildBattleSide actor, int slot = 0)
{
    var result = WildBattleRules.ResolveSourceMoveAction(catalog, state, new(actor, slot));
    Accepted(result.Battle); return result;
}
Test("default omission, source-only admission and opponent atomic rejection", () => {
    Check(Environment.Version.Major == 10 && catalog.Species.Count == 107 && catalog.Moves.Count == 354, "runtime and bounded source catalog");
    var plain = Pair(false); var boosted = Pair();
    Check(!JsonSerializer.Serialize(plain).Contains("DefenseBadgeBoost"), "false omitted from historical serialization");
    var roundtrip = JsonSerializer.Deserialize<WildBattleState>(JsonSerializer.Serialize(boosted))!;
    Check(roundtrip.Player.DefenseBadgeBoost && !roundtrip.Opponent.DefenseBadgeBoost, "explicit flag roundtrip");
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, boosted) is null, "source state accepts player flag");
    Check(WildBattleRules.Validate(predecessor, boosted) == "wild_battle_player_defense_badge_unsupported", "107/244 predecessor rejects flag");
    var bad = plain with { Opponent = plain.Opponent with { DefenseBadgeBoost = true } };
    var rejected = WildBattleRules.ResolveCalledMoveAction(catalog, bad, new(WildBattleSide.Player, 0, Id("TACKLE")));
    Check(!rejected.Succeeded && rejected.Fault == "wild_battle_opponent_defense_badge_unsupported" && ReferenceEquals(bad, rejected.State) && rejected.Battle.Trace.IsEmpty && rejected.Battle.Events.IsEmpty, "opponent rejected without mutation or RNG");
});
Test("2688 unchanged pinned C vectors cover integer edges, stages, critical, burn, Reflect, Metal Powder and Explosion", () => {
    using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "source-vectors.json")));
    Check(document.RootElement.GetProperty("sourceCommit").GetString() == PokemonFactory.SourceCommit, "source pin");
    var vectors = document.RootElement.GetProperty("vectors"); Check(vectors.GetArrayLength() == 2688, "nonempty complete vector count");
    foreach (var v in vectors.EnumerateArray())
    {
        int defense = v[0].GetInt32(), stage = v[2].GetInt32();
        bool explosion = v[1].GetInt32() == 1, critical = v[3].GetInt32() == 1, burn = v[4].GetInt32() == 1,
            reflect = v[5].GetInt32() == 1, metal = v[6].GetInt32() == 1, badge = v[7].GetInt32() == 1, special = v[8].GetInt32() == 1;
        var actor = new WildBattleCombatant(Mon("CLEFABLE", 43)) { Status = burn ? WildBattleStatus.Burn : 0 };
        actor = actor with { Pokemon = actor.Pokemon with { Stats = actor.Pokemon.Stats with { Attack = 157, SpecialAttack = 157 } } };
        var target = new WildBattleCombatant(Mon(metal ? "DITTO" : "RATTATA")) { DefenseStage = stage, SpecialDefenseStageDelta = stage - 6,
            HeldItem = metal ? "ITEM_METAL_POWDER" : "ITEM_NONE", DefenseBadgeBoost = badge };
        target = target with { Pokemon = target.Pokemon with { Stats = target.Pokemon.Stats with { Defense = defense, SpecialDefense = defense } } };
        var move = Id(special ? explosion ? "PSYCHIC" : "EMBER" : explosion ? "SELF_DESTRUCT" : "TACKLE");
        var result = WildBattleDamage.Calculate(catalog, actor, target, move, critical, 100, WildBattleRules.FuchsiaRulesVersion, reflect: reflect);
        Check(result.Steps.Single(x => x.Operation == "plus-two").Value == v[9].GetInt32(), "source base " + v);
        Check(result.Steps.Any(x => x.Operation == "defense-badge") == (badge && !special), "physical-only badge " + v);
        if (badge && !special && metal)
            Check(result.Steps.Single(x => x.Operation == "metal-powder").Value == (defense * 110 / 100) * 2, "badge truncates before powder");
    }
});
Test("Transform retains owner badge, copies raw Defense and never lends badge to opponent", () => {
    var state = Pair(player: "DITTO", opponent: "CLEFABLE");
    state = state with { Player = state.Player with { HeldItem = "ITEM_METAL_POWDER" } };
    var transformed = Called(state, "TRANSFORM").State!;
    Check(transformed.Player.Transform is not null && transformed.Player.DefenseBadgeBoost, "player badge survives Transform");
    Check(transformed.Player.Pokemon.Stats.Defense == state.Opponent.Pokemon.Stats.Defense, "raw copied defense never stores badge bonus");
    Check(WildBattleTransformationRules.Restore(transformed.Player).DefenseBadgeBoost, "badge survives restore");
    var reverse = Called(Pair(player: "CLEFABLE", opponent: "DITTO"), "TRANSFORM", WildBattleSide.Opponent).State!;
    Check(reverse.Opponent.Transform is not null && !reverse.Opponent.DefenseBadgeBoost, "opponent copy cannot acquire player badge");
    Check(reverse.Opponent.Pokemon.Stats.Defense == reverse.Player.Pokemon.Stats.Defense, "same raw stats, separate side flags");
    var hitter = new WildBattleCombatant(Mon("RATTATA", 40, "TACKLE"));
    var damage = WildBattleDamage.Calculate(catalog, hitter, transformed.Player, Id("TACKLE"), false, 100, WildBattleRules.FuchsiaRulesVersion);
    Check(damage.Steps.Single(s => s.Operation == "defense-badge").Value == transformed.Player.Pokemon.Stats.Defense * 110 / 100, "transformed Defense receives badge at calculation");
    Check(!damage.Steps.Any(x => x.Operation == "metal-powder"), "copied non-Ditto loses Powder while retaining own badge");
});
Test("real source party ordinary, Baton Pass, forced and faint replacement preserve player side flag", () => {
    var state = Party(Pair());
    var switched = Accepted(WildBattleRules.ResolveCalledPartySwitch(catalog, state, new(WildBattleSide.Player, 1)));
    Check(switched.Player.DefenseBadgeBoost && switched.CalledParty!.PlayerActiveIndex == 1, "ordinary switch");
    var opponent = Accepted(WildBattleRules.ResolveCalledPartySwitch(catalog, switched, new(WildBattleSide.Opponent, 1)));
    Check(!opponent.Opponent.DefenseBadgeBoost && opponent.Player.DefenseBadgeBoost, "opponent switch stays unbadged");
    state = state with { Player = state.Player with { DefenseStage = 8 } };
    var baton = Called(state, "BATON_PASS").State!;
    Check(baton.CalledParty!.PendingSwitch?.Kind == WildBattleCalledSwitchKind.BatonPass, "real Baton Pass pending");
    baton = Accepted(WildBattleRules.ResolveCalledPartySwitch(catalog, baton, new(WildBattleSide.Player, 1)));
    Check(baton.Player.DefenseBadgeBoost && baton.Player.DefenseStage == 8, "Baton Pass retains side badge independently of stages");
    var forced = Called(Party(Pair()), "ROAR", WildBattleSide.Opponent).State!;
    Check(forced.Player.DefenseBadgeBoost && forced.CalledParty!.PlayerActiveIndex == 1, "actual source forced switch");
    var fragile = Party(Pair()); fragile = fragile with { Player = fragile.Player with { Pokemon = fragile.Player.Pokemon with { CurrentHp = 1 } } };
    fragile = fragile with { CalledParty = fragile.CalledParty! with { Player = fragile.CalledParty.Player.SetItem(0, Member(fragile.Player)) } };
    var faint = Called(fragile, "SEISMIC_TOSS", WildBattleSide.Opponent).State!;
    Check(faint.CalledParty!.PendingSwitch?.Kind == WildBattleCalledSwitchKind.FaintedReplacement, "actual faint replacement required");
    faint = Accepted(WildBattleRules.ResolveCalledPartySwitch(catalog, faint, new(WildBattleSide.Player, 1)));
    Check(faint.Player.DefenseBadgeBoost, "badge persists after faint replacement");
});
Test("Doom Desire records badged physical Defense at cast; Future Sight and fixed damage remain unchanged", () => {
    foreach (string move in new[] { "DOOM_DESIRE", "FUTURE_SIGHT" })
    {
        var plain = Called(Party(Pair(false)), move, WildBattleSide.Opponent).State!;
        var badge = Called(Party(Pair()), move, WildBattleSide.Opponent).State!;
        int a = plain.CalledDelayed!.PlayerFutureAttack!.Damage, b = badge.CalledDelayed!.PlayerFutureAttack!.Damage;
        Check(move == "DOOM_DESIRE" ? b < a : a == b, "cast defense category " + move);
        var switched = Accepted(WildBattleRules.ResolveCalledPartySwitch(catalog, badge, new(WildBattleSide.Player, 1)));
        Check(switched.CalledDelayed!.PlayerFutureAttack!.Damage == b && switched.Player.DefenseBadgeBoost, "delayed cached damage survives replacement " + move);
    }
    foreach (string move in new[] { "SEISMIC_TOSS", "SONIC_BOOM", "DRAGON_RAGE" })
    {
        var a = Called(Pair(false), move, WildBattleSide.Opponent); var b = Called(Pair(), move, WildBattleSide.Opponent);
        Check(a.State!.Player.Pokemon.CurrentHp == b.State!.Player.Pokemon.CurrentHp && a.State.Rng == b.State.Rng, "fixed damage badge independent " + move);
    }
});
Test("Mirror Counter and Mirror Coat never apply target badge to doubled recorded damage", () => {
    foreach (string move in new[] { "COUNTER", "MIRROR_COAT" })
    foreach (bool badge in new[] { false, true })
    {
        var state = Pair(badge); state = state with { Opponent = state.Opponent with { Pokemon = Mon("RATTATA", 40, "MIRROR_MOVE"),
            SourceMoveHistory = new() { LastTakenMove = Id(move) },
            CalledDamage = new() { Turn = 0, PhysicalDamage = move == "COUNTER" ? 17 : 0, PhysicalSource = WildBattleSide.Player,
                SpecialDamage = move == "MIRROR_COAT" ? 17 : 0, SpecialSource = WildBattleSide.Player } } };
        var result = Source(state, WildBattleSide.Opponent);
        Check(result.Battle.Events.OfType<WildBattleHpChangedEvent>().Single(e => e.Target == WildBattleSide.Player).Damage == 34, "fixed counter into badged player " + move);
        Check(!result.Battle.Trace.Any(x => x.Purpose.EndsWith("damage") || x.Purpose.EndsWith("critical")), "no counter damage or crit RNG");
    }
});
Test("confusion self-hit uses own badge before Metal Powder and stages without stored stat mutation", () => {
    foreach (bool badge in new[] { false, true }) foreach (bool powder in new[] { false, true })
    foreach (string move in new[] { "TACKLE", "SELF_DESTRUCT" })
    {
        WildBattleCalledMoveResult? hit = null; WildBattleState? before = null;
        for (uint seed = 0; seed < 64; seed++)
        {
            var state = Pair(badge, "DITTO", seed: seed); state = state with { Player = state.Player with { ConfusionTurnsRemaining = 3,
                Pokemon = Mon("DITTO", 60, move), DefenseStage = 7, HeldItem = powder ? "ITEM_METAL_POWDER" : "ITEM_NONE" } };
            var result = Source(state, WildBattleSide.Player);
            if (result.Battle.Events.OfType<WildBattleConfusionSelfHitEvent>().Any()) { hit = result; before = state; break; }
        }
        Check(hit is not null, "nonempty confusion self-hit branch");
        var mon = before!.Player; var e = hit!.Battle.Events.OfType<WildBattleConfusionSelfHitEvent>().Single();
        int defense = mon.Pokemon.Stats.Defense; if (badge) defense = defense * 110 / 100; if (powder) defense *= 2; if (move == "SELF_DESTRUCT") defense /= 2; defense = defense * 3 / 2;
        int damage = mon.Pokemon.Stats.Attack * 40 * (2 * mon.Pokemon.Level / 5 + 2) / defense / 50;
        damage = Math.Max(1, (Math.Max(1, damage) + 2) * e.RandomPercent / 100);
        Check(e.Damage == damage && hit.State!.Player.Pokemon.Stats == mon.Pokemon.Stats, "source self/self formula and unchanged raw stats");
    }
});
Console.WriteLine(JsonSerializer.Serialize(new { groups, checks, failures, passed = groups > 0 && checks > 0 && failures == 0 }));
return failures == 0 && checks > 0 ? 0 : 1;
