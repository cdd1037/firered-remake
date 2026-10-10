using System.Collections.Immutable;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length != 2 || !Directory.Exists(args[0])) return 2;
string root = Path.GetFullPath(args[0]);
int checks = 0, groups = 0, failures = 0, parity = 0;
var results = new List<object>();

void Check(bool ok, string label) { checks++; if (!ok) throw new InvalidDataException(label); }
void Test(string label, Action run) {
    groups++; try { run(); results.Add(new { label, passed = true }); Console.WriteLine("PASS " + label); }
    catch (Exception e) { failures++; results.Add(new { label, passed = false, error = e.ToString() }); Console.WriteLine("FAIL " + label + " " + e); }
}
var raw = PretRocketHideoutPokemon.Load(root).Catalog;
var extra = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules: true);
var prior = PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(raw with { Moves = raw.Moves.SetItems(extra.Moves) }, root, root).Catalog, root).Catalog, root).Catalog;
var catalog = PretMetronomeMoveCatalog.Load(root).Extend(PretFuchsiaRoutePokemon.Load(prior, root).Catalog);
var mechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(args[1], root);
var canonical = mechanics.Catalog;
MoveId Id(string name) => new("pret:MOVE_" + name);
WildBattleCombatant Mon(string species, int level, uint personality, params string[] moves) {
    var mon = PokemonFactory.Create(catalog, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
    return new(mon with { Moves = moves.Select(name => new PokemonMoveSlot(Id(name), catalog.Moves[Id(name)].Pp, catalog.Moves[Id(name)].Pp)).ToImmutableArray() });
}
WildBattleCalledPartyMember Member(WildBattleCombatant body) => new(WildBattleTransformationRules.PersistentPokemon(body), body.HeldItem,
    WildBattleTransformationRules.PersistentStatus(body), KnockedOffItem: body.KnockedOffItem);

string Json<T>(T value) => JsonSerializer.Serialize(value);
SourceBattleFrame Four(string selected = "TACKLE") {
    var player = Mon("PIDGEY", 20, 1, selected, "RECOVER", "HAZE", "HOWL");
    var foe = Mon("PIDGEY", 100, 2, "TACKLE", "SPLASH");
    return new() { Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
        BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(31), Revision = 10, EventSequence = 20,
        Field = new() { RainTurnsRemaining = 3, Terrain = WildBattleTerrain.Plain, PayDayMoney = 200 },
        Parties = [new(BattleSide.Player, [Member(player), Member(player)]), new(BattleSide.Opponent, [Member(foe), Member(foe)])],
        Slots = [new(new(0), new(BattleSide.Player, 0), 1, player), new(new(1), new(BattleSide.Opponent, 0), 1, foe),
            new(new(2), new(BattleSide.Player, 1), 1, player), new(new(3), new(BattleSide.Opponent, 1), 1, foe)] };
}
SourceKernelResult Act(SourceBattleFrame f, int actor, int target, int slot = 0) => SourceBattleKernel.ResolveAction(f.Layout == SourceBattleLayout.PresetDoubles ? canonical : catalog, f,
    new(f.Slots[actor].Occupant!.Value, f.Revision, slot, new(target)));
Test("real four-slot action preserves unrelated occupants and field registers", () => {
    var f = Four(); var result = Act(f, 2, 3);
    Check(result.Succeeded, result.Fault ?? "accepted");
    Check(result.State.Slots[0] == f.Slots[0] && result.State.Slots[1] == f.Slots[1], "other two slots authoritative");
    Check(result.State.Field == f.Field, "weather, terrain and Pay Day retained");
    Check(result.State.Slots[2].Member != result.State.Slots[0].Member, "identical species distinct party key");
    Check(result.State.Slots[2].Body!.Pokemon.Moves[0].CurrentPp == f.Slots[2].Body!.Pokemon.Moves[0].CurrentPp - 1, "actor2 pays once");
    Check(result.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp == f.Slots[0].Body!.Pokemon.Moves[0].CurrentPp, "identical ally PP untouched");
    Check(result.Events.OfType<SourcePpCommandEvent>().Single().PpOwner.Value == 2, "canonical PP owner");
    Check(result.State.Slots[3].Registers.LastHitBy == new BattlerSlot(2), "canonical last hitter");
    Check(result.State.Slots[3].Registers.History!.LastTakenFrom[2] == Id("TACKLE") && result.State.Slots[3].Registers.History!.LastTakenFrom[0] is null, "four source matrix");
    Check(SourceBattleIdentityRules.Validate(result.State) is null, "canonical state validates");
});
Test("selected move can hit own ally without confusing side and identity", () => {
    var f = Four("TACKLE"); var result = Act(f, 0, 2);
    Check(result.Succeeded, result.Fault ?? "accepted");
    Check(result.Events.OfType<SourceHpChangedEvent>().Single().Target.Value == 2, "actual selected ally target");
    Check(result.State.Slots[1] == f.Slots[1] && result.State.Slots[3] == f.Slots[3], "both enemies untouched");
    Check(result.State.Slots[2].Registers.Damage?.SpecialSource == new BattlerSlot(0) || result.State.Slots[2].Registers.Damage?.PhysicalSource == new BattlerSlot(0), "ally damage source slot");
});
Test("Haze scans all four slots through same stat recipe", () => {
    var f = Four(); f = f with { Slots = f.Slots.Select(c => c with { Body = c.Body! with { AttackStage = 9, SpecialDefenseStageDelta = -2 } }).ToImmutableArray() };
    var result = Act(f, 2, 2, 2); Check(result.Succeeded, result.Fault ?? "accepted");
    Check(result.State.Slots.All(c => c.Body!.AttackStage == 6 && c.Body.SpecialDefenseStageDelta == 0), "all four reset");
    Check(result.Events.OfType<SourceStatChangedEvent>().Select(e => e.Target.Value).Distinct().Count() == 4, "all four events");
});
Test("unmigrated paths and stale occupants fail atomically", () => {
    foreach (string move in new[] { "METRONOME", "EARTHQUAKE", "SNATCH", "FOCUS_PUNCH", "SURF", "DOUBLE_SLAP" }) {
        var f = Four(move); var r = Act(f, 0, 1); Check(!r.Succeeded && ReferenceEquals(f, r.State) && r.Events.IsEmpty && r.Trace.IsEmpty, move + " atomic rejection");
    }
    var state = Four(); var stale = new SourceResolvedMoveAction(state.Slots[0].Occupant!.Value with { EntrySerial = 99 }, state.Revision, 0, new(1));
    var result = SourceBattleKernel.ResolveAction(canonical, state, stale); Check(result.Fault == "source_kernel_stale_action" && ReferenceEquals(state, result.State), "stale token rejected");
});
Test("canonical singles slice matches existing public action state events and RNG", () => {
    foreach (string name in new[] { "POISON_POWDER", "SLEEP_POWDER", "TACKLE", "MEGA_KICK", "FIRE_PUNCH", "MAGICAL_LEAF", "SLUDGE_BOMB", "MUD_SHOT", "CRUNCH", "ROCK_SMASH", "ABSORB", "HOWL", "CHARM", "SCARY_FACE", "FAKE_TEARS", "RECOVER", "IRON_DEFENSE", "TAIL_GLOW", "AMNESIA", "SOFT_BOILED", "SYNTHESIS", "BELLY_DRUM", "TICKLE", "BULK_UP", "CALM_MIND", "DRAGON_DANCE", "SUPERPOWER", "OVERHEAT", "HAZE", "PSYCH_UP", "REFRESH", "SWAGGER", "FLATTER" })
    for (uint seed = 0; seed < 20; seed++) {
        var player = Mon("PIDGEY", 20, 1, name); player = player with { Pokemon = player.Pokemon with { CurrentHp = player.Pokemon.CurrentHp - 5 } };
        var foe = Mon("RATTATA", 100, 2, "SPLASH");
        var state = new WildBattleState(player, foe, 0, 0, 0, new(seed), 0, WildBattleOutcome.Ongoing, RulesVersion: WildBattleRules.FuchsiaRulesVersion) { Terrain = WildBattleTerrain.Plain };
        var expected = WildBattleRules.ResolveSourceMoveAction(catalog, state, new(WildBattleSide.Player, 0)); Check(expected.Succeeded, name + " legacy " + expected.Fault);
        var imported = SinglesBattleAdapter.Import(catalog, state, [new(BattleSide.Player, [Member(player)]), new(BattleSide.Opponent, [Member(foe)])]); Check(imported.Succeeded, name + " import " + imported.Fault);
        var move = catalog.Moves[Id(name)]; int target = move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? 0 : 1;
        var actual = Act(imported.Frame!, 0, target); Check(actual.Succeeded, name + " canonical " + actual.Fault);
        var exported = SinglesBattleAdapter.Export(actual.State); Check(exported.Succeeded, name + " export " + exported.Fault);
        Check(Json(actual.Trace) == Json(expected.Battle.Trace), name + " seed " + seed + " RNG trace");
        Check(Json(actual.Events.Select(SourceBattleEventAdapter.Export).ToImmutableArray()) == Json(expected.Battle.Events), name + " seed " + seed + " events");
        Check(Json(exported.State) == Json(expected.State), name + " seed " + seed + " state\nACTUAL " + Json(exported.State) + "\nEXPECTED " + Json(expected.State));
        parity++;
    }
});

Test("third-slot Cloud Nine governs the shared field damage", () => {
    var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[1], root); var full = proof.Catalog;
    SourceKernelResult FullAct(SourceBattleFrame f) => SourceBattleKernel.ResolveAction(full, f, new(f.Slots[0].Occupant!.Value, f.Revision, 0, new(1)));
    var f = Four("WATER_GUN"); var attacker = Mon("SQUIRTLE", 60, 1, "WATER_GUN");
    f = f with { Slots = f.Slots.SetItem(0, f.Slots[0] with { Body = attacker }), Parties = f.Parties.SetItem(0, f.Parties[0] with { Members = f.Parties[0].Members.SetItem(0, Member(attacker)) }) };
    var normal = FullAct(f); Check(normal.Succeeded, "rain hit " + normal.Fault);
    var pokemon = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_PSYDUCK"), 100, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(1)), new(0)).Pokemon;
    var cloud = new WildBattleCombatant(pokemon);
    Check(cloud.Pokemon.AbilityId == "ABILITY_CLOUD_NINE", "authentic Cloud Nine donor");
    f = f with { Slots = f.Slots.SetItem(3, f.Slots[3] with { Body = cloud }),
        Parties = f.Parties.SetItem(1, f.Parties[1] with { Members = f.Parties[1].Members.SetItem(1, Member(cloud)) }) };
    var suppressed = FullAct(f); Check(suppressed.Succeeded, suppressed.Fault ?? "accepted");
    Check(suppressed.Events.OfType<SourceHpChangedEvent>().Single().Damage < normal.Events.OfType<SourceHpChangedEvent>().Single().Damage, "partner suppresses rain boost");
    Check(suppressed.State.Slots[3] == f.Slots[3] && suppressed.State.Field == f.Field, "suppression never projects away weather or donor");
});
Test("real Pressure ally uses slot equality and charges the correct party member", () => {
    var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(args[1], root); var full = proof.Catalog;
    WildBattleCombatant Native(string species, string name) {
        var mon = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + species), 50, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Check(full.Species[mon.Species].LevelUpMoves.Any(m => m.Move == Id(name) && m.Level <= 50), "authentic retained move");
        return new(mon with { Moves = [new(Id(name), full.Moves[Id(name)].Pp, full.Moves[Id(name)].Pp)] });
    }
    var actor = Native("TREECKO", "ABSORB"); var ally = Native("DUSCLOPS", "NIGHT_SHADE"); var foe = Native("PIDGEY", "TACKLE");
    Check(ally.Pokemon.AbilityId == "ABILITY_PRESSURE", "real source Pressure donor");
    var f = Four(); f = f with { Field = new(), Parties = [new(BattleSide.Player, [Member(actor), Member(ally)]), new(BattleSide.Opponent, [Member(foe), Member(foe)])],
        Slots = [new(new(0), new(BattleSide.Player, 0), 1, actor), new(new(1), new(BattleSide.Opponent, 0), 1, foe),
            new(new(2), new(BattleSide.Player, 1), 1, ally), new(new(3), new(BattleSide.Opponent, 1), 1, foe)] };
    var result = SourceBattleKernel.ResolveAction(full, f, new(f.Slots[0].Occupant!.Value, f.Revision, 0, new(2)));
    Check(result.Succeeded, result.Fault ?? "accepted");
    Check(result.Events.OfType<SourcePpCommandEvent>().Single().CommandCost == 2, "same-side distinct slot incurs Pressure");
    Check(result.State.Party(BattleSide.Player).Members[0].Pokemon.Moves[0].CurrentPp == actor.Pokemon.Moves[0].CurrentPp - 2, "actual party0 pays two");
    Check(result.State.Party(BattleSide.Player).Members[1].Pokemon.Moves[0].CurrentPp == ally.Pokemon.Moves[0].CurrentPp, "actual ally1 PP unchanged");
});
Test("primary status targets a selected ally and real lethal hit emits canonical faint", () => {
    var f = Four("POISON_POWDER") with { Rng = new(0) }; var r = Act(f, 0, 2); Check(r.Succeeded, r.Fault ?? "status accepted");
    Check(r.State.Slots[2].Body!.Status == WildBattleStatus.Poison && r.State.Slots[1].Body!.Status == 0 && r.State.Slots[3].Body!.Status == 0, "actual ally poisoned");
    f = Four(); var target = f.Slots[1].Body!; target = target with { Pokemon = target.Pokemon with { CurrentHp = 1 } };
    f = f with { Slots = f.Slots.SetItem(1, f.Slots[1] with { Body = target }), Parties = f.Parties.SetItem(1, f.Parties[1] with { Members = f.Parties[1].Members.SetItem(0, Member(target)) }) };
    r = Act(f, 0, 1); Check(r.Succeeded, r.Fault ?? "lethal accepted");
    Check(r.Events.OfType<SourceFaintedEvent>().Single().Side.Value == 1 && r.State.Slots[1].Body!.Pokemon.CurrentHp == 0, "real shared hit faint");
    Check(r.State.Party(BattleSide.Opponent).Members[0].Pokemon.CurrentHp == 0, "actual party faint synchronized");
    Check(r.State.Outcome == WildBattleOutcome.Ongoing && r.State.PendingSwitches.IsEmpty && r.State.Slots[3] == f.Slots[3], "owner retains end-turn replacement and outcome");
    Check(!r.State.CanAct(new(1)) && r.State.Slots[1].Body!.FirstTurnCounter == 2, "source faint cleanup and action eligibility");
});
Test("truncated public frame payloads reject atomically before identity projection", () => {
    var valid = Four();
    var corrupt = new[] {
        valid with { Slots = valid.Slots.SetItem(0, valid.Slots[0] with { Body = valid.Slots[0].Body! with { Pokemon = null! } }) },
        valid with { Slots = valid.Slots.SetItem(0, valid.Slots[0] with { Body = valid.Slots[0].Body! with { Pokemon = valid.Slots[0].Body!.Pokemon with { Moves = default } } }) },
        valid with { Slots = valid.Slots.SetItem(0, valid.Slots[0] with { Body = valid.Slots[0].Body! with { Pokemon = valid.Slots[0].Body!.Pokemon with { Moves = [null!] } } }) },
        valid with { Slots = valid.Slots.SetItem(0, valid.Slots[0] with { Body = valid.Slots[0].Body! with { Pokemon = valid.Slots[0].Body!.Pokemon with { Stats = null! } } }) },
        valid with { Parties = valid.Parties.SetItem(0, valid.Parties[0] with { Members = [valid.Parties[0].Members[0] with { Pokemon = null! }, valid.Parties[0].Members[1]] }) },
        valid with { Parties = valid.Parties.SetItem(0, valid.Parties[0] with { Members = [valid.Parties[0].Members[0] with { Pokemon = valid.Parties[0].Members[0].Pokemon with { Moves = default } }, valid.Parties[0].Members[1]] }) }
    };
    foreach (var input in corrupt) {
        var result = SourceBattleKernel.ResolveAction(canonical, input, new(valid.Slots[0].Occupant!.Value, input.Revision, 0, new(1)));
        Check(result.Fault == "source_kernel_nested_pokemon_shape" && ReferenceEquals(input, result.State) && result.Events.IsEmpty && result.Trace.IsEmpty, "malformed nested input atomic fault");
    }
});
Test("doubles require catalog proof and authenticate every reserve", () => {
    var f = Four(); var action = new SourceResolvedMoveAction(f.Slots[0].Occupant!.Value, f.Revision, 0, new(1));
    var forged = SourceBattleKernel.ResolveAction(canonical with { }, f, action);
    Check(forged.Fault == "source_kernel_authenticated_catalog_required" && ReferenceEquals(forged.State, f) && forged.Trace.IsEmpty && forged.Events.IsEmpty, "text-identical unproven catalog rejected");
    var member = f.Parties[0].Members[0]; member = member with { Pokemon = member.Pokemon with { AbilityId = "ABILITY_OVERGROW" } };
    f = f with { Parties = f.Parties.SetItem(0, f.Parties[0] with { Members = f.Parties[0].Members.Add(member) }) };
    var invalidReserve = SourceBattleKernel.ResolveAction(canonical, f, action);
    Check(invalidReserve.Fault == "source_kernel_party_pokemon" && ReferenceEquals(invalidReserve.State, f) && invalidReserve.Trace.IsEmpty && invalidReserve.Events.IsEmpty, "reserve data is authenticated before any action");
});
Test("permanent weather crosses the same singles and canonical recipe boundary", () => {
    foreach (var kind in Enum.GetValues<SourcePermanentWeatherKind>())
    foreach (var pair in new[] { ("SQUIRTLE", "WATER_GUN"), ("BULBASAUR", "SYNTHESIS"), ("CHARMANDER", "EMBER") }) {
        var player = Mon(pair.Item1, 30, 0, pair.Item2);
        player = player with { Pokemon = player.Pokemon with { CurrentHp = Math.Max(1, player.Pokemon.Stats.MaxHp / 4) } };
        var foe = Mon("RATTATA", 100, 0, "TAIL_WHIP");
        var entry = WildBattleRules.StartSourceWild(mechanics, BattleRulesProfile.CanonicalSourceSingles, [player], 0, foe, new(31));
        Check(entry.Succeeded, "canonical entry " + entry.Fault);
        var state = entry.State! with { PermanentWeather = new(kind), RainTurnsRemaining = 0, SandstormTurnsRemaining = 0, CalledField = null };
        Check(WildBattleRules.Validate(canonical, state) is null, "controlled permanent weather input valid");
        var expected = WildBattleRules.ResolveSourceMoveAction(canonical, state, new(WildBattleSide.Player, 0));
        Check(expected.Succeeded, "shared singles action " + expected.Fault);
        var imported = SinglesBattleAdapter.Import(canonical, state);
        Check(imported.Succeeded, "weather frame import " + imported.Fault);
        int target = canonical.Moves[Id(pair.Item2)].Target == "MOVE_TARGET_USER" ? 0 : 1;
        var actual = SourceBattleKernel.ResolveAction(canonical, imported.Frame!, new(imported.Frame!.Slots[0].Occupant!.Value, imported.Frame.Revision, 0, new(target)));
        Check(actual.Succeeded, "canonical weather action " + actual.Fault);
        var exported = SinglesBattleAdapter.Export(actual.State);
        Check(exported.Succeeded, "weather frame export " + exported.Fault);
        Check(Json(actual.Trace) == Json(expected.Battle.Trace), "weather RNG trace");
        Check(Json(actual.Events.Select(SourceBattleEventAdapter.Export).ToImmutableArray()) == Json(expected.Battle.Events), "weather ordered events");
        Check(Json(exported.State) == Json(expected.State), "weather exact state");
        Check(actual.State.Field.PermanentWeather?.Kind == kind, "single action retains sole permanent field authority");
    }
});
Console.WriteLine(Json(new { groups, checks, failures, parity, runtime = Environment.Version.ToString() }));
return failures == 0 && groups > 0 && parity > 0 ? 0 : 1;
