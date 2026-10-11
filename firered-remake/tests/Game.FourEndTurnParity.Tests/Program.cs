using System.Collections.Immutable;
using System.Text.Json;
using System.Reflection;
using Game.Content;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

if (args.Length != 1) throw new ArgumentException("Pass the project root.");
string root = Path.GetFullPath(args[0]);
int groups = 0, checks = 0, failures = 0;
var parity = new List<string>();
void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
void Test(string title, Action body) { groups++; try { body(); Console.WriteLine("PASS " + title); } catch (Exception ex) { failures++; Console.WriteLine("FAIL " + title + " " + ex); } }
MoveId Id(string name) => new("pret:MOVE_" + name);
var original = PretRocketHideoutPokemon.Load(root).Catalog;
var supplement = PretVermilionGymTrainers.ExtendPokemon(root, PretCeladonApproachPokemon.Load(root).Catalog, celadonApproachRules: true);
var prior = original with { Moves = original.Moves.SetItems(supplement.Moves) };
var old = PretEeveePokemon.Load(PretSnorlaxPokemon.Load(PretPokemonTowerPokemon.Load(prior, root, root).Catalog, root).Catalog, root).Catalog;
var catalog = PretMetronomeMoveCatalog.Load(root).Extend(PretFuchsiaRoutePokemon.Load(old, root).Catalog);
PokemonInstance Mon(string species = "CLEFAIRY", int level = 35, uint pid = 0) => PokemonFactory.Create(catalog,
    new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(pid)), new(0)).Pokemon;
PokemonInstance Moves(PokemonInstance mon, params string[] names) => mon with { Moves = names.Select(n => new PokemonMoveSlot(Id(n), catalog.Moves[Id(n)].Pp, catalog.Moves[Id(n)].Pp)).ToImmutableArray() };
WildBattleState Pair(string player = "CLEFAIRY", string opponent = "RATTATA", uint seed = 0) => new(
    new(Moves(Mon(player), "METRONOME")) { FirstTurnCounter = 1 },
    new(Moves(Mon(opponent), "METRONOME")) { FirstTurnCounter = 1 },
    0, 0, 0, new(seed), 0, WildBattleOutcome.Ongoing, RulesVersion: WildBattleRules.FuchsiaRulesVersion) { Terrain = WildBattleTerrain.Plain };
