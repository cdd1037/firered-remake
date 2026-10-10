using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Capture;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

// Controlled source-contract moves on admitted species. This does not import Smeargle or add a natural encounter.
if (args.Length != 2 || Directory.Exists(args[1])) return 2;
string root = Path.GetFullPath(args[0]), output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var content = PretCampaignCatalog.LoadFuchsiaGymProfile(root);
var catalog = content.PokemonContent!.Catalog;
int checks = 0, groups = 0, failures = 0, completeTurns = 0;
var results = new List<object>();
void Check(bool ok, string label) { checks++; if (!ok) throw new InvalidDataException(label); }
void Test(string label, Action run)
{
    groups++;
    try { run(); results.Add(new { label, passed = true }); Console.WriteLine("PASS " + label); }
    catch (Exception e) { failures++; results.Add(new { label, passed = false, error = e.ToString() }); Console.WriteLine("FAIL " + label + " " + e); }
}
MoveId Id(string name) => new("pret:MOVE_" + name);
PokemonMoveSlot Slot(string move, int ups = 0) { int pp = catalog.Moves[Id(move)].Pp; return new(Id(move), pp + pp * 20 * ups / 100, pp + pp * 20 * ups / 100, ups); }
WildBattleCombatant Mon(string species, int level, uint personality, params string[] names)
{
    var p = PokemonFactory.Create(catalog, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(personality)), new(0)).Pokemon;
    return new(p with { Moves = names.Select(name => Slot(name)).ToImmutableArray() });
}
WildBattleCombatant Bonus(WildBattleCombatant mon, int slot, int ups) => mon with { Pokemon = mon.Pokemon with { Moves = mon.Pokemon.Moves.SetItem(slot, Slot(mon.Pokemon.Moves[slot].Move.Value[10..], ups)) } };
WildBattleState Require(WildBattleResult result, string label)
{
    Check(result.Succeeded, label + ": " + result.Fault);
    string? fault = WildBattleRules.IsSourceWild(result.State) ? WildBattleRules.ValidateSourceWild(catalog, result.State!) : WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!);
    Check(fault is null, label + " returned state: " + fault);
    return result.State!;
}
WildBattleState Start(WildBattleCombatant player, WildBattleCombatant opponent, params WildBattleCombatant[] reserves) =>
    Require(WildBattleRules.StartSourceWild(catalog, new[] { player }.Concat(reserves).ToImmutableArray(), 0, opponent, new(7)), "start");
WildBattleState Turn(WildBattleState state, int slot = 0)
{
    var result = WildBattleRules.ResolveMove(catalog, state, slot);
    var next = Require(result, "complete turn");
    Check(next.Turn == state.Turn + 1 || next.Outcome != WildBattleOutcome.Ongoing || next.AwaitingReplacement, "turn reached defined boundary");
    completeTurns++; return next;
}
WildBattleState History(WildBattleState state, string? last, string? printed = null) => state with { Opponent = state.Opponent with {
    LastUsedMove = last is null ? null : Id(last), SourceMoveHistory = new() { LastPrintedMove = printed is null ? last is null ? null : Id(last) : Id(printed) } } };
WildBattleCalledMoveResult Action(WildBattleState state, int slot = 0, WildBattleSide actor = WildBattleSide.Player)
{
    var result = WildBattleRules.ResolveSourceMoveAction(catalog, state, new(actor, slot)); Require(result.Battle, "source action"); return result;
}
void CopyEvent(WildBattleCalledMoveResult result, string copied, bool permanent)
{
    var e = result.Battle.Events.OfType<WildBattleMoveCopiedEvent>().Single();
    Check(e.CopiedMove == Id(copied) && e.Permanent == permanent && e.Replacement.Move == Id(copied), "typed copy event");
    Check(result.Invocation!.ChosenMove == Id("UNAVAILABLE"), "copy command writes chosen unavailable");
}

