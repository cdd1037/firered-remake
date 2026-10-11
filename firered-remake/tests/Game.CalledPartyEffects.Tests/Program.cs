using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length != 1) throw new ArgumentException("Pass the project root.");
string root = Path.GetFullPath(args[0]); int groups = 0, assertions = 0, failures = 0;
void Check(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
void Test(string title, Action work) { groups++; try { work(); Console.WriteLine("PASS " + title); } catch (Exception ex) { failures++; Console.WriteLine("FAIL " + title + " " + ex); } }
MoveId Id(string value) => new("pret:MOVE_" + value);
var original = PretRocketHideoutPokemon.Load(root).Catalog;
var supplement = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules: true);
var prior = original with { Moves = original.Moves.SetItems(supplement.Moves) };
var old = PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior, root, root).Catalog, root).Catalog, root).Catalog;
var catalog = PretMetronomeMoveCatalog.Load(root).Extend(PretFuchsiaRoutePokemon.Load(old, root).Catalog);
PokemonInstance Mon(string species, int level = 35, uint pid = 0) => PokemonFactory.Create(catalog,
    new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
PokemonInstance Moves(PokemonInstance mon, params string[] names) => mon with { Moves = names.Select(n => new PokemonMoveSlot(Id(n), catalog.Moves[Id(n)].Pp, catalog.Moves[Id(n)].Pp)).ToImmutableArray() };
WildBattleState Pair(string actor = "CLEFAIRY", string target = "RATTATA", uint seed = 0) => new(
    new(Moves(Mon(actor), "METRONOME")) { FirstTurnCounter = 1 }, new(Moves(Mon(target, 80), "METRONOME")) { FirstTurnCounter = 1 },
    0, 0, 0, new(seed), 0, WildBattleOutcome.Ongoing, RulesVersion: WildBattleRules.FuchsiaRulesVersion) { Terrain = WildBattleTerrain.Plain };
WildBattleCalledPartyMember Member(string species, int level = 35, uint pid = 0, string item = "ITEM_NONE", uint status = 0, bool egg = false) =>
    new(Moves(Mon(species, level, pid), "METRONOME"), item, status, egg);
WildBattleState Party(WildBattleState state, ImmutableArray<WildBattleCalledPartyMember> player = default,
    ImmutableArray<WildBattleCalledPartyMember> opponent = default)
{
    var p = new WildBattleCalledPartyMember(WildBattleTransformationRules.PersistentPokemon(state.Player), state.Player.HeldItem, WildBattleTransformationRules.PersistentStatus(state.Player), KnockedOffItem: state.Player.KnockedOffItem);
    var o = new WildBattleCalledPartyMember(WildBattleTransformationRules.PersistentPokemon(state.Opponent), state.Opponent.HeldItem, WildBattleTransformationRules.PersistentStatus(state.Opponent), KnockedOffItem: state.Opponent.KnockedOffItem);
    return state with { CalledParty = new(player.IsDefault ? [p] : player.Insert(0, p), opponent.IsDefault ? [o] : opponent.Insert(0, o), 0, 0,
        opponent.IsDefaultOrEmpty ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer) };
}
WildBattleCalledMoveResult Act(string name, WildBattleState? state = null, WildBattleSide side = WildBattleSide.Player)
{
    var result = WildBattleRules.ResolveCalledMoveAction(catalog, state ?? Pair(), new(side, 0, Id(name)));
    Check(result.Succeeded, name + " " + result.Fault); Check(result.State is not null, "state returned");
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!) is null,
        name + " valid output: " + WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!));
    return result;
}
WildBattleState Switch(WildBattleState state, WildBattleSide side, int index)
{
    var result = WildBattleRules.ResolveCalledPartySwitch(catalog, state, new(side, index));
    Check(result.Succeeded, "switch: " + result.Fault);
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!) is null, "switched output valid " + WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!));
    return result.State!;
}
void Atomic(WildBattleState state, WildBattleResult result, string label)
{
    Check(!result.Succeeded && result.State == state && result.Events.IsEmpty && result.Trace.IsEmpty, label + " exact failure atomicity");
}
bool Identity(PokemonInstance a, PokemonInstance b) => a with { Moves = [], CurrentHp = 1 } == b with { Moves = [], CurrentHp = 1 };
Test("source14 member metadata, no natural caller enablement, forbidden transitive IDs remain forbidden", () => {
    Check(Environment.Version.Major == 10 && catalog.Species.Count == 107, "runtime and bounded species profile");
    string[] members = ["TRANSFORM", "CONVERSION", "CONVERSION_2", "SPIKES", "HEAL_BELL", "BATON_PASS", "BEAT_UP", "MEMENTO", "ROLE_PLAY", "RECYCLE", "SKILL_SWAP", "CAMOUFLAGE", "TEETER_DANCE", "AROMATHERAPY"];
    foreach (var member in members) Check(MetronomeSourceMoveDefinition.Matches(catalog.Moves[Id(member)]) && WildBattleRules.IsCalledMoveEffectSupported(catalog.Moves[Id(member)]), member + " authenticated registered");
    foreach (var name in new[] { "TRICK", "THIEF", "COVET" })
    { var state = Party(Pair()); var r = WildBattleRules.ResolveCalledMoveAction(catalog, state, new(WildBattleSide.Player, 0, Id(name))); Atomic(state, r.Battle, name); Check(r.Fault == "called_move_metronome_forbidden", "strict direct Metronome gate"); }
    Check(!catalog.Species.Values.Any(s => new[] { s.Ability1, s.Ability2 }.Any(a => a is "ABILITY_CLOUD_NINE" or "ABILITY_AIR_LOCK" or "ABILITY_FORECAST")), "no invented species or weather suppressor");
});
Test("Role Play copies Koga Weezing Levitate and preserves permanent identity, PID, IVs, ability and PP", () => {
    var state = Pair(target: "WEEZING"); var r = Act("ROLE_PLAY", state);
    Check(WildBattleEffective.Ability(r.State!.Player) == "ABILITY_LEVITATE", "actual copied Levitate");
    Check(Identity(state.Player.Pokemon, r.State.Player.Pokemon), "permanent identity unchanged");
    Check(r.State.Player.Pokemon.AbilityId == state.Player.Pokemon.AbilityId, "permanent ability unchanged");
    Check(r.State.Player.Pokemon.Moves[0].CurrentPp == 9 && r.State.Opponent.Pokemon.Moves[0].CurrentPp == 10, "only actual caller PP slot");
    Check(!r.State.Player.Pokemon.Moves.Any(m => m.Move == Id("ROLE_PLAY")), "called move unlearned");
    var immune = Act("EARTHQUAKE", r.State, WildBattleSide.Opponent);
    Check(immune.State!.Player.Pokemon.CurrentHp == state.Player.Pokemon.CurrentHp, "shared damage honors battle Levitate");
    Check(immune.Battle.Events.OfType<WildBattleAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_LEVITATE"), "shared ability event");
    var again = Act("ROLE_PLAY", r.State); Check(again.State!.Player.CalledOverlay?.Ability == "ABILITY_LEVITATE", "same ability is allowed in source Role Play");
});
Test("Skill Swap swaps battle abilities; shared immunity move-end clears poison and sleep", () => {
    var state = Pair(actor: "SNORLAX", target: "WEEZING");
    // Source Snorlax PID0 is Immunity; determine a valid PID if necessary without forging ability fields.
    state = state with { Player = state.Player with { Pokemon = Moves(Enumerable.Range(0, 4).Select(i => Mon("SNORLAX", pid: (uint)i)).First(m => m.AbilityId == "ABILITY_IMMUNITY"), "METRONOME") },
        Opponent = state.Opponent with { Status = WildBattleStatus.Poison } };
    var r = Act("SKILL_SWAP", state);
    Check(WildBattleEffective.Ability(r.State!.Player) == "ABILITY_LEVITATE" && WildBattleEffective.Ability(r.State.Opponent) == "ABILITY_IMMUNITY", "both battle ability registers swapped");
    Check(r.State.Opponent.Status == 0, "move-end Immunity clears status");
    Check(r.State.Player.Pokemon.AbilityId == "ABILITY_IMMUNITY" && r.State.Opponent.Pokemon.AbilityId == "ABILITY_LEVITATE", "both permanent abilities intact");
    var s = Pair(target: "HYPNO") with { Player = Pair().Player with { Status = 1 } };
    // A last sleep turn wakes before calling, allowing Role Play to copy actual Insomnia.
    var sleep = Act("ROLE_PLAY", s); Check(WildBattleEffective.Ability(sleep.State!.Player) == "ABILITY_INSOMNIA", "authentic insomnia copied after cancellation");
});
Test("Conversion uses actual learned-slot types and rejection RNG, ignores called move presence", () => {
    var s = Pair(); s = s with { Player = s.Player with { Pokemon = Moves(s.Player.Pokemon, "METRONOME", "EMBER", "WATER_GUN") } };
    var r = Act("CONVERSION", s);
    Check(r.State!.Player.CalledOverlay?.Type1 is "TYPE_FIRE" or "TYPE_WATER", "learned move type");
    Check(r.State.Player.CalledOverlay!.Type1 == r.State.Player.CalledOverlay.Type2, "single resulting type");
    Check(Identity(s.Player.Pokemon, r.State.Player.Pokemon), "species/identity do not change");
    Check(r.State.Player.Pokemon.Moves[1].CurrentPp == s.Player.Pokemon.Moves[1].CurrentPp, "donor learned move PP unchanged");
    Check(r.Battle.Trace.All(t => t.Purpose.EndsWith("conversion-slot")), "Conversion no selected target or accuracy draw");
    var failed = Act("CONVERSION", Pair()); Check(failed.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "no-different-move-type") && failed.Battle.Trace.IsEmpty, "source no-candidate failure has no RNG");
});
Test("Camouflage maps all ten source terrains, current types govern status immunity and damage", () => {
    foreach (var terrain in Enum.GetValues<WildBattleTerrain>().Where(t => t <= WildBattleTerrain.Plain))
    {
        var s = Pair(actor: "SQUIRTLE") with { Terrain = terrain }; var r = Act("CAMOUFLAGE", s);
        var expected = WildBattleSourcePartyEffects.CamouflageType(terrain);
        Check(WildBattleEffective.HasType(catalog, r.State!.Player, expected), "terrain " + terrain);
        Check(r.Battle.Trace.IsEmpty && r.State.Player.Pokemon.Moves[0].CurrentPp == 9, "terrain no RNG with caller PP");
        Check(Identity(s.Player.Pokemon, r.State.Player.Pokemon), "terrain permanent data");
    }
    var grass = Act("CAMOUFLAGE", Pair() with { Terrain = WildBattleTerrain.Grass }).State!;
    var seed = Act("LEECH_SEED", grass, WildBattleSide.Opponent);
    Check(seed.State!.Player.LeechSeedSource is null, "effective Grass blocks Leech Seed");
    var fire = Pair(); fire = fire with { Player = fire.Player with { Pokemon = Moves(fire.Player.Pokemon, "METRONOME", "EMBER") } };
    fire = Act("CONVERSION", fire).State!; var burn = Act("WILL_O_WISP", fire, WildBattleSide.Opponent);
    Check(burn.State!.Player.Status == 0, "effective Fire blocks burn");
});
Test("type overlays reach Substitute Curse routing and Spit Up's distinct damage commands", () => {
    var ghost = Pair(); ghost = ghost with { Player = ghost.Player with { Pokemon = Moves(ghost.Player.Pokemon, "METRONOME", "SHADOW_BALL") }, Opponent = ghost.Opponent with { CalledVolatile = new() { SubstituteHp = 5 } } };
    ghost = Act("CONVERSION", ghost).State!; int before = ghost.Player.Pokemon.CurrentHp; var curse = Act("CURSE", ghost);
    Check(curse.State!.Player.Pokemon.CurrentHp == before && !curse.State.Opponent.Cursed, "effective Ghost chooses source Substitute-blocked Curse branch");
    var immune = Pair(); immune = immune with { Player = immune.Player with { CalledVolatile = new() { StockpileCount = 1 } }, Opponent = immune.Opponent with { Pokemon = Moves(immune.Opponent.Pokemon, "METRONOME", "SHADOW_BALL") } };
    immune = Act("CONVERSION", immune, WildBattleSide.Opponent).State!; before = immune.Opponent.Pokemon.CurrentHp;
    var spit = Act("SPIT_UP", immune);
    Check(spit.State!.Opponent.Pokemon.CurrentHp == before && spit.State.Player.CalledVolatile?.StockpileCount is null or 0, "effective Ghost type blocks typeless-base Spit Up after stockpile consumption");
});
Test("modern Conversion2 candidates are complete, unique, ordered, and exclude both current types", () => {
    using var oracle = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "source-vectors.json")));
    string[] types = ["NORMAL", "FIGHTING", "FLYING", "POISON", "GROUND", "ROCK", "BUG", "GHOST", "STEEL", "MYSTERY", "FIRE", "WATER", "GRASS", "ELECTRIC", "PSYCHIC", "ICE", "DRAGON", "DARK"];
    var valid = Enumerable.Range(0, types.Length).Where(i => i != 9).ToArray();
    var sourceLists = oracle.RootElement.GetProperty("conversion2candidateTypes");
    for (int attack = 0; attack < types.Length; attack++) foreach (int first in valid) foreach (int second in valid)
    {
        var expected = sourceLists[attack].EnumerateArray().Select(e => e.GetInt32()).Where(i => i != first && i != second).Select(i => "TYPE_" + types[i]).ToArray();
        var actual = WildBattleSourcePartyEffects.Conversion2Candidates("TYPE_" + types[attack], "TYPE_" + types[first], "TYPE_" + types[second]);
        Check(actual.SequenceEqual(expected) && actual.Distinct().Count() == actual.Length && !actual.Contains("TYPE_MYSTERY"), "independent source-chart completeness, ordered deduplication and dual exclusion");
    }
    var dual = Pair(actor: "CHARIZARD"); dual = dual with { Player = dual.Player with { LastLandedMove = Id("EMBER"), LastHitBy = WildBattleSide.Opponent, SourceMoveHistory = new() { LastHitByType = "TYPE_FIRE" } } };
    var result = Act("CONVERSION_2", dual);
    Check(result.State!.Player.CalledOverlay!.Type1 is "TYPE_ROCK" or "TYPE_WATER" or "TYPE_DRAGON", "actual dual Fire/Flying user excludes its Fire type");
    Check(result.Battle.Trace.Length == 1 && result.State.Player.Pokemon.Moves[0].CurrentPp == 9, "one candidate draw and caller debit");
    var repeated = Act("CONVERSION_2", dual); Check(JsonSerializer.Serialize(result) == JsonSerializer.Serialize(repeated), "same seed and state give identical transaction");
    var absent = Act("CONVERSION_2", Pair()); Check(absent.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "no-landed-move") && absent.Battle.Trace.IsEmpty, "missing source history fails without selection RNG");
    var flying = Act("FLY", Pair(), WildBattleSide.Opponent).State!;
    var locked = Act("CONVERSION_2", flying);
    Check(locked.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "source-still-multiple-turns") && locked.Battle.Trace.IsEmpty, "original two-turn source validity gate retained");
});
Test("modern Conversion2 standard single-draw mapping covers every uint16 with near-equal buckets", () => {
    foreach (int count in Enumerable.Range(1, 17))
    {
        var buckets = new int[count]; for (int raw = 0; raw <= ushort.MaxValue; raw++) buckets[WildBattleSourcePartyEffects.Conversion2ChoiceIndex((ushort)raw, count)]++;
        Check(buckets.Sum() == 65536 && buckets.Min() == 65536 / count && buckets.Max() - buckets.Min() <= 1, "complete engine modulo mapping with bounded one-value bias");
    }
});
Test("Heal Bell excludes eggs/Soundproof, cures fainted reserves; Aromatherapy includes both", () => {
    var s = Pair(); s = s with { Player = s.Player with { Status = WildBattleStatus.Paralysis } };
    var voltorb = Enumerable.Range(0, 4).Select(i => Member("VOLTORB", pid: (uint)i, status: WildBattleStatus.Poison)).First(m => m.Pokemon.AbilityId == "ABILITY_SOUNDPROOF");
    var fainted = Member("RATTATA", status: WildBattleStatus.Burn); fainted = fainted with { Pokemon = fainted.Pokemon with { CurrentHp = 0 } };
    var egg = Member("SQUIRTLE", status: WildBattleStatus.Poison, egg: true);
    s = Party(s, [voltorb, fainted, egg]);
    // Choose a seed that does not fully paralyze the caller.
    var bell = Enumerable.Range(0, 20).Select(seed => WildBattleRules.ResolveCalledMoveAction(catalog, s with { Rng = new((uint)seed) }, new(WildBattleSide.Player, 0, Id("HEAL_BELL")))).First(r => r.Succeeded && r.State!.Player.Status == 0);
    Check(bell.State!.CalledParty!.Player[1].Status == WildBattleStatus.Poison, "Soundproof reserve unchanged");
    Check(bell.State.CalledParty.Player[2].Status == 0 && bell.State.CalledParty.Player[2].Pokemon.CurrentHp == 0, "fainted reserve cured without revival");
    Check(bell.State.CalledParty.Player[3].Status == WildBattleStatus.Poison, "egg excluded");
    var aroma = Act("AROMATHERAPY", bell.State);
    Check(aroma.State!.CalledParty!.Player.All(m => m.Status == 0), "Aromatherapy all genuine slots cured");
    Check(aroma.State.CalledParty.Opponent.SequenceEqual(s.CalledParty!.Opponent), "opposing party unchanged");
});
Test("party cures and Natural Cure clear genuine Transform party-status divergence", () => {
    var state = Act("TRANSFORM", Pair(target: "WEEZING")).State!;
    state = state with { Player = state.Player with { Transform = state.Player.Transform! with { PartyStatusOverride = 2 } } };
    state = Party(state, [Member("SQUIRTLE")]);
    var bell = Act("HEAL_BELL", state).State!;
    Check(bell.Player.Status == 0 && bell.Player.Transform!.PartyStatusOverride is null && bell.CalledParty!.Player[0].Status == 0, "party write clears sleeping permanent member even when battle view awake");
    var natural = Pair(target: "STARYU"); natural = natural with { Opponent = natural.Opponent with { Pokemon = Moves(Enumerable.Range(0, 4).Select(i => Mon("STARYU", 80, (uint)i)).First(m => m.AbilityId == "ABILITY_NATURAL_CURE"), "METRONOME") } };
    natural = Act("TRANSFORM", natural).State!;
    natural = natural with { Player = natural.Player with { Transform = natural.Player.Transform! with { PartyStatusOverride = 2 } } };
    natural = Switch(Party(natural, [Member("SQUIRTLE")]), WildBattleSide.Player, 1);
    Check(natural.CalledParty!.Player[0].Status == 0, "source switch-out Natural Cure writes actual permanent status");
});
Test("Beat Up excludes status/fainted/eggs and hits per real party member base stats", () => {
    var healthy = Member("MACHOKE", 50); var status = Member("RATTATA", status: WildBattleStatus.Poison);
    var dead = Member("SQUIRTLE"); dead = dead with { Pokemon = dead.Pokemon with { CurrentHp = 0 } };
    var egg = Member("CHARMANDER", egg: true);
    var s = Party(Pair(target: "GASTLY"), [healthy, status, dead, egg]);
    var r = Act("BEAT_UP", s); var members = r.Battle.Events.OfType<WildBattleCalledBeatUpMemberEvent>().ToArray();
    Check(members.Select(m => m.PartyIndex).SequenceEqual(new[] { 0, 1 }), "actual healthy contributors only");
    Check(r.State!.Opponent.Pokemon.CurrentHp < s.Opponent.Pokemon.CurrentHp, "source typeless Beat Up hits Ghost");
    var hits = r.Battle.Events.OfType<WildBattleHpChangedEvent>().Where(e => e.Move == Id("BEAT_UP")).ToArray();
    Check(hits.Length == 2, "one damage command per eligible party member");
    for (int i = 0; i < hits.Length; i++)
    {
        var p = s.CalledParty!.Player[i]; var baseDamage = catalog.Species[p.Pokemon.Species].BaseStats.Attack * 10 * (p.Pokemon.Level * 2 / 5 + 2) / catalog.Species[s.Opponent.Pokemon.Species].BaseStats.Defense / 50 + 2;
        Check(hits[i].Damage == Math.Max(1, baseDamage * (hits[i].Critical ? 2 : 1) * hits[i].RandomPercent / 100), "independent integer Beat Up damage " + i);
    }
    Check(r.State.Player.Pokemon.Moves[0].CurrentPp == 9 && r.State.CalledParty!.Player[1].Pokemon.Moves[0].CurrentPp == 10, "one caller debit and zero reserve PP");
    var sub = s with { Opponent = s.Opponent with { CalledVolatile = new() { SubstituteHp = 1 } } };
    var broken = Act("BEAT_UP", sub);
    Check(broken.Battle.Events.OfType<WildBattleSubstituteDamagedEvent>().Count() == 1 && broken.Battle.Events.OfType<WildBattleHpChangedEvent>().Count(e => e.Move == Id("BEAT_UP")) == 1, "first contributor breaks Substitute, next contributor reaches actual target");
});
Test("Spikes layers cap and actual switches consume grounded entry damage with Flying/Levitate exemptions", () => {
    foreach (string species in new[] { "RATTATA", "PIDGEY", "GASTLY" })
    {
        var s = Party(Pair(), opponent: [Member(species)]);
        for (int i = 0; i < 3; i++) s = Act("SPIKES", s).State!;
        Check(s.CalledParty!.OpponentSpikes == 3, "three source layers");
        var capped = Act("SPIKES", s); Check(capped.State!.CalledParty!.OpponentSpikes == 3, "fourth fails without layer change");
        int before = s.CalledParty.Opponent[1].Pokemon.CurrentHp; var after = Switch(capped.State, WildBattleSide.Opponent, 1);
        Check(after.Opponent.Pokemon.CurrentHp == (species == "RATTATA" ? before - before / 4 : before), "actual entry " + species);
        Check(after.CalledParty!.OpponentSpikes == 3, "entry preserves side layers");
    }
});
Test("Rapid Spin consumes genuine Spikes layers after a real called hit", () => {
    var state = Party(Pair()); state = Act("SPIKES", state, WildBattleSide.Opponent).State!;
    Check(state.CalledParty!.PlayerSpikes == 1, "opponent actually placed player-side Spikes");
    var result = Act("RAPID_SPIN", state);
    Check(result.State!.CalledParty!.PlayerSpikes == 0 && result.Battle.Events.OfType<WildBattleCalledSpikesChangedEvent>().Any(e => e.Side == WildBattleSide.Player && e.Before == 1 && e.After == 0), "source spin clears real layer state");
});
Test("Spikes entry faint exposes a second genuine replacement and skips entry ability", () => {
    var frail = Enumerable.Range(0, 4).Select(i => Member("GROWLITHE", pid: (uint)i)).First(m => m.Pokemon.AbilityId == "ABILITY_INTIMIDATE"); frail = frail with { Pokemon = frail.Pokemon with { CurrentHp = 1 } };
    var state = Party(Pair(), [frail, Member("PIDGEY")]); state = Act("SPIKES", state, WildBattleSide.Opponent).State!;
    var result = WildBattleRules.ResolveCalledPartySwitch(catalog, state, new(WildBattleSide.Player, 1));
    Check(result.Succeeded && result.State!.Player.Pokemon.CurrentHp == 0 && result.State.CalledParty!.PendingSwitch!.LegalPartyIndices.SequenceEqual(new[] { 0, 2 }), "entry faint leaves real candidates including departing healthy member");
    Check(!result.Events.OfType<WildBattleAbilityActivatedEvent>().Any(e => e.Ability == "ABILITY_INTIMIDATE"), "fainted entry never activates Intimidate");
    var next = Switch(result.State!, WildBattleSide.Player, 2);
    Check(next.Player.Pokemon.CurrentHp == next.Player.Pokemon.Stats.MaxHp && next.CalledParty!.PendingSwitch is null && next.CalledParty.PlayerSpikes == 1, "flying replacement consumes phase and preserves layers");
});
Test("actual deferred trainer kernel keeps Memento and entry-Spikes faint outcomes for its owner", () => {
    var reserve = Member("SQUIRTLE"); reserve = reserve with { Pokemon = reserve.Pokemon with { CurrentHp = 1 } };
    var state = Party(Pair(), [reserve]); state = state with { CalledParty = state.CalledParty! with { BattleKind = WildBattleCalledBattleKind.Trainer, PlayerSpikes = 1 } };
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, state) is null, "genuine initial party context");
    // Visibility bridge only: invoke the production internal trainer Execution, without a parallel test engine.
    const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
    var type = typeof(WildBattleRules).GetNestedType("Execution", System.Reflection.BindingFlags.NonPublic)!;
    var kernel = type.GetConstructors(flags).Single().Invoke(new object[] { state, true, true });
    type.GetMethod("ExecuteCalledMoveAction", flags)!.Invoke(kernel, new object[] { catalog, new WildBattleCalledMoveAction(WildBattleSide.Player, 0, Id("MEMENTO")), catalog.Moves[Id("METRONOME")], catalog.Moves[Id("MEMENTO")] });
    var afterAction = (WildBattleState)type.GetField("State", flags)!.GetValue(kernel)!;
    Check(afterAction.Player.Pokemon.CurrentHp == 0 && afterAction.CalledParty!.PendingSwitch is null && afterAction.Outcome == WildBattleOutcome.Ongoing, "real called self-faint remains deferred for trainer EXP/phase");
    type.GetMethod("SourceCalledPartySwitch", flags)!.Invoke(kernel, new object?[] { catalog, WildBattleSide.Player, 1, WildBattleCalledSwitchKind.FaintedReplacement, null });
    var afterSwitch = (WildBattleState)type.GetField("State", flags)!.GetValue(kernel)!;
    var events = (IEnumerable<WildBattleEvent>)type.GetField("Events", flags)!.GetValue(kernel)!;
    Check(afterSwitch.Player.Pokemon.CurrentHp == 0 && !WildBattleSourcePartyEffects.HasLivingMember(afterSwitch.CalledParty!, WildBattleSide.Player), "actual last reserve faints from real Spikes entry");
    Check(afterSwitch.Outcome == WildBattleOutcome.Ongoing && afterSwitch.CalledParty!.PendingSwitch is null && !events.OfType<WildBattleEndedEvent>().Any() && !events.OfType<WildBattleCalledPartySwitchRequiredEvent>().Any(), "no premature terminal or pending before trainer owner resolves EXP and phase");
    Check(events.OfType<WildBattleFaintedEvent>().Count() == 2 && events.OfType<WildBattleCalledPartySwitchedEvent>().Count() == 1, "two real faint commands and one actual transition");
});
Test("Baton Pass exposes genuine choices and transfers only exact source subset", () => {
    var s = Pair(); s = s with { Player = s.Player with { AttackStage = 8, FocusEnergy = true, Rooted = true, WaterSport = true,
        MudSport = true, ConfusionTurnsRemaining = 4, DefenseCurled = true, YawnTurnsRemaining = 2, Cursed = true, MeanLookSource = WildBattleSide.Opponent,
        CalledVolatile = new() { SubstituteHp = 5, PerishSongTimer = 2, StockpileCount = 3, Tormented = true, TauntTurnsRemaining = 0, Grudge = true },
        CalledOverlay = new() { Type1 = "TYPE_FIRE", Type2 = "TYPE_FIRE" } } };
    var egg = Member("RATTATA", egg: true); var dead = Member("SQUIRTLE"); dead = dead with { Pokemon = dead.Pokemon with { CurrentHp = 0 } };
    s = Party(s, [Member("PIDGEY"), egg, dead]);
    // Source confusion may prevent the caller; select an actual successful action, retaining its trace.
    var result = Enumerable.Range(0, 100).Select(seed => WildBattleRules.ResolveCalledMoveAction(catalog, s with { Rng = new((uint)seed) }, new(WildBattleSide.Player, 0, Id("BATON_PASS"))))
        .First(r => r.Succeeded && r.State!.CalledParty!.PendingSwitch is not null);
    var pending = result.State!.CalledParty!.PendingSwitch!;
    Check(pending.LegalPartyIndices.SequenceEqual(new[] { 1 }), "only real conscious non-egg reserves");
    Atomic(result.State, WildBattleRules.ResolveCalledPartySwitch(catalog, result.State, new(WildBattleSide.Player, 2)), "invalid Baton candidate");
    var switched = Switch(result.State, WildBattleSide.Player, 1);
    Check(switched.Player.Pokemon.SourceSpeciesId == 16 && switched.CalledParty!.PlayerActiveIndex == 1, "actual reserve installed");
    Check(switched.Player.AttackStage == 8 && switched.Player.FocusEnergy && switched.Player.Rooted && switched.Player.Cursed && switched.Player.MeanLookSource is not null, "source transferred stages/status");
    Check(switched.Player.CalledVolatile?.SubstituteHp == 5 && switched.Player.CalledVolatile.PerishSongTimer == 2 && switched.Player.CalledVolatile.StockpileCount == 0 && !switched.Player.CalledVolatile.Tormented && !switched.Player.CalledVolatile.Grudge, "exact B07 Baton subset");
    Check(switched.Player.CalledOverlay is null && !switched.Player.DefenseCurled && switched.Player.YawnTurnsRemaining == 0, "overlay and nonpassable flags reset");
    Check(switched.CalledParty!.Player[0].Pokemon.Moves[0].CurrentPp == 9, "outgoing caller debit persisted");
});
Test("Voluntary switch restores Role Play and Transform permanent identity and clears local extensions", () => {
    var s = Party(Pair(target: "WEEZING"), [Member("SQUIRTLE")]); s = Act("ROLE_PLAY", s).State!;
    var back = Switch(Switch(s, WildBattleSide.Player, 1), WildBattleSide.Player, 0);
    Check(back.Player.CalledOverlay is null && WildBattleEffective.Ability(back.Player) == s.Player.Pokemon.AbilityId, "Role Play never persists");
    var original = back.Player.Pokemon; var transformed = Act("TRANSFORM", back).State!;
    Check(transformed.Player.Transform is not null && transformed.Player.Pokemon.Species == transformed.Opponent.Pokemon.Species, "existing Transform delegate");
    var returned = Switch(Switch(transformed, WildBattleSide.Player, 1), WildBattleSide.Player, 0);
    Check(Identity(original, returned.Player.Pokemon) && returned.Player.Transform is null, "permanent Transform identity restored");
    Check(returned.Player.Pokemon.Moves[0].Move == Id("METRONOME") && returned.Player.Pokemon.Moves[0].CurrentPp == original.Moves[0].CurrentPp - 1, "original PP rather than copied moves");
});
Test("Recycle restores an actually consumed berry and its source slot register survives switching", () => {
    var s = Pair(); s = s with { Player = s.Player with { HeldItem = "ITEM_PECHA_BERRY", Status = WildBattleStatus.Poison } };
    s = Party(s, [Member("SQUIRTLE")]); var consumed = Act("SPLASH", s).State!;
    Check(consumed.Player.Status == 0 && consumed.Player.HeldItem == "ITEM_NONE" && consumed.CalledParty!.PlayerUsedHeldItem == "ITEM_PECHA_BERRY", "real move-end consumption");
    var switched = Switch(consumed, WildBattleSide.Player, 1); var recycled = Act("RECYCLE", switched).State!;
    Check(recycled.Player.HeldItem == "ITEM_PECHA_BERRY" && recycled.CalledParty!.PlayerUsedHeldItem is null, "same source battler slot recycles after switch");
    Check(recycled.CalledParty!.Player[0].HeldItem == "ITEM_NONE" && recycled.CalledParty.Player[1].HeldItem == "ITEM_PECHA_BERRY", "actual party item delta");
    var again = Act("RECYCLE", recycled); Check(again.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "no-recyclable-item"), "no duplicate item");
});
Test("Memento source minimum, Protect, Substitute and faint reserve transitions", () => {
    var s = Pair(); var minimum = Act("MEMENTO", s with { Opponent = s.Opponent with { AttackStage = 0, SpecialAttackStageDelta = -6 } });
    Check(minimum.State!.Player.Pokemon.CurrentHp == s.Player.Pokemon.CurrentHp, "min stats do not sacrifice");
    var protectedState = Party(s with { Opponent = s.Opponent with { Protected = true } }, [Member("SQUIRTLE")]);
    var protect = Act("MEMENTO", protectedState).State!;
    Check(protect.Player.Pokemon.CurrentHp == 0 && protect.Opponent.AttackStage == 6 && protect.CalledParty!.PendingSwitch?.Kind == WildBattleCalledSwitchKind.FaintedReplacement, "Protect still sacrifices and exposes genuine replacement");
    var replacement = Switch(protect, WildBattleSide.Player, 1); Check(replacement.Player.Pokemon.CurrentHp > 0 && replacement.Outcome == WildBattleOutcome.Ongoing, "faint consumer resumes genuine party");
    var substitute = Act("MEMENTO", Party(s with { Opponent = s.Opponent with { CalledVolatile = new() { SubstituteHp = 5 } } }, [Member("SQUIRTLE")]));
    Check(substitute.State!.Player.Pokemon.CurrentHp == 0 && substitute.State.Opponent.AttackStage == 6, "Substitute protects stats but not actor HP");
    var success = Act("MEMENTO", Party(s, [Member("SQUIRTLE")])); Check(success.State!.Opponent.AttackStage == 4 && success.State.Opponent.SpecialAttackStageDelta == -2, "actual dual stage drop");
});
Test("Teeter Dance primary confusion skips source Own Tempo and Substitute branches before accuracy", () => {
    var s = Pair(target: "RATTATA"); var result = Act("TEETER_DANCE", s);
    Check(result.State!.Opponent.ConfusionTurnsRemaining is >= 2 and <= 5, "actual opponent confusion");
    Check(result.State.Player.ConfusionTurnsRemaining == 0, "caller skipped");
    var ownTempo = Pair(target: "SLOWPOKE"); ownTempo = ownTempo with { Opponent = ownTempo.Opponent with { Pokemon = Moves(Enumerable.Range(0, 4).Select(i => Mon("SLOWPOKE", pid: (uint)i)).First(m => m.AbilityId == "ABILITY_OWN_TEMPO"), "METRONOME") } };
    var prevented = Act("TEETER_DANCE", ownTempo); Check(prevented.State!.Opponent.ConfusionTurnsRemaining == 0 && prevented.Battle.Trace.IsEmpty, "Own Tempo no accuracy draw");
    var sub = Act("TEETER_DANCE", s with { Opponent = s.Opponent with { CalledVolatile = new() { SubstituteHp = 4 } } });
    Check(sub.State!.Opponent.ConfusionTurnsRemaining == 0 && sub.Battle.Trace.IsEmpty, "Substitute no accuracy draw");
});
Test("parties and overlays validate genuine data, preserve legacy omissions, round-trip, and overflow atomically", () => {
    var ordinary = Pair(); string json = JsonSerializer.Serialize(ordinary);
    Check(!json.Contains("CalledParty") && !json.Contains("CalledOverlay"), "legacy default omission");
    var s = Party(ordinary, [Member("SQUIRTLE")]);
    Check(WildBattleRules.Validate(catalog, s) is not null, "legacy gate rejects extension");
    var roundtrip = JsonSerializer.Deserialize<WildBattleState>(JsonSerializer.Serialize(s))!;
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, roundtrip) is null, "genuine active array content survives serialization identity");
    foreach (var ability in new[] { "ABILITY_AIR_LOCK", "ABILITY_FORECAST", "ABILITY_PRESSURE" })
    {
        var invalid = ordinary with { Player = ordinary.Player with { CalledOverlay = new() { Ability = ability } } };
        Check(WildBattleRules.ValidateCalledMoveActionState(catalog, invalid) == "called_overlay_ability_provenance", "reject unauthenticated ability " + ability);
    }
    var mismatch = s with { CalledParty = s.CalledParty! with { PlayerActiveIndex = 1 } }; Check(WildBattleRules.ValidateCalledMoveActionState(catalog, mismatch) == "called_party_active_mismatch", "active mismatch");
    var mystery = ordinary with { Player = ordinary.Player with { CalledOverlay = new() { Type1 = "TYPE_MYSTERY", Type2 = "TYPE_MYSTERY" } } };
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, mystery) == "called_overlay_type", "no source-unreachable Mystery overlay");
    foreach (var name in new[] { "ROLE_PLAY", "SPIKES", "HEAL_BELL", "BATON_PASS", "RECYCLE" })
    { var overflow = s with { EventSequence = long.MaxValue }; Atomic(overflow, WildBattleRules.ResolveCalledMoveAction(catalog, overflow, new(WildBattleSide.Player, 0, Id(name))).Battle, name + " overflow"); }
    foreach (var name in new[] { "SPIKES", "HEAL_BELL", "BATON_PASS", "BEAT_UP", "RECYCLE" })
        Atomic(ordinary, WildBattleRules.ResolveCalledMoveAction(catalog, ordinary, new(WildBattleSide.Player, 0, Id(name))).Battle, name + " genuine party required");
});
Test("Knock Off keeps permanent item; Recycle changes that item and switch preserves source suppression", () => {
    var s = Pair(); s = s with { Player = s.Player with { HeldItem = "ITEM_PECHA_BERRY", Status = WildBattleStatus.Poison },
        Opponent = s.Opponent with { Pokemon = Moves(Mon("RATTATA", 5), "METRONOME") } };
    s = Party(s, [Member("SQUIRTLE", item: "ITEM_HARD_STONE")]);
    s = Act("SPLASH", s).State!; s = Switch(s, WildBattleSide.Player, 1);
    s = Act("KNOCK_OFF", s, WildBattleSide.Opponent).State!;
    Check(s.Player.HeldItem == "ITEM_NONE" && s.Player.KnockedOffItem == "ITEM_HARD_STONE", "source battle knock-off flag");
    Check(WildBattleSourcePartyEffects.PersistentHeldItem(s.CalledParty!.Player[1]) == "ITEM_HARD_STONE", "Knock Off does not write permanent item");
    s = Act("RECYCLE", s).State!;
    Check(s.Player.HeldItem == "ITEM_PECHA_BERRY" && WildBattleSourcePartyEffects.PersistentHeldItem(s.CalledParty!.Player[1]) == "ITEM_PECHA_BERRY", "Recycle writes new permanent item despite old knock-off bit");
    s = Switch(Switch(s, WildBattleSide.Player, 0), WildBattleSide.Player, 1);
    Check(s.Player.HeldItem == "ITEM_NONE" && WildBattleSourcePartyEffects.PersistentHeldItem(s.CalledParty!.Player[1]) == "ITEM_PECHA_BERRY", "re-entry suppresses battle item without undoing permanent recycle");
});
Test("voluntary switch source trapping is consumed while Baton Pass bypasses escape prevention", () => {
    var state = Pair(target: "DIGLETT"); state = state with { Opponent = state.Opponent with {
        Pokemon = Moves(Enumerable.Range(0, 4).Select(i => Mon("DIGLETT", 20, (uint)i)).First(m => m.AbilityId == "ABILITY_ARENA_TRAP"), "METRONOME") } };
    state = Party(state, [Member("SQUIRTLE")]);
    Atomic(state, WildBattleRules.ResolveCalledPartySwitch(catalog, state, new(WildBattleSide.Player, 1)), "source Arena Trap");
    var pending = Act("BATON_PASS", state).State!; Check(Switch(pending, WildBattleSide.Player, 1).Player.Pokemon.SourceSpeciesId == 7, "Baton ignores source escape prevention");
});