WildBattleCombatant At(WildBattleState state, WildBattleSide side) => side == WildBattleSide.Player ? state.Player : state.Opponent;
WildBattleCalledPartyMember Member(string species = "CLEFAIRY", uint status = 0, uint pid = 1) => new(Moves(Mon(species, pid: pid), "METRONOME"), Status: status);
WildBattleState Party(WildBattleState state, ImmutableArray<WildBattleCalledPartyMember> player = default, ImmutableArray<WildBattleCalledPartyMember> opponent = default)
{
    WildBattleCalledPartyMember Active(WildBattleCombatant mon) => new(WildBattleTransformationRules.PersistentPokemon(mon), mon.HeldItem,
        WildBattleTransformationRules.PersistentStatus(mon), KnockedOffItem: mon.KnockedOffItem);
    return state with { CalledParty = new(player.IsDefault ? [Active(state.Player)] : player.Insert(0, Active(state.Player)),
        opponent.IsDefault ? [Active(state.Opponent)] : opponent.Insert(0, Active(state.Opponent)), 0, 0,
        opponent.IsDefaultOrEmpty ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer) };
}
WildBattleSourceEndTurnResult Phase(WildBattleState state, WildBattleSourceEndTurnCursor? cursor = null)
{
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, state) is null, "input valid " + WildBattleRules.ValidateCalledMoveActionState(catalog, state));
    var result = WildBattleRules.ResolveSourceEndTurnPhase(catalog, state, cursor);
    parity.Add(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result)))));
    Check(result.Succeeded, "phase accepted " + result.Battle.Fault);
    Check(result.Cursor is not null, "cursor returned");
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!) is null, "output valid " + WildBattleRules.ValidateCalledMoveActionState(catalog, result.State!));
    Check(result.State!.Turn == state.Turn && result.State.RandomTurnNumber == state.RandomTurnNumber, "phase does not advance turn or randomTurnNumber");
    return result;
}
WildBattleState Replace(WildBattleState state, WildBattleSide side, int index = 1)
{
    var result = WildBattleRules.ResolveCalledPartySwitch(catalog, state, new(side, index));
    Check(result.Succeeded, "real replacement accepted " + result.Fault);
    return result.State!;
}
(WildBattleSide First, WildBattleEvent[] Events, RandomTrace[] Trace) ActionOrder(WildBattleState state)
{
    Check(WildBattleRules.ValidateCalledMoveActionState(catalog, state) is null, "source action-order fixture validates");
    // Exercise the actual internal consumer seam, without adding a second public whole-turn API.
    var type = typeof(WildBattleRules).GetNestedType("Execution", BindingFlags.NonPublic)!;
    var execution = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
        binder: null, args: [state, false, true], culture: null)!;
    var first = (WildBattleSide)type.GetMethod("SourceActionsOrder", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(execution, [catalog])!;
    var events = (ImmutableArray<WildBattleEvent>.Builder)type.GetField("Events")!.GetValue(execution)!;
    var trace = (ImmutableArray<RandomTrace>.Builder)type.GetField("Trace")!.GetValue(execution)!;
    return (first, events.ToArray(), trace.ToArray());
}
WildBattleCalledWish Wish(WildBattleCombatant mon, int turns = 1) => new(turns, 0, mon.Pokemon.Species, mon.Pokemon.OriginalTrainerId, mon.Pokemon.Personality);
WildBattleCalledTurnLock Lock(string move, WildBattleCalledLockKind kind, WildBattleSide target, int turns) => new(kind, Id(move), 0, Id("METRONOME"), target, turns);
void Atomic(WildBattleState state, WildBattleSourceEndTurnCursor? cursor, string label)
{
    var result = WildBattleRules.ResolveSourceEndTurnPhase(catalog, state, cursor);
    parity.Add(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result)))));
    Check(!result.Succeeded && ReferenceEquals(result.State, state) && result.Battle.Events.IsEmpty && result.Battle.Trace.IsEmpty && ReferenceEquals(result.Cursor, cursor), label + " exact rollback");
}
using var vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "source-vectors.json")));
Test("pinned runtime and nonempty independent source phase vectors", () => {
    Check(Environment.Version.Major == 10 && catalog.Species.Count == 107, "runtime10 and genuine bounded roster");
    Check(vectors.RootElement.GetProperty("sourceCommit").GetString() == MetronomeSourceMoveDefinition.SourceCommit, "fixed source commit");
    Check(vectors.RootElement.GetProperty("fieldOrder").GetArrayLength() == 9, "nine source field steps");
    Check(vectors.RootElement.GetProperty("battlerOrder").GetArrayLength() == 19, "nineteen source battler steps");
});
Test("no-effect source phase still yields real faint-action boundary and never readies a new turn", () => {
    var s = Party(Pair()); var first = Phase(s);
    Check(first.Cursor!.Boundary == WildBattleSourceEndTurnBoundary.FaintedActions, "normal checkpoint exposed");
    Check(first.Battle.Events.OfType<WildBattleOrderEvent>().Single().First == WildBattleSide.Opponent, "actual speed order");
    Check(first.Battle.Trace.IsEmpty && first.State!.Player.FirstTurnCounter == 1, "no invented RNG or Ready cleanup");
    var last = Phase(first.State!, first.Cursor);
    Check(last.Cursor!.Boundary == WildBattleSourceEndTurnBoundary.Complete && last.Battle.Trace.IsEmpty, "delayed phase completion");
    Atomic(last.State!, last.Cursor, "completed cursor rejection");
});
Test("Reflect Light Screen Mist Safeguard precede ordered Wish and lethal sand damage", () => {
    var s = Pair(); s = s with { Player = s.Player with { Pokemon = s.Player.Pokemon with { CurrentHp = 1 } },
        PlayerReflectTurnsRemaining = 1, OpponentReflectTurnsRemaining = 2, SandstormTurnsRemaining = 2,
        CalledField = new() { PlayerLightScreenTurnsRemaining = 2, OpponentLightScreenTurnsRemaining = 1,
            PlayerMistTurnsRemaining = 3, OpponentMistTurnsRemaining = 2, PlayerSafeguardTurnsRemaining = 1, OpponentSafeguardTurnsRemaining = 4 } };
    s = s with { CalledDelayed = new() { PlayerWish = Wish(s.Player), OpponentWish = Wish(s.Opponent) } };
    var r = Phase(Party(s));
    string[] expected = ["reflect:Player", "reflect:Opponent", "light-screen:Player", "light-screen:Opponent", "mist:Player", "mist:Opponent", "safeguard:Player", "safeguard:Opponent", "sandstorm:Player"];
    Check(r.Battle.Events.OfType<WildBattleMtMoonVolatileEvent>().Select(e => e.Volatile + ":" + e.Side).SequenceEqual(expected), "source global timer order");
    var wish = r.Battle.Events.OfType<WildBattleCalledDelayedEvent>().ToArray();
    Check(wish.Length == 2 && wish[0].Target == WildBattleSide.Opponent && wish[1].Target == WildBattleSide.Player, "Wish follows retained order");
    var heal = r.Battle.Events.OfType<WildBattleRecoveredEvent>().Single(); var weather = r.Battle.Events.OfType<WildBattleWeatherDamageEvent>().ToArray();
    Check(heal.Sequence < weather.Min(e => e.Sequence), "Wish heals before damage that otherwise kills");
    Check(r.State!.Player.Pokemon.CurrentHp == 1 + s.Player.Pokemon.Stats.MaxHp / 2 - Math.Max(1, s.Player.Pokemon.Stats.MaxHp / 16), "independent Wish then sand arithmetic");
    Check(r.State.CalledDelayed!.PlayerWish is null && r.State.CalledDelayed.OpponentWish is null, "both wishes consumed exactly once");
});
Test("weather expiry suppresses damage; Hail and Sun use source field positions and type overlays", () => {
    foreach (string weather in new[] { "rain", "sandstorm", "sun", "hail" })
    foreach (int remaining in new[] { 1, 2 })
    {
        var s = Pair(); s = s with { RainTurnsRemaining = weather == "rain" ? remaining : 0, SandstormTurnsRemaining = weather == "sandstorm" ? remaining : 0,
            CalledField = new() { SunTurnsRemaining = weather == "sun" ? remaining : 0, HailTurnsRemaining = weather == "hail" ? remaining : 0 } };
        if (weather == "hail") s = s with { Player = s.Player with { CalledOverlay = new() { Type1 = "TYPE_ICE", Type2 = "TYPE_ICE" } } };
        var r = Phase(Party(s)); var count = r.Battle.Events.OfType<WildBattleWeatherDamageEvent>().Count();
        Check(count == (remaining == 1 ? 0 : weather == "sandstorm" ? 2 : weather == "hail" ? 1 : 0), weather + " expiry and immunity");
        Check(r.Battle.Events.OfType<WildBattleMtMoonVolatileEvent>().Single().After == remaining - 1, weather + " exactly one tick");
    }
});
Test("Ingrain items Leech Seed Toxic execute in source order with divide-first Toxic ramp", () => {
    var s = Pair(); int maximum = s.Player.Pokemon.Stats.MaxHp;
    s = s with { Player = s.Player with { Pokemon = s.Player.Pokemon with { CurrentHp = maximum / 3 }, Rooted = true,
        HeldItem = "ITEM_ORAN_BERRY", LeechSeedSource = WildBattleSide.Opponent, Status = WildBattleSourceFieldEffects.ToxicPoison }, CalledField = new() };
    var r = Phase(Party(s)); var ev = r.Battle.Events;
    long ingrain = ev.OfType<WildBattleIngrainHealEvent>().Single().Sequence;
    long oran = ev.OfType<WildBattleHeldItemConsumedEvent>().Single().Sequence;
    long seed = ev.OfType<WildBattleLeechSeedDrainEvent>().Single().Sequence;
    var toxic = ev.OfType<WildBattleStatusDamageEvent>().Single();
    Check(ingrain < oran && oran < seed && seed < toxic.Sequence, "Ingrain items Leech Toxic event order");
    Check(toxic.Damage == Math.Max(1, maximum / 16) && r.State!.Player.Status == 0x180, "first Toxic divide then multiply");
    var ended = Phase(r.State!, r.Cursor);
    var clean = ended.State! with { Turn = ended.State.Turn + 1 };
    var second = Phase(clean);
    Check(second.Battle.Events.OfType<WildBattleStatusDamageEvent>().Single().Damage == Math.Max(1, maximum / 16) * 2, "next turn Toxic ramp exactly once");
});
Test("mixed Disable Encore Lock-On Charge Taunt Yawn timers tick at their own positions", () => {
    var s = Pair(); s = s with { Player = s.Player with { DisabledMove = Id("METRONOME"), DisableTurnsRemaining = 3,
        EncoreMoveSlot = 0, EncoredMove = Id("METRONOME"), EncoreTurnsRemaining = 3, ChargeTurnsRemaining = 2, YawnTurnsRemaining = 2,
        CalledVolatile = new() { TauntTurnsRemaining = 2 }, CalledTurnState = new() { SureHitSource = WildBattleSide.Opponent, SureHitTurnsRemaining = 2 } } };
    var r = Phase(Party(s)); var p = r.State!.Player;
    Check(p.DisableTurnsRemaining == 2 && p.EncoreTurnsRemaining == 2 && p.CalledTurnState!.SureHitTurnsRemaining == 1 && p.ChargeTurnsRemaining == 1 && p.CalledVolatile!.TauntTurnsRemaining == 1 && p.YawnTurnsRemaining == 1, "all independent timers once");
    var timer = r.Battle.Events.OfType<WildBattleMtMoonVolatileEvent>().Where(e => e.Side == WildBattleSide.Player).ToArray();
    long disable = r.Battle.Events.OfType<WildBattleDisableChangedEvent>().Single().Sequence;
    long encore = r.Battle.Events.OfType<WildBattleEncoreChangedEvent>().Single().Sequence;
    long charge = timer.Single(e => e.Volatile == "charge").Sequence;
    long taunt = r.Battle.Events.OfType<WildBattleMtMoonVolatileEvent>().Single(e => e.Volatile == "taunt").Sequence;
    long yawn = r.Battle.Events.OfType<WildBattleYawnEvent>().Single().Sequence;
    Check(disable < encore && encore < charge && charge < taunt && taunt < yawn, "source mixed countdown event order");
});
Test("Yawn is followed by the battler second item pass on both sides", () => {
    foreach (var side in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
    {
        var s = Pair(); var mon = At(s, side) with { YawnTurnsRemaining = 1, HeldItem = "ITEM_CHESTO_BERRY" };
        s = side == WildBattleSide.Player ? s with { Player = mon } : s with { Opponent = mon };
        var r = Phase(Party(s)); var changes = r.Battle.Events.OfType<WildBattleStatusChangedEvent>().Where(e => e.Side == side).ToArray();
        Check(changes.Length == 2 && changes[0].Reason == "yawn" && changes[1].Reason == "held-berry", "Yawn then item2 " + side);
        Check(At(r.State!, side).Status == 0 && At(r.State!, side).HeldItem == "ITEM_NONE", "same-turn Chesto consumption " + side);
    }
});
Test("sleep cancels multiturn locks before fields; Uproar wakes only at its own ordered residual", () => {
    foreach (bool uproarFaster in new[] { true, false })
    {
        var s = Pair(player: uproarFaster ? "RATTATA" : "CLEFAIRY", opponent: uproarFaster ? "CLEFAIRY" : "RATTATA");
        s = s with { Player = s.Player with { CalledTurnState = new() { Lock = Lock("UPROAR", WildBattleCalledLockKind.Uproar, WildBattleSide.Opponent, 2) } },
            Opponent = s.Opponent with { Status = 3, Nightmare = true, CalledTurnState = new() { Lock = Lock("THRASH", WildBattleCalledLockKind.Rampage, WildBattleSide.Player, 2) } } };
        var r = Phase(Party(s));
        Check(r.State!.Opponent.Status == 0 && r.State.Opponent.CalledTurnState?.Lock is null && !r.State.Opponent.Nightmare, "wake and prior sleeping lock cancellation");
        Check(r.State.Player.CalledTurnState!.Lock!.TurnsRemaining == 1, "Uproar countdown after wake");
        var nightmares = r.Battle.Events.OfType<WildBattleTowerHpEvent>().Where(e => e.Effect == "nightmare").ToArray();
        Check(nightmares.Length == (uproarFaster ? 0 : 1), "nightmare observes source battler order");
        long cancel = r.Battle.Events.OfType<WildBattleMtMoonVolatileEvent>().Single(e => e.Side == WildBattleSide.Opponent && e.Volatile == "called-rampage" && e.After == 0).Sequence;
        long order = r.Battle.Events.OfType<WildBattleOrderEvent>().Single().Sequence;
        Check(cancel < order, "sleep cancellation before field order and Uproar wake");
    }
});
Test("normal residual faint waits for real replacement before Future without replaying residuals", () => {
    var s = Pair(); s = s with { Player = s.Player with { Pokemon = s.Player.Pokemon with { CurrentHp = 1 }, Status = WildBattleStatus.Burn },
        Opponent = s.Opponent with { Status = WildBattleStatus.Poison }, SandstormTurnsRemaining = 3,
        CalledDelayed = new() { PlayerFutureAttack = new(1, Id("FUTURE_SIGHT"), WildBattleSide.Opponent, 10) } };
    var reserve = Member(status: WildBattleStatus.Poison);
    var normal = Phase(Party(s, [reserve]));
    Check(normal.State!.CalledParty!.PendingSwitch?.Side == WildBattleSide.Player && normal.State.Outcome == WildBattleOutcome.Ongoing, "real reserve prevents false loss");
    Check(normal.State.CalledDelayed!.PlayerFutureAttack!.TurnsRemaining == 1, "Future untouched before normal faint checkpoint");
    Check(normal.Battle.Events.OfType<WildBattleStatusDamageEvent>().Any(e => e.Side == WildBattleSide.Opponent), "other original battler receives its normal residual");
    Atomic(normal.State, normal.Cursor, "cannot resume before actual replacement");
    var switched = Replace(normal.State, WildBattleSide.Player);
    var future = Phase(switched, normal.Cursor);
    Check(future.State!.Player.Pokemon.CurrentHp is > 0 && future.State.Player.Pokemon.CurrentHp < reserve.Pokemon.CurrentHp, "incoming target receives delayed Future");
    Check(future.State.Player.Status == WildBattleStatus.Poison && future.State.SandstormTurnsRemaining == 2, "incoming poison and weather counters not replayed");
    Check(!future.Battle.Events.Any(e => e is WildBattleStatusDamageEvent or WildBattleWeatherDamageEvent), "no prior residual damages new member");
    Check(future.State.CalledParty!.Player[0].Pokemon.CurrentHp == 0 && future.State.CalledParty.PlayerActiveIndex == 1, "real outgoing and incoming slots retained");
});
Test("each numeric Future callback pauses before the next delayed hit and replacement", () => {
    var s = Pair(); s = s with { CalledDelayed = new() { PlayerFutureAttack = new(1, Id("FUTURE_SIGHT"), WildBattleSide.Opponent, 9999),
        OpponentFutureAttack = new(1, Id("DOOM_DESIRE"), WildBattleSide.Player, 9999) } };
    var normal = Phase(Party(s, [Member()], [Member("RATTATA")]));
    var first = Phase(normal.State!, normal.Cursor);
    Check(first.Battle.Events.OfType<WildBattleCalledDelayedEvent>().Single().Target == WildBattleSide.Player, "Future numeric order ignores faster opponent");
    Check(first.State!.CalledParty!.PendingSwitch?.Side == WildBattleSide.Player && first.State.CalledDelayed!.OpponentFutureAttack!.TurnsRemaining == 1, "first hit yields before second timer");
    var switched = Replace(first.State, WildBattleSide.Player);
    var second = Phase(switched, first.Cursor);
    Check(second.State!.CalledParty!.PendingSwitch?.Side == WildBattleSide.Opponent && second.State.Player.Pokemon.CurrentHp == switched.Player.Pokemon.CurrentHp, "second callback separate and first not replayed");
    switched = Replace(second.State, WildBattleSide.Opponent);
    var last = Phase(switched, second.Cursor);
    Check(last.Cursor!.Boundary == WildBattleSourceEndTurnBoundary.Complete && last.State!.Outcome == WildBattleOutcome.Ongoing, "both genuine reserves complete phase");
});
Test("Perish follows fixed order with an EXP replacement boundary between each callback", () => {
    var s = Pair(); s = s with { Player = s.Player with { CalledVolatile = new() { PerishSongTimer = 0 } },
        Opponent = s.Opponent with { CalledVolatile = new() { PerishSongTimer = 0 } } };
    var normal = Phase(Party(s, [Member()], [Member("RATTATA")]));
    var first = Phase(normal.State!, normal.Cursor);
    Check(first.State!.CalledParty!.PendingSwitch?.Side == WildBattleSide.Opponent && first.State.Player.Pokemon.CurrentHp > 0 && first.State.Player.CalledVolatile!.PerishSongTimer == 0, "faster Perish yields before slower timer");
    var switched = Replace(first.State, WildBattleSide.Opponent);
    var second = Phase(switched, first.Cursor);
    Check(second.State!.CalledParty!.PendingSwitch?.Side == WildBattleSide.Player && second.State.Opponent.Pokemon.CurrentHp == switched.Opponent.Pokemon.CurrentHp, "incoming opponent never inherits departed Perish");
    switched = Replace(second.State, WildBattleSide.Player);
    var last = Phase(switched, second.Cursor);
    Check(last.Cursor!.Boundary == WildBattleSourceEndTurnBoundary.Complete && last.State!.Outcome == WildBattleOutcome.Ongoing, "Perish real reserve survival");
});
Test("terminal first Perish still resumes later delayed callbacks before final cleanup", () => {
    var s = Pair(); s = s with { Player = s.Player with { CalledVolatile = new() { PerishSongTimer = 0 } },
        Opponent = s.Opponent with { CalledVolatile = new() { PerishSongTimer = 0 } } };
    var normal = Phase(Party(s)); var first = Phase(normal.State!, normal.Cursor);
    Check(first.State!.Outcome == WildBattleOutcome.Won && first.State.Player.Pokemon.CurrentHp > 0, "first last-party Perish sets source Won");
    var second = Phase(first.State, first.Cursor);
    Check(second.State!.Outcome == WildBattleOutcome.Draw && second.State.Player.Pokemon.CurrentHp == 0, "later Perish still executes and yields source combined outcome");
    var last = Phase(second.State, second.Cursor);
    Check(last.Cursor!.Boundary == WildBattleSourceEndTurnBoundary.Complete, "terminal states still reach final cleanup");
});
Test("Perish countdown callbacks do not batch and Future silent countdowns do not invent pauses", () => {
    var s = Pair(); s = s with { Player = s.Player with { CalledVolatile = new() { PerishSongTimer = 2 } },
        Opponent = s.Opponent with { CalledVolatile = new() { PerishSongTimer = 3 } },
        CalledDelayed = new() { PlayerFutureAttack = new(3, Id("FUTURE_SIGHT"), WildBattleSide.Opponent, 20) } };
    var normal = Phase(Party(s)); var first = Phase(normal.State!, normal.Cursor);
    Check(first.Cursor!.Stage == WildBattleSourceEndTurnStage.PerishSong && first.Cursor.NextBattler == 1, "silent future continues to first actual Perish callback");
    Check(first.State!.CalledDelayed!.PlayerFutureAttack!.TurnsRemaining == 2 && first.State.Player.CalledVolatile!.PerishSongTimer == 2 && first.State.Opponent.CalledVolatile!.PerishSongTimer == 2, "only first Perish decremented");
    var second = Phase(first.State, first.Cursor);
    Check(second.State!.CalledDelayed!.PlayerFutureAttack!.TurnsRemaining == 2 && second.State.Player.CalledVolatile!.PerishSongTimer == 1, "resume neither replays future nor first Perish");
});
Test("Shed Skin cures poison before toxic damage and nightmare before its slot", () => {
    foreach (uint status in new[] { WildBattleSourceFieldEffects.ToxicPoison, 3u })
    {
        var s = Pair(player: "METAPOD", seed: 0); s = s with { Player = s.Player with { Status = status, Nightmare = status == 3 }, CalledField = new() };
        var r = Phase(Party(s));
        Check(r.State!.Player.Status == 0 && !r.State.Player.Nightmare && !r.Battle.Events.Any(e => e is WildBattleStatusDamageEvent or WildBattleTowerHpEvent), "ability precedes poison/nightmare");
        Check(r.Battle.Trace.Single().Raw == 0 && r.Battle.Trace.Single().Purpose.EndsWith("shed-skin"), "source Random percent3 call position");
    }
});
Test("Chlorophyll and chosen move priority participate in fixed source residual order", () => {
    var s = Pair(player: "ODDISH", opponent: "RATTATA"); s = s with { CalledField = new() { SunTurnsRemaining = 2 } };
    var r = Phase(Party(s));
    Check(r.Cursor!.First == (s.Player.Pokemon.Stats.Speed * 2 > s.Opponent.Pokemon.Stats.Speed ? WildBattleSide.Player : WildBattleSide.Opponent), "Chlorophyll before sun decrement");
    s = Pair(); s = s with { Player = s.Player with { Pokemon = Moves(s.Player.Pokemon, "QUICK_ATTACK") },
        ForestPlayerMoveSlot = 0, ForestOpponentMoveSlot = 0 };
    r = Phase(Party(s)); Check(r.Cursor!.First == WildBattleSide.Player, "GetWhoStrikesFirst false retains chosen priority");
    s = Pair(player: "CLEFAIRY", opponent: "CLEFAIRY", seed: 1);
    r = Phase(Party(s));
    Check(r.Battle.Trace.Length == 1 && r.Battle.Trace[0].Purpose == "wild-battle.endturn.speed-tie", "one tie draw only");
    var last = Phase(r.State!, r.Cursor); Check(last.Battle.Trace.IsEmpty, "fixed order reused across cursor resume");
});
Test("shared action comparator has exact Sun badge paralysis tie and Actions event semantics", () => {
    foreach (bool sun in new[] { false, true })
    {
        var s = Pair(player: "ODDISH"); s = Party(s with { CalledField = new() { SunTurnsRemaining = sun ? 1 : 0 } });
        var action = ActionOrder(s); var end = Phase(s);
        Check(s.Player.Pokemon.Stats.Speed == 36 && s.Opponent.Pokemon.Stats.Speed == 66, "source fixture speed36 and66");
        Check(action.First == (sun ? WildBattleSide.Player : WildBattleSide.Opponent) && end.Cursor!.First == action.First, "sun doubles36 before timer1 expires");
        Check(action.Events.OfType<WildBattleOrderEvent>().Single().Phase == WildBattleOrderPhase.Actions && action.Trace.Length == 0, "Actions event without invented RNG");
    }
    foreach (bool playerParalyzed in new[] { false, true })
    foreach (bool opponentParalyzed in new[] { false, true })
    {
        var s = Pair(opponent: "CLEFAIRY"); s = Party(s with { Player = s.Player with { SpeedBadgeBoost = true, Status = playerParalyzed ? WildBattleStatus.Paralysis : 0 },
            Opponent = s.Opponent with { Status = opponentParalyzed ? WildBattleStatus.Paralysis : 0 } });
        var action = ActionOrder(s);
        // Source: floor(40*110/100)=44, then paralysis44/4=11; opponent40/4=10.
        Check(s.Player.Pokemon.Stats.Speed == 40 && s.Opponent.Pokemon.Stats.Speed == 40, "fixed source badge fixture speed40");
        Check(action.First == (playerParalyzed && !opponentParalyzed ? WildBattleSide.Opponent : WildBattleSide.Player) && action.Trace.Length == 0,
            "badge before paralysis integer division");
    }
    foreach (var row in new[] { (Seed: 0u, Raw: (ushort)0, First: WildBattleSide.Player), (Seed: 3u, Raw: (ushort)50515, First: WildBattleSide.Opponent) })
    {
        var action = ActionOrder(Party(Pair(opponent: "CLEFAIRY", seed: row.Seed)));
        Check(action.First == row.First && action.Trace.Single().Raw == row.Raw && action.Trace.Single().Purpose == "wild-battle.actions.speed-tie", "source tie draw and phase purpose");
        Check(action.Events.OfType<WildBattleOrderEvent>().Single().Phase == WildBattleOrderPhase.Actions, "action phase event remains distinct");
    }
});
Test("real party exhaustion stops ordinary residual damage and never invents reserves", () => {
    var s = Pair(); s = s with { Player = s.Player with { Pokemon = s.Player.Pokemon with { CurrentHp = 1 } },
        Opponent = s.Opponent with { Pokemon = s.Opponent.Pokemon with { CurrentHp = 1 } }, SandstormTurnsRemaining = 2 };
    var r = Phase(Party(s));
    Check(r.State!.Outcome == WildBattleOutcome.Won && r.State.Player.Pokemon.CurrentHp == 1, "faster last opponent faints and source checkteamslost ends weather loop");
    Check(r.Battle.Events.OfType<WildBattleWeatherDamageEvent>().Count() == 1 && r.State.CalledParty!.PendingSwitch is null, "no active-only draw or synthetic replacement");
});
Test("Liquid Ooze double faint exposes both actual replacements before delayed phases", () => {
    var s = Pair(player: "TENTACOOL", opponent: "CLEFAIRY");
    var ooze = Enumerable.Range(0, 4).Select(i => Mon("TENTACOOL", pid: (uint)i)).First(m => m.AbilityId == "ABILITY_LIQUID_OOZE");
    s = s with { Player = s.Player with { Pokemon = Moves(ooze, "METRONOME") with { CurrentHp = 1 }, LeechSeedSource = WildBattleSide.Opponent },
        Opponent = s.Opponent with { Pokemon = s.Opponent.Pokemon with { CurrentHp = 1 } },
        CalledDelayed = new() { PlayerFutureAttack = new(2, Id("FUTURE_SIGHT"), WildBattleSide.Opponent, 10) } };
    var normal = Phase(Party(s, [Member()], [Member("RATTATA")]));
    Check(normal.Battle.Events.OfType<WildBattleFaintedEvent>().Count() == 2 && normal.State!.Outcome == WildBattleOutcome.Ongoing, "two faint events available for real EXP before choices");
    Check(normal.State!.CalledParty!.PendingSwitch?.Side == WildBattleSide.Player, "first actual replacement requested");
    var switched = Replace(normal.State, WildBattleSide.Player);
    Check(switched.CalledParty!.PendingSwitch?.Side == WildBattleSide.Opponent, "other actual replacement remains pending");
    Atomic(switched, normal.Cursor, "both replacements required before delayed phases");
    switched = Replace(switched, WildBattleSide.Opponent);
    var last = Phase(switched, normal.Cursor);
    Check(last.Cursor!.Boundary == WildBattleSourceEndTurnBoundary.Complete && last.State!.CalledDelayed!.PlayerFutureAttack!.TurnsRemaining == 1, "delayed timer only starts after both choices");
});
Test("Spikes replacement chain resumes one saved end-turn cursor without prior residual replay", () => {
    var s = Pair(); s = s with { Player = s.Player with { Pokemon = s.Player.Pokemon with { CurrentHp = 1 }, Status = WildBattleStatus.Poison },
        CalledDelayed = new() { PlayerFutureAttack = new(1, Id("FUTURE_SIGHT"), WildBattleSide.Opponent, 10) } };
    var doomed = Member(); doomed = doomed with { Pokemon = doomed.Pokemon with { CurrentHp = 1 } };
    s = Party(s, [doomed, Member(pid: 2)]); s = s with { CalledParty = s.CalledParty! with { PlayerSpikes = 3 } };
    var normal = Phase(s); var switched = Replace(normal.State!, WildBattleSide.Player);
    Check(switched.Player.Pokemon.CurrentHp == 0 && switched.CalledParty!.PendingSwitch?.LegalPartyIndices.SequenceEqual(new[] { 2 }) == true, "Spikes death requests actual surviving third member");
    Atomic(switched, normal.Cursor, "Spikes chain must finish");
    switched = Replace(switched, WildBattleSide.Player, 2);
    var future = Phase(switched, normal.Cursor);
    Check(future.State!.CalledParty!.PlayerActiveIndex == 2 && future.State.Player.Pokemon.CurrentHp == switched.Player.Pokemon.CurrentHp - future.Battle.Events.OfType<WildBattleHpChangedEvent>().Single().Damage, "only due Future damages final replacement");
    Check(future.State.CalledDelayed!.PlayerFutureAttack is null && !future.Battle.Events.OfType<WildBattleStatusDamageEvent>().Any(), "original poison never reapplies");
});
Test("physical special damage registers survive residual callbacks and clear only at final cleanup", () => {
    var s = Pair(); s = s with {
        Player = s.Player with { CalledDamage = new() { Turn = 0, PhysicalDamage = 7, PhysicalSource = WildBattleSide.Opponent,
            SpecialDamage = 11, SpecialSource = WildBattleSide.Opponent }, CalledVolatile = new() { MagicCoat = true } },
        Opponent = s.Opponent with { CalledDamage = new() { Turn = 0, PhysicalDamage = 13, PhysicalSource = WildBattleSide.Player },
            CalledVolatile = new() { PerishSongTimer = 2, MagicCoat = true } },
        CalledDelayed = new() { PlayerFutureAttack = new(1, Id("FUTURE_SIGHT"), WildBattleSide.Opponent, 10) } };
    var normal = Phase(Party(s));
    Check(normal.State!.Player.CalledDamage == s.Player.CalledDamage && normal.State.Opponent.CalledDamage == s.Opponent.CalledDamage,
        "normal residual boundary preserves actual damage registers");
    var future = Phase(normal.State, normal.Cursor);
    int actual = future.Battle.Events.OfType<WildBattleHpChangedEvent>().Single().Damage;
    Check(future.State!.Player.CalledDamage!.PhysicalDamage == actual && future.State.Player.CalledDamage.PhysicalSource == WildBattleSide.Opponent &&
        future.State.Player.CalledDamage.SpecialDamage == 11, "late Future updates Normal physical ledger and preserves special");
    Check(future.State.Player.CalledVolatile!.MagicCoat && future.State.Opponent.CalledVolatile!.MagicCoat, "no early final cleanup");
    var perish = Phase(future.State, future.Cursor);
    Check(perish.State!.Player.CalledDamage!.PhysicalDamage == actual && perish.State.Opponent.CalledDamage!.PhysicalDamage == 13,
        "Perish callback boundary still preserves damage lifetime");
    var complete = Phase(perish.State, perish.Cursor);
    Check(complete.Cursor!.Boundary == WildBattleSourceEndTurnBoundary.Complete && complete.State!.Player.CalledDamage is null && complete.State.Opponent.CalledDamage is null,
        "both ledgers reset exactly at actual final cleanup");
    Check(complete.State!.Player.CalledVolatile?.MagicCoat != true && complete.State.Opponent.CalledVolatile?.MagicCoat != true, "Magic Coat final cleanup");
});
Test("invalid phase cursor state and overflow reject with exact state cursor RNG event rollback", () => {
    var valid = Party(Pair()); Atomic(valid with { CalledParty = null }, null, "real parties mandatory");
    var phase = Phase(valid);
    Atomic(phase.State!, phase.Cursor! with { Turn = 1 }, "wrong turn");
    Atomic(phase.State!, phase.Cursor! with { RulesVersion = "wrong" }, "wrong cursor version");
    Atomic(phase.State!, phase.Cursor! with { NextBattler = 3 }, "out of range cursor");
    Atomic(valid with { Revision = long.MaxValue }, null, "revision overflow after effect execution");
    Atomic(Party(Pair(opponent: "CLEFAIRY", seed: 1)) with { EventSequence = long.MaxValue }, null, "event overflow after actual tie RNG");
    var historical = WildBattleRules.ResolveMove(catalog, valid, 0);
    Check(!historical.Succeeded && ReferenceEquals(historical.State, valid) && historical.Events.IsEmpty && historical.Trace.IsEmpty, "ordinary historical v1 still rejects extension");
});
Console.WriteLine(JsonSerializer.Serialize(new { suite = "source-end-turn-phases", groups, checks, failures, runtime = Environment.Version.ToString(), sourceCommit = MetronomeSourceMoveDefinition.SourceCommit, completeTurn = false, application = false }));
if (groups < 21 || checks == 0 || failures > 0) Environment.Exit(1);

using var frozenParity = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "baseline-3a0344d.json")));
var expectedParity = frozenParity.RootElement.GetProperty("phaseResultHashes").EnumerateArray().Select(v => v.GetString()).ToArray();
if (expectedParity.Length != 64 || !parity.SequenceEqual(expectedParity))
    throw new InvalidDataException("Serialized singles state/event/RNG/cursor results differ from independently built fixed 3a0344d baseline.");
Console.WriteLine("EXACT_SINGLE_PHASE_HASHES " + JsonSerializer.Serialize(parity));