Test("authenticated recipes and unchanged genuine caller pools", () =>
{
    Check(Environment.Version.Major == 10, "hosted runtime major 10");
    Check(catalog.Moves.Count == 354 && catalog.Moves.Values.Count(WildBattleRules.IsCalledMoveEffectSupported) == 354, "354 distinct recipes; focused copy coverage remains separate");
    foreach (string copy in new[] { "MIMIC", "SKETCH" })
    {
        Check(WildBattleRules.IsCalledMoveEffectSupported(catalog.Moves[Id(copy)]), copy + " registered");
        Check(!WildBattleRules.IsCalledMoveEffectSupported(catalog.Moves[Id(copy)] with { Pp = 99 }), "forged recipe rejected");
        Check(!WildBattleSourceCallers.IsDirectMetronomeChoice(catalog.Moves[Id(copy)].SourceMoveId), "copy remains excluded from Metronome");
        var state = Start(Mon("CLEFAIRY", 50, 1, "METRONOME"), Mon("PIDGEY", 20, 2, "SPLASH"));
        var rejected = WildBattleRules.ResolveCalledMoveAction(catalog, state, new(WildBattleSide.Player, 0, Id(copy)));
        Check(!rejected.Succeeded && ReferenceEquals(rejected.State, state) && rejected.Battle.Events.IsEmpty && rejected.Battle.Trace.IsEmpty, "supplied Metronome cannot bypass pool");
    }
    Check(WildBattleSourceCallers.Assist([new(false, [274]), new(false, [102, 166])], 0, new(1)).SourceMoveId is null, "Assist excludes both copy commands");
});

foreach (string copy in new[] { "MIMIC", "SKETCH" }) foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
Test(copy + " actual complete turns, mirrored " + side, () =>
{
    bool permanent = copy == "SKETCH";
    var copier = Bonus(Mon(side == WildBattleSide.Player ? "CLEFAIRY" : "PIDGEY", 5, 1, copy), 0, 3);
    var donor = Mon(side == WildBattleSide.Player ? "PIDGEY" : "CLEFAIRY", 70, 2, "SPLASH");
    var state = side == WildBattleSide.Player ? Start(copier, donor, Mon("PIDGEY", 40, 3, "SPLASH")) : Start(donor, copier);
    state = Turn(state);
    WildBattleCombatant Get() => side == WildBattleSide.Player ? state.Player : state.Opponent;
    var live = Get(); var persistent = WildBattleTransformationRules.PersistentPokemon(live);
    Check(live.Pokemon.Moves[0] == new PokemonMoveSlot(Id("SPLASH"), permanent ? 40 : 5, 64, 3), "base PP versus temporary five, original PP Ups retained");
    Check(persistent.Moves[0].Move == Id(permanent ? "SPLASH" : "MIMIC") && persistent.Moves[0].CurrentPp == (permanent ? 40 : 15), "one permanent projection");
    Check(live.LastUsedMove == Id("UNAVAILABLE") && live.SourceMoveHistory!.LastPrintedMove == Id("UNAVAILABLE") && live.SourceMoveHistory.LastResultingMove == Id(copy), "source history after copy");
    state = Turn(state);
    Check(Get().Pokemon.Moves[0].CurrentPp == (permanent ? 39 : 4), "copied action spends live PP");
    Check(WildBattleTransformationRules.PersistentPokemon(Get()).Moves[0].CurrentPp == (permanent ? 39 : 15), "temporary PP never leaks");
    if (side == WildBattleSide.Player)
    {
        state = Require(WildBattleRules.SwitchSourceWildPlayer(catalog, state, 1), "switch copied user");
        Check(state.CalledParty!.Player[0].Pokemon.Moves[0].Move == Id(permanent ? "SPLASH" : "MIMIC"), "departure stores intended slot");
        state = Require(WildBattleRules.SwitchSourceWildPlayer(catalog, state, 0), "return copied user");
        Check(state.Player.CopyMoves is null && state.Player.Pokemon.Moves[0].Move == Id(permanent ? "SPLASH" : "MIMIC"), "switch clears temporary copy flag");
    }
    else
    {
        // Controlled injury makes an admitted Great Ball a guaranteed catch for this source Pidgey.
        state = state with { Opponent = state.Opponent with { Pokemon = state.Opponent.Pokemon with { CurrentHp = 1 } },
            CalledParty = state.CalledParty! with { Opponent = state.CalledParty.Opponent.SetItem(0, state.CalledParty.Opponent[0] with { Pokemon = state.CalledParty.Opponent[0].Pokemon with { CurrentHp = 1 } }) } };
        var caught = Require(WildBattleRules.ResolveBall(catalog, state, CaptureBall.Great), "capture copy user");
        Check(caught.Outcome == WildBattleOutcome.Caught && WildBattleTransformationRules.PersistentPokemon(caught.Opponent).Moves[0].Move == Id(permanent ? "SPLASH" : "MIMIC"), "captured identity uses permanent slots");
    }
});

