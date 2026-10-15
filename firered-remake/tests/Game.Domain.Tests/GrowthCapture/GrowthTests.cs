using System.Text.Json;
using Game.Domain.Capture;
using Game.Domain.Growth;
using Game.Domain.Randomness;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using FireRed.Tests;


namespace FireRed.Tests.Domain;
[TestClass, TestCategory("Game")]
public sealed class GrowthTests : GrowthCaptureFixture
{

    [TestMethod]
    public void growth_fixed_source_table_600_independent_values()
    {
        using var document = Fixture("growth-v1.json");
        var root = document.RootElement;
        Assert.AreEqual("037335f4c725d7c9aecdac87066f2002b4bd7e14", root.GetProperty("sourceCommit").GetString());
        var curves = root.GetProperty("curves");
        Assert.AreEqual(6, curves.GetArrayLength());
        var seen = new HashSet<int>();
        foreach (var curve in curves.EnumerateArray())
        {
            int id = curve.GetProperty("id").GetInt32();
            Assert.IsTrue(seen.Add(id), "Duplicate growth fixture ID.");
            var entries = curve.GetProperty("experienceByLevel");
            Assert.AreEqual(100, entries.GetArrayLength());
            int level = 1;
            foreach (var entry in entries.EnumerateArray())
                Assert.AreEqual(entry.GetUInt32(), GrowthRules.ExperienceAtLevel((GrowthRate)id, level++));
        }

        Assert.IsTrue(seen.SetEquals(Enumerable.Range(0, 6)), "Every source curve is required.");
    }

    [TestMethod]
    public void growth_literal_level_one_and_small_medium_slow()
    {
        foreach (GrowthRate rate in Enum.GetValues<GrowthRate>())
            Assert.AreEqual(1u, GrowthRules.ExperienceAtLevel(rate, 1));
        Assert.AreEqual(9u, GrowthRules.ExperienceAtLevel(GrowthRate.MediumSlow, 2));
        Assert.AreEqual(57u, GrowthRules.ExperienceAtLevel(GrowthRate.MediumSlow, 3));
        Assert.AreEqual(112u, GrowthRules.ExperienceAtLevel(GrowthRate.Fluctuating, 6));
    }

    [TestMethod]
    public void growth_hand_checked_piecewise_edges()
    {
        (GrowthRate Rate, int Level, uint Exp)[] vectors = [(GrowthRate.Erratic, 50, 125000), (GrowthRate.Erratic, 51, 131324), (GrowthRate.Erratic, 68, 257834), (GrowthRate.Erratic, 69, 267406), (GrowthRate.Erratic, 98, 583539), (GrowthRate.Erratic, 99, 591882), (GrowthRate.Fluctuating, 15, 1957), (GrowthRate.Fluctuating, 16, 2457), (GrowthRate.Fluctuating, 36, 46656), (GrowthRate.Fluctuating, 37, 50653), (GrowthRate.MediumSlow, 50, 117360)];
        foreach (var vector in vectors)
            Assert.AreEqual(vector.Exp, GrowthRules.ExperienceAtLevel(vector.Rate, vector.Level));
    }

    [TestMethod]
    public void growth_all_levels_before_at_after_exact_threshold()
    {
        foreach (GrowthRate rate in Enum.GetValues<GrowthRate>())
        {
            uint previous = GrowthRules.ExperienceAtLevel(rate, 1);
            Assert.AreEqual(1, GrowthRules.LevelFromExperience(rate, previous));
            for (int level = 2; level <= 100; level++)
            {
                uint threshold = GrowthRules.ExperienceAtLevel(rate, level);
                Assert.IsTrue(threshold > previous, "Growth table must strictly increase in the supported range.");
                Assert.AreEqual(level - 1, GrowthRules.LevelFromExperience(rate, threshold - 1));
                Assert.AreEqual(level, GrowthRules.LevelFromExperience(rate, threshold));
                Assert.AreEqual(level, GrowthRules.LevelFromExperience(rate, threshold + 1));
                previous = threshold;
            }
        }
    }

    [TestMethod]
    public void growth_exact_level_100_caps()
    {
        uint[] caps = [1000000, 600000, 1640000, 1059860, 800000, 1250000];
        foreach (GrowthRate rate in Enum.GetValues<GrowthRate>())
        {
            Assert.AreEqual(caps[(int)rate], GrowthRules.ExperienceAtLevel(rate, 100));
            Assert.AreEqual(100, GrowthRules.LevelFromExperience(rate, uint.MaxValue));
            Assert.AreEqual<uint?>(null, GrowthRules.NextLevelThreshold(rate, 100));
            var p = GrowthRules.GetProgress(rate, caps[(int)rate]);
            Assert.AreEqual(100, p.Level);
            Assert.AreEqual(0u, p.ExperienceToNextLevel);
            Assert.AreEqual<uint?>(null, p.NextLevelThreshold);
        }
    }

    [TestMethod]
    public void growth_progress_exact_and_one_short()
    {
        var p = GrowthRules.GetProgress(GrowthRate.MediumFast, 999);
        Assert.AreEqual(9, p.Level);
        Assert.AreEqual(729u, p.CurrentLevelThreshold);
        Assert.AreEqual<uint?>(1000, p.NextLevelThreshold);
        Assert.AreEqual(1u, p.ExperienceToNextLevel);
        p = GrowthRules.GetProgress(GrowthRate.MediumFast, 1000);
        Assert.AreEqual(10, p.Level);
        Assert.AreEqual<uint?>(1331, p.NextLevelThreshold);
        Assert.AreEqual(331u, p.ExperienceToNextLevel);
    }

    [TestMethod]
    public void growth_invalid_level_rate_and_zero_exp()
    {
        foreach (int level in new[]
        {
            int.MinValue,
            -1,
            0,
            101,
            int.MaxValue
        }

        )
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GrowthRules.ExperienceAtLevel(GrowthRate.Fast, level));
            Assert.Throws<ArgumentOutOfRangeException>(() => GrowthRules.NextLevelThreshold(GrowthRate.Fast, level));
        }

        foreach (int id in new[]
        {
            -1,
            6,
            7,
            int.MaxValue
        }

        )
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GrowthRules.ExperienceAtLevel((GrowthRate)id, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => GrowthRules.LevelFromExperience((GrowthRate)id, 1));
        }

        foreach (GrowthRate rate in Enum.GetValues<GrowthRate>())
            Assert.Throws<ArgumentOutOfRangeException>(() => GrowthRules.LevelFromExperience(rate, 0));
    }
}
