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
public sealed class ElementTypeTests : DomainFixture
{

    [TestMethod]
    public void types_gen3_categories()
    {
        Assert.AreEqual(9, Enum.GetValues<ElementType>().Count(t => TypeRules.Category(t) == DamageCategory.Physical));
        Assert.AreEqual(8, Enum.GetValues<ElementType>().Count(t => TypeRules.Category(t) == DamageCategory.Special));
        Assert.AreEqual(DamageCategory.Physical, TypeRules.Category(ElementType.Ghost));
        Assert.AreEqual(DamageCategory.Special, TypeRules.Category(ElementType.Dark));
        Assert.AreEqual(DamageCategory.Unsupported, TypeRules.Category(ElementType.Mystery));
    }

    [TestMethod]
    public void types_gen3_steel_resistances()
    {
        Assert.AreEqual(5, TypeRules.EffectivenessTenths(ElementType.Dark, ElementType.Steel));
        Assert.AreEqual(5, TypeRules.EffectivenessTenths(ElementType.Ghost, ElementType.Steel));
        Assert.AreEqual(0, TypeRules.EffectivenessTenths(ElementType.Normal, ElementType.Ghost));
    }

    [TestMethod]
    public void types_no_fairy_fallback()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TypeRules.Category((ElementType)18));
    }
}
