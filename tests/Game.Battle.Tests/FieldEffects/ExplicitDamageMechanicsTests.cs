using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Battle.FieldEffects;

[TestClass, TestCategory("Game")]
public sealed class ExplicitDamageMechanicsTests : FieldEffectFixture
{
    [TestMethod]
    public void DamageReadsMechanicsRatherThanRegionalSpeciesCounts()
    {
        var user = Mon("SQUIRTLE", "WATER_GUN");
        var target = Mon("SANDSHREW", "SCRATCH");
        var move = catalog.Moves[user.Pokemon.Moves[0].Move];
        var expected = SourceBattleDamage.Calculate(catalog, user, target, move, false, 91, rain: true);
        foreach (var count in new[] { 2, 79, 102, 103, 107, 189 })
        {
            var species = catalog.Species.Values.Where(s => s.Id == user.Pokemon.Species || s.Id == target.Pokemon.Species)
                .Concat(catalog.Species.Values.Where(s => s.Id != user.Pokemon.Species && s.Id != target.Pokemon.Species).Take(count - 2))
                .ToImmutableDictionary(s => s.Id);
            var subset = catalog with { Species = species };
            var actual = SourceBattleDamage.Calculate(subset, user, target, move, false, 91, rain: true);
            Assert.AreEqual(expected.Damage, actual.Damage);
            Assert.AreEqual(expected.Category, actual.Category);
            CollectionAssert.AreEqual(expected.Steps.ToArray(), actual.Steps.ToArray());
        }
    }

    [TestMethod]
    public void MissingMechanicsAndInvalidDefenderCountsAreRejected()
    {
        var user = Mon("SQUIRTLE", "WATER_GUN");
        var target = Mon("SANDSHREW", "SCRATCH");
        var move = catalog.Moves[user.Pokemon.Moves[0].Move];
        Assert.Throws<ArgumentException>(() => SourceBattleDamage.Calculate(catalog with { MechanicsData = null }, user, target, move, false, 100));
        Assert.Throws<NotSupportedException>(() => SourceBattleDamage.Calculate(catalog, user, target, move, false, 100, sourceDefenderCount: 3));
    }
}
