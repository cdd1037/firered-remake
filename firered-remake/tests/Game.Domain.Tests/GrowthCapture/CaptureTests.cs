using System.Text.Json;
using Game.Domain.Capture;
using Game.Domain.Growth;
using Game.Domain.Randomness;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Domain;
[TestClass, TestCategory("Game")]
public sealed class CaptureTests : GrowthCaptureFixture
{

    [TestMethod]
    public void capture_independent_fixed_input_math_and_rng_vectors()
    {
        using var document = Fixture("capture-v1.json");
        var root = document.RootElement;
        Assert.AreEqual("037335f4c725d7c9aecdac87066f2002b4bd7e14", root.GetProperty("sourceCommit").GetString());
        var vectors = root.GetProperty("vectors");
        Assert.IsTrue(vectors.GetArrayLength() >= 30, "Capture reference must contain the complete nonempty boundary matrix.");
        var ids = new HashSet<string>();
        foreach (var row in vectors.EnumerateArray())
        {
            string id = row.GetProperty("id").GetString()!;
            Assert.IsTrue(ids.Add(id), "Duplicate capture vector.");
            try
            {
                var input = row.GetProperty("input");
                var expected = row.GetProperty("expected");
                var result = CaptureRules.Resolve(new(input.GetProperty("maximumHp").GetInt32(), input.GetProperty("currentHp").GetInt32(), input.GetProperty("catchRate").GetInt32(), (CaptureBall)input.GetProperty("ball").GetInt32(), (CaptureStatus)input.GetProperty("status").GetInt32()), new(row.GetProperty("seed").GetUInt32(), row.GetProperty("cursor").GetUInt64()));
                Assert.AreEqual(expected.GetProperty("caught").GetBoolean() ? CaptureOutcome.Caught : CaptureOutcome.Escaped, result.Outcome);
                Assert.AreEqual(expected.GetProperty("checksPassed").GetInt32(), result.ChecksPassed);
                Assert.AreEqual<int?>(expected.GetProperty("animationCode").GetInt32(), result.AnimationCode);
                Assert.AreEqual(expected.GetProperty("branch").GetString(), result.Branch.ToString());
                Assert.AreEqual(expected.GetProperty("seed").GetUInt32(), result.Rng.Seed);
                Assert.AreEqual(expected.GetProperty("cursor").GetUInt64(), result.Rng.Cursor);
                var math = expected.GetProperty("calculation");
                var actual = result.Calculation!;
                Assert.AreEqual(math.GetProperty("ballMultiplierTenths").GetInt32(), actual.BallMultiplierTenths);
                Assert.AreEqual(math.GetProperty("ballAdjustedCatchRate").GetUInt32(), actual.BallAdjustedCatchRate);
                Assert.AreEqual(math.GetProperty("hpAdjustedCatchValue").GetUInt32(), actual.HpAdjustedCatchValue);
                Assert.AreEqual(math.GetProperty("catchValue").GetUInt32(), actual.CatchValue);
                var threshold = math.GetProperty("shakeThreshold");
                Assert.AreEqual(threshold.ValueKind == JsonValueKind.Null ? null : (uint? )threshold.GetUInt32(), actual.ShakeThreshold);
                var draws = expected.GetProperty("draws");
                Assert.AreEqual(draws.GetArrayLength(), result.RandomTrace.Length);
                int i = 0;
                uint before = row.GetProperty("seed").GetUInt32();
                foreach (var draw in draws.EnumerateArray())
                {
                    var trace = result.RandomTrace[i++];
                    Assert.AreEqual(draw.GetProperty("raw").GetUInt16(), trace.Raw);
                    Assert.AreEqual(draw.GetProperty("seed").GetUInt32(), trace.After);
                    Assert.AreEqual(before, trace.Before);
                    before = trace.After;
                    Assert.AreEqual(row.GetProperty("cursor").GetUInt64() + (ulong)i, trace.Cursor);
                    Assert.AreEqual($"capture:shake-check-{i}", trace.Purpose);
                    Assert.AreEqual(65536, trace.Bound);
                    Assert.AreEqual((int)trace.Raw, trace.Result);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Vector {id}: {e.Message}", e);
            }
        }
    }

    [TestMethod]
    public void capture_source_integer_floor_before_hp_and_status()
    {
        var result = CaptureRules.Resolve(Input(hp: 80, rate: 3, ball: CaptureBall.Great, status: CaptureStatus.Burn), new(2));
        var math = result.Calculation!;
        Assert.AreEqual(4u, math.BallAdjustedCatchRate);
        Assert.AreEqual(1u, math.HpAdjustedCatchValue);
        Assert.AreEqual(1u, math.CatchValue);
        Assert.AreEqual<uint?>(16643, math.ShakeThreshold);
    }

    [TestMethod]
    public void capture_sleep_freeze_use_gen3_double()
    {
        foreach (var status in new[]
        {
            CaptureStatus.Sleep,
            CaptureStatus.Freeze
        }

        )
        {
            var math = CaptureRules.Resolve(Input(status: status), new(2)).Calculation!;
            Assert.AreEqual(15u, math.HpAdjustedCatchValue);
            Assert.AreEqual(30u, math.CatchValue);
            Assert.AreEqual<uint?>(38835, math.ShakeThreshold);
        }
    }

    [TestMethod]
    public void capture_other_major_statuses_use_floor_one_point_five()
    {
        foreach (var status in new[]
        {
            CaptureStatus.Poison,
            CaptureStatus.Burn,
            CaptureStatus.Paralysis,
            CaptureStatus.BadPoison
        }

        )
            Assert.AreEqual(22u, CaptureRules.Resolve(Input(status: status), new(2)).Calculation!.CatchValue);
    }

    [TestMethod]
    public void capture_value_254_still_requires_rng()
    {
        var rng = new RngState(0x87654321, ulong.MaxValue);
        var result = CaptureRules.Resolve(new(2, 1, 255, CaptureBall.Great), rng);
        NoDraw(result, rng, CaptureOutcome.InvalidInput, "rng-cursor-exhausted");
        result = CaptureRules.Resolve(new(2, 1, 255, CaptureBall.Great), new(0));
        Assert.AreEqual(254u, result.Calculation!.CatchValue);
        Assert.AreEqual<uint?>(65535, result.Calculation.ShakeThreshold);
        Assert.AreEqual(4, result.RandomTrace.Length);
    }

    [TestMethod]
    public void capture_guaranteed_above_254_with_exhausted_cursor()
    {
        var rng = new RngState(0x87654321, ulong.MaxValue);
        var result = CaptureRules.Resolve(new(2, 1, 255, CaptureBall.Ultra), rng);
        Assert.AreEqual(CaptureOutcome.Caught, result.Outcome);
        Assert.AreEqual(340u, result.Calculation!.CatchValue);
        Assert.AreEqual(CaptureDecisionBranch.GuaranteedCatchValue, result.Branch);
        Assert.AreEqual(0, result.ChecksPassed);
        Assert.AreEqual<int?>(4, result.AnimationCode);
        Assert.AreEqual(rng, result.Rng);
        Assert.AreEqual(0, result.RandomTrace.Length);
    }

    [TestMethod]
    public void capture_exact_255_consumes_no_rng()
    {
        var rng = new RngState(0x87654321, ulong.MaxValue);
        var result = CaptureRules.Resolve(new(1, 1, 255, CaptureBall.Ultra, CaptureStatus.Poison), rng);
        Assert.AreEqual(CaptureOutcome.Caught, result.Outcome);
        Assert.AreEqual(255u, result.Calculation!.CatchValue);
        Assert.AreEqual(CaptureDecisionBranch.GuaranteedCatchValue, result.Branch);
        Assert.AreEqual(rng, result.Rng);
        Assert.AreEqual(0, result.RandomTrace.Length);
    }

    [TestMethod]
    public void capture_invalid_inputs_atomic()
    {
        var rng = new RngState(0x12345678, 57);
        foreach (var item in new[]
        {
            (Input()with { MaximumHp = 0 }, "invalid-hp"),
            (Input()with { MaximumHp = 65536 }, "invalid-hp"),
            (Input()with { CurrentHp = 0 }, "invalid-hp"),
            (Input()with { CurrentHp = -1 }, "invalid-hp"),
            (Input()with { CurrentHp = 101 }, "invalid-hp"),
            (Input()with { CatchRate = 0 }, "invalid-catch-rate"),
            (Input()with { CatchRate = 256 }, "invalid-catch-rate"),
            (Input()with { Status = (CaptureStatus)99 }, "invalid-major-status"),
            (Input()with { Ball = (CaptureBall)0 }, "invalid-ball"),
            (Input()with { Ball = (CaptureBall)13 }, "invalid-ball"),
            (Input()with { Encounter = (CaptureEncounterKind)99 }, "invalid-encounter-kind")
        }

        )
            NoDraw(CaptureRules.Resolve(item.Item1, rng), rng, CaptureOutcome.InvalidInput, item.Item2);
    }

    [TestMethod]
    public void capture_unsupported_balls_atomic()
    {
        var rng = new RngState(0, 3);
        foreach (var ball in Enum.GetValues<CaptureBall>().Where(x => x >= CaptureBall.Safari))
            NoDraw(CaptureRules.Resolve(Input(ball: ball), rng), rng, CaptureOutcome.Unsupported, "unsupported-ball");
    }

    [TestMethod]
    public void capture_unsupported_contexts_atomic_including_master()
    {
        var rng = new RngState(123, 8);
        foreach (var kind in Enum.GetValues<CaptureEncounterKind>().Where(x => x != CaptureEncounterKind.OrdinaryWild))
            foreach (var ball in new[]
            {
                CaptureBall.Poke,
                CaptureBall.Master
            }

            )
                NoDraw(CaptureRules.Resolve(Input(ball: ball)with { Encounter = kind }, rng), rng, CaptureOutcome.Unsupported, "unsupported-encounter-kind");
    }

    [TestMethod]
    public void capture_unknown_rng_rejected_even_guaranteed()
    {
        var rng = new RngState(0, 0, "unknown");
        NoDraw(CaptureRules.Resolve(Input(1, 255, CaptureBall.Ultra, CaptureStatus.Sleep), rng), rng, CaptureOutcome.InvalidInput, "invalid-rng-algorithm");
    }

    [TestMethod]
    public void capture_cursor_exhaustion_atomic_before_and_during()
    {
        foreach (ulong cursor in new[]
        {
            ulong.MaxValue,
            ulong.MaxValue - 1
        }

        )
        {
            var rng = new RngState(0, cursor); // seed 0 passes first check and requires a second
            NoDraw(CaptureRules.Resolve(Input(), rng), rng, CaptureOutcome.InvalidInput, "rng-cursor-exhausted");
        }

        var oneLeft = new RngState(2, ulong.MaxValue - 1);
        var result = CaptureRules.Resolve(Input(), oneLeft);
        Assert.AreEqual(CaptureOutcome.Escaped, result.Outcome);
        Assert.AreEqual(1, result.RandomTrace.Length);
        Assert.AreEqual(ulong.MaxValue, result.Rng.Cursor);
    }

    [TestMethod]
    public void capture_master_overrides_after_one_consumed_check()
    {
        var before = new RngState(0, 11);
        var result = CaptureRules.Resolve(Input(ball: CaptureBall.Master), before);
        Assert.AreEqual(CaptureOutcome.Caught, result.Outcome);
        Assert.AreEqual(CaptureDecisionBranch.MasterBallOverride, result.Branch);
        Assert.AreEqual(0, result.ChecksPassed);
        Assert.AreEqual<int?>(4, result.AnimationCode);
        Assert.AreEqual(1, result.RandomTrace.Length);
        Assert.AreEqual(new RngState(24691, 12), result.Rng);
        Assert.AreEqual((ushort)0, result.RandomTrace[0].Raw);
    }

    [TestMethod]
    public void capture_zero_value_still_draws_and_escapes()
    {
        var result = CaptureRules.Resolve(Input(rate: 1), new(0));
        Assert.AreEqual(CaptureOutcome.Escaped, result.Outcome);
        Assert.AreEqual(0u, result.Calculation!.CatchValue);
        Assert.AreEqual<uint?>(0, result.Calculation.ShakeThreshold);
        Assert.AreEqual(1, result.RandomTrace.Length);
        Assert.AreEqual(0, result.ChecksPassed);
    }

    [TestMethod]
    public void capture_replay_and_inputs_immutable()
    {
        var input = Input(30, 45, CaptureBall.Ultra, CaptureStatus.Paralysis);
        var rng = new RngState(900, 22);
        string before = JsonSerializer.Serialize(new { input, rng });
        var first = CaptureRules.Resolve(input, rng);
        var second = CaptureRules.Resolve(input, rng);
        Assert.AreEqual(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.AreEqual(before, JsonSerializer.Serialize(new { input, rng }));
        Assert.AreEqual(JsonSerializer.Serialize(first), JsonSerializer.Serialize(JsonSerializer.Deserialize<CaptureResult>(JsonSerializer.Serialize(first))));
    }
}
