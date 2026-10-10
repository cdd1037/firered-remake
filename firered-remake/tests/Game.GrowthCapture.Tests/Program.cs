using System.Text.Json;
using Game.Domain.Capture;
using Game.Domain.Growth;
using Game.Domain.Randomness;

var tests = new List<(string Id, Action Run)>();
int assertions = 0;
void Test(string id, Action run) => tests.Add((id, run));
void Equal<T>(T expected, T actual)
{
    assertions++;
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}; got {actual}.");
}
void True(bool value, string message) { assertions++; if (!value) throw new Exception(message); }
void Throws<T>(Action action) where T : Exception
{
    assertions++;
    try { action(); } catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}
JsonDocument Fixture(string name) => JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name)));
CaptureInput Input(int hp = 100, int rate = 45, CaptureBall ball = CaptureBall.Poke, CaptureStatus status = CaptureStatus.None)
    => new(100, hp, rate, ball, status);
void NoDraw(CaptureResult result, RngState before, CaptureOutcome outcome, string code)
{
    Equal(outcome, result.Outcome); Equal(code, result.Diagnostic); Equal(before, result.Rng);
    Equal(0, result.RandomTrace.Length); Equal(0, result.ChecksPassed);
    True(result.Calculation is null && result.Branch is null && result.AnimationCode is null, "Rejected/unsupported requests must not fabricate a resolved calculation.");
}