Test("Mimic reads last-used; Sketch reads last-printed; no ordinary accuracy draw", () =>
{
    foreach (string copy in new[] { "MIMIC", "SKETCH" })
    {
        var state = History(Start(Mon("CLEFAIRY", 50, 1, copy), Mon("PIDGEY", 20, 2, "TACKLE")), "GROWL", "SPLASH");
        state = state with { Player = state.Player with { AccuracyStage = 0 }, Opponent = state.Opponent with { EvasionStage = 12 } };
        var result = Action(state); CopyEvent(result, copy == "MIMIC" ? "GROWL" : "SPLASH", copy == "SKETCH");
        Check(!result.Battle.Trace.Any(t => t.Purpose.Contains("accuracy")), "no accuracy RNG for copy");
    }
});

Test("source exclusion lists, duplicate and command failure history are exact", () =>
{
    foreach (string copy in new[] { "MIMIC", "SKETCH" }) foreach (string? history in new string?[] { null, "NONE", "UNAVAILABLE", "STRUGGLE", "SKETCH", "MIMIC", "METRONOME", "COUNTER" })
    {
        var state = History(Start(Mon("CLEFAIRY", 50, 1, copy), Mon("PIDGEY", 20, 2, "TACKLE")), history);
        var result = Action(state);
        bool forbidden = history is null or "NONE" or "UNAVAILABLE" or "STRUGGLE" or "SKETCH" || copy == "MIMIC" && history is "MIMIC" or "METRONOME";
        Check(result.Battle.Events.OfType<WildBattleMoveCopiedEvent>().Any() == !forbidden, copy + " copying " + history);
        if (forbidden)
        {
            Check(result.State!.Player.Pokemon.Moves[0] == state.Player.Pokemon.Moves[0] with { CurrentPp = state.Player.Pokemon.Moves[0].CurrentPp - 1 }, "failure only spends caller PP");
            Check(result.State.Player.CopyMoves is null && result.State.Player.LastUsedMove == Id("UNAVAILABLE"), "failed copy command has unavailable chosen history");
        }
    }
    foreach (string copy in new[] { "MIMIC", "SKETCH" })
    {
        var result = Action(History(Start(Mon("CLEFAIRY", 50, 1, copy, "GROWL"), Mon("PIDGEY", 20, 2, "TACKLE")), "GROWL"));
        Check(result.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "copy-already-known"), "known move fails");
    }
});

Test("Substitute blocks before command; Mimic semi-invulnerable gate differs from Sketch", () =>
{
    foreach (string copy in new[] { "MIMIC", "SKETCH" })
    {
        var state = History(Start(Mon("CLEFAIRY", 50, 1, copy), Mon("PIDGEY", 20, 2, "DIG")), "DIG");
        var substitute = state with { Opponent = state.Opponent with { CalledVolatile = new() { SubstituteHp = 10 } } };
        var protectedResult = Action(state with { Opponent = state.Opponent with { Protected = true } });
        Check(protectedResult.Battle.Events.OfType<WildBattleMoveCopiedEvent>().Any() == (copy == "SKETCH"), "Mimic respects Protect; Sketch does not test it");
        var blocked = Action(substitute);
        Check(blocked.State!.Player.LastUsedMove == Id(copy) && !blocked.Battle.Events.OfType<WildBattleMoveCopiedEvent>().Any(), "substitute exits before chosen unavailable");
        state = Action(state, actor: WildBattleSide.Opponent).State!;
        Check(WildBattleRules.CalledSemiInvulnerable(state.Opponent) == WildBattleSemiInvulnerable.Underground, "actual Dig charge");
        var copied = Action(state);
        Check(copied.Battle.Events.OfType<WildBattleMoveCopiedEvent>().Any() == (copy == "SKETCH"), "source accuracy gate");
        if (copy == "MIMIC")
        {
            Check(copied.State!.Player.LastUsedMove == Id("MIMIC"), "Mimic semi-invulnerable failure precedes command");
            state = state with { Opponent = state.Opponent with { CalledTurnState = state.Opponent.CalledTurnState! with { SureHitTurnsRemaining = 2, SureHitSource = WildBattleSide.Player } } };
            CopyEvent(Action(state), "DIG", false);
        }
    }
});