Test("Baton passes sure-hit source state, resets other-side duration, cures incoming Own Tempo", () => {
    var state = Pair(); state = state with { Player = state.Player with { ConfusionTurnsRemaining = 4,
        CalledTurnState = new() { SureHitTurnsRemaining = 1, SureHitSource = WildBattleSide.Opponent } },
        Opponent = state.Opponent with { CalledTurnState = new() { SureHitTurnsRemaining = 1, SureHitSource = WildBattleSide.Player } } };
    var reserve = Enumerable.Range(0, 4).Select(i => Member("LICKITUNG", pid: (uint)i)).First(m => m.Pokemon.AbilityId == "ABILITY_OWN_TEMPO");
    state = Party(state, [reserve]);
    var baton = Enumerable.Range(0, 100).Select(seed => WildBattleRules.ResolveCalledMoveAction(catalog, state with { Rng = new((uint)seed) }, new(WildBattleSide.Player, 0, Id("BATON_PASS"))))
        .First(r => r.Succeeded && r.State!.CalledParty!.PendingSwitch is not null).State!;
    var result = Switch(baton, WildBattleSide.Player, 1);
    Check(result.Player.CalledTurnState?.SureHitTurnsRemaining == 1 && result.Player.CalledTurnState.SureHitSource == WildBattleSide.Opponent, "own incoming sure-hit source carried");
    Check(result.Opponent.CalledTurnState?.SureHitTurnsRemaining == 2, "other side sure-hit duration reset2");
    Check(result.Player.ConfusionTurnsRemaining == 0, "source move-end Own Tempo cures passed confusion");
});
Test("true toxic reserve entry resets counter while preserving side weather and delayed slots", () => {
    // Stored party status has no active Toxic counter. Reject an impossible boxed counter first.
    var invalid = Party(Pair(), [Member("SQUIRTLE", status: WildBattleSourceFieldEffects.ToxicPoison | 0xA00)]);
    Atomic(invalid, WildBattleRules.ResolveCalledPartySwitch(catalog, invalid, new(WildBattleSide.Player, 1)), "invalid reserve toxic counter");
    var state = Party(Pair(), [Member("SQUIRTLE", status: WildBattleSourceFieldEffects.ToxicPoison)]);
    state = state with { RainTurnsRemaining = 3, CalledField = new() { OpponentLightScreenTurnsRemaining = 4 } };
    var entered = Switch(state, WildBattleSide.Player, 1);
    entered = entered with { Player = entered.Player with { Status = WildBattleSourceFieldEffects.ToxicPoison | 0xA00 } };
    var result = Switch(Switch(entered, WildBattleSide.Player, 0), WildBattleSide.Player, 1);
    Check(result.Player.Status == WildBattleSourceFieldEffects.ToxicPoison && result.CalledParty!.Player[1].Status == WildBattleSourceFieldEffects.ToxicPoison, "source active toxic counter resets across actual switch and genuine party sync");
    Check(result.RainTurnsRemaining == 3 && result.CalledField!.OpponentLightScreenTurnsRemaining == 4, "side weather preserved");
});

