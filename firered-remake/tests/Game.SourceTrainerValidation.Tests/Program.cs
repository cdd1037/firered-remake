using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
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
TrainerId Trainer(string name) => new("pret:TRAINER_" + name);
var original = PretRocketHideoutPokemon.Load(root).Catalog;
var supplement = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules: true);
var prior = original with { Moves = original.Moves.SetItems(supplement.Moves) };
var old = PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior, root, root).Catalog, root).Catalog, root).Catalog;
var route = PretFuchsiaRoutePokemon.Load(old, root);
var catalog = PretMetronomeMoveCatalog.Load(root).Extend(route.Catalog);
var loadedGym = PretFuchsiaGymTrainers.Load(root);
var gym = new FuchsiaGymTrainerCatalog(loadedGym.SourceCommit, loadedGym.Profile, loadedGym.CompleteSourceRoster, loadedGym.Trainers, loadedGym.Battles);
PokemonInstance Pokemon(string name = "CLEFAIRY", int level = 50) => PokemonFactory.Create(catalog,
    new(new("pret:SPECIES_" + name), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
WildBattleCombatant Player(string name = "CLEFAIRY") => new(Pokemon(name) with {
    Moves = [new(Move("METRONOME"), catalog.Moves[Move("METRONOME")].Pp, catalog.Moves[Move("METRONOME")].Pp)]
});
WildBattleCalledPartyMember Member(WildBattleCombatant mon) => new(WildBattleTransformationRules.PersistentPokemon(mon),
    mon.HeldItem, WildBattleTransformationRules.PersistentStatus(mon), KnockedOffItem: mon.KnockedOffItem);
ForestTrainerCombatState State(string trainer = "BEAUTY_SHEILA", bool koga = false)
{
    var created = koga ? FuchsiaGymTrainerPartyFactory.Create(gym, catalog, Trainer(trainer), new(42)) :
        FuchsiaRouteTrainerPartyFactory.Create(route.Trainers, catalog, Trainer(trainer), new(42));
    var opponents = created.Party.Select(m => new WildBattleCombatant(m.Pokemon) { HeldItem = m.HeldItem, FirstTurnCounter = 2 }).ToImmutableArray();
    ImmutableArray<WildBattleCombatant?> players = [Player() with { FirstTurnCounter = 2 }, Player("RATTATA")];
    var battle = new WildBattleState(players[0]!, opponents[0], 0, 0, 0, created.Rng, 0, WildBattleOutcome.Ongoing,
        RulesVersion: WildBattleRules.FuchsiaRulesVersion) {
        Terrain = WildBattleTerrain.Building,
        CalledParty = new(players.Select(m => Member(m!)).ToImmutableArray(), opponents.Select(Member).ToImmutableArray(), 0, 0, WildBattleCalledBattleKind.Trainer)
    };
    return new(Trainer(trainer), battle, opponents, 0, HasPlayerReserves: true, StaleCurrentMove: new("pret:MOVE_NONE"),
        RulesVersion: koga ? ForestTrainerCombatRules.KogaRulesVersion : ForestTrainerCombatRules.FuchsiaRulesVersion) {
        SourceMoveRulesVersion = ForestTrainerCombatRules.SourceTrainerContractVersion, SsAnnePlayerParty = players
    };
}
ForestTrainerCombatState Sync(ForestTrainerCombatState state, WildBattleState battle)
{
    var party = battle.CalledParty!;
    var players = party.Player.Select(m => (WildBattleCombatant?)new WildBattleCombatant(m.Pokemon, HeldItem: m.HeldItem, Status: m.Status) { KnockedOffItem = m.KnockedOffItem }).ToImmutableArray();
    var opponents = party.Opponent.Select(m => new WildBattleCombatant(m.Pokemon, HeldItem: m.HeldItem, Status: m.Status) { KnockedOffItem = m.KnockedOffItem }).ToImmutableArray();
    return state with { Battle = battle, OpponentIndex = party.OpponentActiveIndex, CeruleanPlayerPartyIndex = party.PlayerActiveIndex,
        OpponentParty = opponents.SetItem(party.OpponentActiveIndex, battle.Opponent), SsAnnePlayerParty = players.SetItem(party.PlayerActiveIndex, battle.Player),
        HasPlayerReserves = party.Player.Where((_, i) => i != party.PlayerActiveIndex).Any(m => m.Pokemon.CurrentHp > 0) };
}
ForestTrainerCombatState SetMon(ForestTrainerCombatState state, WildBattleSide side, int index, WildBattleCombatant mon)
{
    var battle = state.Battle; var party = battle.CalledParty!;
    if (side == WildBattleSide.Player) {
        party = party with { Player = party.Player.SetItem(index, Member(mon)) };
        state = state with { SsAnnePlayerParty = state.SsAnnePlayerParty!.Value.SetItem(index, mon) };
        if (index == party.PlayerActiveIndex) battle = battle with { Player = mon };
    } else {
        party = party with { Opponent = party.Opponent.SetItem(index, Member(mon)) };
        state = state with { OpponentParty = state.OpponentParty.SetItem(index, mon) };
        if (index == party.OpponentActiveIndex) battle = battle with { Opponent = mon };
    }
    return state with { Battle = battle with { CalledParty = party } };
}
void Valid(ForestTrainerCombatState state, string label) => Check(ForestTrainerCombatRules.Validate(catalog, state) is null,
    label + ": " + ForestTrainerCombatRules.Validate(catalog, state));
void Invalid(ForestTrainerCombatState state, string label, string? expected = null) {
    string? error = ForestTrainerCombatRules.Validate(catalog, state);
    Check(error is not null && (expected is null || error == expected), label + ": " + error);
}
ForestTrainerCombatState Called(ForestTrainerCombatState state, string move, WildBattleSide side = WildBattleSide.Player, int slot = 0)
{
    var result = WildBattleRules.ResolveCalledMoveAction(catalog, state.Battle, new(side, slot, Move(move)));
    Check(result.Succeeded, "actual " + move + ": " + result.Fault);
    var after = Sync(state, result.State!);
    return move == "BATON_PASS" ? after with {
        SourceTurn = new(side, side == WildBattleSide.Player ? slot : 0, side == WildBattleSide.Opponent ? slot : 0, 1),
        Battle = after.Battle with { ForestPlayerMoveSlot = side == WildBattleSide.Player ? slot : 0,
            ForestOpponentMoveSlot = side == WildBattleSide.Opponent ? slot : 0 }
    } : after;
}

Test("exact source all 37 route singles including natural Metronome rosters", () => {
    Check(Environment.Version.Major == 10 && catalog.Species.Count == 107, "actual runtime and profile");
    foreach (var trainer in FuchsiaRouteTrainerPartyFactory.SupportedTrainers) Valid(State(trainer.Value[13..]), trainer.Value);
    Valid(State(), "Sheila actual Clefairy roster"); Valid(State("PICNICKER_CELIA"), "Celia actual Clefairy roster");
    Check(State().OpponentParty[0].Pokemon.Moves[3].Move == Move("METRONOME"), "Sheila source slot retained");
    Check(State("PICNICKER_CELIA").OpponentParty[0].Pokemon.Moves[2].Move == Move("METRONOME"), "Celia source slot retained");
});
Test("exact source seven gym trainer rosters", () => {
    foreach (var trainer in FuchsiaGymTrainerPartyFactory.SupportedTrainers) Valid(State(trainer.Value[13..], true), trainer.Value);
});
Test("earlier Fuchsia-profile trainer rosters reuse immutable validation after real source checks", () => {
    var forest = ForestTrainerCombatRules.Start(PretForestTrainers.Load(root), catalog, ForestTrainerPartyFactory.SupportedTrainers[0], Player(), new(42), fuchsiaRules: true);
    var brock = ForestTrainerCombatRules.Start(PretPewterTrainers.Load(root), catalog, PewterTrainerPartyFactory.Brock, Player(), new(42), fuchsiaRules: true);
    foreach (var entry in new[] { forest, brock }) {
        Check(entry.Succeeded, "historical entry: " + entry.Fault);
        var source = entry.State!; Valid(source, "historical state unchanged");
        ImmutableArray<WildBattleCombatant?> players = [source.Battle.Player, Player("RATTATA")];
        source = source with { SourceMoveRulesVersion = ForestTrainerCombatRules.SourceTrainerContractVersion,
            SsAnnePlayerParty = players, HasPlayerReserves = true,
            Battle = source.Battle with { CalledParty = new(players.Select(m => Member(m!)).ToImmutableArray(),
                source.OpponentParty.Select(Member).ToImmutableArray(), 0, 0, WildBattleCalledBattleKind.Trainer) } };
        Valid(source, "same actual earlier trainer admitted by explicit source marker");
        Valid(Called(source, "TRANSFORM"), "actual earlier-trainer Transform keeps original immutable roster");
        Valid(SetMon(source, WildBattleSide.Opponent, 0, source.Battle.Opponent with { HeldItem = "ITEM_ORAN_BERRY" }), "mutable held item after source validation");
        var changed = source.Battle.Opponent with { Pokemon = source.Battle.Opponent.Pokemon with { Friendship = 1 } };
        Invalid(SetMon(source, WildBattleSide.Opponent, 0, changed), "earlier source immutable roster remains strict");
    }
});
Test("current source marker is exact and legacy remains closed", () => {
    var state = State();
    Invalid(state with { SourceMoveRulesVersion = "firered-source-single-trainer-v0" }, "unknown marker");
    Invalid(state with { SourceMoveRulesVersion = "" }, "empty marker");
    Invalid(state with { SourceMoveRulesVersion = null }, "old v1 cannot carry source party");
    Invalid(state with { RulesVersion = ForestTrainerCombatRules.RulesVersion }, "old profile cannot be relabeled source");
    Invalid(state with { Battle = state.Battle with { CalledParty = null } }, "actual party required");
    Invalid(state with { SsAnnePlayerParty = null }, "real player party required");
    var ordinary = State("FISHERMAN_NED");
    Invalid(ordinary with { SourceMoveRulesVersion = null }, "ordinary old trainer cannot carry a new party extension");
    Invalid(ordinary with { SourceMoveRulesVersion = null, Battle = ordinary.Battle with { CalledParty = null, CalledField = new() } }, "ordinary old trainer cannot carry a new field extension");
    Invalid(ordinary with { SourceMoveRulesVersion = null, SourceTurn = new(WildBattleSide.Player, 0, 0),
        Battle = ordinary.Battle with { CalledParty = null } }, "ordinary old trainer cannot carry a source queue");
});
Test("actual called history and ability/type overlays survive validation without replacing party identities", () => {
    var state = State();
    var role = Called(state, "ROLE_PLAY"); Valid(role, "real Role Play source history");
    Check(role.Battle.Player.Pokemon.AbilityId == state.Battle.Player.Pokemon.AbilityId, "base ability immutable");
    Check(role.Battle.Player.SourceMoveHistory is not null && role.Battle.Player.CalledOverlay is not null, "real extended registers");
    Valid(Called(state, "CONVERSION"), "real Conversion overlay");
    var malformed = SetMon(role, WildBattleSide.Player, 0, role.Battle.Player with { CalledOverlay = new() { Ability = "ABILITY_FORECAST" } });
    Invalid(malformed, "unavailable ability provenance", "called_overlay_ability_provenance");
    Invalid(SetMon(role, WildBattleSide.Player, 0, role.Battle.Player with { CalledOverlay = new() { Type1 = "TYPE_FIRE" } }), "half type overlay", "called_overlay_type");
    Invalid(SetMon(role, WildBattleSide.Player, 0, role.Battle.Player with { SourceMoveHistory = role.Battle.Player.SourceMoveHistory! with { RulesVersion = "old" } }), "history marker", "called_move_history_version");
});
Test("Transform authenticates original roster and copied view independently including JSON round trip", () => {
    var state = State(); var transformed = Called(state, "TRANSFORM", WildBattleSide.Opponent, 3);
    Valid(transformed, "genuine opponent Transform");
    Check(transformed.Battle.Opponent.Transform is not null, "source transformed");
    Check(WildBattleTransformationRules.PersistentPokemon(transformed.Battle.Opponent).Personality == state.Battle.Opponent.Pokemon.Personality, "original personality");
    var encoded = JsonSerializer.Serialize(transformed);
    Valid(JsonSerializer.Deserialize<ForestTrainerCombatState>(encoded)!, "independently deserialized immutable arrays");
    var t = transformed.Battle.Opponent.Transform!;
    Invalid(SetMon(transformed, WildBattleSide.Opponent, 0, transformed.Battle.Opponent with {
        Transform = t with { OriginalPokemon = t.OriginalPokemon with { Personality = t.OriginalPokemon.Personality + 1 } }
    }), "mutated Transform original");
    Invalid(SetMon(transformed, WildBattleSide.Opponent, 0, transformed.Battle.Opponent with {
        Pokemon = transformed.Battle.Opponent.Pokemon with { AbilityId = "ABILITY_LEVITATE" }
    }), "mutated copied view");
});
Test("toxic active and reserve status remain actual and malformed poison is rejected", () => {
    var state = State();
    var poisoned = SetMon(state, WildBattleSide.Player, 0, state.Battle.Player with { Status = 0x380 });
    Valid(poisoned with { Battle = poisoned.Battle with { CalledField = new() } }, "actual active toxic counter");
    var reserve = SetMon(state, WildBattleSide.Player, 1, state.SsAnnePlayerParty!.Value[1]! with { Status = 0x180 });
    Valid(reserve, "actual reserve toxic");
    var sleepingInsomnia = SetMon(state, WildBattleSide.Player, 1, Player("DROWZEE") with { Status = 3 });
    Valid(sleepingInsomnia, "inactive original Insomnia does not erase status gained under Skill Swap");
    Invalid(SetMon(state, WildBattleSide.Player, 1, state.SsAnnePlayerParty!.Value[1]! with { Status = 0x188 }), "mixed toxic bits");
    var fainted = state.SsAnnePlayerParty!.Value[1]! with { Pokemon = state.SsAnnePlayerParty.Value[1]!.Pokemon with { CurrentHp = 0 }, Status = 0x80 };
    Invalid(SetMon(state, WildBattleSide.Player, 1, fainted), "fainted reserve cannot keep status", "forest_trainer_fainted_status");
});
Test("EXP-first and player replacement phases project only kernel phase, never HP or roster", () => {
    var state = State();
    var deadOpponent = new WildBattleCombatant(state.Battle.Opponent.Pokemon with { CurrentHp = 0 });
    var experience = SetMon(state, WildBattleSide.Opponent, 0, deadOpponent) with { Phase = ForestTrainerCombatPhase.AwaitingExperience };
    Valid(experience, "fainted opponent awaiting actual EXP");
    Check(experience.Battle.CalledParty!.PendingSwitch is null && experience.Battle.Outcome == WildBattleOutcome.Ongoing, "validation did not mutate actual phase");
    Invalid(experience with { Phase = ForestTrainerCombatPhase.Ready }, "no Ready with fainted opponent");
    Invalid(experience with { ExperienceCompletedMask = 1 }, "public contract cannot publish the internal completed-EXP selection boundary", "forest_trainer_phase_hp");
    var shift = experience with { Phase = ForestTrainerCombatPhase.AwaitingShiftChoice, ExperienceCompletedMask = 1, PendingOpponentIndex = 1 };
    Valid(shift, "real shift phase with original fainted opponent");
    var deadPlayer = new WildBattleCombatant(state.Battle.Player.Pokemon with { CurrentHp = 0 });
    var replacement = SetMon(state, WildBattleSide.Player, 0, deadPlayer) with { Phase = ForestTrainerCombatPhase.AwaitingPlayerReplacement };
    replacement = replacement with { Battle = replacement.Battle with { AwaitingReplacement = true } };
    Valid(replacement, "actual fainted player and living reserve");
    Invalid(replacement with { HasPlayerReserves = false }, "reserve claim cannot lie", "source_trainer_reserves");
    var celia = State("PICNICKER_CELIA");
    var finalExperience = SetMon(celia, WildBattleSide.Opponent, 0, new(celia.Battle.Opponent.Pokemon with { CurrentHp = 0 })) with { Phase = ForestTrainerCombatPhase.AwaitingExperience };
    Valid(finalExperience, "last enemy still waits for EXP");
    Valid(finalExperience with { Phase = ForestTrainerCombatPhase.Ended, ExperienceCompletedMask = 1,
        Battle = finalExperience.Battle with { Outcome = WildBattleOutcome.Won } }, "true final outcome");
    Invalid(finalExperience with { Battle = finalExperience.Battle with { Outcome = WildBattleOutcome.Won } }, "terminal cannot skip EXP phase contract");
});
Test("real Baton pending survives and malformed replacement data is not repaired", () => {
    var state = State(); var baton = Called(state, "BATON_PASS");
    Valid(baton, "real paused Baton invocation");
    Check(baton.Battle.CalledParty!.PendingSwitch?.Kind == WildBattleCalledSwitchKind.BatonPass, "real Baton boundary");
    Invalid(baton with { SourceTurn = null }, "paused Baton requires its actual source queue", "source_trainer_turn_phase");
    Invalid(baton with { SourceTurn = baton.SourceTurn! with { NextAction = 0 } }, "Baton requires an executed queue action", "source_trainer_turn_phase");
    Invalid(state with { SourceTurn = new(WildBattleSide.Player, 0, 0) }, "ordinary Ready has no suspended turn", "source_trainer_turn_phase");
    var party = baton.Battle.CalledParty!;
    Invalid(baton with { Battle = baton.Battle with { CalledParty = party with {
        PendingSwitch = party.PendingSwitch! with { LegalPartyIndices = [1, 0] }
    } } }, "malformed Baton indices rejected", "called_party_pending_switch");
    var deadOpponent = new WildBattleCombatant(state.Battle.Opponent.Pokemon with { CurrentHp = 0 });
    var experience = SetMon(state, WildBattleSide.Opponent, 0, deadOpponent) with { Phase = ForestTrainerCombatPhase.AwaitingExperience };
    var delayed = experience with { SourceTurn = new(WildBattleSide.Player, 0, 0, 2) {
        EndTurn = new(experience.Battle.Turn, WildBattleSide.Player, WildBattleSourceEndTurnStage.FutureAttacks, 1, 0, 0)
    } };
    Valid(delayed, "actual delayed end-turn continuation survives EXP boundary");
    Invalid(delayed with { SourceTurn = delayed.SourceTurn! with { EndTurn = delayed.SourceTurn!.EndTurn! with { Turn = experience.Battle.Turn + 1 } } },
        "future turn cursor cannot be substituted", "source_trainer_end_turn");
    Invalid(experience with { Battle = experience.Battle with { CalledParty = experience.Battle.CalledParty! with {
        PendingSwitch = new(WildBattleSide.Opponent, WildBattleCalledSwitchKind.FaintedReplacement, [0])
    } } }, "existing malformed fainted pending never replaced", "called_party_pending_switch");
});
Test("all mirror slots and immutable opponent roster fields remain strict", () => {
    var state = State();
    Invalid(state with { OpponentParty = state.OpponentParty.SetItem(0, state.OpponentParty[0] with { Status = WildBattleStatus.Burn }) }, "live opponent mirror mismatch");
    var party = state.Battle.CalledParty!;
    Invalid(state with { Battle = state.Battle with { CalledParty = party with { Opponent = party.Opponent.SetItem(1,
        party.Opponent[1] with { Pokemon = party.Opponent[1].Pokemon with { CurrentHp = 1 } }) } } }, "inactive party mirror mismatch");
    Invalid(state with { Battle = state.Battle with { CalledParty = party with { PlayerActiveIndex = 1 } } }, "player index mismatch");
    Invalid(state with { Battle = state.Battle with { CalledParty = party with { Player = party.Player.SetItem(1, party.Player[1] with { IsEgg = true }) } } }, "egg cannot impersonate real supplied combatant");
    var swapped = state.OpponentParty.Reverse().ToImmutableArray();
    var swapState = state with { OpponentParty = swapped, Battle = state.Battle with { Opponent = swapped[0],
        CalledParty = party with { Opponent = swapped.Select(Member).ToImmutableArray() } } };
    Invalid(swapState, "order authenticates source roster");
    var altered = state.OpponentParty[1] with { Pokemon = state.OpponentParty[1].Pokemon with { Friendship = 1 } };
    Invalid(SetMon(state, WildBattleSide.Opponent, 1, altered), "source factory immutable friendship", "route22_trainer_party_identity");
    Invalid(SetMon(state, WildBattleSide.Player, 1, state.SsAnnePlayerParty!.Value[1]! with { CalledOverlay = new() { Type1 = "TYPE_FIRE", Type2 = "TYPE_FIRE" } }), "inactive overlay cannot hide");
    var item = SetMon(state, WildBattleSide.Opponent, 1, state.OpponentParty[1] with { HeldItem = "ITEM_ORAN_BERRY" });
    Valid(item, "supported changed item is mutable battle data");
});
Test("Koga source construction identity and item mask cannot be substituted", () => {
    var state = State("LEADER_KOGA", true);
    Valid(state with { CeruleanItemsUsedMask = 7 }, "only three nonempty source trainer items");
    Invalid(state with { CeruleanItemsUsedMask = 8 }, "empty source item slot");
    var reordered = state.OpponentParty.SetItem(0, state.OpponentParty[2]).SetItem(2, state.OpponentParty[0]);
    Invalid(state with { OpponentParty = reordered, Battle = state.Battle with { Opponent = reordered[0],
        CalledParty = state.Battle.CalledParty! with { Opponent = reordered.Select(Member).ToImmutableArray() } } }, "identical-species Koffing remain source-position identities");
    Invalid(State("JUGGLER_KAYDEN", true) with { CeruleanItemsUsedMask = 1 }, "ordinary gym trainer no invented item");
});
Test("replacement AI validates genuine swapped player context before the next phase is published", () => {
    foreach (string trainer in new[] { "JUGGLER_KIRK", "LEADER_KOGA" }) {
        var state = State(trainer, true);
        state = SetMon(state, WildBattleSide.Opponent, 0, new(state.Battle.Opponent.Pokemon with { CurrentHp = 0 }));
        state = SetMon(state, WildBattleSide.Player, 0, new(state.Battle.Player.Pokemon with { CurrentHp = 0 })) with {
            Phase = ForestTrainerCombatPhase.AwaitingPlayerReplacement, ExperienceCompletedMask = 1
        };
        state = state with { Battle = state.Battle with { AwaitingReplacement = true } };
        Valid(state, trainer + " public replacement boundary");
        // Actual source party switch, with only the documented kernel/trainer phase adapter.
        var kernel = state.Battle with { AwaitingReplacement = false, CalledParty = state.Battle.CalledParty! with {
            PendingSwitch = new(WildBattleSide.Player, WildBattleCalledSwitchKind.FaintedReplacement, [1])
        } };
        var switched = WildBattleRules.ResolveCalledPartySwitch(catalog, kernel, new(WildBattleSide.Player, 1));
        Check(switched.Succeeded, trainer + " real source replacement: " + switched.Fault);
        var transient = Sync(state, switched.State!);
        Invalid(transient, trainer + " intermediate phase cannot be published", "forest_trainer_phase_hp");
        string before = JsonSerializer.Serialize(transient);
        var chosen = SourceTrainerAi.ChooseReplacement(catalog, transient);
        Check(chosen.Succeeded && chosen.PartyIndex is > 0, trainer + " internal replacement selection: " + chosen.Diagnostic?.Code);
        Check(JsonSerializer.Serialize(transient) == before, trainer + " AI never alters HP, phase, mask or roster");
        Check(!SourceTrainerAi.ChooseReplacement(catalog, transient with { ExperienceCompletedMask = 256 }).Succeeded,
            trainer + " invalid EXP mask remains rejected inside AI");
        var corrupted = SetMon(transient, WildBattleSide.Opponent, 1, transient.OpponentParty[1] with {
            Pokemon = transient.OpponentParty[1].Pokemon with { Friendship = 1 }
        });
        Check(!SourceTrainerAi.ChooseReplacement(catalog, corrupted).Succeeded, trainer + " malformed immutable reserve still rejected inside AI");
    }
});
Console.WriteLine($"SOURCE TRAINER VALIDATION: groups={groups} assertions={assertions} failures={failures} runtime={Environment.Version}");
if (groups == 0 || assertions == 0 || failures != 0) Environment.Exit(1);