Test("Sleep Talk genuinely calls copy command and replaces selected result slot", () =>
{
    foreach (string copy in new[] { "MIMIC", "SKETCH" })
    {
        var state = Start(Mon("CLEFAIRY", 5, 1, "SLEEP_TALK", copy) with { Status = 5 }, Mon("PIDGEY", 70, 2, "SPLASH"));
        var result = WildBattleRules.ResolveMove(catalog, state, 0); state = Require(result, "Sleep Talk complete turn"); completeTurns++;
        Check(state.Turn == 1 && result.Events.OfType<WildBattleCallerSelectionEvent>().Any(e => e.Result == Id(copy)), "real selection reaches source recipe");
        Check(state.Player.Pokemon.Moves[0].Move == Id("SLEEP_TALK") && state.Player.Pokemon.Moves[0].CurrentPp == 9 && state.Player.Pokemon.Moves[1].Move == Id("SPLASH"), "result slot changed, caller paid once");
        Check(WildBattleTransformationRules.PersistentPokemon(state.Player).Moves[1].Move == Id(copy == "MIMIC" ? "MIMIC" : "SPLASH"), "called copy uses same ownership boundary");
        if (copy == "MIMIC") Check(WildBattleTransformationRules.PersistentPokemon(state.Player).Moves[1].CurrentPp == 10, "Sleep Talk suppresses result PP deduction");
    }
});

Test("Transform preserves permanent originals, clears Mimic overlay, and forbids transformed copy", () =>
{
    foreach (string copy in new[] { "MIMIC", "SKETCH" })
    {
        var state = History(Start(Mon("DITTO", 50, 1, copy, "TRANSFORM"), Mon("PIDGEY", 20, 2, "SPLASH")), "SPLASH");
        state = Action(state).State!; state = Action(state, 1).State!;
        Check(state.Player.Transform is not null && state.Player.CopyMoves is null, "Transform clears source mimicked mask");
        var persistent = WildBattleTransformationRules.PersistentPokemon(state.Player);
        Check(persistent.Moves[0].Move == Id(copy == "MIMIC" ? "MIMIC" : "SPLASH") && persistent.Moves[1].CurrentPp == 9, "Transform stores correct permanent moves and own spent PP");
        var restored = WildBattleTransformationRules.Restore(state.Player);
        Check(restored.Transform is null && restored.CopyMoves is null && restored.Pokemon == persistent, "restored permanent slots");
        state = Start(Mon("DITTO", 50, 1, "TRANSFORM"), Mon("PIDGEY", 20, 2, copy));
        state = Action(state).State!; state = History(state, "SPLASH");
        var failed = Action(state);
        Check(failed.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "copy-transformed") &&
            WildBattleTransformationRules.PersistentPokemon(failed.State!.Player).Moves[0].Move == Id("TRANSFORM"), "transformed actor never writes owned copied moves");
    }
});

Test("Mimic slot remains live during permanent learning and restores newly learned party slot", () =>
{
    var state = History(Start(Bonus(Mon("CLEFAIRY", 50, 1, "MIMIC", "TACKLE"), 0, 3), Mon("PIDGEY", 20, 2, "SPLASH")), "SPLASH");
    state = Action(state).State!;
    var old = WildBattleTransformationRules.PersistentPokemon(state.Player);
    var learned = old with { Moves = old.Moves.SetItem(0, Slot("GROWL")) };
    var updated = WildBattleTransformationRules.ApplyPersistentPokemon(state.Player, learned);
    Check(updated.Pokemon.Moves[0] == state.Player.Pokemon.Moves[0], "learning does not overwrite temporary live slot or PP Ups");
    Check(CanonicalJson.Hash(WildBattleTransformationRules.PersistentPokemon(updated)) == CanonicalJson.Hash(learned), "party receives new learned slot and reset PP bonus");
    Check(CanonicalJson.Hash(WildBattleTransformationRules.Restore(updated).Pokemon) == CanonicalJson.Hash(learned), "switch restoration returns new learned move, never obsolete Mimic");
});

Test("Sketch sends all live moves and PP; existing Mimic mask still isolates subsequent PP", () =>
{
    var state = History(Start(Mon("CLEFAIRY", 50, 1, "MIMIC", "SKETCH"), Mon("PIDGEY", 20, 2, "TACKLE")), "SPLASH");
    state = Action(state).State!;
    state = History(state, "GROWL"); state = Action(state, 1).State!;
    Check(WildBattleTransformationRules.PersistentPokemon(state.Player).Moves.Select(m => m.Move).SequenceEqual(new[] { Id("SPLASH"), Id("GROWL") }), "source REQUEST_MOVES_PP_BATTLE writes every live slot");
    state = Action(state).State!;
    Check(state.Player.Pokemon.Moves[0].CurrentPp == 4 && WildBattleTransformationRules.PersistentPokemon(state.Player).Moves[0].CurrentPp == 5, "source existing mimickedMoves flag survives whole-party write");
    Check(WildBattleTransformationRules.Restore(state.Player).Pokemon.Moves[0].CurrentPp == 5, "restoration uses controller-written PP");
});