Test("source party/overlay failure branches preserve PP order and exact target checks", () => {
    var soundproof = Pair(actor: "VOLTORB"); soundproof = soundproof with { Player = soundproof.Player with {
        Pokemon = Moves(Enumerable.Range(0, 4).Select(i => Mon("VOLTORB", pid: (uint)i)).First(m => m.AbilityId == "ABILITY_SOUNDPROOF"), "METRONOME"), Status = WildBattleStatus.Poison } };
    soundproof = Party(soundproof, [Member("SQUIRTLE", status: WildBattleStatus.Burn)]);
    var bell = Act("HEAL_BELL", soundproof).State!;
    Check(bell.Player.Status == WildBattleStatus.Poison && bell.CalledParty!.Player[1].Status == 0, "active Soundproof skips only itself");
    var poisoned = Pair(); poisoned = poisoned with { Player = poisoned.Player with { Status = WildBattleStatus.Burn } };
    var emptyBeatUp = Act("BEAT_UP", Party(poisoned));
    Check(emptyBeatUp.State!.Opponent.Pokemon.CurrentHp == poisoned.Opponent.Pokemon.CurrentHp && emptyBeatUp.State.Player.Pokemon.Moves[0].CurrentPp == 9, "no eligible BeatUp spends only caller PP");
    var noBaton = Act("BATON_PASS", Party(Pair()));
    Check(noBaton.State!.CalledParty!.PendingSwitch is null && noBaton.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "no-eligible-reserve"), "no fabricated Baton reserve");
    var protectedState = Pair(target: "WEEZING"); protectedState = protectedState with { Opponent = protectedState.Opponent with { Protected = true } };
    var role = Act("ROLE_PLAY", protectedState); Check(role.State!.Player.CalledOverlay?.Ability == "ABILITY_LEVITATE", "source Role Play ignores Protect");
    var skill = Act("SKILL_SWAP", protectedState); Check(skill.State!.Player.CalledOverlay is null && skill.State.Player.Pokemon.Moves[0].CurrentPp == 9, "source Skill Swap respects Protect without accuracy RNG");
    var curse = Pair(actor: "MAGNETON"); curse = curse with { Player = curse.Player with { Pokemon = Moves(curse.Player.Pokemon, "METRONOME", "CURSE") } };
    var converted = Act("CONVERSION", curse); Check(converted.State!.Player.CalledOverlay!.Type1 == "TYPE_NORMAL", "Mystery source Curse move maps to Normal for non-Ghost");
    var cutter = catalog.Species.Values.FirstOrDefault(s => s.Ability1 == "ABILITY_HYPER_CUTTER" || s.Ability2 == "ABILITY_HYPER_CUTTER");
    if (cutter is not null)
    {
        string species = cutter.Id.Value["pret:SPECIES_".Length..];
        var hyper = Pair(target: species); hyper = hyper with { Opponent = hyper.Opponent with {
            Pokemon = Moves(Enumerable.Range(0, 4).Select(i => Mon(species, 80, (uint)i)).First(m => m.AbilityId == "ABILITY_HYPER_CUTTER"), "METRONOME") } };
        var memento = Act("MEMENTO", Party(hyper, [Member("SQUIRTLE")]));
        Check(memento.State!.Opponent.AttackStage == 6 && memento.State.Opponent.SpecialAttackStageDelta == -2, "real Hyper Cutter blocks Attack only");
    }
    else Check(!catalog.Species.Values.Any(s => s.Ability1 == "ABILITY_HYPER_CUTTER" || s.Ability2 == "ABILITY_HYPER_CUTTER"), "no fabricated Hyper Cutter user");
});

