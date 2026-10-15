using System.Collections.Immutable;
using System.Text.Json;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

if (args.Length != 1) throw new ArgumentException("Pass the project content root.");
string root = Path.GetFullPath(args[0]);
int groups = 0, checks = 0, failures = 0;
void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
void Test(string label, Action work) { groups++; try { work(); Console.WriteLine("PASS " + label); } catch (Exception e) { failures++; Console.WriteLine("FAIL " + label + " " + e); } }
MoveId Id(string name) => new("pret:MOVE_" + name);
var original = PretRocketHideoutPokemon.Load(root).Catalog;
var supplement = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules: true);
var prior = original with { Moves = original.Moves.SetItems(supplement.Moves) };
var old = PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior, root, root).Catalog, root).Catalog, root).Catalog;
var predecessor = PretFuchsiaRoutePokemon.Load(old, root).Catalog;
var metadata = PretMetronomeMoveCatalog.Load(root);
var catalog = metadata.Extend(predecessor);
PokemonInstance Mon(string name, int level = 20, uint pid = 0, int iv = 31) => PokemonFactory.Create(catalog,
    new(new("pret:SPECIES_" + name), level, 42, IvPolicy.Fixed(iv), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
PokemonInstance WithMove(PokemonInstance p, string name) => p with { Moves = [new(Id(name), catalog.Moves[Id(name)].Pp, catalog.Moves[Id(name)].Pp)] };
WildBattleState Pair(uint seed = 0, string target = "RATTATA", int actorLevel = 20, int targetLevel = 100, int iv = 31) =>
    new(new(WithMove(Mon("CLEFAIRY", actorLevel, iv: iv), "METRONOME")) { FirstTurnCounter = 1 },
        new(WithMove(Mon(target, targetLevel), "SPLASH")) { FirstTurnCounter = 1 }, 0, 0, 0, new(seed), 0,
        WildBattleOutcome.Ongoing, RulesVersion: WildBattleRules.FuchsiaRulesVersion) { Terrain = WildBattleTerrain.Plain };
WildBattleCalledMoveResult Act(string name, WildBattleState? input = null)
{
    var s = input ?? Pair(); var r = WildBattleRules.ResolveCalledMoveAction(catalog, s, new(WildBattleSide.Player, 0, Id(name)));
    Check(r.Succeeded, name + " " + r.Fault);
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, r.State!) is null, name + " valid output " + WildBattleRules.ValidateCalledMoveActionState(catalog, r.State!));
    return r;
}
void Trace(WildBattleCalledMoveResult r, Oracle o, string label)
{
    Check(r.Battle.Trace.Select(t => (int)t.Raw).SequenceEqual(o.Raw), label + " raw trace expected=" + string.Join(',', o.Raw) + " actual=" + string.Join(',', r.Battle.Trace.Select(t => t.Raw)));
    Check(r.State!.Rng.Seed == o.Seed && r.State.Rng.Cursor == o.InitialCursor + (ulong)o.Raw.Count, label + " RNG terminal state");
}
void Exact(string name, WildBattleState s)
{
    var move = catalog.Moves[Id(name)]; var o = Oracle.Execute(move, s); var r = Act(name, s);
    Trace(r, o, name);
    Check(r.State!.Player.Pokemon.CurrentHp == o.ActorHp && r.State.Opponent.Pokemon.CurrentHp == o.TargetHp,
        name + $" exact HP expected={o.ActorHp}/{o.TargetHp}, actual={r.State.Player.Pokemon.CurrentHp}/{r.State.Opponent.Pokemon.CurrentHp}");
    Check(r.State.Player.Pokemon.Moves[0].CurrentPp == 9, name + " caller PP only");
    Check(r.Battle.Events.OfType<WildBattlePpCommandEvent>().Single().RngCursor == o.PpCursor, name + " exact PP command position");
}
using var membersDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "members.json")));
var members = membersDocument.RootElement.GetProperty("members").EnumerateArray().Select(x => x.GetString()![5..]).ToArray();
Test("all 25 source-authenticated scalar moves execute meaningful unlearned results", () => {
    Check(Environment.Version.Major == 10, "official runtime10"); Check(members.Length == 25 && members.Distinct().Count() == 25, "exact25");
    foreach (string name in members)
    {
        var m = catalog.Moves[Id(name)]; var s = Pair();
        if (m.Effect == "EFFECT_OHKO") s = Pair(actorLevel: 100, targetLevel: 80);
        if (m.Effect == "EFFECT_SMELLINGSALT") s = s with { Opponent = s.Opponent with { Status = WildBattleStatus.Paralysis } };
        uint seed = Enumerable.Range(0, 10000).Select(x => (uint)x).First(x => {
            var o = Oracle.Execute(m, s with { Rng = new(x) });
            return o.TargetHp < s.Opponent.Pokemon.CurrentHp;
        });
        s = s with { Rng = new(seed) }; Exact(name, s); var r = Act(name, s);
        Check(WildBattleRules.IsCalledMoveEffectSupported(m) && SourceMoveDefinitionRules.IsValid(m), name + " registered/authenticated");
        Check(!s.Player.Pokemon.Moves.Any(x => x.Move == m.Id) && !r.State!.Player.Pokemon.Moves.Any(x => x.Move == m.Id), name + " current stays unlearned");
        Check(r.State!.Player.LastUsedMove == Id("METRONOME") && r.State.Player.LastCalledMove == m.Id, name + " caller/result history");
        Check(!WildBattleRules.IsMoveSupported(m.Id, WildBattleRules.FuchsiaRulesVersion), name + " no ordinary v1 admission");
        if (m.Effect == "EFFECT_SMELLINGSALT") Check(r.State.Opponent.Status == WildBattleStatus.None, "Smelling Salt removes paralysis");
    }
});
Test("Return and Frustration preserve zero fallback and friendship endpoints", () => {
    foreach (string name in new[] { "RETURN", "FRUSTRATION" })
        foreach (int friendship in new[] { 0, 1, 2, 3, 70, 254, 255 })
        { var s = Pair(); Exact(name, s with { Player = s.Player with { Pokemon = s.Player.Pokemon with { Friendship = friendship } } }); }
});
Test("Eruption and Water Spout scale current HP before source integer damage", () => {
    foreach (string name in new[] { "ERUPTION", "WATER_SPOUT" })
        foreach (int hp in new[] { 1, 2, 17, Pair().Player.Pokemon.Stats.MaxHp })
        { var s = Pair(); Exact(name, s with { Player = s.Player with { Pokemon = s.Player.Pokemon with { CurrentHp = hp } } }); }
});
Test("Hidden Power uses IV bit ordering, physical-special type and resulting type history", () => {
    foreach (int iv in new[] { 0, 1, 2, 3, 30, 31 })
    {
        var s = Pair(iv: iv); Exact("HIDDEN_POWER", s); var r = Act("HIDDEN_POWER", s); var (_, type) = Oracle.Hidden(s.Player.Pokemon);
        Check(r.State!.Opponent.SourceMoveHistory!.LastHitByType == type, "effective hidden type " + iv);
    }
    // Six independently varied least bits exercise all 64 mappings and source order HP/Atk/Def/Speed/SpAtk/SpDef.
    for (int bits = 0; bits < 64; bits++)
    {
        var s = Pair(); var p = s.Player.Pokemon;
        var ivs = p.Ivs with { Hp = 30 + (bits & 1), Attack = 30 + ((bits >> 1) & 1), Defense = 30 + ((bits >> 2) & 1),
            Speed = 30 + ((bits >> 3) & 1), SpecialAttack = 30 + ((bits >> 4) & 1), SpecialDefense = 30 + ((bits >> 5) & 1) };
        p = Recalculate(p with { Ivs = ivs }); s = s with { Player = s.Player with { Pokemon = p } };
        Exact("HIDDEN_POWER", s); var r = Act("HIDDEN_POWER", s);
        Check(r.State!.Opponent.SourceMoveHistory!.LastHitByType == (Oracle.Hidden(p).Type == "TYPE_GHOST" ? null : Oracle.Hidden(p).Type), "IV pattern type " + bits);
    }
});
Test("Hidden Power second IV bits independently determine every power bucket", () => {
    for (int bits = 0; bits < 64; bits++)
    {
        var s = Pair(); var p = s.Player.Pokemon;
        var ivs = p.Ivs with { Hp = 1 + 2 * (bits & 1), Attack = 1 + 2 * ((bits >> 1) & 1), Defense = 1 + 2 * ((bits >> 2) & 1),
            Speed = 1 + 2 * ((bits >> 3) & 1), SpecialAttack = 1 + 2 * ((bits >> 4) & 1), SpecialDefense = 1 + 2 * ((bits >> 5) & 1) };
        p = Recalculate(p with { Ivs = ivs }); s = s with { Player = s.Player with { Pokemon = p } }; Exact("HIDDEN_POWER", s);
    }
});
Test("scalar weather and Light Screen use the source order and critical bypass", () => {
    foreach (string name in new[] { "ERUPTION", "WATER_SPOUT", "HIDDEN_POWER", "BLAZE_KICK" })
    {
        foreach (int weather in new[] { 0, 1, 2 }) foreach (bool screen in new[] { false, true })
        {
            var s = Pair(); s = s with { RainTurnsRemaining = weather == 1 ? 5 : 0,
                CalledField = new() { SunTurnsRemaining = weather == 2 ? 5 : 0, OpponentLightScreenTurnsRemaining = screen ? 5 : 0 } };
            Exact(name, s);
        }
    }
    var m = catalog.Moves[Id("ERUPTION")];
    uint seed = Enumerable.Range(0, 10000).Select(x => (uint)x).First(x => { var o = new Oracle(Pair(x)); o.Target(m); o.Next(); return o.Next() % 16 == 0; });
    var critical = Pair(seed); critical = critical with { CalledField = new() { OpponentLightScreenTurnsRemaining = 5 } }; Exact("ERUPTION", critical);
    var toxic = Pair(); toxic = toxic with { CalledField = new(), Player = toxic.Player with { Status = 0x580 } }; Exact("FACADE", toxic);
});
Test("Hidden Power preserves Fire thaw and Charge quirks while using derived ability type", () => {
    WildBattleState Typed(int bits, string target = "RATTATA")
    {
        var s = Pair(0, target); var p = s.Player.Pokemon;
        p = Recalculate(p with { Ivs = p.Ivs with { Hp = 30 + (bits & 1), Attack = 30 + ((bits >> 1) & 1), Defense = 30 + ((bits >> 2) & 1),
            Speed = 30 + ((bits >> 3) & 1), SpecialAttack = 30 + ((bits >> 4) & 1), SpecialDefense = 30 + ((bits >> 5) & 1) } });
        return s with { Player = s.Player with { Pokemon = p } };
    }
    var fire = Typed(34); fire = fire with { Opponent = fire.Opponent with { Status = WildBattleStatus.Freeze } };
    Check(Oracle.Hidden(fire.Player.Pokemon).Type == "TYPE_FIRE", "Fire IV fixture");
    var result = Act("HIDDEN_POWER", fire); Check(result.State!.Opponent.Status == WildBattleStatus.Freeze && result.State.Opponent.Pokemon.CurrentHp < fire.Opponent.Pokemon.CurrentHp, "metadata physicalDmg prevents Fire Hidden Power thaw");
    var electric = Typed(48); Check(Oracle.Hidden(electric.Player.Pokemon).Type == "TYPE_ELECTRIC", "Electric IV fixture");
    var normal = Act("HIDDEN_POWER", electric); var charged = Act("HIDDEN_POWER", electric with { Player = electric.Player with { ChargeTurnsRemaining = 2 } });
    Check(normal.State!.Opponent.Pokemon.CurrentHp == charged.State!.Opponent.Pokemon.CurrentHp, "Charge reads Normal source metadata");
    var water = Typed(40, "POLIWAG"); water = water with { Opponent = water.Opponent with { Pokemon = water.Opponent.Pokemon with { CurrentHp = 10 } } };
    Check(Oracle.Hidden(water.Player.Pokemon).Type == "TYPE_WATER" && water.Opponent.Pokemon.AbilityId == "ABILITY_WATER_ABSORB", "Water IV/ability fixture");
    result = Act("HIDDEN_POWER", water); Check(result.State!.Opponent.Pokemon.CurrentHp > 10 && result.State.Opponent.LastLandedMove == Id("UNAVAILABLE"), "derived Water absorbed and history failed");
    Check(!result.Battle.Trace.Any(t => t.Purpose.EndsWith(".critical") || t.Purpose.EndsWith(".damage")), "absorption before critical and damage");
});
Test("called scalar snapshots round-trip and resume without inventing learned slots", () => {
    var first = Act("HIDDEN_POWER"); string json = JsonSerializer.Serialize(first.State); var restored = JsonSerializer.Deserialize<WildBattleState>(json)!;
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, restored) is null, "serialized called state validates");
    var next = Act("RETURN", restored); Check(next.State!.Player.Pokemon.Moves[0].CurrentPp == 8 && next.State.Player.LastCalledMove == Id("RETURN"), "second action caller PP/history");
    Check(next.State.Player.Pokemon.Moves.Length == 1 && next.State.Player.Pokemon.Moves[0].Move == Id("METRONOME"), "no fabricated slot after restore");
    var s = Pair(); var reverse = s with { Player = s.Opponent, Opponent = s.Player };
    var r = WildBattleRules.ResolveCalledMoveAction(catalog, reverse, new(WildBattleSide.Opponent, 0, Id("RETURN")));
    Check(r.Succeeded && r.State!.Opponent.Pokemon.Moves[0].CurrentPp == 9 && r.State.Player.Pokemon.CurrentHp < reverse.Player.Pokemon.CurrentHp, "opponent called action");
    var oracle = new Oracle(reverse); while (oracle.Next() % 2 == 1) { } oracle.Next(); oracle.Next(); oracle.Next(); oracle.Next(); Trace(r, oracle, "opponent current-target RNG");
});
Test("Present exact 256-bucket damage and healing branches include Ghost and full HP", () => {
    var move = catalog.Moves[Id("PRESENT")];
    foreach (int roll in new[] { 0, 101, 102, 177, 178, 203, 204, 255 })
    {
        uint seed = Enumerable.Range(0, 200000).Select(x => (uint)x).First(x => { var o = new Oracle(Pair(x)); o.Target(move); return o.Next() % 100 + 1 <= move.Accuracy && (o.Next() & 255) == roll; });
        var s = Pair(seed); s = s with { Opponent = s.Opponent with { Pokemon = s.Opponent.Pokemon with { CurrentHp = 10 } } }; Exact("PRESENT", s);
        if (roll >= 204)
        {
            var full = Act("PRESENT", Pair(seed)); Check(full.Battle.Events.OfType<WildBattleMoveFailedEvent>().Any(e => e.Reason == "already-full-hp"), "full HP Present fails");
            var ghost = Pair(seed, "GASTLY"); ghost = ghost with { Opponent = ghost.Opponent with { Pokemon = ghost.Opponent.Pokemon with { CurrentHp = 10 } } };
            var heal = Act("PRESENT", ghost); Check(heal.State!.Opponent.Pokemon.CurrentHp > 10 && !heal.Battle.Events.OfType<WildBattleEffectivenessEvent>().Any(), "heal clears Ghost immunity");
        }
    }
});
Test("Psywave rejection draws and fixed damage do not draw critical or damage RNG", () => {
    var m = catalog.Moves[Id("PSYWAVE")];
    uint seed = Enumerable.Range(0, 100000).Select(x => (uint)x).First(x => { var o = new Oracle(Pair(x)); o.Target(m); return o.Next() % 100 + 1 <= m.Accuracy && o.Next() % 16 > 10 && o.Next() % 16 > 10; });
    Exact("PSYWAVE", Pair(seed)); var r = Act("PSYWAVE", Pair(seed));
    Check(r.Battle.Trace.Count(t => t.Purpose.EndsWith(".psywave")) >= 3 && !r.Battle.Trace.Any(t => t.Purpose.EndsWith(".critical") || t.Purpose.EndsWith(".damage")), "rejection loop exact");
    foreach (int desired in new[] { 0, 10 })
    {
        seed = Enumerable.Range(0, 10000).Select(x => (uint)x).First(x => { var o = new Oracle(Pair(x)); o.Target(m); return o.Next() % 100 + 1 <= m.Accuracy && o.Next() % 16 == desired; });
        var tiny = Pair(seed, actorLevel: 1); Exact("PSYWAVE", tiny);
        Check(Act("PSYWAVE", tiny).Battle.Events.OfType<WildBattleHpChangedEvent>().Single().Damage == (desired == 0 ? 0 : 1), "level-one Psywave source zero/one damage " + desired);
    }
});
Test("OHKO strict chance boundary, level failure, immunity, Sturdy and Endure", () => {
    var m = catalog.Moves[Id("GUILLOTINE")];
    foreach (int roll in new[] { 29, 30 })
    {
        uint seed = Enumerable.Range(0, 10000).Select(x => (uint)x).First(x => { var o = new Oracle(Pair(x)); o.Target(m); return o.Next() % 100 + 1 == roll; });
        var s = Pair(seed, actorLevel: 50, targetLevel: 50); Exact("GUILLOTINE", s);
        Check((Act("GUILLOTINE", s).State!.Opponent.Pokemon.CurrentHp == 0) == (roll == 29), "strict < " + roll);
    }
    Exact("GUILLOTINE", Pair(actorLevel: 20, targetLevel: 100));
    var sturdy = Pair(0, "GEODUDE", actorLevel: 100, targetLevel: 20); var sr = Act("FISSURE", sturdy);
    Check(sturdy.Opponent.Pokemon.AbilityId == "ABILITY_ROCK_HEAD", "slot0 RockHead fixture");
    sturdy = sturdy with { Opponent = sturdy.Opponent with { Pokemon = WithMove(Mon("GEODUDE", 20, 1), "SPLASH") } };
    sr = Act("FISSURE", sturdy); Check(sr.State!.Opponent.Pokemon.CurrentHp == sturdy.Opponent.Pokemon.CurrentHp && sr.Battle.Events.OfType<WildBattleAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_STURDY") && !sr.Battle.Trace.Any(t => t.Purpose.EndsWith(".ohko")), "Sturdy before hit roll");
    var immune = Act("GUILLOTINE", Pair(0, "GASTLY")); Check(immune.Battle.Trace.Last().Purpose.EndsWith(".secondary-effect") && !immune.Battle.Trace.Any(t => t.Purpose.EndsWith(".ohko")), "Ghost before OHKO roll but HitFromAnimation secondary");
    var s2 = Pair(0, actorLevel: 100, targetLevel: 20); s2 = s2 with { Opponent = s2.Opponent with { Enduring = true } }; var er = Act("SHEER_COLD", s2); Check(er.State!.Opponent.Pokemon.CurrentHp == 1, "OHKO Endure");
});
Test("Jump Kick miss and Protect crash use no critical draw, cap and immunity", () => {
    foreach (string name in new[] { "JUMP_KICK", "HI_JUMP_KICK" })
    {
        var m = catalog.Moves[Id(name)]; uint seed = Enumerable.Range(0, 10000).Select(x => (uint)x).First(x => { var o = new Oracle(Pair(x)); o.Target(m); return o.Next() % 100 + 1 > m.Accuracy; });
        Exact(name, Pair(seed)); var s = Pair(actorLevel: 100, targetLevel: 5); s = s with { Opponent = s.Opponent with { Protected = true } }; Exact(name, s);
        var r = Act(name, s); Check(r.Battle.Trace.Any(t => t.Purpose.EndsWith(".damage")) && !r.Battle.Trace.Any(t => t.Purpose.EndsWith(".critical") || t.Purpose.EndsWith(".accuracy")), "Protect crash draw order");
        Check(r.Battle.Events.OfType<WildBattleRecoilEvent>().Single().Damage == s.Opponent.Pokemon.Stats.MaxHp / 2, "crash target max HP cap");
        var ghost = Pair(seed, "GASTLY"); r = Act(name, ghost); Check(r.State!.Player.Pokemon.CurrentHp == ghost.Player.Pokemon.CurrentHp && !r.Battle.Trace.Any(t => t.Purpose.EndsWith(".damage")), "ordinary immune miss skips crash");
        ghost = ghost with { Opponent = ghost.Opponent with { Protected = true } }; r = Act(name, ghost); Check(r.State!.Player.Pokemon.CurrentHp == ghost.Player.Pokemon.CurrentHp && r.Battle.Trace.Any(t => t.Purpose.EndsWith(".damage")), "protected Ghost reaches crash math with ineffective HP write");
    }
});
Test("recoil uses actual HP loss, source thirds, Rock Head and certain-effect draws", () => {
    foreach (string name in new[] { "TAKE_DOWN", "SUBMISSION", "DOUBLE_EDGE", "VOLT_TACKLE" })
    {
        var s = Pair(actorLevel: 100, targetLevel: 5); Exact(name, s); var r = Act(name, s);
        Check(!r.Battle.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect")), name + " certain skips secondary RNG");
        var rock = s with { Player = s.Player with { Pokemon = WithMove(Mon("GEODUDE", 100), "METRONOME") } }; r = Act(name, rock);
        Check(r.State!.Player.Pokemon.CurrentHp == rock.Player.Pokemon.CurrentHp && !r.Battle.Events.OfType<WildBattleRecoilEvent>().Any(), "Rock Head " + name);
    }
});
Test("False Swipe leaves one HP, still calculates at one HP, and respects Ghost", () => {
    var s = Pair(actorLevel: 100, targetLevel: 5); Exact("FALSE_SWIPE", s); var r = Act("FALSE_SWIPE", s); Check(r.State!.Opponent.Pokemon.CurrentHp == 1, "one HP");
    s = s with { Opponent = s.Opponent with { Pokemon = s.Opponent.Pokemon with { CurrentHp = 1 } } }; Exact("FALSE_SWIPE", s);
    r = Act("FALSE_SWIPE", Pair(0, "GASTLY")); Check(r.Battle.Events.OfType<WildBattleHpChangedEvent>().Single().Damage == 0, "Ghost immunity");
    var move = catalog.Moves[Id("FALSE_SWIPE")]; uint criticalSeed = Enumerable.Range(0, 10000).Select(x => (uint)x).First(x => { var o = new Oracle(Pair(x)); o.Target(move); o.Next(); return o.Next() % 16 == 0; });
    r = Act("FALSE_SWIPE", s with { Rng = new(criticalSeed) }); Check(r.Battle.Events.OfType<WildBattleCriticalHitEvent>().Count() == 1 && r.State!.Opponent.Pokemon.CurrentHp == 1, "zero-damage critical still emits critical message");
});
Test("Endeavor pre-accuracy failure, successful fixed delta, and Ghost immunity", () => {
    Exact("ENDEAVOR", Pair()); var s = Pair(actorLevel: 100, targetLevel: 5); Exact("ENDEAVOR", s);
    var r = Act("ENDEAVOR", s); Check(!r.Battle.Trace.Any(t => t.Purpose.EndsWith(".accuracy")), "HP failure precedes accuracy");
    r = Act("ENDEAVOR", Pair(0, "GASTLY")); Check(r.State!.Opponent.Pokemon.CurrentHp == Pair(0, "GASTLY").Opponent.Pokemon.CurrentHp, "Ghost fixed damage immune");
});
Test("Pain Split averages original HP, clamps each max HP, ignores typing and skips random damage", () => {
    foreach (string target in new[] { "RATTATA", "GASTLY" })
    {
        var s = Pair(0, target); var r = Act("PAIN_SPLIT", s); int average = (s.Player.Pokemon.CurrentHp + s.Opponent.Pokemon.CurrentHp) / 2;
        Check(r.State!.Player.Pokemon.CurrentHp == Math.Min(average, s.Player.Pokemon.Stats.MaxHp) && r.State.Opponent.Pokemon.CurrentHp == average, "exact split " + target);
        Check(r.Battle.Trace.All(t => t.Purpose.EndsWith(".called-target")), "no accuracy/crit/damage/secondary " + target);
        s = s with { Opponent = s.Opponent with { Protected = true } }; r = Act("PAIN_SPLIT", s); Check(r.State!.Player.Pokemon.CurrentHp == s.Player.Pokemon.CurrentHp && r.State.Opponent.Pokemon.CurrentHp == s.Opponent.Pokemon.CurrentHp, "Protect split failure");
    }
});
Test("Triple Kick performs accuracy per hit and one final secondary even after first miss", () => {
    var m = catalog.Moves[Id("TRIPLE_KICK")];
    foreach (int hits in new[] { 0, 1, 2, 3 })
    {
        uint seed = Enumerable.Range(0, 100000).Select(x => (uint)x).First(x => Oracle.Execute(m, Pair(x)).Hits == hits);
        Exact("TRIPLE_KICK", Pair(seed)); var r = Act("TRIPLE_KICK", Pair(seed));
        Check(r.Battle.Events.OfType<WildBattleMultiHitEvent>().Single().ActualHits == hits, "hits " + hits);
        Check(r.Battle.Trace.Count(t => t.Purpose.EndsWith(".secondary-effect")) == 1, "single secondary " + hits);
        Check(r.Battle.Events.OfType<WildBattleMoveMissedEvent>().Any() == (hits == 0), "partial success clears missed flag " + hits);
    }
    var ghost = Act("TRIPLE_KICK", Pair(0, "GASTLY")); Check(ghost.Battle.Trace.Any(t => t.Purpose.EndsWith(".damage")), "TripleKick immune typecalc still adjusts damage before branch");
});
Test("Facade and Smelling Salt double calculated damage after plus two and preserve burn penalty", () => {
    foreach (uint status in new[] { WildBattleStatus.Poison, WildBattleStatus.Burn, WildBattleStatus.Paralysis })
    {
        var s = Pair(); s = s with { Player = s.Player with { Status = status } };
        // Choose a non-full-paralysis seed; the cancellation draw is part of the independent oracle.
        if (status == WildBattleStatus.Paralysis) s = s with { Rng = new(Enumerable.Range(0, 100).Select(x => (uint)x).First(x => new Oracle(Pair(x)).Next() % 4 != 0)) };
        Exact("FACADE", s);
    }
    var salty = Pair(); salty = salty with { Opponent = salty.Opponent with { Status = WildBattleStatus.Paralysis } }; Exact("SMELLING_SALT", salty);
    Check(!Act("SMELLING_SALT", salty).Battle.Trace.Any(t => t.Purpose.EndsWith(".secondary-effect")), "certain paralysis cure skips draw");
});
Test("Blaze Kick and Poison Tail use high-critical and inclusive secondary chance", () => {
    foreach (string name in new[] { "BLAZE_KICK", "POISON_TAIL" })
    {
        var m = catalog.Moves[Id(name)]; uint seed = Enumerable.Range(0, 100000).Select(x => (uint)x).First(x => {
            var o = new Oracle(Pair(x)); o.Target(m); return o.Next() % 100 + 1 <= m.Accuracy && o.Next() % 16 == 8;
        });
        Exact(name, Pair(seed)); Check(Act(name, Pair(seed)).Battle.Events.OfType<WildBattleHpChangedEvent>().Single().Critical, name + " high crit");
        seed = Enumerable.Range(0, 100000).Select(x => (uint)x).First(x => { var o = Oracle.Execute(m, Pair(x)); return o.SecondaryRoll == m.SecondaryEffectChance; });
        var r = Act(name, Pair(seed)); Check(r.State!.Opponent.Status == (name == "BLAZE_KICK" ? WildBattleStatus.Burn : WildBattleStatus.Poison), name + " chance equality");
    }
});
Test("Protect failures retain recipe PP; metadata and overflow reject atomically", () => {
    foreach (string name in members)
    {
        var s = Pair(); s = s with { Opponent = s.Opponent with { Protected = true } }; var r = Act(name, s);
        Check(r.State!.Player.Pokemon.Moves[0].CurrentPp == 9 && r.State.Opponent.Pokemon.CurrentHp == s.Opponent.Pokemon.CurrentHp, name + " Protect PP and target HP");
    }
    var state = Pair(); var current = catalog.Moves[Id("HIDDEN_POWER")]; var forged = catalog with { Moves = catalog.Moves.SetItem(current.Id, current with { Power = 70 }) };
    var fault = WildBattleRules.ResolveCalledMoveAction(forged, state, new(WildBattleSide.Player, 0, current.Id));
    Check(!fault.Succeeded && ReferenceEquals(fault.State, state) && fault.Battle.Trace.IsEmpty && fault.Battle.Events.IsEmpty, "tampered source atomic");
    uint longSeed = Enumerable.Range(0, 10000).Select(x => (uint)x).First(x => Oracle.Execute(catalog.Moves[Id("TRIPLE_KICK")], Pair(x)).Hits == 3);
    int longDraws = Oracle.Execute(catalog.Moves[Id("TRIPLE_KICK")], Pair(longSeed)).Raw.Count;
    foreach (var s in new[] { state with { Rng = new(longSeed, ulong.MaxValue - (ulong)(longDraws - 1)) }, state with { Revision = long.MaxValue }, state with { EventSequence = long.MaxValue } })
    {
        fault = WildBattleRules.ResolveCalledMoveAction(catalog, s, new(WildBattleSide.Player, 0, Id("TRIPLE_KICK")));
        Check(!fault.Succeeded && ReferenceEquals(fault.State, s) && fault.Battle.Trace.IsEmpty && fault.Battle.Events.IsEmpty, "instrumentation overflow atomic");
    }
});
Console.WriteLine($"RESULT groups={groups} checks={checks} failures={failures} exactB04Members={members.Length} naturalMetronomeEnabled=false crossStateClosure=false");
if (failures > 0 || checks == 0) Environment.Exit(1);

PokemonInstance Recalculate(PokemonInstance p)
{
    var b = catalog.Species[p.Species].BaseStats; var ev = p.Evs;
    int Other(int basis, int iv, int effort, Stat stat) => StatRules.CalculateOther(basis, iv, effort, p.Level, p.Nature, stat);
    var stats = new BattleStats(StatRules.CalculateHp(b.Hp, p.Ivs.Hp, ev.Hp, p.Level), Other(b.Attack, p.Ivs.Attack, ev.Attack, Stat.Attack),
        Other(b.Defense, p.Ivs.Defense, ev.Defense, Stat.Defense), Other(b.SpecialAttack, p.Ivs.SpecialAttack, ev.SpecialAttack, Stat.SpecialAttack),
        Other(b.SpecialDefense, p.Ivs.SpecialDefense, ev.SpecialDefense, Stat.SpecialDefense), Other(b.Speed, p.Ivs.Speed, ev.Speed, Stat.Speed));
    return p with { Stats = stats, CurrentHp = stats.MaxHp };
}

// Independent transcription of source command ordering and integer formulas for
// the neutral Clefairy/Rattata fixture. No production damage/RNG helpers are used.
sealed class Oracle(WildBattleState state)
{
    public uint Seed = state.Rng.Seed; public ulong InitialCursor = state.Rng.Cursor; public List<int> Raw = [];
    public int ActorHp = state.Player.Pokemon.CurrentHp, TargetHp = state.Opponent.Pokemon.CurrentHp, Hits; public ulong PpCursor; public int? SecondaryRoll;
    public int Next() { Seed = unchecked(Seed * 1103515245u + 24691u); int value = (int)(Seed >> 16); Raw.Add(value); return value; }
    public void Target(PokemonMoveDefinition move) { if (move.Target == "MOVE_TARGET_SELECTED") while (Next() % 2 == 0) { } }
    public static (int Power, string Type) Hidden(PokemonInstance p)
    {
        int[] iv = [p.Ivs.Hp, p.Ivs.Attack, p.Ivs.Defense, p.Ivs.Speed, p.Ivs.SpecialAttack, p.Ivs.SpecialDefense]; int bits = 0, powers = 0;
        for (int i = 0; i < 6; i++) { bits += (iv[i] % 2) * (1 << i); powers += ((iv[i] / 2) % 2) * (1 << i); }
        string[] types = ["FIGHTING", "FLYING", "POISON", "GROUND", "ROCK", "BUG", "GHOST", "STEEL", "FIRE", "WATER", "GRASS", "ELECTRIC", "PSYCHIC", "ICE", "DRAGON", "DARK"];
        return (30 + 40 * powers / 63, "TYPE_" + types[15 * bits / 63]);
    }
    public static Oracle Execute(PokemonMoveDefinition m, WildBattleState s)
    {
        var o = new Oracle(s); var a = s.Player.Pokemon; var t = s.Opponent.Pokemon; bool protectedTarget = s.Opponent.Protected;
        if (s.Player.Status == WildBattleStatus.Paralysis && o.Next() % 4 == 0) return o;
        o.Target(m); bool firstPp = m.Effect is "EFFECT_OHKO" or "EFFECT_PAIN_SPLIT" or "EFFECT_ENDEAVOR" or "EFFECT_TRIPLE_KICK";
        void Pp() => o.PpCursor = o.InitialCursor + (ulong)o.Raw.Count;
        if (firstPp) Pp();
        if (m.Effect == "EFFECT_PAIN_SPLIT") { if (!protectedTarget) { int average = (o.ActorHp + o.TargetHp) / 2; o.ActorHp = Math.Min(average, a.Stats.MaxHp); o.TargetHp = Math.Min(average, t.Stats.MaxHp); } return o; }
        if (m.Effect == "EFFECT_OHKO") { if (!protectedTarget && o.Next() % 100 + 1 < m.Accuracy + a.Level - t.Level && a.Level >= t.Level) { o.TargetHp = s.Opponent.Enduring ? 1 : 0; o.Next(); } return o; }
        if (m.Effect == "EFFECT_ENDEAVOR" && o.TargetHp <= o.ActorHp) return o;
        bool Accuracy() => !protectedTarget && o.Next() % 100 + 1 <= m.Accuracy;
        if (m.Effect == "EFFECT_TRIPLE_KICK")
        {
            for (int i = 1; i <= 3 && o.TargetHp > 0; i++) { if (!Accuracy()) break; bool crit = o.Next() % 16 == 0; int percent = 100 - o.Next() % 16; int dmg = Damage(m, s, i * 10, m.Type, crit, percent, 1); o.TargetHp = Math.Max(s.Opponent.Enduring ? 1 : 0, o.TargetHp - dmg); o.Hits++; if (s.Opponent.Enduring && o.TargetHp == 1) break; }
            o.SecondaryRoll = o.Next() % 100; return o;
        }
        bool hit = Accuracy(); if (!firstPp) Pp();
        if (!hit)
        {
            if (m.Effect == "EFFECT_RECOIL_IF_MISS") { int damage = Damage(m, s, m.Power, m.Type, false, 100 - o.Next() % 16, 1); o.ActorHp = Math.Max(0, o.ActorHp - Math.Min(Math.Max(1, damage / 2), t.Stats.MaxHp / 2)); }
            return o;
        }
        if (m.Effect == "EFFECT_PSYWAVE") { int v; do { v = o.Next() % 16; } while (v > 10); o.TargetHp = Math.Max(0, o.TargetHp - a.Level * (50 + 10 * v) / 100); o.SecondaryRoll = o.Next() % 100; return o; }
        if (m.Effect == "EFFECT_ENDEAVOR") { o.TargetHp = o.ActorHp; o.SecondaryRoll = o.Next() % 100; return o; }
        int power = m.Power, mult = 1; string type = m.Type;
        if (m.Effect == "EFFECT_PRESENT") { int roll = o.Next() & 255; if (roll >= 204) { o.TargetHp = Math.Min(t.Stats.MaxHp, o.TargetHp + Math.Max(1, t.Stats.MaxHp / 4)); return o; } power = roll < 102 ? 40 : roll < 178 ? 80 : 120; }
        if (m.Effect == "EFFECT_RETURN") power = Math.Max(1, a.Friendship * 10 / 25);
        if (m.Effect == "EFFECT_FRUSTRATION") power = Math.Max(1, (255 - a.Friendship) * 10 / 25);
        if (m.Effect == "EFFECT_ERUPTION") power = Math.Max(1, a.CurrentHp * 150 / a.Stats.MaxHp);
        if (m.Effect == "EFFECT_HIDDEN_POWER") (power, type) = Hidden(a);
        if (m.Effect == "EFFECT_FACADE" && (s.Player.Status & 0xd8) != 0 || m.Effect == "EFFECT_SMELLINGSALT" && s.Opponent.Status == WildBattleStatus.Paralysis) mult = 2;
        bool critical = o.Next() % (m.Effect is "EFFECT_BLAZE_KICK" or "EFFECT_POISON_TAIL" ? 8 : 16) == 0;
        int damage2 = Damage(m, s, power, type, critical, 100 - o.Next() % 16, mult);
        int before = o.TargetHp; o.TargetHp = Math.Max(m.Effect == "EFFECT_FALSE_SWIPE" || s.Opponent.Enduring ? 1 : 0, before - damage2); int lost = before - o.TargetHp;
        if (m.Effect is "EFFECT_RECOIL" or "EFFECT_DOUBLE_EDGE") o.ActorHp = Math.Max(0, o.ActorHp - Math.Max(1, lost / (m.Effect == "EFFECT_RECOIL" ? 4 : 3)));
        else if (m.Effect != "EFFECT_SMELLINGSALT") o.SecondaryRoll = o.Next() % 100;
        return o;
    }
    private static int Damage(PokemonMoveDefinition move, WildBattleState s, int power, string type, bool crit, int percent, int mult)
    {
        var a = s.Player.Pokemon; var t = s.Opponent.Pokemon;
        bool physical = type is "TYPE_NORMAL" or "TYPE_FIGHTING" or "TYPE_FLYING" or "TYPE_POISON" or "TYPE_GROUND" or "TYPE_ROCK" or "TYPE_BUG" or "TYPE_GHOST" or "TYPE_STEEL";
        int atk = physical ? a.Stats.Attack : a.Stats.SpecialAttack, def = physical ? t.Stats.Defense : t.Stats.SpecialDefense;
        int v = atk * power * (2 * a.Level / 5 + 2) / def / 50;
        if (physical && s.Player.Status == WildBattleStatus.Burn) v /= 2;
        if (!physical && !crit && (s.CalledField?.OpponentLightScreenTurnsRemaining ?? 0) > 0) v /= 2;
        if (physical) v = Math.Max(1, v);
        bool suppressed = new[] { s.Player.Pokemon.AbilityId, s.Opponent.Pokemon.AbilityId }.Any(x => x is "ABILITY_CLOUD_NINE" or "ABILITY_AIR_LOCK");
        if (!physical && !suppressed)
        {
            if (s.RainTurnsRemaining > 0) { if (type == "TYPE_FIRE") v /= 2; else if (type == "TYPE_WATER") v = v * 15 / 10; }
            if ((s.CalledField?.SunTurnsRemaining ?? 0) > 0) { if (type == "TYPE_WATER") v /= 2; else if (type == "TYPE_FIRE") v = v * 15 / 10; }
        }
        v += 2; if (crit) v *= 2; v *= mult;
        if (type == "TYPE_NORMAL") v = v * 15 / 10;
        if (type == "TYPE_FIGHTING") v *= 2;
        if (type == "TYPE_GHOST") v = 0;
        return v == 0 ? 0 : Math.Max(1, v * percent / 100);
    }
}
