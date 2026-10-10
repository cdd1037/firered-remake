using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.Battle;
using Game.Domain.World;

var tests = new List<(string Id, Action Run)>();
void Test(string id, Action action) => tests.Add((id, action));
void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}."); }
void True(bool value, string reason = "Condition not true") { if (!value) throw new Exception(reason); }
void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}."); }
string Json<T>(T x) => JsonSerializer.Serialize(x);
string Hash<T>(T x) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Json(x))));

var fixtureMove = new MoveDefinition(new("fixture/tackle"), ElementType.Normal, 40, 100, 35);
var catalog = new Catalog(fixtureMove);
CombatantSnapshot Combatant(string id, BattleSide side, int speed = 20, int hp = 100) => new(new(id), new("fixture/species"), side, 5,
    new(100, 50, 50, 30, 30, speed), ElementType.Normal, null, hp, [new(fixtureMove.Id, 35, 35)], new("fixture/trainer"));
BattleSnapshot Battle(uint seed = 0) => BattleSnapshot.Create(new("fixture/battle"), "fixture-content-v1",
    Combatant("player", BattleSide.Player), Combatant("opponent", BattleSide.Opponent, 10), seed);
UseMove Player(BattleSnapshot s, string id = "cmd/player") => new(new(id), s.Revision, new("player"), 0, new("opponent"));
UseMove Opponent(BattleSnapshot s, string id = "cmd/opponent") => new(new(id), s.Revision, new("opponent"), 0, new("player"));
BattleTransition Turn(BattleSnapshot s, IBattleContent? content = null, string prefix = "cmd")
{
    var a = BattleRules.Apply(s, Player(s, prefix + "/player"), content ?? catalog);
    Equal(TransitionStatus.Accepted, a.Status);
    return BattleRules.Apply(a.State, Opponent(a.State, prefix + "/opponent"), content ?? catalog);
}
void Unchanged(BattleSnapshot before, BattleTransition after, TransitionStatus status, string code)
{
    Equal(status, after.Status); True(ReferenceEquals(before, after.State), "Failure must return the original snapshot reference.");
    Equal(code, after.Diagnostic!.Code); Equal(0, after.Events.Length); Equal(0, after.RandomTrace.Length);
}
DamageInput Damage(ElementType type = ElementType.Normal) => new(50, 40, type, 100, 100, type, null, ElementType.Normal, null, false, 100);
GridMap Map() => new(new("fixture/map"), 3, 3, Enumerable.Repeat(new TileCell(false), 9).ToImmutableArray().SetItem(5, new(true)));
WorldSnapshot World() => new(RuleVersions.GridWorldV0, 0, 0, new(new("fixture/map"), 1, 1, 0, Direction.South), new(99));
MoveIntent Walk(WorldSnapshot s, Direction d) => new(new("cmd/walk"), s.Revision, d);

