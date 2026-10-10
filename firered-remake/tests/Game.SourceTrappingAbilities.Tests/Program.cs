using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

if (args.Length != 3) throw new ArgumentException("CURRENT_CONTENT_ROOT SOURCE_ORACLE SOURCE_REFERENCE_ROOT");
var mechanics = SourceMechanicsCatalogLoader.LoadAuthenticated(args[0], args[0]);
var catalog = mechanics.Catalog;
int groups = 0, checks = 0, failures = 0, completeTurns = 0;
void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
void Test(string name, Action work) { groups++; try { work(); Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " " + e); } }
MoveId Move(string name) => new("pret:MOVE_" + name);
PokemonInstance Mon(string species, int level = 50, uint pid = 0, params string[] moves)
{
    var mon = SourcePokemonFactory.Create(mechanics, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
    foreach (var name in moves)
    {
        bool learned = catalog.Species[mon.Species].LevelUpMoves.Any(e => e.Move == Move(name) && e.Level <= level);
        if (name == "SKILL_SWAP" && species == "KADABRA")
        {
            var text = File.ReadAllText(Path.Combine(args[2], "src/data/pokemon/tmhm_learnsets.h"));
            int begin = text.IndexOf("[SPECIES_KADABRA]", StringComparison.Ordinal);
            int end = text.IndexOf("[SPECIES_ALAKAZAM]", begin, StringComparison.Ordinal);
            learned = begin >= 0 && end > begin && text[begin..end].Contains("TMHM(TM48_SKILL_SWAP)", StringComparison.Ordinal);
        }
        Check(learned, "authentic retained level move or source TM48 " + species + "/" + name);
    }
    return moves.Length == 0 ? mon : mon with { Moves = moves.Select(n => new PokemonMoveSlot(Move(n), catalog.Moves[Move(n)].Pp, catalog.Moves[Move(n)].Pp)).ToImmutableArray() };
}
WildBattleState Require(WildBattleResult r, string label)
{
    Check(r.Succeeded && r.State is not null, label + ": " + r.Fault);
    var s = r.State!;
    var error = s.SourceWild is null ? WildBattleRules.ValidateCalledMoveActionState(catalog, s) : WildBattleRules.Validate(catalog, s);
    Check(error is null, label + " validates: " + error);
    return s;
}
WildBattleState Start(WildBattleCombatant player, WildBattleCombatant opponent, PokemonInstance? reserve = null, uint seed = 0) => Require(
    WildBattleRules.StartSourceWild(mechanics, BattleRulesProfile.CanonicalSourceSingles,
        reserve is null ? [player] : [player, new(reserve)], 0, opponent, new(seed)), "canonical singles entry");
WildBattleState Pair(string player, string playerMove, string opponent, string opponentMove, string reserve = "SNORLAX") =>
    Start(new(Mon(player, 50, 0, playerMove)), new(Mon(opponent, 50, 0, opponentMove)), Mon(reserve, 50, 0, "TACKLE"));
WildBattleResult Action(WildBattleState s, int slot = 0, WildBattleSide side = WildBattleSide.Player) =>
    WildBattleRules.ResolveSourceMoveAction(catalog, s with { SourceWild = null }, new(side, slot)).Battle;
WildBattleState Turn(WildBattleState s, int slot = 0)
{
    var r = WildBattleRules.ResolveMove(catalog, s, slot); var next = Require(r, "complete turn"); completeTurns++;
    Check(next.Turn == s.Turn + 1 && next.SourceWild!.Turn is null, "both actions and residual boundary completed");
    return next;
}
void Atomic(WildBattleState s, WildBattleResult r, string fault)
{
    Check(!r.Succeeded && r.Fault == fault, "exact rejection " + fault + " got " + r.Fault);
    Check(ReferenceEquals(s, r.State) && r.Events.IsEmpty && r.Trace.IsEmpty && r.State!.Rng == s.Rng, "rejection preserves state, events, RNG and PP");
}
(int Run, bool Switch) Oracle(string actorAbility, string foeAbility, string type, int foeHp = 1, int item = 0, int link = 0)
{
    using var p = new Process { StartInfo = new(args[1]) { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false } };
    p.Start();
    p.StandardInput.WriteLine($"{mechanics.Abilities[actorAbility].SourceAbilityId} {mechanics.Abilities[foeAbility].SourceAbilityId} {type} {foeHp} {item} {link}");
    p.StandardInput.Close(); var result = p.StandardOutput.ReadToEnd().Trim().Split(' '); p.WaitForExit();
    Check(p.ExitCode == 0 && result.Length == 2, "extracted original C result");
    return (int.Parse(result[0]), result[1] == "1");
}
Test("official runtime and source donor identity remain authenticated", () => {
    Check(Environment.Version.Major == 10, "actual runtime major 10");
    foreach (var name in new[] { "WOBBUFFET", "WYNAUT", "OCTILLERY", "LILEEP", "CRADILY" })
    {
        var p = Mon(name);
        Check(p.AbilityId == (name is "WOBBUFFET" or "WYNAUT" ? "ABILITY_SHADOW_TAG" : "ABILITY_SUCTION_CUPS"), name + " real ability");
        Check(SourcePokemonValidator.ValidatePermanent(mechanics, p) is null, "authentic permanent identity");
        Check(CampaignAdmissionPolicy.CurrentFuchsia.ValidatePokemon(p) == "campaign_species_unsupported", "campaign roster still closed");
    }
});
Test("Shadow Tag blocks grounded, Flying, Ghost/Levitate and its own holder without spending the turn", () => {
    foreach (var specimen in new[] { ("SNORLAX", "TACKLE", "0"), ("PIDGEOT", "GUST", "2"), ("GASTLY", "LICK", "7"), ("WOBBUFFET", "COUNTER", "14") })
    foreach (var donor in new[] { ("WOBBUFFET", "COUNTER"), ("WYNAUT", "SPLASH") })
    {
        var s = Pair(specimen.Item1, specimen.Item2, donor.Item1, donor.Item2);
        var oracle = Oracle(s.Player.Pokemon.AbilityId, s.Opponent.Pokemon.AbilityId, specimen.Item3);
        Check(oracle.Run == 2 && oracle.Switch, "Gen III source traps actor including own Shadow Tag and Ghost");
        Atomic(s, WildBattleRules.Run(catalog, s), "wild_battle_run_shadow_tag");
        Atomic(s, WildBattleRules.SwitchSourceWildPlayer(catalog, s, 1), "wild_battle_switch_shadow_tag");
        var actionState = s with { SourceWild = null };
        Atomic(actionState, WildBattleRules.ResolveCalledPartySwitch(catalog, actionState, new(WildBattleSide.Player, 1)), "called_party_switch_ability_trapped");
    }
});
Test("source active-register semantics ignore HP; item/link source precedence stays an explicit unsupported boundary", () => {
    Check(Oracle("ABILITY_LEVITATE", "ABILITY_SHADOW_TAG", "7", foeHp: 0) == (2, true), "original source reads retained ability at HP0");
    Check(Oracle("ABILITY_RUN_AWAY", "ABILITY_SHADOW_TAG", "0") == (0, true), "Run Away only exempts escape");
    Check(Oracle("ABILITY_LEVITATE", "ABILITY_SHADOW_TAG", "7", item: 1) == (0, true), "source Smoke Ball escape priority");
    Check(Oracle("ABILITY_LEVITATE", "ABILITY_SHADOW_TAG", "7", link: 1) == (0, true), "source link escape priority");
    var blocked = WildBattleRules.StartSourceWild(mechanics, BattleRulesProfile.CanonicalSourceSingles,
        [new(Mon("GASTLY", 50, 0, "LICK"), HeldItem: "ITEM_SMOKE_BALL")], 0, new(Mon("WYNAUT", 50, 0, "SPLASH")), new(0));
    Check(!blocked.Succeeded && blocked.Events.IsEmpty && blocked.Trace.IsEmpty, "Smoke Ball not silently admitted");
    Check(BattleRulesProfile.CanonicalSourceSingles.ActiveBattlersPerSide == 1, "no doubles/link owner admitted");
});
Test("Run Away escapes Shadow Tag, consumes no run roll and never allows voluntary switch", () => {
    var s = Pair("EEVEE", "TACKLE", "WYNAUT", "SPLASH");
    Atomic(s, WildBattleRules.SwitchSourceWildPlayer(catalog, s, 1), "wild_battle_switch_shadow_tag");
    var r = WildBattleRules.Run(catalog, s); var next = Require(r, "Run Away");
    Check(next.Outcome == WildBattleOutcome.Escaped && next.RunAttempts == s.RunAttempts, "guaranteed escape and unchanged run counter");
    Check(r.Trace.All(t => t.Purpose != "wild-battle.run") && next.Player.Pokemon.Moves[0].CurrentPp == s.Player.Pokemon.Moves[0].CurrentPp, "no escape draw or move PP");
});
Test("Teleport spends PP and loses its full turn against Shadow Tag", () => {
    var s = Pair("ABRA", "TELEPORT", "WYNAUT", "SPLASH");
    var r = WildBattleRules.ResolveMove(catalog, s, 0); var next = Require(r, "Teleport turn"); completeTurns++;
    Check(next.Outcome == WildBattleOutcome.Ongoing && next.Turn == s.Turn + 1, "failed move still finishes turn");
    Check(r.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Move == Move("TELEPORT") && e.Reason == "cannot-escape"), "escape blocked");
    Check(next.Player.Pokemon.Moves[0].CurrentPp == s.Player.Pokemon.Moves[0].CurrentPp - 1 && r.Events.OfType<WildBattleMoveUsedEvent>().Any(e => e.Actor == WildBattleSide.Opponent), "actual PP and foe action");
});
Test("Baton Pass bypasses Shadow Tag and resumes the selected wild turn", () => {
    var s = Pair("MR_MIME", "BATON_PASS", "WYNAUT", "SPLASH");
    var pending = Require(WildBattleRules.ResolveMove(catalog, s, 0), "Baton pause");
    Check(pending.CalledParty!.PendingSwitch?.Kind == WildBattleCalledSwitchKind.BatonPass, "Baton pending");
    var r = WildBattleRules.ChooseSourceWildParty(catalog, pending, 1); var next = Require(r, "Baton resume"); completeTurns++;
    Check(next.CalledParty!.PlayerActiveIndex == 1 && next.Turn == s.Turn + 1 && next.SourceWild!.Turn is null, "real reserve and whole-turn continuation");
    Check(next.CalledParty.Player[0].Pokemon.Moves[0].CurrentPp == s.Player.Pokemon.Moves[0].CurrentPp - 1, "Baton PP belongs to departing member");
    Atomic(next, WildBattleRules.SwitchSourceWildPlayer(catalog, next, 0), "wild_battle_switch_shadow_tag");
});
Test("faint replacement and post-faint escape bypass Shadow Tag selection checks", () => {
    var damaged = Mon("OCTILLERY", 50, 0, "FOCUS_ENERGY") with { CurrentHp = 1 };
    var s = Start(new(damaged) { Status = WildBattleStatus.Poison }, new(Mon("WYNAUT", 50, 0, "SPLASH")), Mon("EEVEE", 50, 0, "TACKLE"));
    var pending = Require(WildBattleRules.ResolveMove(catalog, s, 0), "faint replacement pause");
    Check(pending.AwaitingReplacement && pending.CalledParty!.PendingSwitch?.Kind == WildBattleCalledSwitchKind.FaintedReplacement, "actual poison faint");
    var r = WildBattleRules.ReplaceSourceWildPlayer(catalog, pending, 1); var next = Require(r, "mandatory replacement");
    Check(next.CalledParty!.PlayerActiveIndex == 1 && !next.AwaitingReplacement, "new living owned member");
    var flee = Require(WildBattleRules.TryRunAfterFaint(catalog, pending), "post-faint flee");
    Check(flee.Outcome == WildBattleOutcome.Escaped, "source post-faint tryrun bypasses ability selection gate");
});
Test("Skill Swap removes Shadow Tag from the active foe, with ability overlays surviving JSON", () => {
    var s = Start(new(Mon("KADABRA", 50, 0, "SKILL_SWAP", "ROLE_PLAY")), new(Mon("WYNAUT", 50, 0, "SPLASH")), Mon("SNORLAX", 50, 0, "TACKLE"));
    var swapped = Turn(s);
    Check(WildBattleEffective.Ability(swapped.Player) == "ABILITY_SHADOW_TAG" && WildBattleEffective.Ability(swapped.Opponent) == "ABILITY_SYNCHRONIZE", "effective ability swap");
    Check(swapped.Player.Pokemon.AbilityId == "ABILITY_SYNCHRONIZE" && swapped.Opponent.Pokemon.AbilityId == "ABILITY_SHADOW_TAG", "permanent identities unchanged");
    var restored = JsonSerializer.Deserialize<WildBattleState>(JsonSerializer.Serialize(swapped))!;
    var next = Require(WildBattleRules.SwitchSourceWildPlayer(catalog, restored, 1), "switch after foe ability replacement");
    Check(next.CalledParty!.PlayerActiveIndex == 1 && next.Player.CalledOverlay is null, "switch uses effective foe ability and clears outgoing overlay");
});
Test("Role Play copies Shadow Tag but gives no Gen IV self exemption", () => {
    var s = Pair("KADABRA", "ROLE_PLAY", "WYNAUT", "SPLASH"); var next = Turn(s);
    Check(next.Player.CalledOverlay?.Ability == "ABILITY_SHADOW_TAG", "real Role Play copy");
    Atomic(next, WildBattleRules.Run(catalog, next), "wild_battle_run_shadow_tag");
});
Test("Roar and Whirlwind are stopped by authentic Suction Cups after PP and before accuracy or level RNG", () => {
    foreach (var actor in new[] { ("GROWLITHE", "ROAR"), ("PIDGEOT", "WHIRLWIND") })
    foreach (var donor in new[] { ("OCTILLERY", "FOCUS_ENERGY"), ("LILEEP", "AMNESIA"), ("CRADILY", "AMNESIA") })
    {
        var s = Start(new(Mon(actor.Item1, 50, 0, actor.Item2)), new(Mon(donor.Item1, 70, 0, donor.Item2)));
        var r = Action(s); var next = Require(r, "Suction Cups source action");
        Check(next.Outcome == WildBattleOutcome.Ongoing && next.Opponent.Pokemon.CurrentHp == s.Opponent.Pokemon.CurrentHp, "target remains");
        Check(next.Player.Pokemon.Moves[0].CurrentPp == s.Player.Pokemon.Moves[0].CurrentPp - 1, "actual PP consumed");
        var pp = r.Events.OfType<WildBattlePpCommandEvent>().Single(); var guard = r.Events.OfType<WildBattleAbilityPreventedEvent>().Single(e => e.Ability == "ABILITY_SUCTION_CUPS");
        Check(pp.Sequence < guard.Sequence && guard.Target == WildBattleSide.Opponent, "source PP-before-ability event order");
        Check(r.Trace.IsEmpty && !r.Events.OfType<WildBattleForcedOutEvent>().Any(), "no accuracy, level, reserve or other draw");
    }
});
Test("Suction Cups wins over Ingrain; cancellation wins before PP", () => {
    var s = Pair("GROWLITHE", "ROAR", "LILEEP", "INGRAIN");
    var ingrain = Require(Action(s, side: WildBattleSide.Opponent), "actual Ingrain");
    Check(ingrain.Opponent.Rooted, "real rooted target");
    var r = Action(ingrain); Require(r, "rooted Suction Cups");
    Check(r.Events.OfType<WildBattleAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_SUCTION_CUPS") && !r.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "rooted"), "ability precedes rooted branch");
    var sleeping = Start(new(Mon("GROWLITHE", 50, 0, "ROAR")) { Status = 2 }, new(Mon("OCTILLERY", 50, 0, "FOCUS_ENERGY")));
    var blocked = Action(sleeping); var after = Require(blocked, "sleep cancellation");
    Check(after.Player.Pokemon.Moves[0].CurrentPp == sleeping.Player.Pokemon.Moves[0].CurrentPp && !blocked.Events.OfType<WildBattleAbilityPreventedEvent>().Any(), "canceller does not reach PP or target ability");
});
Test("Suction Cups prevention completes both source actions and the turn boundary", () => {
    var s = Pair("GROWLITHE", "ROAR", "OCTILLERY", "FOCUS_ENERGY");
    var r = WildBattleRules.ResolveMove(catalog, s, 0); var next = Require(r, "full Suction Cups turn"); completeTurns++;
    Check(next.Turn == s.Turn + 1 && next.SourceWild!.Turn is null && next.Outcome == WildBattleOutcome.Ongoing, "ordinary complete turn");
    Check(r.Events.OfType<WildBattleAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_SUCTION_CUPS"), "actual ability prevention");
    Check(next.Player.Pokemon.Moves[0].CurrentPp == s.Player.Pokemon.Moves[0].CurrentPp - 1 && next.Opponent.Pokemon.Moves[0].CurrentPp == s.Opponent.Pokemon.Moves[0].CurrentPp - 1, "both actual chosen slots spent");
    Check(r.Trace.All(t => !t.Purpose.Contains("accuracy", StringComparison.Ordinal) && !t.Purpose.Contains("force-out", StringComparison.Ordinal)), "prevented phasing draws neither accuracy nor level RNG");
});
Test("Shadow Tag does not block forced wild escape; Suction Cups does not block voluntary switching or fleeing", () => {
    var s = Pair("GROWLITHE", "ROAR", "WYNAUT", "SPLASH");
    var r = WildBattleRules.ResolveMove(catalog, s, 0); var next = Require(r, "forced wild escape");
    Check(next.Outcome == WildBattleOutcome.Escaped && r.Events.OfType<WildBattleForcedOutEvent>().Any(), "Roar bypasses Shadow Tag");
    var cups = Pair("OCTILLERY", "FOCUS_ENERGY", "SNORLAX", "YAWN");
    Check(Require(WildBattleRules.SwitchSourceWildPlayer(catalog, cups, 1), "voluntary Suction Cups").CalledParty!.PlayerActiveIndex == 1, "Suction Cups only forced switch prevention");
    Check(Require(WildBattleRules.Run(catalog, cups), "Suction Cups Run").Outcome == WildBattleOutcome.Escaped, "Suction Cups holder may run");
});
Test("Role Play and Skill Swap replace Suction Cups dynamically without altering species abilities", () => {
    var s = Start(new(Mon("KADABRA", 50, 0, "SKILL_SWAP")), new(Mon("OCTILLERY", 50, 0, "FOCUS_ENERGY")), Mon("GROWLITHE", 50, 0, "ROAR"));
    var swapped = Turn(s);
    Check(WildBattleEffective.Ability(swapped.Opponent) == "ABILITY_SYNCHRONIZE" && swapped.Opponent.Pokemon.AbilityId == "ABILITY_SUCTION_CUPS", "Suction Cups actually removed");
    var switched = Require(WildBattleRules.SwitchSourceWildPlayer(catalog, swapped, 1), "switch to authentic Roar donor");
    Check(Require(WildBattleRules.ResolveMove(catalog, switched, 0), "Roar after Skill Swap").Outcome == WildBattleOutcome.Escaped, "base species does not retain removed effect");
    var copied = Start(new(Mon("MR_MIME", 50, 0, "ROLE_PLAY", "BATON_PASS")), new(Mon("OCTILLERY", 50, 0, "FOCUS_ENERGY")), Mon("SNORLAX", 50, 0, "TACKLE"));
    copied = Turn(copied);
    Check(WildBattleEffective.Ability(copied.Player) == "ABILITY_SUCTION_CUPS", "Role Play copied live ability");
    var pending = Require(WildBattleRules.ResolveMove(catalog, copied, 1), "Suction Cups Baton pause");
    Check(Require(WildBattleRules.ChooseSourceWildParty(catalog, pending, 1), "Suction Cups Baton resume").CalledParty!.PlayerActiveIndex == 1, "Suction Cups permits Baton Pass");
});
Test("forged ability, forged provenance, unsupported donor and copied catalog remain atomic", () => {
    var s = Pair("KADABRA", "ROLE_PLAY", "WYNAUT", "SPLASH");
    var forgedMon = s.Player.Pokemon with { AbilityId = "ABILITY_SHADOW_TAG" };
    var forged = s with { Player = s.Player with { Pokemon = forgedMon }, CalledParty = s.CalledParty! with { Player = s.CalledParty.Player.SetItem(0, s.CalledParty.Player[0] with { Pokemon = forgedMon }) } };
    var r = WildBattleRules.Run(catalog, forged); Check(!r.Succeeded && ReferenceEquals(r.State, forged) && r.Events.IsEmpty && r.Trace.IsEmpty, "forged base ability rejected");
    var badOverlay = s with { Player = s.Player with { CalledOverlay = new() { Ability = "ABILITY_SUCTION_CUPS" } } };
    Atomic(badOverlay, WildBattleRules.Run(catalog, badOverlay), "called_overlay_ability_provenance");
    var cloned = WildBattleRules.Run(catalog with { }, s); Check(!cloned.Succeeded && cloned.Events.IsEmpty && cloned.Trace.IsEmpty, "cloned source proof rejected");
    var unsupported = WildBattleRules.StartSourceWild(mechanics, BattleRulesProfile.CanonicalSourceSingles,
        [new(Mon("CASTFORM", 50, 0, "TACKLE"))], 0, new(Mon("WYNAUT", 50, 0, "SPLASH")), new(0));
    Check(!unsupported.Succeeded && unsupported.Fault == "source_mechanics_ability_not_implemented", "unimplemented Forecast donor remains rejected");
});
Console.WriteLine(JsonSerializer.Serialize(new { groups, checks, failures, completeTurns, runtime = Environment.Version.ToString() }));
if (groups == 0 || failures != 0) Environment.Exit(1);
