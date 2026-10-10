using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain;
using Game.Domain.Capture;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length is < 1 or > 2 || args.Length == 2 && args[1] != "--boundaries-only") throw new ArgumentException("repository root [--boundaries-only]");
bool runStress = args.Length == 1;
string root = Path.GetFullPath(args[0]);
int checks = 0, groups = 0, failed = 0, turns = 0, calls = 0;
void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
void Test(string label, Action test) { groups++; try { test(); Console.WriteLine("PASS " + label); } catch (Exception e) { failed++; Console.WriteLine("FAIL " + label + " " + e); } }
var raw = PretRocketHideoutPokemon.Load(root).Catalog;
var extra = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules: true);
var prior = PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(raw with { Moves = raw.Moves.SetItems(extra.Moves) }, root, root).Catalog, root).Catalog, root).Catalog;
var catalog = PretMetronomeMoveCatalog.Load(root).Extend(PretFuchsiaRoutePokemon.Load(prior, root).Catalog);
MoveId Move(string id) => new("pret:MOVE_" + id);
WildBattleCombatant Mon(string species, int level, uint personality, params string[] moves)
{
    var mon = PokemonFactory.Create(catalog, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
    return new(mon with { Moves = moves.Select(id => new PokemonMoveSlot(Move(id), catalog.Moves[Move(id)].Pp, catalog.Moves[Move(id)].Pp)).ToImmutableArray() });
}
WildBattleState Require(WildBattleResult result, string label)
{
    Check(result.Succeeded, label + " " + result.Fault);
    var state = result.State!;
    string? error = WildBattleRules.Validate(catalog, state);
    if (error is not null) File.WriteAllText(Path.Combine(root, "tests/Game.SourceWildTurns.Tests/evidence/invalid-state.json"), JsonSerializer.Serialize(state));
    Check(error is null, label + " validation " + error);
    calls += result.Events.Count(e => e is WildBattleCalledMoveTransitionEvent);
    return state;
}
WildBattleState Start(ImmutableArray<WildBattleCombatant> players, WildBattleCombatant foe, uint seed = 0) =>
    Require(WildBattleRules.StartSourceWild(catalog, players, 0, foe, new(seed)), "start");
WildBattleState Resume(WildBattleState state)
{
    for (int i = 0; i < 8 && state.CalledParty?.PendingSwitch is { } pending; i++)
    {
        int index = pending.LegalPartyIndices[0];
        if (pending.Kind == WildBattleCalledSwitchKind.BatonPass) state = Require(WildBattleRules.ChooseSourceWildParty(catalog, state, index), "Baton choice");
        else
        {
            var next = state.CalledParty.Player[index];
            state = Require(WildBattleRules.ReplaceFaintedPlayer(catalog, state, new(next.Pokemon, HeldItem: WildBattleSourcePartyEffects.PersistentHeldItem(next), Status: next.Status), true), "faint replacement");
        }
    }
    return state;
}

Test("learned Toxic executes, grows each end turn, then Run exits without another action", () =>
{
    var state = Start([Mon("CLEFAIRY", 70, 1, "TOXIC", "SPLASH")], Mon("PIDGEY", 20, 2, "SPLASH"));
    for (int attempt = 0; attempt < 4 && !WildBattleSourceFieldEffects.IsValidToxic(state.Opponent.Status); attempt++)
        state = Require(WildBattleRules.ResolveMove(catalog, state, 0), "Toxic");
    Check(WildBattleSourceFieldEffects.IsValidToxic(state.Opponent.Status), "Toxic inflicted");
    int before = state.Opponent.Pokemon.CurrentHp;
    int count = (int)((state.Opponent.Status >> 8) & 15);
    var splash = WildBattleRules.ResolveMove(catalog, state, 1);
    state = Require(splash, "second toxic turn");
    Check(((state.Opponent.Status >> 8) & 15) == count + 1, "Toxic counter increments exactly once");
    Check(before - state.Opponent.Pokemon.CurrentHp == Math.Max(1, state.Opponent.Pokemon.Stats.MaxHp / 16) * (count + 1), "Toxic damage source fraction");
    var run = WildBattleRules.Run(catalog, state);
    state = Require(run, "run after source action");
    Check(state.Outcome == WildBattleOutcome.Escaped && !run.Events.OfType<WildBattleMoveUsedEvent>().Any(), "run succeeds before opponent");
    Check(!run.Trace.Any(t => t.Purpose.Contains("turn-end")), "successful run no turn-end draw");
});

Test("Baton Pass pauses real turn, rejects unrelated actions, then opponent attacks real reserve", () =>
{
    var state = Start([Mon("CLEFAIRY", 70, 1, "TACKLE", "SPLASH", "GROWL", "BATON_PASS"), Mon("PIDGEY", 50, 2, "TACKLE")], Mon("PIDGEY", 10, 3, "TACKLE"));
    var baton = WildBattleRules.ResolveMove(catalog, state, 3);
    state = Require(baton, "Baton");
    Check(state.CalledParty!.PendingSwitch?.Kind == WildBattleCalledSwitchKind.BatonPass, "required party selection");
    Check(state.Turn == 0 && !baton.Events.OfType<WildBattleMoveUsedEvent>().Any(e => e.Actor == WildBattleSide.Opponent), "queued opponent waits");
    foreach (var rejected in new[] { WildBattleRules.Run(catalog, state), WildBattleRules.ResolveBall(catalog, state, CaptureBall.Poke), WildBattleRules.ResolveMove(catalog, state, 0), WildBattleRules.ChooseSourceWildParty(catalog, state, 0) })
        Check(!rejected.Succeeded && ReferenceEquals(rejected.State, state) && rejected.Events.IsEmpty && rejected.Trace.IsEmpty, "pending choice rejection is atomic");
    var choose = WildBattleRules.ChooseSourceWildParty(catalog, state, 1);
    state = Require(choose, "choose real reserve");
    Check(state.Turn == 1 && state.CalledParty!.PlayerActiveIndex == 1 && state.Player.Pokemon.SourceSpeciesId == 16, "real reserve active after completed turn");
    Check(choose.Events.OfType<WildBattleMoveUsedEvent>().Count(e => e.Actor == WildBattleSide.Opponent) == 1, "exactly one queued opponent move");
    Check(state.CalledParty!.Player[0].Pokemon.Moves[3].CurrentPp == catalog.Moves[Move("BATON_PASS")].Pp - 1, "outgoing PP retained");
});

Test("source move then capture failure/success preserves turn and actual wild identity", () =>
{
    bool failedThrow = false, caught = false;
    for (uint seed = 0; seed < 100 && (!failedThrow || !caught); seed++)
    {
        var state = Start([Mon("CLEFAIRY", 80, 1, "TOXIC")], Mon("PIDGEY", 50, 2, "SPLASH"), seed);
        state = Require(WildBattleRules.ResolveMove(catalog, state, 0), "pre-capture source move");
        var target = WildBattleTransformationRules.PersistentPokemon(state.Opponent);
        int turn = state.Turn;
        var thrown = WildBattleRules.ResolveBall(catalog, state, CaptureBall.Great);
        state = Require(thrown, "source capture");
        if (state.Outcome == WildBattleOutcome.Caught)
        {
            caught = true;
            Check(state.Turn == turn && !thrown.Events.OfType<WildBattleMoveUsedEvent>().Any(), "catch skips opponent and next turn");
            Check(state.Opponent.Pokemon.Personality == target.Personality && state.Opponent.Pokemon.CurrentHp == target.CurrentHp, "actual wounded captured instance");
        }
        else
        {
            failedThrow = true;
            Check(state.Turn == turn + 1 && thrown.Events.OfType<WildBattleMoveUsedEvent>().Count(e => e.Actor == WildBattleSide.Opponent) == 1, "failed ball executes one source response and end turn");
        }
    }
    Check(failedThrow && caught, "both capture boundaries covered");
});

Test("wild faint replacement is genuine, cancels queued action, and resumes one end turn", () =>
{
    var state = Start([Mon("PIDGEY", 1, 1, "TACKLE"), Mon("CLEFAIRY", 70, 2, "TACKLE")], Mon("PIDGEY", 60, 3, "TACKLE"));
    var hit = WildBattleRules.ResolveMove(catalog, state, 0, true);
    state = Require(hit, "lethal enemy action");
    Check(state.AwaitingReplacement && state.CalledParty!.PendingSwitch?.Kind == WildBattleCalledSwitchKind.FaintedReplacement, "standard faint choice visible");
    Check(!WildBattleRules.ReplaceFaintedPlayer(catalog, state, Mon("CLEFAIRY", 71, 2, "TACKLE"), true).Succeeded, "forged reserve rejected");
    state = Resume(state);
    Check(state.Turn == 1 && !state.AwaitingReplacement && state.CalledParty!.PlayerActiveIndex == 1, "replacement completes original turn");
});

Test("ordinary legacy Fuchsia owner still rejects called full-turn state", () =>
{
    var player = Mon("CLEFAIRY", 50, 1, "TOXIC"); var foe = Mon("PIDGEY", 20, 2, "TACKLE");
    var old = WildBattleRules.Start(catalog, player, foe, new(0), WildBattleRules.FuchsiaRulesVersion);
    Check(old.Succeeded && !WildBattleRules.ResolveMove(catalog, old.State!, 0).Succeeded, "legacy learned Toxic remains unselectable");
    var state = Start([Mon("CLEFAIRY", 50, 1, "TACKLE")], foe);
    Check(WildBattleRules.Validate(catalog, state with { SourceWild = null }) == "wild_battle_called_party_requires_contract", "opt-in marker required");
    Check(!WildBattleRules.StartSourceWild(catalog, [player], 0, Mon("BULBASAUR", 50, 3, "TACKLE"), new(0)).Succeeded, "not an unrestricted wild roster");
    Check(!WildBattleRules.StartSourceWild(catalog with { Moves = catalog.Moves.Remove(Move("METRONOME")) },
        [Mon("CLEFAIRY", 50, 1, "TACKLE")], 0, foe, new(0)).Succeeded, "incomplete source move catalog rejected");
});

Test("by-index switching distinguishes identical owned records", () =>
{
    var twin = Mon("CLEFAIRY", 50, 2, "SPLASH");
    var state = Start([Mon("CLEFAIRY", 50, 1, "SPLASH"), twin, twin], Mon("PIDGEY", 20, 3, "SPLASH"));
    Check(!WildBattleRules.SwitchPlayer(catalog, state, twin, true).Succeeded, "ambiguous legacy identity rejected");
    state = Require(WildBattleRules.SwitchSourceWildPlayer(catalog, state, 2), "exact owned index switch");
    Check(state.CalledParty!.PlayerActiveIndex == 2, "selected second identical reserve");
});

Test("source Transform at slot three reads a now-empty move slot as priority zero", () =>
{
    var state = Start([Mon("DITTO", 70, 1, "TACKLE", "SPLASH", "GROWL", "TRANSFORM")], Mon("PIDGEY", 20, 3, "QUICK_ATTACK"));
    var moved = WildBattleRules.ResolveMove(catalog, state, 3);
    state = Require(moved, "slot three Transform");
    Check(state.Player.Transform is not null && state.Player.Pokemon.Moves.Length == 1 && state.Turn == 1, "transformed into actual one-move foe and completed turn");
    Check(moved.Events.OfType<WildBattleOrderEvent>().Last(e => e.Phase == WildBattleOrderPhase.EndTurn).First == WildBattleSide.Opponent,
        "emptied slot three has MOVE_NONE priority, rather than reading copied slot zero");
    state = Start([Mon("DITTO", 70, 1, "TRANSFORM") with { SpeedBadgeBoost = true }], Mon("PIDGEY", 20, 3, "QUICK_ATTACK"));
    moved = WildBattleRules.ResolveMove(catalog, state, 0);
    state = Require(moved, "slot zero Transform");
    Check(moved.Events.OfType<WildBattleOrderEvent>().Last(e => e.Phase == WildBattleOrderPhase.EndTurn).First == WildBattleSide.Player,
        "present copied slot zero uses its current Quick Attack priority; badge-boosted player wins speed comparison");
});

Test("residual faint resumes delayed boundary without repeating poison", () =>
{
    var active = Mon("CLEFAIRY", 50, 1, "SPLASH");
    active = active with { Pokemon = active.Pokemon with { CurrentHp = 1 }, Status = 0x80 };
    var state = Start([active, Mon("CLEFAIRY", 50, 2, "SPLASH")], Mon("PIDGEY", 20, 3, "SPLASH"));
    state = Require(WildBattleRules.ResolveMove(catalog, state, 0, true), "Toxic residual faint");
    Check(state.AwaitingReplacement && state.SourceWild!.Turn!.EndTurn is not null, "paused after normal residuals");
    var replacement = WildBattleRules.ReplaceSourceWildPlayer(catalog, state, 1);
    state = Require(replacement, "resume delayed phase");
    Check(state.Turn == 1 && !replacement.Events.OfType<WildBattleStatusDamageEvent>().Any(), "no repeated status residual");
});

Test("Ghost and scripted Snorlax entry policies retain capture and RNG contracts", () =>
{
    var player = Mon("CLEFAIRY", 50, 1, "METRONOME");
    var ghost = Require(WildBattleRules.StartSourceWild(catalog, [player], 0, Mon("GASTLY", 20, 2, "LICK"), new(0),
        WildBattleTerrain.Building, towerKind: PokemonTowerBattleKind.DisguisedWild), "ghost source entry");
    var ball = WildBattleRules.ResolveBall(catalog, ghost, CaptureBall.Great);
    ghost = Require(ball, "ghost dodges ball");
    Check(ball.Events.OfType<WildBattleTowerBallDodgedEvent>().Count() == 1 && !ball.Events.OfType<WildBattleCaptureResultEvent>().Any(), "ghost dodges before capture calculation");
    Check(Require(WildBattleRules.Run(catalog, ghost), "ghost run").Outcome == WildBattleOutcome.Escaped, "ghost guaranteed escape");
    var made = SnorlaxBattleContract.Create(catalog, 42, new(7), true);
    var snorlax = Require(WildBattleRules.StartSourceWild(catalog, [player], 0, new(made.Pokemon), made.Rng, scriptedSnorlax: true), "scripted source Snorlax");
    Check(snorlax.Opponent.Pokemon == made.Pokemon && snorlax.Rng.Cursor == made.Rng.Cursor + 6, "already generated Snorlax retained and only setup draws consumed");
    Check(snorlax.Opponent.HeldItem == "ITEM_CHESTO_BERRY", "source fixed Snorlax item");
    Check(!WildBattleRules.StartSourceWild(catalog, [player], 0, Mon("SNORLAX", 30, 1, "SPLASH"), new(0), scriptedSnorlax: true).Succeeded, "forged scripted roster rejected");
});

Test("source medicine and Poké Flute synchronize every actual owned party member", () =>
{
    var campaign = PretCampaignCatalog.LoadFuchsiaGymProfile(root);
    var content = campaign.PokemonContent!; var shop = campaign.EarlyShop!; var opening = campaign.OpeningItems!;
    ItemId Item(string id) => new("pret:ITEM_" + id);
    InventoryState Bag(string item) => new([new(Item(item), 2)], [], shop.Limits) { KeyItems = [new(PokemonTowerItemRules.PokeFluteId, 1)] };
    ImmutableArray<PartyPokemon> Owned(WildBattleState state) => state.CalledParty!.Player.Select((m, i) =>
        new PartyPokemon(i + 100, m.Pokemon, "PARTNER", "RED", 0, 2, "MAPSEC_FUCHSIA_CITY", 30, 4, "ITEM_POKE_BALL", WildBattleSourcePartyEffects.PersistentHeldItem(m)) { Status = m.Status }).ToImmutableArray();
    var active = Mon("CLEFAIRY", 50, 1, "SPLASH");
    var bench = Mon("CLEFAIRY", 50, 2, "SPLASH");
    var state = Start([active, bench with { Pokemon = bench.Pokemon with { CurrentHp = 10 }, Status = 0x80 }], Mon("PIDGEY", 20, 3, "SPLASH"));
    var party = Owned(state); var bag = Bag("POTION");
    var potion = WildBattleRules.ResolveMedicine(shop, opening, content, state, bag, party, 0, new(Item("POTION"), 101));
    state = Require(potion.Battle, "reserve source Potion");
    Check(potion.Party[1].Pokemon.CurrentHp == 30 && potion.Party[1].Status == 0x80, "bench HP recovered with true Toxic kind retained");
    Check(state.CalledParty!.Player[1].Pokemon.CurrentHp == 30 && state.CalledParty.Player[1].Status == 0x80, "actual source bench synchronized");
    Check(potion.Battle.Events.OfType<WildBattleMoveUsedEvent>().Count(e => e.Actor == WildBattleSide.Opponent) == 1, "one response to item");
    var antidote = WildBattleRules.ResolveMedicine(shop, opening, content, state, Bag("ANTIDOTE"), potion.Party, 0, new(Item("ANTIDOTE"), 101));
    state = Require(antidote.Battle, "reserve source Antidote");
    Check(antidote.Party[1].Status == 0 && state.CalledParty!.Player[1].Status == 0, "real reserve Toxic cured");
    var sleeper = Mon("CLEFAIRY", 50, 2, "SPLASH") with { Status = 3 };
    state = Start([active, sleeper], Mon("PIDGEY", 20, 3, "SPLASH") with { Status = 2 });
    var flute = WildBattleRules.ResolvePokeFlute(shop, content, state, Bag("POTION"), Owned(state), 0);
    state = Require(flute.Battle, "source flute");
    Check(flute.Party[1].Status == 0 && state.CalledParty!.Player[1].Status == 0 && state.Opponent.Status == 0, "flute wakes actual reserve and wild foe");
    var bad = Owned(state).SetItem(1, Owned(state)[1] with { Pokemon = Owned(state)[1].Pokemon with { CurrentHp = 10 } });
    var rejected = WildBattleRules.ResolveMedicine(shop, opening, content, state, bag, bad, 0, new(Item("POTION"), 101));
    Check(!rejected.Succeeded && ReferenceEquals(rejected.Battle.State, state) && ReferenceEquals(rejected.Inventory, bag), "stale reserve item context rejects atomically");
});

if (runStress) Test("actual unfiltered Metronome turns keep full owned parties over broad RNG states", () =>
{
    var distinct = new HashSet<MoveId>(); bool actualBaton = false;
    for (uint seed = 0; seed < 512; seed++)
    {
        var state = Start([Mon("CLEFAIRY", 70, 1, "METRONOME", "TACKLE"), Mon("CLEFAIRY", 70, 2, "METRONOME", "TACKLE"), Mon("CLEFAIRY", 70, 3, "METRONOME", "TACKLE")], Mon("PIDGEY", 60, 5, "SPLASH"), seed);
        for (int step = 0; step < 4 && state.Outcome == WildBattleOutcome.Ongoing; step++)
        {
            actualBaton |= state.CalledParty?.PendingSwitch?.Kind == WildBattleCalledSwitchKind.BatonPass;
            state = Resume(state);
            if (state.Outcome != WildBattleOutcome.Ongoing) break;
            int slot = state.Player.CalledTurnState?.Lock?.PpSlot ?? WildBattleRules.ForcedMoveSlot(state.Player) ?? Enumerable.Range(-1, state.Player.Pokemon.Moves.Length + 1)
                .First(i => WildBattleRules.ValidateSourceMoveSelection(catalog, state, WildBattleSide.Player, i) is null);
            var action = WildBattleRules.ResolveMove(catalog, state, slot, true);
            if (!action.Succeeded) File.WriteAllText(Path.Combine(root, "tests/Game.SourceWildTurns.Tests/evidence/last-fault.json"), JsonSerializer.Serialize(new { seed, step, slot, action.Fault, state }));
            state = Require(action, $"Metronome seed {seed} step {step}"); turns++;
            foreach (var chosen in action.Events.OfType<WildBattleCallerSelectionEvent>().Where(e => e.Caller == Move("METRONOME") && e.Result is not null)) distinct.Add(chosen.Result!.Value);
            Check(state.CalledParty!.Player.Length == 3, "real full party retained");
        }
    }
    Check(distinct.Count > 250, "broad unfiltered selected pool " + distinct.Count);
    Check(actualBaton, "actual RNG-selected Baton Pass reached");
    Console.WriteLine(JsonSerializer.Serialize(new { metronomeDistinct = distinct.Count, actualBaton }));
});

Console.WriteLine(JsonSerializer.Serialize(new { passed = failed == 0, groups, checks, failed, turns, calls, runtime = Environment.Version.ToString() }));
return failed == 0 && groups > 0 && checks > 0 ? 0 : 1;
