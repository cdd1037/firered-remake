using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Application;
using Game.Domain.Trainers;
using System.Reflection;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length != 2) return 2;
var proof = SourceMechanicsCatalogLoader.LoadAuthenticated(Path.GetFullPath(args[0]), Path.GetFullPath(args[1]));
var catalog = proof.Catalog;
int groups = 0, checks = 0, failures = 0;
var evidence = new List<object>();
const WildBattleSide P = WildBattleSide.Player, O = WildBattleSide.Opponent;
MoveId Id(string name) => new("pret:MOVE_" + name);
void Check(bool yes, string reason) { checks++; if (!yes) throw new InvalidDataException(reason); }
void Test(string name, Action work) { groups++; try { work(); Console.WriteLine("PASS " + name); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + name + " " + e); } }
PokemonInstance Mon(string name, int level = 30, uint pid = 0, IvPolicy? ivs = null, uint seed = 0)
{
    var p = SourcePokemonFactory.Create(proof, new(new("pret:SPECIES_" + name), level, 42, ivs ?? IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(seed)).Pokemon;
    Check(SourcePokemonValidator.ValidatePermanent(proof, p) is null, "authentic permanent identity " + name);
    return p;
}
WildBattleCombatant Get(WildBattleState s, WildBattleSide side) => side == P ? s.Player : s.Opponent;
WildBattleSide Other(WildBattleSide side) => side == P ? O : P;
WildBattleState Pair(PokemonInstance actor, PokemonInstance holder, uint seed = 0, WildBattleSide side = P) =>
    new(new(side == P ? actor : holder) { FirstTurnCounter = 1 }, new(side == P ? holder : actor) { FirstTurnCounter = 1 },
        0, 0, 0, new(seed), 0, WildBattleOutcome.Ongoing, RulesVersion: WildBattleRules.FuchsiaRulesVersion)
    { Terrain = WildBattleTerrain.Plain, SourceMechanicsRulesVersion = BattleRulesProfile.CanonicalSourceSinglesVersion };
int Slot(PokemonInstance p, string name)
{
    int slot = Array.FindIndex(p.Moves.ToArray(), m => m.Move == Id(name));
    Check(slot >= 0, p.Species + " naturally knows " + name);
    return slot;
}
void Trace(WildBattleState before, WildBattleResult result)
{
    uint seed = before.Rng.Seed;
    foreach (var draw in result.Trace)
    {
        seed = unchecked(seed * 1103515245 + 24691);
        Check(draw.Raw == (ushort)(seed >> 16), "source raw LCG trace " + draw.Purpose);
    }
    Check(result.State!.Rng.Seed == seed && result.State.Rng.Cursor == before.Rng.Cursor + (ulong)result.Trace.Length, "terminal RNG");
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
    for (uint seed = 0; seed < 1000; seed++)
    { var r = Act(s with { Rng = new(seed) }, move, side, called); if (predicate(r)) return r; }
    throw new InvalidDataException("No deterministic source branch: " + move);
}
WildBattleResult FullTurn(PokemonInstance player, string move, PokemonInstance foe, string ability, WildBattleSide holder)
{
    for (uint seed = 0; seed < 1000; seed++)
    {
        var start = WildBattleRules.StartSourceWild(proof, BattleRulesProfile.CanonicalSourceSingles, [new(player)], 0, new(foe), new(seed));
        Check(start.Succeeded, "canonical source entry " + start.Fault);
        var r = WildBattleRules.ResolveMove(catalog, start.State!, Slot(player, move));
        Check(r.Succeeded, "complete source turn " + r.Fault);
        Check(WildBattleRules.ValidateSourceWild(catalog, r.State!) is null, "complete source output " + WildBattleRules.ValidateSourceWild(catalog, r.State!));
        Trace(start.State!, r);
        if (!r.Events.OfType<WildBattleAbilityActivatedEvent>().Any(e => e.Ability == ability && e.Side == holder)) continue;
        Check(r.State!.Turn > start.State!.Turn || r.State.Outcome != WildBattleOutcome.Ongoing, "whole turn completed");
        Check(r.State.Player.Pokemon.Moves.Select(m => m.Move).SequenceEqual(player.Moves.Select(m => m.Move)) &&
            r.State.Opponent.Pokemon.Moves.Select(m => m.Move).SequenceEqual(foe.Moves.Select(m => m.Move)), "factory learned slots retained");
        evidence.Add(new { ability, holder, seed, player = player.Species.Value, opponent = foe.Species.Value,
            turn = r.State.Turn, outcome = r.State.Outcome.ToString(), rngCursor = r.State.Rng.Cursor,
            events = r.Events.Where(e => e is WildBattleAbilityActivatedEvent or WildBattleAbilityDamageEvent or WildBattleStatusChangedEvent or WildBattleRecoveredEvent).ToArray() });
        return r;
    }
    throw new InvalidDataException("No full turn activation " + ability);
}

Test("authentic factory donors and both sides execute complete shared source turns", () => {
    Check(Environment.Version.Major == 10, "actual .NET 10");
    foreach (var (name, pid, ability) in new[] { ("JOLTEON", 0u, "ABILITY_VOLT_ABSORB"), ("CHINCHOU", 0u, "ABILITY_VOLT_ABSORB"),
        ("CARVANHA", 0u, "ABILITY_ROUGH_SKIN"), ("SHARPEDO", 0u, "ABILITY_ROUGH_SKIN"),
        ("MAGMAR", 0u, "ABILITY_FLAME_BODY"), ("SLUGMA", 1u, "ABILITY_FLAME_BODY") })
        Check(Mon(name, 30, pid).AbilityId == ability, "source ability slot " + name);
    FullTurn(Mon("SQUIRTLE", 5), "TACKLE", Mon("CARVANHA"), "ABILITY_ROUGH_SKIN", O);
    FullTurn(Mon("CARVANHA"), "FOCUS_ENERGY", Mon("SQUIRTLE", 5), "ABILITY_ROUGH_SKIN", P);
    FullTurn(Mon("SQUIRTLE", 5), "TACKLE", Mon("MAGMAR"), "ABILITY_FLAME_BODY", O);
    FullTurn(Mon("MAGMAR"), "LEER", Mon("SQUIRTLE", 5), "ABILITY_FLAME_BODY", P);
    FullTurn(Mon("PIKACHU", 5), "THUNDER_SHOCK", Mon("JOLTEON"), "ABILITY_VOLT_ABSORB", O);
    FullTurn(Mon("JOLTEON"), "SAND_ATTACK", Mon("PIKACHU", 5), "ABILITY_VOLT_ABSORB", P);
});

Test("Rough Skin uses source sixteenth, actual HP loss, both sides and passive typed damage", () => {
    foreach (var side in new[] { P, O }) foreach (int level in new[] { 1, 30 })
    {
        var s = Pair(Mon("SQUIRTLE", level), Mon("CARVANHA", 100), side: side);
        var r = Act(s, level == 1 ? "TACKLE" : "BITE", side); var hit = r.Events.OfType<WildBattleAbilityDamageEvent>().Single();
        int expected = Math.Max(1, Get(s, side).Pokemon.Stats.MaxHp / 16);
        Check(hit.Holder == Other(side) && hit.Target == side && hit.Damage == expected && hit.HpBefore - hit.HpAfter == expected, "sixteenth/minimum/passive sides");
        Check(Get(r.State!, side).CalledDamage is not { PhysicalDamage: > 0 } and not { SpecialDamage: > 0 }, "no Counter registers from passive damage");
        Check(!Get(r.State!, side).DamagedByOpponentThisTurn, "no direct-hit flag from passive damage");
        Check(!r.Events.OfType<WildBattleRecoilEvent>().Any() && !r.Trace.Any(t => t.Purpose.Contains("rough-skin")), "not recoil/no Rough Skin random draw");
        string json = JsonSerializer.Serialize<WildBattleEvent>(hit);
        Check(JsonSerializer.Deserialize<WildBattleEvent>(json) == hit && json.Contains("ability-damage"), "typed event serializes");
    }
});

Test("contact needs actual holder HP loss and respects Substitute, Protect, type immunity and noncontact", () => {
    var actor = Mon("SQUIRTLE", 5); var holder = Mon("CARVANHA", 100);
    var s = Pair(actor, holder);
    foreach (int gate in new[] { 0, 1, 2 })
    {
        var target = gate switch { 0 => s.Opponent with { Protected = true },
            1 => s.Opponent with { CalledVolatile = new() { SubstituteHp = 1 } },
            _ => s.Opponent with { CalledOverlay = new() { Type1 = "TYPE_GHOST", Type2 = "TYPE_GHOST" } } };
        var r = Act(s with { Opponent = target }, "TACKLE");
        Check(!r.Events.OfType<WildBattleAbilityDamageEvent>().Any() && r.State!.Player.Pokemon.CurrentHp == actor.CurrentHp, "contact gate " + gate);
    }
    s = Pair(Mon("CLEFAIRY", 29), holder with { CurrentHp = 1 });
    var zero = Act(s, "FALSE_SWIPE", called: true);
    Check(zero.State!.Opponent.Pokemon.CurrentHp == 1 && !zero.Events.OfType<WildBattleAbilityDamageEvent>().Any(), "zero real HP False Swipe");
    var noncontact = Act(Pair(Mon("SQUIRTLE", 13), holder), "WATER_GUN");
    Check(!noncontact.Events.OfType<WildBattleAbilityDamageEvent>().Any(), "Water Gun noncontact");
    var substituteActor = Pair(actor, holder); substituteActor = substituteActor with { Player = substituteActor.Player with { CalledVolatile = new() { SubstituteHp = 1 } } };
    var passive = Act(substituteActor, "TACKLE");
    Check(passive.Events.OfType<WildBattleAbilityDamageEvent>().Count() == 1 && passive.State!.Player.CalledVolatile!.SubstituteHp == 1, "Rough Skin bypasses attacker Substitute");
});

Test("contact multi-hit order, attacker and holder faint, recoil KO and no contact chaining", () => {
    var s = Pair(Mon("SPEAROW", 13), Mon("CARVANHA", 100));
    var r = Act(s, "FURY_ATTACK");
    var hits = r.Events.OfType<WildBattleHpChangedEvent>().Where(e => e.Target == O && e.HpAfter < e.HpBefore).ToArray();
    var passive = r.Events.OfType<WildBattleAbilityDamageEvent>().ToArray();
    Check(passive.Length == hits.Length && passive.Length >= 2, "per-hit Rough Skin");
    for (int i = 0; i < hits.Length; i++) Check(hits[i].Sequence < passive[i].Sequence && (i + 1 == hits.Length || passive[i].Sequence < hits[i + 1].Sequence), "damage/contact order");
    s = s with { Player = s.Player with { Pokemon = s.Player.Pokemon with { CurrentHp = 1 } } };
    r = Act(s, "FURY_ATTACK");
    Check(r.State!.Player.Pokemon.CurrentHp == 0 && r.Events.OfType<WildBattleMultiHitEvent>().Single().ActualHits == 1, "attacker faint stops remaining hits");
    Check(r.Events.OfType<WildBattleFaintedEvent>().Count(e => e.Side == P) == 1, "attacker faint once");
    s = Pair(Mon("SQUIRTLE", 5), Mon("CARVANHA", 1) with { CurrentHp = 1 }); r = Act(s, "TACKLE");
    Check(r.Events.OfType<WildBattleFaintedEvent>().Single(e => e.Side == O).Sequence < r.Events.OfType<WildBattleAbilityDamageEvent>().Single().Sequence, "single-hit holder faint precedes contact");
    s = Pair(Mon("CLEFAIRY", 29) with { CurrentHp = 1 }, Mon("CARVANHA", 100)); r = Act(s, "TAKE_DOWN", called: true);
    Check(r.State!.Player.Pokemon.CurrentHp == 0 && !r.Events.OfType<WildBattleAbilityDamageEvent>().Any(), "recoil already fainted attacker cannot trigger Rough Skin");
    s = Pair(Mon("CARVANHA", 5), Mon("CARVANHA", 100)); r = Act(s, "BITE");
    Check(r.Events.OfType<WildBattleAbilityDamageEvent>().Count() == 1, "passive ability does not trigger another contact");
});

Test("Flame Body raw modulo-three roll precedes burn guards and bypasses Safeguard/Shield Dust", () => {
    var basePair = Pair(Mon("SQUIRTLE", 5), Mon("MAGMAR", 100));
    foreach (var side in new[] { P, O })
    {
        var s = Pair(Mon("SQUIRTLE", 5), Mon("MAGMAR", 100), side: side);
        bool burned = false, skipped = false;
        for (uint seed = 0; seed < 18; seed++)
        {
            var r = Act(s with { Rng = new(seed) }, "TACKLE", side);
            var roll = r.Trace.Single(t => t.Purpose.EndsWith(".flame-body"));
            bool shouldBurn = roll.Raw % 3 == 0;
            Check((Get(r.State!, side).Status == WildBattleStatus.Burn) == shouldBurn, "source modulo3");
            burned |= shouldBurn; skipped |= !shouldBurn;
            Check(r.Trace.ToList().IndexOf(roll) > r.Trace.ToList().FindIndex(t => t.Purpose.EndsWith(".secondary-effect")), "secondary draw before contact roll");
        }
        Check(burned && skipped, "both chance branches");
    }
    var burn = Branch(basePair, "TACKLE", r => r.State!.Player.Status == WildBattleStatus.Burn);
    uint burnSeed = 0;
    for (; burnSeed < 1000; burnSeed++) { var r = Act(basePair with { Rng = new(burnSeed) }, "TACKLE"); if (r.State!.Player.Status == WildBattleStatus.Burn) break; }
    foreach (var kind in new[] { "status", "fire", "water-veil", "safeguard", "shield-dust" })
    {
        var s = kind switch {
            "fire" => Pair(Mon("MAGMAR", 5), Mon("MAGMAR", 100), burnSeed),
            "water-veil" => Pair(Mon("GOLDEEN", 5, 1), Mon("MAGMAR", 100), burnSeed),
            "shield-dust" => Pair(Mon("CATERPIE", 5), Mon("MAGMAR", 100), burnSeed),
            _ => basePair with { Rng = new(burnSeed) }
        };
        if (kind == "status") s = s with { Player = s.Player with { Status = WildBattleStatus.Poison } };
        if (kind == "safeguard") s = s with { CalledField = new() { PlayerSafeguardTurnsRemaining = 5 } };
        string move = kind == "fire" ? "FIRE_PUNCH" : kind == "water-veil" ? "PECK" : "TACKLE";
        var r = Branch(s, move, x => x.Trace.Any(t => t.Purpose.EndsWith(".flame-body") && t.Raw % 3 == 0));
        Check(r.Trace.Count(t => t.Purpose.EndsWith(".flame-body")) == 1, "roll despite guard " + kind);
        Check((r.State!.Player.Status == WildBattleStatus.Burn) == (kind is "safeguard" or "shield-dust"), "burn outcome " + kind);
    }
    foreach (bool protect in new[] { false, true })
    {
        var s = basePair with { Opponent = basePair.Opponent with { Protected = protect, CalledVolatile = protect ? null : new() { SubstituteHp = 1 } } };
        var r = Act(s, "TACKLE"); Check(!r.Trace.Any(t => t.Purpose.EndsWith(".flame-body")), "no roll on protection/Substitute");
    }
    var fainted = Branch(Pair(Mon("SQUIRTLE", 5), Mon("MAGMAR", 1) with { CurrentHp = 1 }), "TACKLE", r => r.State!.Player.Status == WildBattleStatus.Burn);
    Check(fainted.Events.OfType<WildBattleFaintedEvent>().Single(e => e.Side == O).Sequence < fainted.Events.OfType<WildBattleStatusChangedEvent>().Single(e => e.Reason == "flame-body").Sequence, "fainted holder can burn");
});

Test("Flame Body multi-hit draws remain per hit after first burn and skip already fainted attackers", () => {
    var s = Pair(Mon("SPEAROW", 13), Mon("MAGMAR", 100));
    var r = Branch(s, "FURY_ATTACK", x => x.State!.Player.Status == WildBattleStatus.Burn);
    Check(r.Trace.Count(t => t.Purpose.EndsWith(".flame-body")) == r.Events.OfType<WildBattleMultiHitEvent>().Single().ActualHits, "each real hit draws even after burn");
    Check(r.Events.OfType<WildBattleStatusChangedEvent>().Count(e => e.Reason == "flame-body") == 1, "only first successful burn changes status");
    s = Pair(Mon("CLEFAIRY", 29) with { CurrentHp = 1 }, Mon("MAGMAR", 100));
    r = Act(s, "TAKE_DOWN", called: true);
    Check(r.State!.Player.Pokemon.CurrentHp == 0 && !r.Trace.Any(t => t.Purpose.EndsWith(".flame-body")), "no roll after recoil KO");
    var counter = Pair(Mon("SQUIRTLE", 5), Mon("CARVANHA", 100));
    counter = counter with { Player = counter.Player with { CalledDamage = new() { Turn = 0, PhysicalDamage = 7, PhysicalSource = O } } };
    r = Act(counter, "TACKLE");
    Check(r.State!.Player.CalledDamage!.PhysicalDamage == 7, "Rough Skin preserves previous Counter damage register");
});

Test("Volt Absorb full HP, quarter healing, no status absorption, Charge, weather, PP and history", () => {
    foreach (var side in new[] { P, O }) foreach (int missing in new[] { 0, 1, 1000 })
    {
        var holder = Mon("JOLTEON"); holder = holder with { CurrentHp = Math.Max(1, holder.CurrentHp - missing) };
        var s = Pair(Mon("PIKACHU", 5), holder, side: side); var r = Act(s, "THUNDER_SHOCK", side);
        Check(Get(r.State!, Other(side)).Pokemon.CurrentHp == Math.Min(holder.Stats.MaxHp, holder.CurrentHp + Math.Max(1, holder.Stats.MaxHp / 4)), "quarter heal/full clamp");
        Check(r.Events.OfType<WildBattleAbilityPreventedEvent>().Single().Ability == "ABILITY_VOLT_ABSORB", "absorb ability");
        Check(!r.Trace.Any(t => t.Purpose.EndsWith(".critical") || t.Purpose.EndsWith(".damage") || t.Purpose.EndsWith(".secondary-effect")), "no damage script draws after absorb");
        Check(r.Events.OfType<WildBattlePpCommandEvent>().Single().Before - r.Events.OfType<WildBattlePpCommandEvent>().Single().After == 1, "exact PP once");
        Check(Get(r.State!, side).LastUsedMove == Id("THUNDER_SHOCK") && Get(r.State!, Other(side)).LastLandedMove == Id("UNAVAILABLE") && Get(r.State!, Other(side)).SourceMoveHistory!.LastHitByType is null, "failed landed history");
    }
    var status = Act(Pair(Mon("PIKACHU", 8), Mon("JOLTEON")), "THUNDER_WAVE");
    Check(!status.Events.OfType<WildBattleAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_VOLT_ABSORB") && status.State!.Opponent.Status == WildBattleStatus.Paralysis, "Gen III damaging Electric only");
    var s2 = Pair(Mon("VOLTORB", 21), Mon("JOLTEON") with { CurrentHp = 1 });
    var plain = Act(s2, "SPARK"); var charged = Act(s2 with { Player = s2.Player with { ChargeTurnsRemaining = 2 } }, "SPARK");
    Check(plain.State!.Opponent.Pokemon.CurrentHp == charged.State!.Opponent.Pokemon.CurrentHp && charged.Events.OfType<WildBattleRecoveredEvent>().Single().Recovered == s2.Opponent.Pokemon.Stats.MaxHp / 4, "Charge never boosts heal");
    foreach (bool rain in new[] { false, true })
    {
        var s = Pair(Mon("CLEFAIRY", 29), Mon("JOLTEON") with { CurrentHp = 1 });
        s = s with { RainTurnsRemaining = rain ? 5 : 0, CalledField = rain ? null : new() { SunTurnsRemaining = 5 } };
        var r = Branch(s, "THUNDER", x => x.Events.OfType<WildBattleAbilityPreventedEvent>().Any(), called: true);
        Check(r.Events.OfType<WildBattleRecoveredEvent>().Single().Recovered == s.Opponent.Pokemon.Stats.MaxHp / 4, "weather Electric absorption");
        Check(r.Trace.Any(t => t.Purpose.EndsWith(".accuracy")) != rain, "rain/sun accuracy rules before absorb");
    }
    var pressure = Act(Pair(Mon("PIKACHU", 5), Mon("AERODACTYL", 30, 1)), "THUNDER_SHOCK");
    Check(pressure.Events.OfType<WildBattlePpCommandEvent>().Single().CommandCost == 2, "real Pressure holder retains two PP cost");
});

Test("dynamic Electric Hidden Power authentic IVs, Substitute healing and protection/miss", () => {
    PokemonInstance? unown = null;
    for (uint seed = 0; seed < 1000; seed++)
    {
        var p = Mon("UNOWN", 20, ivs: IvPolicy.Random, seed: seed); var iv = p.Ivs;
        int bits = (iv.Hp & 1) | ((iv.Attack & 1) << 1) | ((iv.Defense & 1) << 2) | ((iv.Speed & 1) << 3) | ((iv.SpecialAttack & 1) << 4) | ((iv.SpecialDefense & 1) << 5);
        if (bits * 15 / 63 + 1 == 12) { unown = p; break; } // source type index skips Mystery at 9
    }
    Check(unown is not null, "factory random IV Electric Hidden Power donor");
    var s = Pair(unown!, Mon("JOLTEON") with { CurrentHp = 1 });
    var r = Act(s, "HIDDEN_POWER"); Check(r.Events.OfType<WildBattleAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_VOLT_ABSORB"), "derived Electric absorbed");
    s = s with { Opponent = s.Opponent with { CalledVolatile = new() { SubstituteHp = 1 } } };
    r = Act(s, "HIDDEN_POWER"); Check(r.State!.Opponent.CalledVolatile!.SubstituteHp == 1 && r.State.Opponent.Pokemon.CurrentHp > 1, "absorb healing ignores target Substitute");
    r = Act(s with { Opponent = s.Opponent with { Protected = true } }, "HIDDEN_POWER");
    Check(!r.Events.OfType<WildBattleAbilityPreventedEvent>().Any() && r.State!.Opponent.Pokemon.CurrentHp == 1, "Protect before absorption");
    var miss = Branch(s with { Player = s.Player with { AccuracyStage = 0 } }, "HIDDEN_POWER", x => x.Events.OfType<WildBattleMoveMissedEvent>().Any());
    Check(!miss.Events.OfType<WildBattleAbilityPreventedEvent>().Any(), "accuracy miss before absorption");
    var water = Pair(Mon("CLEFAIRY", 29), Mon("POLIWAG", 30, 0) with { CurrentHp = 1 });
    var weatherBall = Act(water with { RainTurnsRemaining = 5 }, "WEATHER_BALL", called: true);
    Check(weatherBall.Events.OfType<WildBattleAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_WATER_ABSORB"), "shared Weather Ball dynamic Water absorption regression");
});

Test("forged source metadata and permanent abilities rejected atomically", () => {
    var s = Pair(Mon("SQUIRTLE", 5), Mon("CARVANHA"));
    var forged = s with { Opponent = s.Opponent with { Pokemon = s.Opponent.Pokemon with { AbilityId = "ABILITY_FLAME_BODY" } } };
    var r = WildBattleRules.ResolveSourceMoveAction(catalog, forged, new(P, Slot(s.Player.Pokemon, "TACKLE"))).Battle;
    Check(!r.Succeeded && ReferenceEquals(r.State, forged) && r.Events.IsEmpty && r.Trace.IsEmpty, "forged permanent donor atomic");
    var badMove = catalog.Moves[Id("TACKLE")] with { Power = 41 };
    r = WildBattleRules.ResolveSourceMoveAction(catalog with { Moves = catalog.Moves.SetItem(badMove.Id, badMove) }, s, new(P, Slot(s.Player.Pokemon, "TACKLE"))).Battle;
    Check(!r.Succeeded && ReferenceEquals(r.State, s) && r.Events.IsEmpty && r.Trace.IsEmpty, "unauthenticated catalog/definition atomic");
});

Test("presentation emits original ability text with correct sides and no duplicate healed text", () => {
    string Name(WildBattleSide side) => side == P ? "Squirtle" : "Foe Carvanha";
    var sourceTexts = PretBattleText.Load(Path.GetFullPath(args[1]));
    SourceTrainerEventDisplay Display(WildBattleEvent[] events, int index)
    {
        var display = SourceTrainerPresentationModel.Describe(events, index, Name, id => id.Value, (side, index) => Name(side))!;
        foreach (var text in display.Texts)
        {
            var pages = PretBattleText.Pages(sourceTexts, text.Symbol, text.Values.ToImmutableDictionary(x => x.Key, x => x.Value));
            Check(!pages.IsEmpty && pages.All(p => !string.IsNullOrWhiteSpace(p)), "authenticated source text resolves all fields");
        }
        return display;
    }
    var damage = new WildBattleAbilityDamageEvent(1, 1, O, P, Id("TACKLE"), "ABILITY_ROUGH_SKIN", 20, 19, 1);
    var d = Display([damage], 0); Check(d.Texts.Single().Symbol == "sText_PkmnHurtsWith" && d.Texts[0].Values.Contains(("B_ATK_NAME_WITH_PREFIX", "Squirtle")), "rough source text/target");
    d = Display([new WildBattleStatusChangedEvent(1, 1, P, 0, WildBattleStatus.Burn, "flame-body")], 0);
    Check(d.Texts.Single().Symbol == "sText_PkmnBurnedBy" && d.Texts[0].Values.Contains(("B_SCR_ACTIVE_ABILITY", "FLAME BODY")), "flame source text");
    WildBattleEvent[] healed = [new WildBattleAbilityPreventedEvent(1, 1, P, O, Id("THUNDER_SHOCK"), "ABILITY_VOLT_ABSORB"), new WildBattleRecoveredEvent(2, 1, O, Id("THUNDER_SHOCK"), 1, 21, 20)];
    Check(Display(healed, 0).Silent && Display(healed, 1).Texts.Single().Symbol == "sText_PkmnRestoredHPUsing", "heal text exactly once");
    Check(Display([healed[0]], 0).Texts.Single().Symbol == "sText_PkmnsXMadeYUseless", "full HP source text");
});
Test("wild and trainer presentation project passive HP on either side without changing permanent state", () => {
    // Event-projection fixture only. Canonical donors are not admitted into the campaign by this check.
    var owner = new CampaignSession(PretCampaignCatalog.LoadFuchsiaGymProfile(Path.GetFullPath(args[1])));
    foreach (var side in new[] { P, O })
    {
        var before = Pair(Mon("SQUIRTLE", 5), Mon("CARVANHA", 100), side: side);
        var result = Act(before, "TACKLE", side); var ability = result.Events.OfType<WildBattleAbilityDamageEvent>().Single();
        var owned = new PartyPokemon(1, before.Player.Pokemon, "PLAYER", "Player", 0, 2, "MAP_PALLET_TOWN", 5, 4, "ITEM_POKE_BALL", "ITEM_NONE");
        var display = new CampaignWildBattle("projection", owned, result.State!, [ability], 0, before.Player.Pokemon, before.Opponent.Pokemon,
            CampaignBattlePhase.Events, null, [], 0, [], []);
        var input = owner.State with { Party = [owned], WildBattle = display };
        var wild = (CampaignState)typeof(CampaignSession).GetMethod("PrepareWildPresentation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, [input])!;
        Check((side == P ? wild.WildBattle!.DisplayPlayer : wild.WildBattle!.DisplayOpponent).CurrentHp == ability.HpAfter, "wild display HP " + side);
        Check(ReferenceEquals(wild.Party[0], owned) && ReferenceEquals(wild.WildBattle!.State, result.State), "wild projection never rewrites authoritative party/battle");
        var trainer = new CampaignForestTrainerBattle("projection", new("pret:TRAINER_BROCK"), "projection", new(new("pret:TRAINER_BROCK"), result.State!, [result.State!.Opponent], 0), display,
            CampaignForestTrainerStage.Combat, [5], [], 0, 0, [0]);
        input = input with { WildBattle = null, ForestTrainerBattle = trainer };
        var projected = (CampaignState)typeof(CampaignSession).GetMethod("PrepareForestTrainerPresentation", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, [input])!;
        var shown = projected.ForestTrainerBattle!.Presentation;
        Check((side == P ? shown.DisplayPlayer : shown.DisplayOpponent).CurrentHp == ability.HpAfter, "trainer display HP " + side);
        Check(ReferenceEquals(projected.Party[0], owned) && ReferenceEquals(projected.ForestTrainerBattle.Combat.Battle, result.State), "trainer projection never rewrites authoritative party/battle");
    }
});
Console.WriteLine(JsonSerializer.Serialize(new { runtime = Environment.Version.ToString(), groups, checks, failures, evidence }, new JsonSerializerOptions { WriteIndented = true }));
return failures == 0 && groups >= 9 ? 0 : 1;