Test("Wish and delayed attacks follow genuine field slots across both-side switches", () => {
    WildBattleState End(WildBattleState state) { var r = WildBattleRules.ResolveCalledVolatileEndTurn(catalog, state); Check(r.Succeeded, "real-party volatile consumer " + r.Fault); Check(WildBattleRules.ValidateCalledMoveActionState(catalog, r.State!) is null, "end-turn party sync"); return r.State!; }
    var wounded = Member("SNORLAX"); wounded = wounded with { Pokemon = wounded.Pokemon with { CurrentHp = 1 } };
    var wishState = Switch(Party(Pair(), [Member("SQUIRTLE"), wounded]), WildBattleSide.Player, 1);
    var wish = WildBattleRules.ResolveCalledMoveAction(catalog, wishState, new(WildBattleSide.Player, 0, Id("WISH")) { Context = new(0, 5) });
    Check(wish.Succeeded && wish.State!.CalledDelayed!.PlayerWish!.SourcePartyIndex == 1, "genuine active index overrides isolated caller context");
    var source = wish.State!.CalledDelayed!.PlayerWish!; var switched = Switch(wish.State, WildBattleSide.Player, 2);
    Check(switched.CalledDelayed!.PlayerWish == source, "Wish survives actual departure");
    switched = End(End(switched));
    Check(switched.Player.Pokemon.CurrentHp == 1 + wounded.Pokemon.Stats.MaxHp / 2 && switched.CalledDelayed!.PlayerWish is null, "Wish heals actual incoming occupant's maximum HP");
    Check(switched.CalledParty!.Player[1].Pokemon.CurrentHp == wishState.Player.Pokemon.CurrentHp && switched.CalledParty.Player[1].Pokemon.Moves[0].CurrentPp == 9, "departed caster HP and source caller debit");
    var transformed = Act("TRANSFORM", Party(Pair(target: "WEEZING"), [Member("SQUIRTLE")])).State!;
    var transformedWish = Act("WISH", transformed).State!;
    Check(transformedWish.CalledDelayed!.PlayerWish!.SourceSpecies == transformedWish.CalledParty!.Player[0].Pokemon.Species && transformedWish.CalledDelayed.PlayerWish.SourceSpecies != transformedWish.Player.Pokemon.Species, "transformed Wish attribution follows permanent party member");
    Check(Switch(transformedWish, WildBattleSide.Player, 1).CalledDelayed!.PlayerWish == transformedWish.CalledDelayed.PlayerWish, "transformed caster departure keeps genuine attribution");
    foreach (string move in new[] { "FUTURE_SIGHT", "DOOM_DESIRE" })
    {
        var state = Party(Pair(), [Member("SQUIRTLE")], [Member("DROWZEE", 80)]); var cast = Act(move, state).State!;
        var delayed = cast.CalledDelayed!.OpponentFutureAttack!;
        cast = Switch(Switch(cast, WildBattleSide.Player, 1), WildBattleSide.Opponent, 1);
        Check(cast.CalledDelayed!.OpponentFutureAttack == delayed, "delayed damage survives genuine attacker and target departure");
        int hp = cast.Opponent.Pokemon.CurrentHp; cast = cast with { Rng = new(0) };
        var resolved = End(End(End(cast)));
        Check(resolved.CalledDelayed!.OpponentFutureAttack is null && resolved.Opponent.Pokemon.CurrentHp < hp, "delayed source command hits true incoming target");
        Check(resolved.CalledParty!.Opponent[0].Pokemon.CurrentHp == state.Opponent.Pokemon.CurrentHp && resolved.CalledParty.Player[0].Pokemon.Moves[0].CurrentPp == 9, "departed target untouched and caster PP retained");
    }
});
Test("ordinary genuine departure clears Perish and local volatiles while the other slot keeps counting", () => {
    var state = Act("PERISH_SONG", Party(Pair(), [Member("SQUIRTLE")], [Member("PIDGEY")])).State!;
    state = state with { Player = state.Player with { CalledVolatile = state.Player.CalledVolatile! with { SubstituteHp = 5, StockpileCount = 2, Tormented = true, TauntTurnsRemaining = 2, Grudge = true } } };
    var switched = Switch(state, WildBattleSide.Player, 1);
    Check(switched.Player.CalledVolatile is null && switched.Opponent.CalledVolatile?.PerishSongTimer == 3, "true departure clears only departing slot");
    for (int i = 0; i < 4; i++) { var r = WildBattleRules.ResolveCalledVolatileEndTurn(catalog, switched); Check(r.Succeeded, "Perish residual " + r.Fault); switched = r.State!; }
    Check(switched.Player.Pokemon.CurrentHp > 0 && switched.Opponent.Pokemon.CurrentHp == 0 && switched.CalledParty!.PendingSwitch is { Side: WildBattleSide.Opponent, Kind: WildBattleCalledSwitchKind.FaintedReplacement }, "source timer faints remaining target and exposes its genuine reserve");
    Check(Switch(switched, WildBattleSide.Opponent, 1).Outcome == WildBattleOutcome.Ongoing, "actual reserve continues battle");
});

