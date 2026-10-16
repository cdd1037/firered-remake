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
public sealed class DamageTests : DomainFixture
{

    [TestMethod]
    public void damage_basic_integer_stab()
    {
        var d = DamageRules.Calculate(Damage());
        Assert.AreEqual(28, d.Damage);
        Assert.AreEqual(17, d.Steps.Single(x => x.Operation == "divide-50").Value);
        Assert.AreEqual(19, d.Steps.Single(x => x.Operation == "plus-two").Value);
    }

    [TestMethod]
    public void damage_critical_before_stab()
    {
        Assert.AreEqual(48, DamageRules.Calculate(Damage()with { Critical = true, RandomPercent = 85 }).Damage);
    }

    [TestMethod]
    public void damage_physical_special_low_end()
    {
        var d = Damage()with
        {
            Level = 1,
            Power = 1,
            Attack = 1,
            Defense = 999,
            AttackerType1 = ElementType.Water
        };
        Assert.AreEqual(3, DamageRules.Calculate(d).Damage);
        Assert.AreEqual(2, DamageRules.Calculate(d with { MoveType = ElementType.Fire }).Damage);
    }

    [TestMethod]
    public void damage_source_type_order_rounding()
    {
        var d = new DamageInput(5, 40, ElementType.Fire, 10, 30, ElementType.Normal, null, ElementType.Grass, ElementType.Water, false, 100);
        Assert.AreEqual(2, DamageRules.Calculate(d).Damage);
        Assert.AreEqual(2, DamageRules.Calculate(d with { DefenderType1 = ElementType.Water, DefenderType2 = ElementType.Grass }).Damage);
        Assert.AreEqual("type-Water", DamageRules.Calculate(d).Steps.First(x => x.Operation.StartsWith("type-", StringComparison.Ordinal)).Operation);
    }

    [TestMethod]
    public void damage_duplicate_type_only_once()
    {
        var d = Damage(ElementType.Fire)with
        {
            DefenderType1 = ElementType.Water,
            DefenderType2 = ElementType.Water
        };
        Assert.AreEqual(14, DamageRules.Calculate(d).Damage);
    }

    [TestMethod]
    public void damage_immunity_stays_zero()
    {
        var d = Damage(ElementType.Electric)with
        {
            DefenderType1 = ElementType.Ground,
            DefenderType2 = ElementType.Flying
        };
        Assert.AreEqual(0, DamageRules.Calculate(d).Damage);
    }

    [TestMethod]
    public void damage_unimplemented_modifiers_fail()
    {
        foreach (var m in new[]
        {
            UnsupportedBattleMechanics.Abilities,
            UnsupportedBattleMechanics.Badges,
            UnsupportedBattleMechanics.Weather,
            UnsupportedBattleMechanics.HeldItems
        }

        )
            Assert.Throws<NotSupportedException>(() => DamageRules.Calculate(Damage()with { ActiveMechanics = m }));
    }

    [TestMethod]
    public void damage_invalid_and_mystery_fail()
    {
        Assert.Throws<NotSupportedException>(() => DamageRules.Calculate(Damage(ElementType.Mystery)));
        Assert.Throws<ArgumentOutOfRangeException>(() => DamageRules.Calculate(Damage()with { Power = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => DamageRules.Calculate(Damage()with { Defense = 0 }));
    }
}