Test("ids.stable-json-roundtrip", () => { var id = new SpeciesId("species/bulbasaur"); Equal(id, JsonSerializer.Deserialize<SpeciesId>(Json(id))); Equal("species/bulbasaur", id.ToString()); });
Test("ids.reject-invalid", () => { foreach (var v in new[] { "", " ", "hello world", "皮卡丘", "x?y" }) Throws<ArgumentException>(() => new MapId(v)); True(!StableId.IsValid(default(MapId).Value)); });
Test("rng.pinned-zero-seed-vectors", () =>
{
    string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "rng-v1.json");
    using var doc = JsonDocument.Parse(File.ReadAllText(path)); var root = doc.RootElement;
    Equal(RuleVersions.SourceCommit, root.GetProperty("sourceCommit").GetString());
    Equal(Gen3Rng.AlgorithmId, root.GetProperty("algorithmId").GetString());
    var draws = root.GetProperty("draws"); True(draws.GetArrayLength() >= 8, "Fixture must not be empty/truncated.");
    var s = new RngState(root.GetProperty("seed").GetUInt32());
    foreach (var row in draws.EnumerateArray())
    {
        var draw = Gen3Rng.Next(s, "fixture", 100);
        Equal(row.GetProperty("seed").GetUInt32(), draw.State.Seed); Equal(row.GetProperty("cursor").GetUInt64(), draw.State.Cursor);
        Equal(row.GetProperty("raw").GetUInt16(), draw.Value); Equal(s.Seed, draw.Trace.Before); Equal(draw.Value % 100, draw.Trace.Result); s = draw.State;
    }
});
Test("rng.unsigned-overflow", () => { var d = Gen3Rng.Next(new(uint.MaxValue), "overflow"); Equal(3191476742u, d.State.Seed); Equal((ushort)48698, d.Value); });
Test("rng.invalid-bound-and-exhaustion", () => { Throws<ArgumentOutOfRangeException>(() => Gen3Rng.Next(new(0), "bound", 0)); Throws<ArgumentOutOfRangeException>(() => Gen3Rng.Next(new(0), "bound", 65537)); Throws<OverflowException>(() => Gen3Rng.Next(new(0, ulong.MaxValue), "overflow")); });
Test("rng.bound-one-and-purpose", () => { var d = Gen3Rng.Next(new(123), "encounter:test", 1); Equal(0, d.Trace.Result); Equal("encounter:test", d.Trace.Purpose); Equal(1ul, d.Trace.Cursor); });
Test("stats.hp-boundaries", () => { Equal(11, StatRules.CalculateHp(1, 0, 0, 1)); Equal(714, StatRules.CalculateHp(255, 31, 255, 100)); Equal(142, StatRules.CalculateHp(35, 31, 252, 50)); Equal(19, StatRules.CalculateHp(45, 0, 0, 5)); });
Test("stats.shedinja-hp", () => Equal(1, StatRules.CalculateHp(1, 31, 255, 100, true)));
Test("stats.ev-integer-floor", () => { Equal(StatRules.CalculateHp(35, 0, 0, 100), StatRules.CalculateHp(35, 0, 3, 100)); Equal(181, StatRules.CalculateHp(35, 0, 4, 100)); });
Test("stats.nature-up-down-neutral", () => { Equal(167, StatRules.CalculateOther(100, 31, 252, 50, Nature.Adamant, Stat.Attack)); Equal(136, StatRules.CalculateOther(100, 31, 252, 50, Nature.Adamant, Stat.SpecialAttack)); Equal(152, StatRules.CalculateOther(100, 31, 252, 50, Nature.Adamant, Stat.Speed)); });
Test("stats.all-neutral-natures", () => { foreach (var n in new[] { Nature.Hardy, Nature.Docile, Nature.Serious, Nature.Bashful, Nature.Quirky }) foreach (var stat in Enum.GetValues<Stat>()) Equal(101, StatRules.ApplyNature(101, n, stat)); });
Test("stats.nature-u16-original-wrap", () => { Equal(654, StatRules.ApplyNature(595, Nature.Lonely, Stat.Attack)); Equal(0, StatRules.ApplyNature(596, Nature.Lonely, Stat.Attack)); Equal(0, StatRules.ApplyNature(729, Nature.Lonely, Stat.Defense)); });
Test("stats.invalid-inputs", () => { Throws<ArgumentOutOfRangeException>(() => StatRules.CalculateHp(45, 32, 0, 5)); Throws<ArgumentOutOfRangeException>(() => StatRules.CalculateHp(45, 0, 256, 5)); Throws<ArgumentOutOfRangeException>(() => StatRules.CalculateHp(45, 0, 0, 0)); Throws<ArgumentOutOfRangeException>(() => StatRules.ApplyNature(5, (Nature)25, Stat.Attack)); });
Test("types.gen3-categories", () => { Equal(9, Enum.GetValues<ElementType>().Count(t => TypeRules.Category(t) == DamageCategory.Physical)); Equal(8, Enum.GetValues<ElementType>().Count(t => TypeRules.Category(t) == DamageCategory.Special)); Equal(DamageCategory.Physical, TypeRules.Category(ElementType.Ghost)); Equal(DamageCategory.Special, TypeRules.Category(ElementType.Dark)); Equal(DamageCategory.Unsupported, TypeRules.Category(ElementType.Mystery)); });
Test("types.gen3-steel-resistances", () => { Equal(5, TypeRules.EffectivenessTenths(ElementType.Dark, ElementType.Steel)); Equal(5, TypeRules.EffectivenessTenths(ElementType.Ghost, ElementType.Steel)); Equal(0, TypeRules.EffectivenessTenths(ElementType.Normal, ElementType.Ghost)); });
Test("types.no-fairy-fallback", () => Throws<ArgumentOutOfRangeException>(() => TypeRules.Category((ElementType)18)));
Test("damage.basic-integer-stab", () => { var d = DamageRules.Calculate(Damage()); Equal(28, d.Damage); Equal(17, d.Steps.Single(x => x.Operation == "divide-50").Value); Equal(19, d.Steps.Single(x => x.Operation == "plus-two").Value); });
Test("damage.critical-before-stab", () => Equal(48, DamageRules.Calculate(Damage() with { Critical = true, RandomPercent = 85 }).Damage));
Test("damage.physical-special-low-end", () => { var d = Damage() with { Level = 1, Power = 1, Attack = 1, Defense = 999, AttackerType1 = ElementType.Water }; Equal(3, DamageRules.Calculate(d).Damage); Equal(2, DamageRules.Calculate(d with { MoveType = ElementType.Fire }).Damage); });
Test("damage.source-type-order-rounding", () =>
{
    var d = new DamageInput(5, 40, ElementType.Fire, 10, 30, ElementType.Normal, null, ElementType.Grass, ElementType.Water, false, 100);
    Equal(2, DamageRules.Calculate(d).Damage); Equal(2, DamageRules.Calculate(d with { DefenderType1 = ElementType.Water, DefenderType2 = ElementType.Grass }).Damage);
    Equal("type-Water", DamageRules.Calculate(d).Steps.First(x => x.Operation.StartsWith("type-", StringComparison.Ordinal)).Operation);
});
Test("damage.duplicate-type-only-once", () => { var d = Damage(ElementType.Fire) with { DefenderType1 = ElementType.Water, DefenderType2 = ElementType.Water }; Equal(14, DamageRules.Calculate(d).Damage); });
Test("damage.immunity-stays-zero", () => { var d = Damage(ElementType.Electric) with { DefenderType1 = ElementType.Ground, DefenderType2 = ElementType.Flying }; Equal(0, DamageRules.Calculate(d).Damage); });
Test("damage.unimplemented-modifiers-fail", () => { foreach (var m in new[] { UnsupportedBattleMechanics.Abilities, UnsupportedBattleMechanics.Badges, UnsupportedBattleMechanics.Weather, UnsupportedBattleMechanics.HeldItems }) Throws<NotSupportedException>(() => DamageRules.Calculate(Damage() with { ActiveMechanics = m })); });
Test("damage.invalid-and-mystery-fail", () => { Throws<NotSupportedException>(() => DamageRules.Calculate(Damage(ElementType.Mystery))); Throws<ArgumentOutOfRangeException>(() => DamageRules.Calculate(Damage() with { Power = 0 })); Throws<ArgumentOutOfRangeException>(() => DamageRules.Calculate(Damage() with { Defense = 0 })); });
Test("battle.queue-without-rng-pp-hp", () => { var s = Battle(); var t = BattleRules.Apply(s, Player(s), catalog); Equal(TransitionStatus.Accepted, t.Status); Equal(1L, t.State.Revision); Equal(1, t.State.Turn); Equal(s.Rng, t.State.Rng); Equal(1, t.State.PendingMoves.Length); Equal(35, t.State.Battlers[0].Moves[0].Pp); Equal(100, t.State.Battlers[0].Hp); Equal(1, t.Events.Length); Equal(0, t.RandomTrace.Length); });
Test("battle.controlled-turn-golden", () => { var t = Turn(Battle()); Equal(TransitionStatus.Accepted, t.Status); Equal(2L, t.State.Revision); Equal(2, t.State.Turn); Equal(8L, t.State.EventSequence); Equal(new RngState(3805062638, 6), t.State.Rng); Equal(94, t.State.Battlers[0].Hp); Equal(94, t.State.Battlers[1].Hp); Equal(34, t.State.Battlers[0].Moves[0].Pp); Equal(6, t.RandomTrace.Length); Equal("MoveChosenEvent,TurnOrderEvent,MoveUsedEvent,HpChangedEvent,MoveUsedEvent,HpChangedEvent,TurnEndedEvent", string.Join(',', t.Events.Select(x => x.GetType().Name))); });
Test("battle.event-causes-sequence", () => { var t = Turn(Battle()); Equal(2L, t.Events[0].Metadata.Sequence); Equal(8L, t.Events[^1].Metadata.Sequence); Equal(new CommandId("cmd/player"), t.Events.OfType<HpChangedEvent>().First().Metadata.CausedByCommandId); True(t.Events.All(x => x.Metadata.BattleId == t.State.BattleId)); });
Test("battle.stale-revision-rejected", () => { var s = Battle(); Unchanged(s, BattleRules.Apply(s, Player(s) with { ExpectedRevision = 9 }, catalog), TransitionStatus.Rejected, "stale-revision"); });
Test("battle.no-pp-rejected", () => { var s = Battle(); s = s with { Battlers = s.Battlers.SetItem(0, s.Battlers[0] with { Moves = [new(fixtureMove.Id, 0, 35)] }) }; Unchanged(s, BattleRules.Apply(s, Player(s), catalog), TransitionStatus.Rejected, "no-pp"); });
Test("battle.invalid-target-rejected", () => { var s = Battle(); Unchanged(s, BattleRules.Apply(s, Player(s) with { TargetId = new("player") }, catalog), TransitionStatus.Rejected, "invalid-target"); });
Test("battle.unknown-actor-rejected", () => { var s = Battle(); Unchanged(s, BattleRules.Apply(s, Player(s) with { ActorId = new("stranger") }, catalog), TransitionStatus.Rejected, "unknown-actor"); });
Test("battle.invalid-slot-rejected", () => { var s = Battle(); Unchanged(s, BattleRules.Apply(s, Player(s) with { MoveSlot = -1 }, catalog), TransitionStatus.Rejected, "invalid-move-slot"); });
Test("battle.already-chosen-rejected", () => { var s = Battle(); s = BattleRules.Apply(s, Player(s), catalog).State; Unchanged(s, BattleRules.Apply(s, Player(s, "cmd/other"), catalog), TransitionStatus.Rejected, "already-selected"); });
Test("battle.duplicate-pending-id-rejected", () => { var s = Battle(); s = BattleRules.Apply(s, Player(s), catalog).State; Unchanged(s, BattleRules.Apply(s, Opponent(s, "cmd/player"), catalog), TransitionStatus.Rejected, "duplicate-command-id"); });
Test("battle.missing-content-fault", () => { var s = Battle(); Unchanged(s, BattleRules.Apply(s, Player(s), new Catalog()), TransitionStatus.Fault, "missing-move-content"); });
Test("battle.unsupported-effect-fault", () => { var s = Battle(); Unchanged(s, BattleRules.Apply(s, Player(s), new Catalog(fixtureMove with { Effect = MoveEffect.Unsupported })), TransitionStatus.Fault, "unsupported-move"); });
Test("battle.double-mode-fault", () => { var s = Battle() with { Mode = BattleMode.Doubles }; Unchanged(s, BattleRules.Apply(s, Player(s), catalog), TransitionStatus.Fault, "unsupported-rules-profile"); });
Test("battle.badge-profile-fault", () => { var s = Battle() with { ActiveMechanics = UnsupportedBattleMechanics.Badges }; Unchanged(s, BattleRules.Apply(s, Player(s), catalog), TransitionStatus.Fault, "unsupported-rules-profile"); });
Test("battle.invalid-hp-fault", () => { var s = Battle(); s = s with { Battlers = s.Battlers.SetItem(0, s.Battlers[0] with { Hp = 101 }) }; Unchanged(s, BattleRules.Apply(s, Player(s), catalog), TransitionStatus.Fault, "invalid-combatant"); });
Test("battle.corrupt-pending-fault", () => { var s = Battle(); s = s with { PendingMoves = [new(new("cmd/tamper"), new("player"), 3, new("opponent"), fixtureMove.Id)] }; Unchanged(s, BattleRules.Apply(s, Opponent(s), catalog), TransitionStatus.Fault, "invalid-pending-move"); });
Test("battle.fast-player-first", () => Equal(new CombatantId("player"), Turn(Battle()).Events.OfType<TurnOrderEvent>().Single().Actors[0]));
Test("battle.priority-before-speed", () => { var s = Battle(); var quick = fixtureMove with { Id = new("fixture/quick"), Priority = 1 }; s = s with { Battlers = s.Battlers.SetItem(1, s.Battlers[1] with { Moves = [new(quick.Id, 35, 35)] }) }; Equal(new CombatantId("opponent"), Turn(s, new Catalog(fixtureMove, quick)).Events.OfType<TurnOrderEvent>().Single().Actors[0]); });
Test("battle.speed-tie-draw", () => { var s = Battle(); s = s with { Battlers = s.Battlers.SetItem(1, s.Battlers[1] with { Stats = s.Battlers[1].Stats with { Speed = 20 } }) }; var t = Turn(s); Equal("turn-order:speed-tie", t.RandomTrace[0].Purpose); Equal(new CombatantId("player"), t.Events.OfType<TurnOrderEvent>().Single().Actors[0]); Equal(7, t.RandomTrace.Length); });
Test("battle.zero-accuracy-not-assumed-always-hit", () => { var s = Battle(); Unchanged(s, BattleRules.Apply(s, Player(s), new Catalog(fixtureMove with { Accuracy = 0 })), TransitionStatus.Fault, "unsupported-move"); });
Test("battle.miss-spends-pp-only", () => { var t = Turn(Battle(1), new Catalog(fixtureMove with { Accuracy = 1 })); True(t.Events.OfType<MoveMissedEvent>().Any()); Equal(34, t.State.Battlers[0].Moves[0].Pp); True(t.RandomTrace.Length < 6); });
Test("battle.ko-stops-opponent-rng-pp", () => { var s = Battle(); s = s with { Battlers = s.Battlers.SetItem(1, s.Battlers[1] with { Hp = 1 }) }; var t = Turn(s); Equal(BattleOutcome.PlayerWon, t.State.Outcome); Equal(BattlePhase.Ended, t.State.Phase); Equal(0, t.State.Battlers[1].Hp); Equal(35, t.State.Battlers[1].Moves[0].Pp); Equal(3, t.RandomTrace.Length); Equal(1, t.Events.OfType<FaintedEvent>().Count()); Unchanged(t.State, BattleRules.Apply(t.State, Player(t.State, "cmd/late"), catalog), TransitionStatus.Rejected, "battle-ended"); });
Test("battle.opponent-victory", () => { var s = Battle(); s = s with { Battlers = s.Battlers.SetItem(0, s.Battlers[0] with { Hp = 1, Stats = s.Battlers[0].Stats with { Speed = 1 } }) }; Equal(BattleOutcome.OpponentWon, Turn(s).State.Outcome); });
Test("battle.input-snapshot-immutable", () => { var s = Battle(); string before = Json(s); Turn(s); Equal(before, Json(s)); });
Test("battle.json-snapshot-roundtrip", () => { var s = Battle(); s = BattleRules.Apply(s, Player(s), catalog).State; var copy = JsonSerializer.Deserialize<BattleSnapshot>(Json(s))!; Equal(Json(s), Json(copy)); Equal(Json(BattleRules.Apply(s, Opponent(s), catalog)), Json(BattleRules.Apply(copy, Opponent(copy), catalog))); });
Test("battle.deterministic-command-replay", () => { var initial = Battle(0x12345678); var a = initial; var b = initial; for (int i = 0; i < 5 && a.Outcome == BattleOutcome.Ongoing; i++) { var x = Turn(a, prefix: $"turn/{i}"); var y = Turn(b, prefix: $"turn/{i}"); Equal(Hash(x), Hash(y)); a = x.State; b = y.State; } Equal(Hash(a), Hash(b)); });
Test("battle.selection-order-semantic-invariance", () => { var s = Battle(); var normal = Turn(s); var queued = BattleRules.Apply(s, Opponent(s), catalog); var reverse = BattleRules.Apply(queued.State, Player(queued.State), catalog); Equal(Json(normal.State), Json(reverse.State)); Equal(Json(normal.RandomTrace), Json(reverse.RandomTrace)); });
Test("battle.rejected-input-does-not-perturb-replay", () => { var s = Battle(); var rejected = BattleRules.Apply(s, Player(s) with { MoveSlot = 8 }, catalog); Equal(Json(Turn(s)), Json(Turn(rejected.State))); });
Test("world.orthogonal-move", () => { var s = World(); var t = WorldRules.Move(s, Walk(s, Direction.North), Map()); Equal(TransitionStatus.Accepted, t.Status); Equal(1, t.State.Player.X); Equal(0, t.State.Player.Y); Equal(Direction.North, t.State.Player.Facing); Equal(1L, t.State.StepCount); Equal(s.Rng, t.State.Rng); Equal(WorldEventKind.Moved, t.Events[0].Kind); });
Test("world.signpost-blocks-but-faces", () => { var s=World();var m=Map();var t=WorldRules.Move(s,Walk(s,Direction.North),new GridMap(m.Id,3,3,m.Cells.SetItem(1,new(true,0,TileBehavior.Sign))));Equal(TransitionStatus.Accepted,t.Status);Equal(Direction.North,t.State.Player.Facing);Equal(0L,t.State.StepCount);Equal(s.Rng,t.State.Rng);Equal("collision",t.Events[0].Reason); });
Test("world.blocked-turns-facing-only", () => { var s = World(); var t = WorldRules.Move(s, Walk(s, Direction.East), Map()); Equal(TransitionStatus.Accepted, t.Status); Equal(1, t.State.Player.X); Equal(1, t.State.Player.Y); Equal(Direction.East, t.State.Player.Facing); Equal(0L, t.State.StepCount); Equal(1L, t.State.Revision); Equal(s.Rng, t.State.Rng); Equal("collision", t.Events[0].Reason); });
Test("world.boundary-blocked", () => { var s = World(); s = s with { Player = s.Player with { Y = 0 } }; var t = WorldRules.Move(s, Walk(s, Direction.North), Map()); Equal("map-boundary", t.Events[0].Reason); Equal(0, t.State.Player.Y); });
Test("world.stale-rejected-unchanged", () => { var s = World(); var t = WorldRules.Move(s, Walk(s, Direction.North) with { ExpectedRevision = 9 }, Map()); Equal(TransitionStatus.Rejected, t.Status); True(ReferenceEquals(s, t.State)); Equal(0, t.Events.Length); });
Test("world.elevation-blocked", () => { var s = World(); var m = Map(); var t = WorldRules.Move(s, Walk(s, Direction.North), new GridMap(m.Id, 3, 3, m.Cells.SetItem(1, new(false, 1)))); Equal("elevation", t.Events[0].Reason); Equal(1, t.State.Player.Y); });
Test("world.unsupported-grass-fault-atomic", () => { var s = World(); var m = Map(); var t = WorldRules.Move(s, Walk(s, Direction.North), new GridMap(m.Id, 3, 3, m.Cells.SetItem(1, new(false, 0, TileBehavior.Grass)))); Equal(TransitionStatus.Fault, t.Status); True(ReferenceEquals(s, t.State)); Equal(0, t.Events.Length); });
Test("world.invalid-direction-rejected", () => { var s = World(); var t = WorldRules.Move(s, Walk(s, (Direction)99), Map()); Equal(TransitionStatus.Rejected, t.Status); True(ReferenceEquals(s, t.State)); });
Test("world.mismatched-map-fault", () => { var s = World(); s = s with { Player = s.Player with { MapId = new("other/map") } }; Equal(TransitionStatus.Fault, WorldRules.Move(s, Walk(s, Direction.North), Map()).Status); });
Test("world.invalid-grid-fails", () => Throws<ArgumentException>(() => new GridMap(new("map"), 2, 2, [new(false)])));
Test("world.deterministic-replay", () => { var a = World(); var b = World(); foreach (var direction in new[] { Direction.North, Direction.West, Direction.West, Direction.South, Direction.East }) { var x = WorldRules.Move(a, Walk(a, direction), Map()); var y = WorldRules.Move(b, Walk(b, direction), Map()); Equal(Json(x), Json(y)); a = x.State; b = y.State; } Equal(4L, a.StepCount); Equal(5L, a.Revision); Equal(a.Rng, World().Rng); });

