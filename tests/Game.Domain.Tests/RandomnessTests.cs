using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Rules;
using Game.Domain.Battle;
using Game.Domain.World;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;

namespace FireRed.Tests.Domain;
[TestClass, TestCategory("Game")]
public sealed class RandomnessTests : DomainFixture
{

    [TestMethod]
    public void rng_zero_seed_independent_vectors()
    {
        (uint Seed, ushort Raw, int Result)[] expected = [(24691u, 0, 0), (3917380458u, 59774, 74), (1383151765u, 21105, 5), (833674724u, 12720, 20), (2386711175u, 36418, 18), (3805062638u, 58060, 60), (2948958921u, 44997, 97), (1742452232u, 26587, 87)];
        var state = new RngState(0);
        for (int i = 0; i < expected.Length; i++)
        {
            var row = expected[i];
            ulong cursor = (ulong)i + 1;
            uint before = i == 0 ? 0 : expected[i - 1].Seed;
            var draw = Gen3Rng.Next(state, "independent-vector", 100);
            Assert.AreEqual(new RngState(row.Seed, cursor, "gen3-lcg32-hi16-v1"), draw.State);
            Assert.AreEqual(row.Raw, draw.Value);
            Assert.AreEqual(new RandomTrace(cursor, "independent-vector", before, row.Seed, row.Raw, 100, row.Result), draw.Trace);
            state = draw.State;
        }
    }

    [TestMethod]
    public void rng_unsigned_overflow()
    {
        var d = Gen3Rng.Next(new(uint.MaxValue), "overflow");
        Assert.AreEqual(3191476742u, d.State.Seed);
        Assert.AreEqual((ushort)48698, d.Value);
    }

    [TestMethod]
    public void rng_invalid_bound_and_exhaustion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Gen3Rng.Next(new(0), "bound", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Gen3Rng.Next(new(0), "bound", 65537));
        Assert.Throws<OverflowException>(() => Gen3Rng.Next(new(0, ulong.MaxValue), "overflow"));
    }

    [TestMethod]
    public void rng_bound_one_and_purpose()
    {
        var d = Gen3Rng.Next(new(123), "encounter:test", 1);
        Assert.AreEqual(0, d.Trace.Result);
        Assert.AreEqual("encounter:test", d.Trace.Purpose);
        Assert.AreEqual(1ul, d.Trace.Cursor);
    }

    [TestMethod]
    public void rng_algorithm_is_persisted_and_validated()
    {
        var copy = JsonSerializer.Deserialize<RngState>("{\"Seed\":0,\"Cursor\":0,\"AlgorithmId\":\"unknown\"}");
        Assert.AreEqual("unknown", copy.AlgorithmId);
        Assert.Throws<NotSupportedException>(() => Gen3Rng.Next(copy, "invalid"));
    }

    [TestMethod]
    public void rng_ten_thousand_draws_repeat()
    {
        var a = new RngState(0x76543210);
        var b = a;
        for (int i = 0; i < 10000; i++)
        {
            var x = Gen3Rng.Next(a, "stress", 37);
            var y = Gen3Rng.Next(b, "stress", 37);
            Assert.AreEqual(x, y);
            Assert.IsTrue(x.Trace.Result is >= 0 and < 37);
            a = x.State;
            b = y.State;
        }

        Assert.AreEqual(10000ul, a.Cursor);
    }
}