Test("growth.fixed-source-table-600-independent-values", () =>
{
    using var document = Fixture("growth-v1.json"); var root = document.RootElement;
    Equal("037335f4c725d7c9aecdac87066f2002b4bd7e14", root.GetProperty("sourceCommit").GetString());
    var curves = root.GetProperty("curves"); Equal(6, curves.GetArrayLength());
    var seen = new HashSet<int>();
    foreach (var curve in curves.EnumerateArray())
    {
        int id = curve.GetProperty("id").GetInt32(); True(seen.Add(id), "Duplicate growth fixture ID.");
        var entries = curve.GetProperty("experienceByLevel"); Equal(100, entries.GetArrayLength());
        int level = 1;
        foreach (var entry in entries.EnumerateArray()) Equal(entry.GetUInt32(), GrowthRules.ExperienceAtLevel((GrowthRate)id, level++));
    }
    True(seen.SetEquals(Enumerable.Range(0, 6)), "Every source curve is required.");
});
Test("growth.literal-level-one-and-small-medium-slow", () =>
{
    foreach (GrowthRate rate in Enum.GetValues<GrowthRate>()) Equal(1u, GrowthRules.ExperienceAtLevel(rate, 1));
    Equal(9u, GrowthRules.ExperienceAtLevel(GrowthRate.MediumSlow, 2));
    Equal(57u, GrowthRules.ExperienceAtLevel(GrowthRate.MediumSlow, 3));
    Equal(112u, GrowthRules.ExperienceAtLevel(GrowthRate.Fluctuating, 6)); // (floor(7/3) + 24) * 216 / 50
});
Test("growth.hand-checked-piecewise-edges", () =>
{
    (GrowthRate Rate, int Level, uint Exp)[] vectors =
    [
        (GrowthRate.Erratic, 50, 125000), (GrowthRate.Erratic, 51, 131324),
        (GrowthRate.Erratic, 68, 257834), (GrowthRate.Erratic, 69, 267406),
        (GrowthRate.Erratic, 98, 583539), (GrowthRate.Erratic, 99, 591882),
        (GrowthRate.Fluctuating, 15, 1957), (GrowthRate.Fluctuating, 16, 2457),
        (GrowthRate.Fluctuating, 36, 46656), (GrowthRate.Fluctuating, 37, 50653),
        (GrowthRate.MediumSlow, 50, 117360)
    ];
    foreach (var vector in vectors) Equal(vector.Exp, GrowthRules.ExperienceAtLevel(vector.Rate, vector.Level));
});
Test("growth.all-levels-before-at-after-exact-threshold", () =>
{
    foreach (GrowthRate rate in Enum.GetValues<GrowthRate>())
    {
        uint previous = GrowthRules.ExperienceAtLevel(rate, 1);
        Equal(1, GrowthRules.LevelFromExperience(rate, previous));
        for (int level = 2; level <= 100; level++)
        {
            uint threshold = GrowthRules.ExperienceAtLevel(rate, level);
            True(threshold > previous, "Growth table must strictly increase in the supported range.");
            Equal(level - 1, GrowthRules.LevelFromExperience(rate, threshold - 1));
            Equal(level, GrowthRules.LevelFromExperience(rate, threshold));
            Equal(level, GrowthRules.LevelFromExperience(rate, threshold + 1));
            previous = threshold;
        }
    }
});
Test("growth.exact-level-100-caps", () =>
{
    uint[] caps = [1000000, 600000, 1640000, 1059860, 800000, 1250000];
    foreach (GrowthRate rate in Enum.GetValues<GrowthRate>())
    {
        Equal(caps[(int)rate], GrowthRules.ExperienceAtLevel(rate, 100));
        Equal(100, GrowthRules.LevelFromExperience(rate, uint.MaxValue));
        Equal<uint?>(null, GrowthRules.NextLevelThreshold(rate, 100));
        var p = GrowthRules.GetProgress(rate, caps[(int)rate]);
        Equal(100, p.Level); Equal(0u, p.ExperienceToNextLevel); Equal<uint?>(null, p.NextLevelThreshold);
    }
});
Test("growth.progress-exact-and-one-short", () =>
{
    var p = GrowthRules.GetProgress(GrowthRate.MediumFast, 999);
    Equal(9, p.Level); Equal(729u, p.CurrentLevelThreshold); Equal<uint?>(1000, p.NextLevelThreshold); Equal(1u, p.ExperienceToNextLevel);
    p = GrowthRules.GetProgress(GrowthRate.MediumFast, 1000);
    Equal(10, p.Level); Equal<uint?>(1331, p.NextLevelThreshold); Equal(331u, p.ExperienceToNextLevel);
});
Test("growth.invalid-level-rate-and-zero-exp", () =>
{
    foreach (int level in new[] { int.MinValue, -1, 0, 101, int.MaxValue })
    {
        Throws<ArgumentOutOfRangeException>(() => GrowthRules.ExperienceAtLevel(GrowthRate.Fast, level));
        Throws<ArgumentOutOfRangeException>(() => GrowthRules.NextLevelThreshold(GrowthRate.Fast, level));
    }
    foreach (int id in new[] { -1, 6, 7, int.MaxValue })
    {
        Throws<ArgumentOutOfRangeException>(() => GrowthRules.ExperienceAtLevel((GrowthRate)id, 1));
        Throws<ArgumentOutOfRangeException>(() => GrowthRules.LevelFromExperience((GrowthRate)id, 1));
    }
    foreach (GrowthRate rate in Enum.GetValues<GrowthRate>()) Throws<ArgumentOutOfRangeException>(() => GrowthRules.LevelFromExperience(rate, 0));
});