Test("rng.algorithm-is-persisted-and-validated", () =>
{
    var copy = JsonSerializer.Deserialize<RngState>("{\"Seed\":0,\"Cursor\":0,\"AlgorithmId\":\"unknown\"}");
    Equal("unknown", copy.AlgorithmId); Throws<NotSupportedException>(() => Gen3Rng.Next(copy, "invalid"));
});
Test("rng.ten-thousand-draws-repeat", () =>
{
    var a = new RngState(0x76543210); var b = a;
    for (int i = 0; i < 10000; i++) { var x = Gen3Rng.Next(a, "stress", 37); var y = Gen3Rng.Next(b, "stress", 37); Equal(x, y); True(x.Trace.Result is >= 0 and < 37); a = x.State; b = y.State; }
    Equal(10000ul, a.Cursor);
});
Test("battle.bad-rng-algorithm-fault", () => { var s = Battle() with { Rng = new(0, 0, "unknown") }; Unchanged(s, BattleRules.Apply(s, Player(s), catalog), TransitionStatus.Fault, "invalid-battle-state"); });
Test("battle.second-selection-content-fault-atomic", () =>
{
    var s = Battle(); var missing = fixtureMove with { Id = new("fixture/missing-later") };
    s = s with { Battlers = s.Battlers.SetItem(0, s.Battlers[0] with { Moves = [new(missing.Id, 35, 35)] }) };
    s = BattleRules.Apply(s, Player(s), new Catalog(fixtureMove, missing)).State;
    Unchanged(s, BattleRules.Apply(s, Opponent(s), catalog), TransitionStatus.Fault, "missing-move-content");
});
Test("world.unsupported-current-tile-fault", () =>
{
    var s = World(); var m = Map(); m = new(m.Id, 3, 3, m.Cells.SetItem(4, new(false, 0, TileBehavior.Grass)));
    var t = WorldRules.Move(s, Walk(s, Direction.North), m); Equal(TransitionStatus.Fault, t.Status); True(ReferenceEquals(s, t.State));
});
Test("world.rng-algorithm-fault", () => { var s = World() with { Rng = new(0, 0, "unknown") }; Equal(TransitionStatus.Fault, WorldRules.Move(s, Walk(s, Direction.North), Map()).Status); });