Test("trainer Roar and Whirlwind consume real reserve selection, entry, level gate, and last healthy member", () => {
    var egg = Member("SQUIRTLE", egg: true); var dead = Member("RATTATA"); dead = dead with { Pokemon = dead.Pokemon with { CurrentHp = 0 } };
    var incoming = Enumerable.Range(0, 4).Select(i => Member("GROWLITHE", pid: (uint)i)).First(m => m.Pokemon.AbilityId == "ABILITY_INTIMIDATE");
    int successes = 0, failuresByLevel = 0, rejectedSlots = 0;
    foreach (string move in new[] { "ROAR", "WHIRLWIND" }) for (uint seed = 0; seed < 64; seed++)
    {
        var state = Party(Pair(seed: seed), opponent: [egg, dead, incoming]);
        state = state with { CalledParty = state.CalledParty! with { OpponentSpikes = 3 } };
        uint expectedSeed = seed; var raw = new List<int>();
        int Next() { expectedSeed = unchecked(expectedSeed * 0x41C64E6Du + 0x6073u); int value = (int)(expectedSeed >> 16); raw.Add(value); return value; }
        while (Next() % 2 == 0) { } // source selected-target rejection
        Next(); // accuracy command, 100% with neutral stages
        int levelRoll = Next() & 255;
        bool forced = ((levelRoll * (35 + 80) >> 8) + 1) > 80 / 4;
        if (forced) { while (Next() % 6 != 3) rejectedSlots++; }
        var result = Act(move, state);
        Check(result.Battle.Trace.Select(t => (int)t.Raw).SequenceEqual(raw) && result.State!.Rng.Seed == expectedSeed, "source target/accuracy/level/six-slot exact RNG");
        Check(result.State!.Outcome == WildBattleOutcome.Ongoing && result.State.Player.Pokemon.Moves[0].CurrentPp == 9, "trainer result and true caller PP");
        if (forced)
        {
            successes++;
            Check(result.State.CalledParty!.OpponentActiveIndex == 3 && result.State.Opponent.Pokemon.Species == incoming.Pokemon.Species, "selected true healthy member");
            Check(result.State.Opponent.Pokemon.CurrentHp == incoming.Pokemon.CurrentHp - incoming.Pokemon.Stats.MaxHp / 4 && result.State.Player.AttackStage == 5, "Spikes then genuine Intimidate entry");
            Check(result.Battle.Events.OfType<WildBattleForcedOutEvent>().Single().TrainerBattle && result.Battle.Events.OfType<WildBattleCalledPartySwitchedEvent>().Single().Kind == WildBattleCalledSwitchKind.Forced, "explicit immediate force transition for queued action cancellation");
            Check(result.State.CalledParty.PendingSwitch is null && result.State.Turn == state.Turn, "no replacement deferral or fabricated turn scheduling");
        }
        else
        {
            failuresByLevel++; Check(result.State.CalledParty!.OpponentActiveIndex == 0 && result.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "force-out-level"), "source lower-level failure");
        }
    }
    Check(successes > 0 && failuresByLevel > 0 && rejectedSlots > 0, "all source branches exercised");
    var alone = Party(Pair()); alone = alone with { CalledParty = alone.CalledParty! with { BattleKind = WildBattleCalledBattleKind.Trainer } };
    var failed = Act("ROAR", alone);
    Check(failed.State!.Outcome == WildBattleOutcome.Ongoing && failed.State.CalledParty!.OpponentActiveIndex == 0 && failed.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "no-force-out-reserve"), "last healthy trainer member cannot escape");
    Check(!failed.Battle.Trace.Any(t => t.Purpose.Contains("force-out")), "reserve absence precedes level and selection draws");
    var wild = Enumerable.Range(0, 32).Select(i => WildBattleRules.ResolveCalledMoveAction(catalog, Party(Pair(seed: (uint)i)), new(WildBattleSide.Player, 0, Id("WHIRLWIND")))).First(r => r.Succeeded && r.State!.Outcome == WildBattleOutcome.Escaped);
    Check(!wild.Battle.Events.OfType<WildBattleCalledPartySwitchedEvent>().Any() && !wild.Battle.Events.OfType<WildBattleForcedOutEvent>().Single().TrainerBattle, "wild force-out remains source escape");
});
Test("Teleport reads genuine trainer context while preserving wild and legacy isolated behavior", () => {
    var trainer = Party(Pair(), opponent: [Member("RATTATA")]); var result = Act("TELEPORT", trainer);
    Check(result.State!.Outcome == WildBattleOutcome.Ongoing && result.State.Player.Pokemon.Moves[0].CurrentPp == 9 && result.Battle.Trace.IsEmpty, "trainer Teleport source PP and no RNG");
    Check(result.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "trainer-battle") && !result.Battle.Events.OfType<WildBattleEndedEvent>().Any(), "actual trainer failure, no escape event");
    Check(Act("TELEPORT", Party(Pair())).State!.Outcome == WildBattleOutcome.Escaped, "real wild Teleport");
    Check(Act("TELEPORT", Pair()).State!.Outcome == WildBattleOutcome.Escaped, "legacy null context preserved");
});