Test("actual faint/replacement and terminal escape keep temporary moves out of party", () =>
{
    foreach (string copy in new[] { "MIMIC", "SKETCH" })
    {
        var mon = Mon("CLEFAIRY", 5, 1, copy); mon = mon with { Pokemon = mon.Pokemon with { CurrentHp = 1 } };
        var state = History(Start(mon, Mon("PIDGEY", 100, 2, "TACKLE"), Mon("CLEFAIRY", 100, 3, "SPLASH")), "SPLASH");
        state = Action(state).State!; state = Turn(state);
        Check(state.AwaitingReplacement && state.Player.Pokemon.CurrentHp == 0, "actual lethal turn pauses replacement");
        state = Require(WildBattleRules.ReplaceSourceWildPlayer(catalog, state, 1), "actual faint replacement");
        Check(state.CalledParty!.Player[0].Pokemon.Moves[0].Move == Id(copy == "MIMIC" ? "MIMIC" : "SPLASH"), "fainted party retains correct copy lifetime");
        state = History(Start(Mon("CLEFAIRY", 80, 1, copy), Mon("PIDGEY", 5, 2, "SPLASH")), "SPLASH");
        state = Action(state).State!; state = Require(WildBattleRules.Run(catalog, state), "escape copied battler");
        Check(state.Outcome == WildBattleOutcome.Escaped && state.CalledParty!.Player[0].Pokemon.Moves[0].Move == Id(copy == "MIMIC" ? "MIMIC" : "SPLASH"), "terminal escape leaves correct permanent slots");
    }
});

Test("battle-only overlay validates atomically and stays unavailable to historical contracts", () =>
{
    var state = History(Start(Mon("CLEFAIRY", 50, 1, "MIMIC"), Mon("PIDGEY", 20, 2, "SPLASH")), "SPLASH");
    state = Action(state).State!;
    foreach (var bad in new[] { state.Player.CopyMoves! with { RulesVersion = "future" }, new WildBattleCopiedMoves([new(8, Slot("MIMIC"))]), new WildBattleCopiedMoves([new(0, Slot("MIMIC")), new(0, Slot("MIMIC"))]) })
    {
        var input = state with { Player = state.Player with { CopyMoves = bad } };
        var result = WildBattleRules.ResolveMove(catalog, input, 0);
        Check(!result.Succeeded && ReferenceEquals(result.State, input) && result.Events.IsEmpty && result.Trace.IsEmpty, "invalid copy overlay rejection is atomic");
    }
    Check(!WildBattleRules.StartSourceWild(catalog, [state.Player], 0, Mon("PIDGEY", 20, 2, "SPLASH"), new(7)).Succeeded, "cannot bring existing live overlay into new encounter");
    var legacy = state with { SourceWild = null, CalledParty = null, Player = state.Player with { SourceMoveHistory = null, LastUsedMove = null }, Opponent = state.Opponent with { SourceMoveHistory = null, LastUsedMove = null } };
    Check(WildBattleRules.Validate(catalog, legacy) == "wild_battle_copy_moves_requires_contract", "historical battle cannot accept new overlay");
    var roundtrip = JsonSerializer.Deserialize<WildBattleState>(JsonSerializer.Serialize(state))!;
    Check(WildBattleRules.ValidateSourceWild(catalog, roundtrip) is null, "diagnostic copy state roundtrip is valid");
});