Test("battle.independently-constructed-golden-snapshot", () =>
{
    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "battle-turn-v1.json")));
    var fixture = document.RootElement; Equal(RuleVersions.SourceCommit, fixture.GetProperty("sourceCommit").GetString());
    Equal(RuleVersions.SingleBattleV0, fixture.GetProperty("rulesVersion").GetString());
    var result = Turn(Battle()); var actual = JsonSerializer.SerializeToElement(result.State);
    byte[] Canonical(JsonElement value)
    {
        using var stream = new MemoryStream(); using (var writer = new Utf8JsonWriter(stream)) Write(writer, value); return stream.ToArray();
    }
    void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject(); foreach (var p in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); Write(writer, p.Value); } writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array) { writer.WriteStartArray(); foreach (var v in value.EnumerateArray()) Write(writer, v); writer.WriteEndArray(); }
        else value.WriteTo(writer);
    }
    string expectedHash = fixture.GetProperty("expectedCanonicalStateSha256").GetString()!;
    Equal(expectedHash, Convert.ToHexStringLower(SHA256.HashData(Canonical(fixture.GetProperty("expectedFinalSnapshot")))));
    Equal(expectedHash, Convert.ToHexStringLower(SHA256.HashData(Canonical(actual))));
    Equal(string.Join(',', fixture.GetProperty("rawRandomResults").EnumerateArray().Select(x => x.GetInt32())), string.Join(',', result.RandomTrace.Select(x => (int)x.Raw)));
});