Test("independent C rules match322 Conversion/modern Conversion2/BeatUp vectors", () => {
    using var vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "source-vectors.json")));
    Check(vectors.RootElement.GetProperty("sourceCommit").GetString() == catalog.SourceCommit, "oracle source pin");
    string[] types = ["NORMAL", "FIGHTING", "FLYING", "POISON", "GROUND", "ROCK", "BUG", "GHOST", "STEEL", "MYSTERY", "FIRE", "WATER", "GRASS", "ELECTRIC", "PSYCHIC", "ICE", "DRAGON", "DARK"];
    void Trace(WildBattleCalledMoveResult result, JsonElement expected)
    {
        Check(result.Battle.Trace.Select(t => (int)t.Raw).SequenceEqual(expected.GetProperty("raw").EnumerateArray().Select(x => x.GetInt32())), "exact independent random command stream");
        Check(result.State!.Rng.Seed == expected.GetProperty("seed").GetUInt32(), "exact independent final LCG");
    }
    int count = 0;
    foreach (var v in vectors.RootElement.GetProperty("conversion").EnumerateArray())
    {
        var state = Pair(seed: v.GetProperty("input").GetUInt32()); state = state with { Player = state.Player with { Pokemon = Moves(state.Player.Pokemon, "METRONOME", "EMBER", "WATER_GUN") } };
        var result = Act("CONVERSION", state); Trace(result, v);
        Check(result.State!.Player.CalledOverlay!.Type1 == "TYPE_" + types[v.GetProperty("type").GetInt32()], "independent Conversion selected type"); count++;
    }
    foreach (var v in vectors.RootElement.GetProperty("conversion2").EnumerateArray())
    {
        var state = Pair(seed: v.GetProperty("input").GetUInt32()); state = state with { Player = state.Player with {
            LastLandedMove = Id("EMBER"), LastHitBy = WildBattleSide.Opponent, SourceMoveHistory = new() { LastHitByType = "TYPE_FIRE" } } };
        var result = WildBattleRules.ResolveCalledMoveAction(catalog, state, new(WildBattleSide.Player, 0, Id("CONVERSION_2")));
        int type = v.GetProperty("type").GetInt32();
        Check(result.Succeeded, "normal Conversion2 vector"); Trace(result, v); Check(result.State!.Player.CalledOverlay!.Type1 == "TYPE_" + types[type], "independent Conversion2 result");
        count++;
    }
    foreach (var v in vectors.RootElement.GetProperty("beatup").EnumerateArray())
    {
        var state = Party(Pair(target: "GASTLY", seed: v.GetProperty("input").GetUInt32()), [Member("MACHOKE", 50)]);
        var result = Act("BEAT_UP", state); Trace(result, v);
        var hits = result.Battle.Events.OfType<WildBattleHpChangedEvent>().Where(e => e.Move == Id("BEAT_UP")).ToArray();
        Check(hits.Select(e => e.Damage).SequenceEqual(v.GetProperty("damage").EnumerateArray().Select(x => x.GetInt32())), "independent BeatUp per-member damage");
        Check(hits.Select(e => e.Critical ? 1 : 0).SequenceEqual(v.GetProperty("critical").EnumerateArray().Select(x => x.GetInt32())), "independent BeatUp critical branches"); count++;
    }
    foreach (var v in vectors.RootElement.GetProperty("conversion2edge").EnumerateArray())
    {
        bool noCandidate = v.GetProperty("type").GetInt32() == -1;
        var state = Pair(actor: noCandidate ? "MAGNETON" : "CLEFAIRY", seed: v.GetProperty("input").GetUInt32());
        state = state with { Player = state.Player with { LastLandedMove = Id("DRAGON_BREATH"), LastHitBy = WildBattleSide.Opponent, SourceMoveHistory = new() { LastHitByType = "TYPE_DRAGON" } } };
        var result = Act("CONVERSION_2", state); Trace(result, v);
        Check(result.Battle.Trace.Length == (noCandidate ? 0 : 1), "modern empty set consumes no RNG; singleton consumes one standard draw");
        if (noCandidate) Check(result.State!.Player.CalledOverlay is null && result.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "no-resistant-type"), "all resistant types already present: normal script failure");
        else Check(result.State!.Player.CalledOverlay!.Type1 == "TYPE_STEEL", "sole resistant candidate selected directly");
        Check(result.State!.Player.Pokemon.Moves[0].CurrentPp == 9, "modern candidate path original caller debit"); count++;
    }
    Check(count == 322, "nonempty exact oracle vector count");
});

Console.WriteLine($"{groups} groups / {assertions} assertions / {failures} failures");
if (groups == 0 || failures != 0) Environment.Exit(1);
