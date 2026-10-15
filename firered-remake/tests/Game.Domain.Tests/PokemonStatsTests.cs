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
public sealed class PokemonStatsTests : DomainFixture
{

    [TestMethod]
    public void stats_hp_boundaries()
    {
        Assert.AreEqual(11, StatRules.CalculateHp(1, 0, 0, 1));
        Assert.AreEqual(714, StatRules.CalculateHp(255, 31, 255, 100));
        Assert.AreEqual(142, StatRules.CalculateHp(35, 31, 252, 50));
        Assert.AreEqual(19, StatRules.CalculateHp(45, 0, 0, 5));
    }

    [TestMethod]
    public void stats_shedinja_hp()
    {
        Assert.AreEqual(1, StatRules.CalculateHp(1, 31, 255, 100, true));
    }

    [TestMethod]
    public void stats_ev_integer_floor()
    {
        Assert.AreEqual(StatRules.CalculateHp(35, 0, 0, 100), StatRules.CalculateHp(35, 0, 3, 100));
        Assert.AreEqual(181, StatRules.CalculateHp(35, 0, 4, 100));
    }

    [TestMethod]
    public void stats_nature_up_down_neutral()
    {
        Assert.AreEqual(167, StatRules.CalculateOther(100, 31, 252, 50, Nature.Adamant, Stat.Attack));
        Assert.AreEqual(136, StatRules.CalculateOther(100, 31, 252, 50, Nature.Adamant, Stat.SpecialAttack));
        Assert.AreEqual(152, StatRules.CalculateOther(100, 31, 252, 50, Nature.Adamant, Stat.Speed));
    }

    [TestMethod]
    public void stats_all_neutral_natures()
    {
        foreach (var n in new[]
        {
            Nature.Hardy,
            Nature.Docile,
            Nature.Serious,
            Nature.Bashful,
            Nature.Quirky
        }

        )
            foreach (var stat in Enum.GetValues<Stat>())
                Assert.AreEqual(101, StatRules.ApplyNature(101, n, stat));
    }

    [TestMethod]
    public void stats_nature_u16_original_wrap()
    {
        Assert.AreEqual(654, StatRules.ApplyNature(595, Nature.Lonely, Stat.Attack));
        Assert.AreEqual(0, StatRules.ApplyNature(596, Nature.Lonely, Stat.Attack));
        Assert.AreEqual(0, StatRules.ApplyNature(729, Nature.Lonely, Stat.Defense));
    }

    [TestMethod]
    public void stats_invalid_inputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StatRules.CalculateHp(45, 32, 0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => StatRules.CalculateHp(45, 0, 256, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => StatRules.CalculateHp(45, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => StatRules.ApplyNature(5, (Nature)25, Stat.Attack));
    }
}