Test("validation.valid-battle-and-world-pure", () => { var b = Battle(); var w = World(); string bh = Hash(b), wh = Hash(w); True(BattleRules.ValidateState(b, catalog) is null); True(WorldRules.ValidateState(w, Map()) is null); Equal(bh, Hash(b)); Equal(wh, Hash(w)); });
Test("validation.duplicate-combatant-id", () => { var b = Battle(); b = b with { Battlers = b.Battlers.SetItem(1, b.Battlers[1] with { Id = b.Battlers[0].Id }) }; Equal("invalid-roster", BattleRules.ValidateState(b, catalog)!.Code); });
Test("validation.two-player-sides", () => { var b = Battle(); b = b with { Battlers = b.Battlers.SetItem(1, b.Battlers[1] with { Side = BattleSide.Player }) }; Equal("invalid-roster", BattleRules.ValidateState(b, catalog)!.Code); });
Test("validation.outcome-hp-disagreement", () => { var b = Battle() with { Phase = BattlePhase.Ended, Outcome = BattleOutcome.PlayerWon }; Equal("invalid-battle-outcome", BattleRules.ValidateState(b, catalog)!.Code); });
Test("validation.unselected-move-reference", () => { var b = Battle(); b = b with { Battlers = b.Battlers.SetItem(0, b.Battlers[0] with { Moves = b.Battlers[0].Moves.Add(new(new("missing/unselected"), 10, 10)) }) }; Equal("missing-move-content", BattleRules.ValidateState(b, catalog)!.Code); });
Test("validation.valid-pending-recovery", () => { var b = Battle(); b = BattleRules.Apply(b, Player(b), catalog).State; True(BattleRules.ValidateState(b, catalog) is null); });
Test("validation.invalid-world-facing", () => { var w = World(); w = w with { Player = w.Player with { Facing = (Direction)99 } }; Equal("invalid-world-state", WorldRules.ValidateState(w, Map())!.Code); });
Test("validation.invalid-world-steps", () => { var w = World() with { StepCount = -1 }; Equal("invalid-world-state", WorldRules.ValidateState(w, Map())!.Code); });

