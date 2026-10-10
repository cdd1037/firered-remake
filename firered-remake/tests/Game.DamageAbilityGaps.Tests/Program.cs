using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

if (args.Length != 2) throw new ArgumentException("CONTENT_REPOSITORY_ROOT SOURCE_VECTOR_JSON");
string root = Path.GetFullPath(args[0]), vectorsPath = Path.GetFullPath(args[1]);
int checks = 0, groups = 0, failures = 0, numericVectors = 0, sourceTurns = 0;
var observed = new List<object>();
void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
void Test(string label, Action body) { groups++; try { body(); Console.WriteLine("PASS " + label); } catch (Exception ex) { failures++; Console.WriteLine("FAIL " + label + " " + ex); } }
MoveId Id(string move) => new("pret:MOVE_" + move);
var raw = PretRocketHideoutPokemon.Load(root).Catalog;
var extra = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules: true);
var prior = PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(raw with { Moves = raw.Moves.SetItems(extra.Moves) }, root, root).Catalog, root).Catalog, root).Catalog;
var predecessor = PretFuchsiaRoutePokemon.Load(prior, root).Catalog;
var catalog = PretMetronomeMoveCatalog.Load(root).Extend(predecessor);
PokemonInstance Mon(string species, int level = 37, uint pid = 0, params string[] moves)
{
    var mon = PokemonFactory.Create(catalog, new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
    return moves.Length == 0 ? mon : mon with { Moves = moves.Select(move => new PokemonMoveSlot(Id(move), catalog.Moves[Id(move)].Pp, catalog.Moves[Id(move)].Pp)).ToImmutableArray() };
}
var method = typeof(WildBattleRules).Assembly.GetType("Game.Domain.WildBattle.ForestBattleDamage", true)!
    .GetMethod("Calculate", BindingFlags.Static | BindingFlags.NonPublic)!;
DamageResult Calculate(WildBattleCombatant actor, WildBattleCombatant target, PokemonMoveDefinition move,
    bool critical = false, int percent = 100, bool source = true, bool reflect = false, PokemonCatalog? selected = null)
{
    var inputs = method.GetParameters().Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
    var values = new Dictionary<string, object?> {
        ["catalog"] = selected ?? catalog, ["user"] = actor, ["target"] = target, ["moveId"] = move.Id,
        ["critical"] = critical, ["randomPercent"] = percent, ["sourceDefinition"] = source ? move : null,
        ["reflect"] = reflect };
    foreach (string flag in new[] { "pewterRules", "earlyStarterRules", "earlyPartyRules", "route22Rules", "route3Rules", "mtMoonRules", "ceruleanRules", "billRules", "vermilionRules", "ssAnneRules", "vermilionGymRules", "route11Rules", "rockTunnelRules" }) values[flag] = true;
    foreach (var p in method.GetParameters()) if (values.TryGetValue(p.Name!, out var value)) inputs[p.Position] = value;
    return (DamageResult)method.Invoke(null, inputs)!;
}
using var document = JsonDocument.Parse(File.ReadAllText(vectorsPath));
var vectorRoot = document.RootElement;
var vectors = vectorRoot.GetProperty("vectors").EnumerateArray().ToArray();
string[] newAbilities = ["ABILITY_HUGE_POWER", "ABILITY_PURE_POWER", "ABILITY_HUSTLE", "ABILITY_MARVEL_SCALE"];
Test("pinned runtime/source and honest roster boundary", () => {
    Check(Environment.Version.Major == 10, "runtime major 10");
    Check(vectorRoot.GetProperty("sourceCommit").GetString() == catalog.SourceCommit, "pinned source");
    Check(vectors.Length == 33, "nonempty bounded 33-vector suite");
    Check(newAbilities.All(a => !catalog.Species.Values.Any(s => s.Ability1 == a || s.Ability2 == a)), "no authentic donor in current 107-species roster");
});
Test("verbatim source CalculateBaseDamage: 33 focused physical/special vectors", () => {
    foreach (var v in vectors)
    {
        int I(string key) => v.GetProperty(key).GetInt32();
        string S(string key) => v.GetProperty(key).GetString()!;
        bool B(string key) => v.GetProperty(key).GetBoolean();
        // Explicitly controlled low-level registers. Permanent AbilityId is never changed.
        var player = new WildBattleCombatant(Mon("CUBONE") with { Stats = new(500, I("attack"), 50, I("attack"), 50, 50), CurrentHp = 500 })
        { CalledOverlay = new() { Ability = S("abilityA") }, Status = (uint)I("statusA"), AttackStage = I("stageA"),
            SpecialAttackStageDelta = I("stageA") - 6, AttackBadgeBoost = B("badgeAttack"), HeldItem = S("itemA") };
        var target = new WildBattleCombatant(Mon("DITTO") with { Stats = new(500, 50, I("defense"), 50, I("defense"), 50), CurrentHp = 500 })
        { CalledOverlay = new() { Ability = S("abilityD") }, Status = (uint)I("statusD"), DefenseStage = I("stageD"),
            SpecialDefenseStageDelta = I("stageD") - 6, DefenseBadgeBoost = B("badgeDefense"), HeldItem = S("itemD") };
        var move = catalog.Moves[Id(S("move"))];
        var damage = Calculate(player, target, move, B("critical"), I("percent"), reflect: B("reflect"));
        Check(damage.Damage == I("damage"), S("name") + $" expected {I("damage")}, got {damage.Damage}");
        Check(player.Pokemon.AbilityId == Mon("CUBONE").AbilityId && target.Pokemon.AbilityId == Mon("DITTO").AbilityId, "permanent identities retained");
        if (S("name") == "HUGE_POWER-before-badge-odd-rounding")
            Check(damage.Steps.Take(2).SequenceEqual(new[] { new DamageStep("huge-pure-power", 110), new DamageStep("attack-badge", 121) }), "huge before badge with distinct floor");
        if (S("name") == "hustle-after-badge-and-type-item")
            Check(damage.Steps.Take(3).SequenceEqual(new[] { new DamageStep("attack-badge", 58), new DamageStep("hard-stone", 63), new DamageStep("hustle", 94) }), "hustle after two separate floors");
        if (S("name") == "marvel-after-metal-powder-before-explosion")
            Check(damage.Steps.Take(3).SequenceEqual(new[] { new DamageStep("defense-badge", 60), new DamageStep("metal-powder", 120), new DamageStep("marvel-scale", 180) }), "marvel after badge/item before traditional Explosion");
        if (S("move") == "EMBER") Check(!damage.Steps.Any(s => s.Operation is "huge-pure-power" or "hustle" or "marvel-scale"), "physical abilities leave special registers unchanged");
        int? legacyDamage = null;
        if (WildBattleRules.IsMoveSupported(move.Id, WildBattleRules.FuchsiaRulesVersion))
        {
            var legacy = Calculate(player, target, move, B("critical"), I("percent"), source: false, reflect: B("reflect"));
            var baseline = Calculate(player with { CalledOverlay = S("abilityA") == "ABILITY_GUTS" ? player.CalledOverlay : null },
                target with { CalledOverlay = null }, move, B("critical"), I("percent"), source: false, reflect: B("reflect"));
            Check(legacy.Damage == baseline.Damage && legacy.Steps.SequenceEqual(baseline.Steps), S("name") + " legacy isolated, trace unchanged");
            legacyDamage = legacy.Damage;
        }
        else
        {
            bool rejected = false;
            try { Calculate(player, target, move, source: false); }
            catch (TargetInvocationException ex) when (ex.InnerException is NotSupportedException e && e.Message == "wild_battle_damage_move") { rejected = true; }
            Check(rejected, S("name") + " legacy unsupported move remains rejected");
        }
        observed.Add(new { name = S("name"), expected = I("damage"), actual = damage.Damage, legacy = legacyDamage }); numericVectors++;
    }
});
Test("unauthenticated source definition fails before arithmetic and leaves fixtures unchanged", () => {
    var actor = new WildBattleCombatant(Mon("CUBONE")); var target = new WildBattleCombatant(Mon("DITTO"));
    string before = JsonSerializer.Serialize(new[] { actor, target });
    bool rejected = false;
    try { Calculate(actor, target, catalog.Moves[Id("TACKLE")] with { Power = 36 }); }
    catch (TargetInvocationException ex) when (ex.InnerException is NotSupportedException e && e.Message == "called_damage_source_definition") { rejected = true; }
    Check(rejected && JsonSerializer.Serialize(new[] { actor, target }) == before, "definition failure leaves inputs untouched");
});
WildBattleState Require(WildBattleResult result, string label)
{
    Check(result.Succeeded, label + " " + result.Fault);
    Check(WildBattleRules.Validate(catalog, result.State!) is null, label + " validated output");
    return result.State!;
}
WildBattleState Start(uint seed = 0) => Require(WildBattleRules.StartSourceWild(catalog,
    [new(Mon("CLEFAIRY", 37, 0, "ROLE_PLAY", "TACKLE", "NATURE_POWER", "SKILL_SWAP")) { Status = WildBattleStatus.Burn }],
    0, new(Mon("RATTATA", 80, 1, "SPLASH")), new(seed)), "start authentic source wild");
int OracleBase(WildBattleCombatant actor, WildBattleCombatant target, PokemonMoveDefinition move, bool critical)
{
    int Ability(string name) => vectorRoot.GetProperty("abilityIds").GetProperty(name).GetInt32();
    using var process = new Process { StartInfo = new(Path.Combine(Path.GetDirectoryName(vectorsPath)!, "source-oracle"))
        { RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false } };
    process.Start();
    process.StandardInput.WriteLine(string.Join(' ', new[] { actor.Pokemon.Stats.Attack, target.Pokemon.Stats.Defense,
        Ability(WildBattleEffective.Ability(actor)), Ability(WildBattleEffective.Ability(target)), (int)actor.Status, (int)target.Status,
        actor.AttackStage, target.DefenseStage, critical ? 1 : 0, move.Power, 0, 0, 0, 0, 0, actor.AttackBadgeBoost ? 1 : 0, target.DefenseBadgeBoost ? 1 : 0 }));
    process.StandardInput.Close(); string output = process.StandardOutput.ReadToEnd(); process.WaitForExit();
    Check(process.ExitCode == 0 && int.TryParse(output, out _), "source oracle subprocess"); return int.Parse(output);
}
Test("authentic Role Play and Skill Swap survive full source turns, ordinary and called damage, and serialization", () => {
    var state = Start(); string originalAbility = state.Player.Pokemon.AbilityId;
    Check(state.Opponent.Pokemon.AbilityId == "ABILITY_GUTS", "actual PID-selected donor");
    state = Require(WildBattleRules.ResolveMove(catalog, state, 0), "Role Play full turn"); sourceTurns++;
    Check(state.Player.CalledOverlay?.Ability == "ABILITY_GUTS" && state.Player.Pokemon.AbilityId == originalAbility, "authentic transient ability, permanent unchanged");
    foreach (int slot in new[] { 1, 2 })
    {
        state = JsonSerializer.Deserialize<WildBattleState>(JsonSerializer.Serialize(state))!;
        var before = state; var result = WildBattleRules.ResolveMove(catalog, state, slot);
        state = Require(result, slot == 1 ? "ordinary Tackle" : "Nature Power called Swift"); sourceTurns++;
        var hit = result.Events.OfType<WildBattleHpChangedEvent>().Single(e => e.Actor == WildBattleSide.Player && e.Target == WildBattleSide.Opponent);
        var move = catalog.Moves[hit.Move]; int expected = OracleBase(before.Player, before.Opponent, move, hit.Critical);
        expected *= hit.Critical ? 2 : 1; expected = expected * 15 / 10; expected = Math.Max(1, expected * hit.RandomPercent / 100);
        Check(hit.Damage == expected && hit.HpBefore - hit.HpAfter == expected, "authentic source C numeric HP and STAB");
        Check(state.Turn == before.Turn + 1 && result.Events.OfType<WildBattleMoveUsedEvent>().Any(e => e.Actor == WildBattleSide.Opponent), "both sides plus complete turn");
        Check(state.Player.Pokemon.Moves[slot].CurrentPp == before.Player.Pokemon.Moves[slot].CurrentPp - 1, "actual chosen slot PP");
        Check(state.Player.Pokemon.AbilityId == originalAbility && state.CalledParty!.Player[0].Pokemon.AbilityId == originalAbility, "party permanent ability not projected from overlay");
        if (slot == 2) Check(hit.Move == Id("SWIFT") && state.Player.LastCalledMove == Id("SWIFT"), "called resulting move remains distinct");
        observed.Add(new { fullTurn = slot == 1 ? "Tackle" : "Nature Power -> Swift", expected, actual = hit.Damage, hit.Critical, hit.RandomPercent, rng = state.Rng });
    }
    // Swap between an unmodified source Clefairy and a source Guts Rattata, then resolve an ordinary hit.
    state = Start(1); state = Require(WildBattleRules.ResolveMove(catalog, state, 3), "Skill Swap full turn"); sourceTurns++;
    Check(WildBattleEffective.Ability(state.Player) == "ABILITY_GUTS" && WildBattleEffective.Ability(state.Opponent) == originalAbility, "two authenticated transient abilities");
    state = Require(WildBattleRules.ResolveMove(catalog, state, 1), "post Skill Swap turn"); sourceTurns++;
    Check(state.Player.Pokemon.AbilityId == originalAbility && state.Opponent.Pokemon.AbilityId == "ABILITY_GUTS", "swapped ability never persists");
});
Test("all four missing donor overlays fail atomically for source selection, called action and complete turn", () => {
    var valid = Start();
    foreach (string ability in newAbilities)
    {
        var forged = valid with { Player = valid.Player with { CalledOverlay = new() { Ability = ability } } };
        Check(WildBattleRules.ValidateCalledMoveActionState(catalog, forged) == "called_overlay_ability_provenance", ability + " provenance gate");
        foreach (var result in new[] { WildBattleRules.ResolveMove(catalog, forged, 1),
            WildBattleRules.ResolveSourceMoveAction(catalog, forged, new(WildBattleSide.Player, 1)).Battle,
            WildBattleRules.ResolveCalledMoveAction(catalog, forged, new(WildBattleSide.Player, 0, Id("TACKLE"))).Battle })
            Check(!result.Succeeded && ReferenceEquals(result.State, forged) && result.Events.IsEmpty && result.Trace.IsEmpty,
                ability + " failure preserves HP, PP, status, RNG, revision and party");
    }
});
Test("legacy predecessor Tackle vector retains exact damage and operation trace", () => {
    var actor = new WildBattleCombatant(Mon("CUBONE")); var target = new WildBattleCombatant(Mon("DITTO"));
    var old = WildBattleDamage.Calculate(predecessor, actor, target, Id("TACKLE"), false, 100, WildBattleRules.FuchsiaRulesVersion);
    var currentLegacy = WildBattleDamage.Calculate(catalog, actor, target, Id("TACKLE"), false, 100, WildBattleRules.FuchsiaRulesVersion);
    Check(old.Damage == currentLegacy.Damage && old.Steps.SequenceEqual(currentLegacy.Steps), "predecessor current legacy unchanged");
    int baseDamage = OracleBase(actor, target, catalog.Moves[Id("TACKLE")], false);
    Check(old.Damage == baseDamage, "predecessor exact pinned C value"); observed.Add(new { predecessor = old.Damage, sourceExpected = baseDamage });
});
var summary = new { groups, checks, failures, numericVectors, sourceTurns, runtime = Environment.Version.ToString(),
    newAbilityFullTurns = "not admitted: current authentic 107-species roster has no donors", observed };
File.WriteAllText(Path.Combine(Path.GetDirectoryName(vectorsPath)!, "result.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine(JsonSerializer.Serialize(new { groups, checks, failures, numericVectors, sourceTurns }));
if (groups == 0 || numericVectors == 0 || failures > 0) Environment.Exit(1);
