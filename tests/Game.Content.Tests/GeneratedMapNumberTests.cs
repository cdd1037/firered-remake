using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class GeneratedMapNumberTests
{
    [TestMethod]
    public void CompositionSharesOneTableWithoutChangingProjectionOrValidation()
    {
        var source = PretCelioMapNumbers.LoadSourceTable(TestAssets.Root);
        var first = PretPowerPlantRegion.Load(PretCurrentWorld.Load(TestAssets.Root)).NewMaps;
        var additions = PretFossilLabRegion.Load(PretCurrentWorld.Load(TestAssets.Root)).NewMaps;
        var maps = first.AddRange(additions);
        var predecessor = PretCelioMapNumbers.Load(source, first);
        var current = PretCelioMapNumbers.ExtendAuthenticatedMaps(source, maps, predecessor, additions);
        var independent = PretCelioMapNumbers.Load(TestAssets.Root, maps);
        CollectionAssert.AreEquivalent(independent.ToArray(), current.ToArray());
        foreach (var pair in predecessor) Assert.AreEqual(pair.Value, current[pair.Key]);
        Assert.ThrowsExactly<InvalidDataException>(() => PretCelioMapNumbers.Load(source.Clear(), maps));
        Assert.ThrowsExactly<InvalidDataException>(() => PretCelioMapNumbers.ExtendAuthenticatedMaps(source, maps, predecessor, ImmutableDictionary<MapId, PretWorldMap>.Empty));
        var aliases = source.ToImmutableDictionary(p => p.Key, p => predecessor.Values.First());
        Assert.ThrowsExactly<InvalidDataException>(() => PretCelioMapNumbers.Load(aliases, maps));
        var wrongPredecessor = predecessor.SetItem(predecessor.Keys.First(), new(999, 999));
        Assert.ThrowsExactly<InvalidDataException>(() => PretCelioMapNumbers.ExtendAuthenticatedMaps(source, maps, wrongPredecessor, additions));
    }
}
