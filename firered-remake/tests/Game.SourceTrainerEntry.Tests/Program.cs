using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;

if (args.Length != 1) throw new ArgumentException("Pass the project root.");
string root = Path.GetFullPath(args[0]); int groups = 0, assertions = 0, failures = 0;
void Check(bool value, string label) { assertions++; if (!value) throw new Exception(label); }
void Test(string name, Action action) { groups++; try { action(); Console.WriteLine("PASS " + name); } catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + " " + ex); } }
MoveId Move(string name) => new("pret:MOVE_" + name);
var original = PretRocketHideoutPokemon.Load(root).Catalog;
var supplement = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules: true);
var old = PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(original with { Moves = original.Moves.SetItems(supplement.Moves) }, root, root).Catalog, root).Catalog, root).Catalog;
var route = PretFuchsiaRoutePokemon.Load(old, root);
var catalog = PretMetronomeMoveCatalog.Load(root).Extend(route.Catalog);
PokemonInstance Pokemon(string species, int level = 29, uint personality = 0) => PokemonFactory.Create(catalog,
    new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
WildBattleCombatant Plain(string species = "RATTATA") => new(Pokemon(species, 30));
var clefairy = Pokemon("CLEFAIRY");
// Clefairy learns Metronome at level 29 and is compatible with TM06 in the pinned source.
// Use real source PP values, then retain a partially used slot to detect PP substitution.
var toxic = catalog.Moves[Move("TOXIC")];
clefairy = clefairy with { Moves = clefairy.Moves.SetItem(0, new(Move("TOXIC"), toxic.Pp - 1, toxic.Pp)) };
var fainted = Plain("BULBASAUR"); fainted = fainted with { Pokemon = fainted.Pokemon with { CurrentHp = 0 } };
ImmutableArray<WildBattleCombatant> players = [fainted, new(clefairy, HeldItem: "ITEM_ORAN_BERRY") { AttackBadgeBoost = true, SpeedBadgeBoost = true }, Plain("GYARADOS")];
string Encode<T>(T value) => JsonSerializer.Serialize(value);
ForestTrainerCombatState Require(ForestTrainerCombatResult result, string label)
{
    Check(result.Succeeded, label + " fault=" + result.Fault);
    Check(ForestTrainerCombatRules.Validate(catalog, result.State) is null, label + " validation=" + ForestTrainerCombatRules.Validate(catalog, result.State));
    return result.State!;
}
var families = new List<(string Name, ImmutableArray<TrainerId> Ids,
    Func<TrainerId, ImmutableArray<WildBattleCombatant>, int, RngState, ForestTrainerCombatResult> Source,
    Func<TrainerId, WildBattleCombatant, RngState, ForestTrainerCombatResult> Legacy)>();
var forest = PretForestTrainers.Load(root);
families.Add(("Forest", forest.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(forest, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(forest, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var pewter = PretPewterTrainers.Load(root);
families.Add(("Pewter", pewter.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(pewter, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(pewter, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var route22 = PretRoute22Trainers.Load(PretRoute22Region.Load(root), PretRoute22Pokemon.Load(root, includeSourceCryWaves: false));
families.Add(("Route22", route22.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(route22, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(route22, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var route3 = PretRoute3Trainers.Load(root);
families.Add(("Route3", route3.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(route3, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(route3, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var mtMoon = PretMtMoonTrainers.Load(root);
families.Add(("MtMoon", mtMoon.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(mtMoon, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(mtMoon, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var cerulean = PretCeruleanTrainers.Load(root);
families.Add(("Cerulean", cerulean.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(cerulean, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(cerulean, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var bill = PretBillTrainers.Load(root);
families.Add(("Bill", bill.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(bill, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(bill, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var vermilion = PretVermilionTrainers.Load(root);
families.Add(("Vermilion", vermilion.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(vermilion, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(vermilion, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var ssAnne = PretSsAnneTrainers.Load(root);
families.Add(("SsAnne", ssAnne.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(ssAnne, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(ssAnne, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var vermilionGym = PretVermilionGymTrainers.Load(root);
families.Add(("VermilionGym", vermilionGym.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(vermilionGym, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(vermilionGym, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var route11 = PretRoute11Trainers.Load(root);
families.Add(("Route11", route11.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(route11, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(route11, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var rockTunnel = PretRockTunnelTrainers.Load(root);
families.Add(("RockTunnel", rockTunnel.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(rockTunnel, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(rockTunnel, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var celadonApproach = PretCeladonApproachTrainers.Load(root);
families.Add(("CeladonApproach", celadonApproach.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(celadonApproach, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(celadonApproach, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var celadonGym = PretCeladonGymTrainers.Load(root);
families.Add(("CeladonGym", celadonGym.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(celadonGym, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(celadonGym, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var rocketHideout = PretRocketHideoutTrainers.Load(root);
families.Add(("RocketHideout", rocketHideout.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(rocketHideout, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(rocketHideout, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));
var pokemonTower = PretPokemonTowerTrainers.Load(root);
families.Add(("PokemonTower", pokemonTower.Trainers.Keys.OrderBy(x => x.Value, StringComparer.Ordinal).ToImmutableArray(),
    (id, party, index, rng) => ForestTrainerCombatRules.StartSource(pokemonTower, catalog, id, party, index, rng, terrain: WildBattleTerrain.Building),
    (id, lead, rng) => ForestTrainerCombatRules.Start(pokemonTower, catalog, id, lead, rng, terrain: WildBattleTerrain.Building, fuchsiaRules: true)));

foreach (var family in families)
{
    Test(family.Name + " all pinned single rosters enter with real Toxic/Metronome and a fainted first slot", () => {
        foreach (var id in family.Ids)
        {
            var result = family.Source(id, players, 1, new(0x12345678));
            var state = Require(result, id.Value);
            Check(state.SourceMoveRulesVersion == ForestTrainerCombatRules.SourceTrainerContractVersion, "explicit source version");
            Check(state.CeruleanPlayerPartyIndex == 1 && state.Battle.CalledParty!.PlayerActiveIndex == 1, "actual lead slot");
            Check(Encode(state.Battle.Player.Pokemon) == Encode(players[1].Pokemon), "unchanged HP, PP and identity");
            Check(Encode(state.SsAnnePlayerParty!.Value[0]) == Encode(fainted), "fainted member retained exactly");
            Check(Encode(state.SsAnnePlayerParty.Value[2]) == Encode(players[2]), "reserve retained exactly");
            Check(state.Battle.Player.HeldItem == players[1].HeldItem && state.HasPlayerReserves, "held item and living reserve");
        }
    });
    Test(family.Name + " genuine old entry promotion preserves every event and RNG draw", () => {
        var id = family.Ids[0]; ImmutableArray<WildBattleCombatant> party = [fainted, Plain("GYARADOS"), players[1]];
        var entry = family.Legacy(id, party[1], new(0x56789abc));
        Check(entry.Succeeded, "historical start=" + entry.Fault);
        string before = Encode(entry);
        var promoted = ForestTrainerCombatRules.PromoteSourceTrainer(catalog, entry, party, 1);
        var state = Require(promoted, "promotion");
        Check(Encode(entry) == before, "original result untouched");
        Check(promoted.Events == entry.Events && promoted.TrainerEvents == entry.TrainerEvents && promoted.Trace == entry.Trace, "same immutable event/trace arrays");
        Check(state.Battle.Rng == entry.State!.Battle.Rng && state.Battle.EventSequence == entry.State.Battle.EventSequence && state.Battle.Revision == entry.State.Battle.Revision, "same RNG and counters");
        Check(Encode(state.Battle.Player) == Encode(entry.State.Battle.Player) && Encode(state.Battle.Opponent) == Encode(entry.State.Battle.Opponent), "entry abilities retained");
        Check(state.Battle.Opponent.AttackStage == entry.State.Battle.Opponent.AttackStage, "Intimidate stage retained");
        var direct = Require(family.Source(id, party, 1, new(0x56789abc)), "direct source");
        Check(Encode(direct) == Encode(state), "direct source equals promoted genuine historical entry");
    });
}
Test("source entry rejects invalid actual party and unsupported catalog atomically", () => {
    var family = families[0]; var id = family.Ids[0];
    void Rejected(ImmutableArray<WildBattleCombatant> party, int index, string label) {
        var result = family.Source(id, party, index, new(42));
        Check(!result.Succeeded && result.State is null && result.Events.IsEmpty && result.Trace.IsEmpty, label + " atomic fault=" + result.Fault);
    }
    Rejected(default, 0, "missing party"); Rejected([], 0, "empty party");
    Rejected(players, 0, "fainted active"); Rejected(players, 3, "active index");
    Rejected(players.AddRange(players).Add(players[1]), 1, "seven members");
    Rejected(players.SetItem(2, players[2] with { AttackStage = 7 }), 1, "reserve stage");
    Rejected(players.SetItem(2, players[2] with { FirstTurnCounter = 2 }), 1, "reserve entry counter");
    Rejected(players.SetItem(2, players[2] with { SourcePartyStatusOverride = 0x80 }), 1, "reserve source status override");
    Rejected(players.SetItem(0, fainted with { Status = WildBattleStatus.Poison }), 1, "fainted status");
    Rejected(players.SetItem(2, players[2] with { Pokemon = players[2].Pokemon with { CurrentHp = 9999 } }), 1, "invalid reserve HP");
    var wrongCatalog = ForestTrainerCombatRules.StartSource(forest, route.Catalog, id, [Plain()], 0, new(42));
    Check(!wrongCatalog.Succeeded && wrongCatalog.Fault == "source_trainer_catalog", "244 moves cannot enter successor");
});
Test("promotion rejects changed lead, played turns, wrong profiles and duplicate promotion", () => {
    var family = families[0]; var id = family.Ids[0]; ImmutableArray<WildBattleCombatant> party = [Plain(), players[1]];
    var entry = family.Legacy(id, party[0], new(42)); Check(entry.Succeeded, "entry");
    var changed = party.SetItem(0, party[0] with { Pokemon = party[0].Pokemon with { CurrentHp = party[0].Pokemon.CurrentHp - 1 } });
    Check(ForestTrainerCombatRules.PromoteSourceTrainer(catalog, entry, changed, 0).Fault == "source_trainer_promotion_player_mismatch", "real HP match");
    var spent = entry with { State = entry.State! with { Battle = entry.State.Battle with { Turn = 1 } } };
    Check(ForestTrainerCombatRules.PromoteSourceTrainer(catalog, spent, party, 0).Fault == "source_trainer_promotion_entry", "played turn");
    var relabeled = entry with { State = entry.State! with { RulesVersion = ForestTrainerCombatRules.RulesVersion } };
    Check(ForestTrainerCombatRules.PromoteSourceTrainer(catalog, relabeled, party, 0).Fault == "source_trainer_promotion_entry", "historical profile");
    var promoted = ForestTrainerCombatRules.PromoteSourceTrainer(catalog, entry, party, 0); Require(promoted, "once");
    Check(ForestTrainerCombatRules.PromoteSourceTrainer(catalog, promoted, party, 0).Fault == "source_trainer_promotion_entry", "cannot promote twice");
    var failure = family.Source(id, [], 0, new(42));
    Check(ReferenceEquals(failure, ForestTrainerCombatRules.PromoteSourceTrainer(catalog, failure, party, 0)), "preserve failed result");
});
Test("source roster projection keeps identities and metadata authenticated", () => {
    var state = Require(families[0].Source(families[0].Ids[0], players, 1, new(42)), "source entry");
    var opponent = state.OpponentParty[0];
    var edited = opponent with { Pokemon = opponent.Pokemon with { Personality = opponent.Pokemon.Personality + 1 } };
    var forged = state with { OpponentParty = state.OpponentParty.SetItem(0, edited), Battle = state.Battle with { Opponent = edited,
        CalledParty = state.Battle.CalledParty! with { Opponent = state.Battle.CalledParty.Opponent.SetItem(0, new(edited.Pokemon)) } } };
    Check(ForestTrainerCombatRules.Validate(catalog, forged) is not null, "opponent identity tampering rejected");
    var malformed = catalog with { Moves = catalog.Moves.SetItem(Move("TOXIC"), toxic with { Pp = toxic.Pp + 1 }) };
    Check(ForestTrainerCombatRules.Validate(malformed, state) is not null, "source definition tampering rejected");
    var restored = JsonSerializer.Deserialize<ForestTrainerCombatState>(Encode(state));
    Check(ForestTrainerCombatRules.Validate(catalog, restored) is null, "JSON roundtrip");
});
Test("canonical Toxic enters as actual status while battle counters cannot be imported", () => {
    var party = players.SetItem(1, players[1] with { Status = 0x80 }).SetItem(2, players[2] with { Status = 0x80 });
    var state = Require(families[0].Source(families[0].Ids[0], party, 1, new(42)), "canonical Toxic entry");
    Check(state.Battle.CalledField is not null && state.Battle.Player.Status == 0x80, "active Toxic and field present");
    Check(state.Battle.CalledParty!.Player[2].Status == 0x80 && state.SsAnnePlayerParty!.Value[2]!.Status == 0x80, "canonical reserve retained");
    foreach (uint status in new uint[] { 0x180, 0x280, 0xF80 }) {
        var imported = familyEntry(party.SetItem(2, party[2] with { Status = status }));
        Check(imported.Fault == "source_trainer_entry_toxic_counter" && imported.State is null && imported.Trace.IsEmpty, "reject reserve live counter");
    }
    ForestTrainerCombatResult familyEntry(ImmutableArray<WildBattleCombatant> team) => families[0].Source(families[0].Ids[0], team, 1, new(42));
});
Test("older trainer executes naturally learned Metronome and TM Toxic through source turns", () => {
    var id = new TrainerId("pret:TRAINER_BUG_CATCHER_ANTHONY");
    foreach (string move in new[] { "TOXIC", "METRONOME" }) {
        var entered = Require(families[0].Source(id, players, 1, new(42)), move + " entry");
        int slot = Enumerable.Range(0, entered.Battle.Player.Pokemon.Moves.Length).Single(i => entered.Battle.Player.Pokemon.Moves[i].Move == Move(move));
        var result = ForestTrainerCombatRules.ResolveMove(catalog, entered, slot);
        var state = Require(result, move + " actual source turn");
        Check(result.Events.Any(e => e is WildBattleAttackStringEvent printed && printed.Actor == WildBattleSide.Player && printed.ChosenMove == Move(move) && printed.CurrentMove == Move(move)), "actual selected attack string " + move);
        Check(state.Battle.Player.Pokemon.Moves[slot].CurrentPp == entered.Battle.Player.Pokemon.Moves[slot].CurrentPp - 1, "one real PP consumed");
        if (move == "METRONOME") Check(result.Events.Any(e => e is WildBattleCalledMoveTransitionEvent), "actual unfiltered random call");
    }
});
Console.WriteLine($"Source trainer entry: {groups} groups, {assertions} assertions, {families.Sum(f => f.Ids.Length)} pinned rosters, {failures} failures.");
if (groups == 0 || assertions == 0 || failures != 0) Environment.Exit(1);