Test("capture.independent-fixed-input-math-and-rng-vectors", () =>
{
    using var document = Fixture("capture-v1.json"); var root = document.RootElement;
    Equal("037335f4c725d7c9aecdac87066f2002b4bd7e14", root.GetProperty("sourceCommit").GetString());
    var vectors = root.GetProperty("vectors"); True(vectors.GetArrayLength() >= 30, "Capture reference must contain the complete nonempty boundary matrix.");
    var ids = new HashSet<string>();
    foreach (var row in vectors.EnumerateArray())
    {
        string id = row.GetProperty("id").GetString()!; True(ids.Add(id), "Duplicate capture vector.");
        try
        {
            var input = row.GetProperty("input"); var expected = row.GetProperty("expected");
            var result = CaptureRules.Resolve(new(input.GetProperty("maximumHp").GetInt32(), input.GetProperty("currentHp").GetInt32(),
                input.GetProperty("catchRate").GetInt32(), (CaptureBall)input.GetProperty("ball").GetInt32(),
                (CaptureStatus)input.GetProperty("status").GetInt32()), new(row.GetProperty("seed").GetUInt32(), row.GetProperty("cursor").GetUInt64()));
            Equal(expected.GetProperty("caught").GetBoolean() ? CaptureOutcome.Caught : CaptureOutcome.Escaped, result.Outcome);
            Equal(expected.GetProperty("checksPassed").GetInt32(), result.ChecksPassed);
            Equal<int?>(expected.GetProperty("animationCode").GetInt32(), result.AnimationCode);
            Equal(expected.GetProperty("branch").GetString(), result.Branch.ToString());
            Equal(expected.GetProperty("seed").GetUInt32(), result.Rng.Seed); Equal(expected.GetProperty("cursor").GetUInt64(), result.Rng.Cursor);
            var math = expected.GetProperty("calculation"); var actual = result.Calculation!;
            Equal(math.GetProperty("ballMultiplierTenths").GetInt32(), actual.BallMultiplierTenths);
            Equal(math.GetProperty("ballAdjustedCatchRate").GetUInt32(), actual.BallAdjustedCatchRate);
            Equal(math.GetProperty("hpAdjustedCatchValue").GetUInt32(), actual.HpAdjustedCatchValue);
            Equal(math.GetProperty("catchValue").GetUInt32(), actual.CatchValue);
            var threshold = math.GetProperty("shakeThreshold");
            Equal(threshold.ValueKind == JsonValueKind.Null ? null : (uint?)threshold.GetUInt32(), actual.ShakeThreshold);
            var draws = expected.GetProperty("draws"); Equal(draws.GetArrayLength(), result.RandomTrace.Length);
            int i = 0; uint before = row.GetProperty("seed").GetUInt32();
            foreach (var draw in draws.EnumerateArray())
            {
                var trace = result.RandomTrace[i++]; Equal(draw.GetProperty("raw").GetUInt16(), trace.Raw);
                Equal(draw.GetProperty("seed").GetUInt32(), trace.After); Equal(before, trace.Before); before = trace.After;
                Equal(row.GetProperty("cursor").GetUInt64() + (ulong)i, trace.Cursor);
                Equal($"capture:shake-check-{i}", trace.Purpose); Equal(65536, trace.Bound); Equal((int)trace.Raw, trace.Result);
            }
        }
        catch (Exception e) { throw new Exception($"Vector {id}: {e.Message}", e); }
    }
});
Test("capture.source-integer-floor-before-hp-and-status", () =>
{
    var result = CaptureRules.Resolve(Input(hp: 80, rate: 3, ball: CaptureBall.Great, status: CaptureStatus.Burn), new(2));
    var math = result.Calculation!; Equal(4u, math.BallAdjustedCatchRate); Equal(1u, math.HpAdjustedCatchValue); Equal(1u, math.CatchValue);
    Equal<uint?>(16643, math.ShakeThreshold);
});
Test("capture.sleep-freeze-use-gen3-double", () =>
{
    foreach (var status in new[] { CaptureStatus.Sleep, CaptureStatus.Freeze })
    {
        var math = CaptureRules.Resolve(Input(status: status), new(2)).Calculation!;
        Equal(15u, math.HpAdjustedCatchValue); Equal(30u, math.CatchValue); Equal<uint?>(38835, math.ShakeThreshold);
    }
});
Test("capture.other-major-statuses-use-floor-one-point-five", () =>
{
    foreach (var status in new[] { CaptureStatus.Poison, CaptureStatus.Burn, CaptureStatus.Paralysis, CaptureStatus.BadPoison })
        Equal(22u, CaptureRules.Resolve(Input(status: status), new(2)).Calculation!.CatchValue);
});
Test("capture.value-254-still-requires-rng", () =>
{
    var rng = new RngState(0x87654321, ulong.MaxValue);
    var result = CaptureRules.Resolve(new(2, 1, 255, CaptureBall.Great), rng);
    NoDraw(result, rng, CaptureOutcome.InvalidInput, "rng-cursor-exhausted");
    result = CaptureRules.Resolve(new(2, 1, 255, CaptureBall.Great), new(0));
    Equal(254u, result.Calculation!.CatchValue); Equal<uint?>(65535, result.Calculation.ShakeThreshold);
    Equal(4, result.RandomTrace.Length);
});
Test("capture.guaranteed-above-254-with-exhausted-cursor", () =>
{
    var rng = new RngState(0x87654321, ulong.MaxValue);
    var result = CaptureRules.Resolve(new(2, 1, 255, CaptureBall.Ultra), rng);
    Equal(CaptureOutcome.Caught, result.Outcome); Equal(340u, result.Calculation!.CatchValue);
    Equal(CaptureDecisionBranch.GuaranteedCatchValue, result.Branch); Equal(0, result.ChecksPassed);
    Equal<int?>(4, result.AnimationCode); Equal(rng, result.Rng); Equal(0, result.RandomTrace.Length);
});
Test("capture.exact-255-consumes-no-rng", () =>
{
    var rng = new RngState(0x87654321, ulong.MaxValue);
    var result = CaptureRules.Resolve(new(1, 1, 255, CaptureBall.Ultra, CaptureStatus.Poison), rng);
    Equal(CaptureOutcome.Caught, result.Outcome); Equal(255u, result.Calculation!.CatchValue);
    Equal(CaptureDecisionBranch.GuaranteedCatchValue, result.Branch); Equal(rng, result.Rng); Equal(0, result.RandomTrace.Length);
});
Test("capture.invalid-inputs-atomic", () =>
{
    var rng = new RngState(0x12345678, 57);
    foreach (var item in new[]
    {
        (Input() with { MaximumHp = 0 }, "invalid-hp"), (Input() with { MaximumHp = 65536 }, "invalid-hp"),
        (Input() with { CurrentHp = 0 }, "invalid-hp"), (Input() with { CurrentHp = -1 }, "invalid-hp"),
        (Input() with { CurrentHp = 101 }, "invalid-hp"), (Input() with { CatchRate = 0 }, "invalid-catch-rate"),
        (Input() with { CatchRate = 256 }, "invalid-catch-rate"), (Input() with { Status = (CaptureStatus)99 }, "invalid-major-status"),
        (Input() with { Ball = (CaptureBall)0 }, "invalid-ball"), (Input() with { Ball = (CaptureBall)13 }, "invalid-ball"),
        (Input() with { Encounter = (CaptureEncounterKind)99 }, "invalid-encounter-kind")
    }) NoDraw(CaptureRules.Resolve(item.Item1, rng), rng, CaptureOutcome.InvalidInput, item.Item2);
});
Test("capture.unsupported-balls-atomic", () =>
{
    var rng = new RngState(0, 3);
    foreach (var ball in Enum.GetValues<CaptureBall>().Where(x => x >= CaptureBall.Safari))
        NoDraw(CaptureRules.Resolve(Input(ball: ball), rng), rng, CaptureOutcome.Unsupported, "unsupported-ball");
});
Test("capture.unsupported-contexts-atomic-including-master", () =>
{
    var rng = new RngState(123, 8);
    foreach (var kind in Enum.GetValues<CaptureEncounterKind>().Where(x => x != CaptureEncounterKind.OrdinaryWild))
        foreach (var ball in new[] { CaptureBall.Poke, CaptureBall.Master })
            NoDraw(CaptureRules.Resolve(Input(ball: ball) with { Encounter = kind }, rng), rng, CaptureOutcome.Unsupported, "unsupported-encounter-kind");
});
Test("capture.unknown-rng-rejected-even-guaranteed", () =>
{
    var rng = new RngState(0, 0, "unknown");
    NoDraw(CaptureRules.Resolve(Input(1, 255, CaptureBall.Ultra, CaptureStatus.Sleep), rng), rng, CaptureOutcome.InvalidInput, "invalid-rng-algorithm");
});
Test("capture.cursor-exhaustion-atomic-before-and-during", () =>
{
    foreach (ulong cursor in new[] { ulong.MaxValue, ulong.MaxValue - 1 })
    {
        var rng = new RngState(0, cursor); // seed 0 passes first check and requires a second
        NoDraw(CaptureRules.Resolve(Input(), rng), rng, CaptureOutcome.InvalidInput, "rng-cursor-exhausted");
    }
    var oneLeft = new RngState(2, ulong.MaxValue - 1); // raw 33676 fails first check
    var result = CaptureRules.Resolve(Input(), oneLeft);
    Equal(CaptureOutcome.Escaped, result.Outcome); Equal(1, result.RandomTrace.Length); Equal(ulong.MaxValue, result.Rng.Cursor);
});
Test("capture.master-overrides-after-one-consumed-check", () =>
{
    var before = new RngState(0, 11); var result = CaptureRules.Resolve(Input(ball: CaptureBall.Master), before);
    Equal(CaptureOutcome.Caught, result.Outcome); Equal(CaptureDecisionBranch.MasterBallOverride, result.Branch);
    Equal(0, result.ChecksPassed); Equal<int?>(4, result.AnimationCode); Equal(1, result.RandomTrace.Length);
    Equal(new RngState(24691, 12), result.Rng); Equal((ushort)0, result.RandomTrace[0].Raw);
});
Test("capture.zero-value-still-draws-and-escapes", () =>
{
    var result = CaptureRules.Resolve(Input(rate: 1), new(0));
    Equal(CaptureOutcome.Escaped, result.Outcome); Equal(0u, result.Calculation!.CatchValue);
    Equal<uint?>(0, result.Calculation.ShakeThreshold); Equal(1, result.RandomTrace.Length); Equal(0, result.ChecksPassed);
});
Test("capture.replay-and-inputs-immutable", () =>
{
    var input = Input(30, 45, CaptureBall.Ultra, CaptureStatus.Paralysis); var rng = new RngState(900, 22);
    string before = JsonSerializer.Serialize(new { input, rng });
    var first = CaptureRules.Resolve(input, rng); var second = CaptureRules.Resolve(input, rng);
    Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    Equal(before, JsonSerializer.Serialize(new { input, rng }));
    Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(JsonSerializer.Deserialize<CaptureResult>(JsonSerializer.Serialize(first))));
});

string? filter = null;
if (args.Length != 0)
{
    if (args.Length != 2 || args[0] != "--filter" || string.IsNullOrWhiteSpace(args[1]))
    {
        Console.WriteLine(JsonSerializer.Serialize(new { status = "invalid-arguments", tests = 0 })); return 2;
    }
    filter = args[1];
}
var selected = tests.Where(x => filter is null || x.Id.Contains(filter, StringComparison.Ordinal)).ToArray();
if (selected.Length == 0)
{
    Console.WriteLine(JsonSerializer.Serialize(new { status = "empty-suite", tests = 0 })); return 2;
}
var results = new List<object>(); int failures = 0;
foreach (var test in selected)
{
    int before = assertions;
    try { test.Run(); results.Add(new { id = test.Id, passed = true, assertions = assertions - before }); }
    catch (Exception error) { failures++; results.Add(new { id = test.Id, passed = false, assertions = assertions - before, error = error.Message }); }
}
Console.WriteLine(JsonSerializer.Serialize(new { status = failures == 0 ? "passed" : "failed", runtime = Environment.Version.ToString(), tests = selected.Length, assertions, failures, results }, new JsonSerializerOptions { WriteIndented = true }));
return failures == 0 ? 0 : 1;