string? filter = null;
if (args.Length == 2 && args[0] == "--filter") filter = args[1];
else if (args.Length != 0) { Console.WriteLine(Json(new { passed = false, error = "Usage: [--filter substring]" })); return 2; }
var selected = tests.Where(x => filter is null || x.Id.Contains(filter, StringComparison.Ordinal)).ToArray();
var results = new List<object>(); int failed = 0;
foreach (var test in selected)
{
    try { test.Run(); results.Add(new { id = test.Id, passed = true, error = (string?)null }); }
    catch (Exception error) { failed++; results.Add(new { id = test.Id, passed = false, error = error.GetType().Name + ": " + error.Message }); }
}
Console.WriteLine(JsonSerializer.Serialize(new
{
    suite = "Game.Domain.Tests", rulesVersion = RuleVersions.SingleBattleV0, sourceCommit = RuleVersions.SourceCommit,
    targetFramework = "net10.0", runtime = Environment.Version.ToString(), total = selected.Length,
    passed = selected.Length > 0 && failed == 0, failed, emptySuite = selected.Length == 0, results
}, new JsonSerializerOptions { WriteIndented = true }));
return selected.Length == 0 ? 2 : failed == 0 ? 0 : 1;

sealed class Catalog(params MoveDefinition[] moves) : IBattleContent
{
    public MoveDefinition? FindMove(MoveId id) => moves.FirstOrDefault(x => x.Id == id);
}