Test("copy events project original source text and replacement PP without another permanent write", () =>
{
    foreach (string copy in new[] { "MIMIC", "SKETCH" })
    {
        var result = Action(History(Start(Mon("CLEFAIRY", 50, 1, copy), Mon("PIDGEY", 20, 2, "SPLASH")), "SPLASH"));
        var events = result.Battle.Events; int index = events.IndexOf(events.OfType<WildBattleMoveCopiedEvent>().Single());
        var view = SourceTrainerPresentationModel.Describe(events, index, side => side.ToString(), move => move.Value, (side, slot) => slot.ToString())!;
        Check(!view.Unsupported && view.Texts.Single().Symbol == (copy == "MIMIC" ? "sText_PkmnLearnedMove2" : "sText_PkmnSketchedMove"), "exact source success symbol");
        Check(view.Texts.Single().Values.Contains(("B_BUFF1", Id("SPLASH").Value)), "source copied move text substitution");
        var text = view.Texts.Single(); var pages = PretBattleText.Pages(PretBattleText.Load(root), text.Symbol, text.Values.ToDictionary(x => x.Key, x => x.Value));
        Check(pages.Single() == (copy == "MIMIC" ? "Player learned\npret:MOVE_SPLASH!" : "Player SKETCHED\npret:MOVE_SPLASH!"), "source-authenticated text resolves completely");
        var decoded = JsonSerializer.Deserialize<WildBattleEvent>(JsonSerializer.Serialize<WildBattleEvent>(events[index]));
        Check(decoded == events[index], "typed event JSON roundtrip");
    }
});

Test("complete Domain turn permanent party projects to ordinary Application field save and fresh Continue", () =>
{
    var previous = PretCampaignCatalog.LoadFuchsiaRouteProfile(root);
    var owner = new CampaignSession(content);
    var upgraded = CampaignSaveUpgrade.FuchsiaRouteToFuchsiaGym(File.ReadAllBytes(Path.Combine(root, "tests/Game.FuchsiaCampaign.Tests/Fixtures/fuchsia-center.json")), previous, content);
    var loaded = SaveCodec.Decode<CampaignState>(upgraded.Bytes, owner.RulesVersion, content.ContentVersion, validate: owner.ValidatePlayerSave);
    foreach (string copy in new[] { "MIMIC", "SKETCH" })
    {
        var before = loaded.State;
        var owned = before.Party[0] with { Pokemon = before.Party[0].Pokemon with { Moves = [Slot(copy)] } };
        var start = Require(WildBattleRules.StartSourceWild(catalog, before.Party.SetItem(0, owned).Select(p => new WildBattleCombatant(p.Pokemon, HeldItem: p.HeldItem, Status: p.Status)).ToImmutableArray(), 0,
            Mon("PIDGEY", 100, 2, "SPLASH"), new(7)), "owned battle start");
        var state = Turn(start);
        Check(state.Player.Pokemon.Moves[0].Move == Id("SPLASH"), "actual turn copies before projecting owned result");
        // Same public permanent party output consumed by existing Application source owners.
        var projected = before with { Party = before.Party.Select((p, i) => p with { Pokemon = state.CalledParty!.Player[i].Pokemon, Status = state.CalledParty.Player[i].Status,
            HeldItem = WildBattleSourcePartyEffects.PersistentHeldItem(state.CalledParty.Player[i]) }).ToImmutableArray() };
        Check(owner.ValidatePlayerSave(projected) is null, "ordinary field state validates");
        var bytes = SaveCodec.Encode(projected, loaded.Generation, owner.RulesVersion, content.ContentVersion);
        var decoded = SaveCodec.Decode<CampaignState>(bytes, owner.RulesVersion, content.ContentVersion, validate: owner.ValidatePlayerSave);
        var continued = CampaignSession.Restore(content, decoded.State, decoded.Generation);
        Check(continued.State.Party[0].Pokemon.Moves[0].Move == Id(copy == "MIMIC" ? "MIMIC" : "SPLASH") && CanonicalJson.Hash(continued.State) == CanonicalJson.Hash(projected), "fresh Continue preserves exact intended learned record");
        Check(!System.Text.Encoding.UTF8.GetString(bytes).Contains("copyMoves", StringComparison.OrdinalIgnoreCase), "no battle-only state enters field save");
        File.WriteAllBytes(Path.Combine(output, copy.ToLowerInvariant() + "-controlled-field-save.json"), bytes);
    }
});

Check(groups > 0 && checks > 0 && completeTurns >= 10, "nonempty bounded suite");
File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { passed = failures == 0, groups, checks, failures, completeTurns, runtime = Environment.Version.ToString(), results,
    assemblies = new[] { typeof(WildBattleRules).Assembly, typeof(CampaignSession).Assembly, typeof(PretCampaignCatalog).Assembly }.Select(a => new { path = a.Location, sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(a.Location))) }) }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{groups} groups; {checks} checks; {completeTurns} complete turns; {failures} failures");
return failures == 0 ? 0 : 1;
