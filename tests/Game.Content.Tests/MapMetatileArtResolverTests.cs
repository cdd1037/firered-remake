using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class MapMetatileArtResolverTests
{
    private sealed record Pixels(string Asset, int X, int Y);

    [TestMethod]
    public void SameMapLayoutAndTileResolveIdenticallyForLiveRecapAndNeighborProjections()
    {
        var atlas = new Pixels("door-atlas", 32, 16);
        var resolver = new MapMetatileArtResolver<Pixels>(
            new Dictionary<(string, int, int), Pixels> { [("map.door", 672, 1)] = atlas },
            new Dictionary<(string, int, int), Pixels>());
        // Live map, historical main map and translated neighbor coordinates all
        // supply the destination map/layout. Position and current save flags cannot
        // affect the source pixels, which are selected by the same contract.
        var main = resolver.Resolve("map.door", "layout.door", 672, 1, () => new("live-original", 0, 0));
        var recap = resolver.Resolve("map.door", "layout.door", 672, 1, () => new("recap-original", 16, 0));
        var neighbor = resolver.Resolve("map.door", "layout.door", 672, 1, () => new("neighbor-original", 0, 16));
        Assert.AreSame(atlas, main);
        Assert.AreSame(main, recap);
        Assert.AreSame(main, neighbor);
    }

    [TestMethod]
    public void LayoutBindingWinsAndNeverLeaksIntoAnotherMapOrHistoricalLayout()
    {
        var map = new Pixels("map", 0, 0);
        var cut = new Pixels("cut", 16, 32);
        var original = new Pixels("original", 48, 64);
        var resolver = new MapMetatileArtResolver<Pixels>(
            new Dictionary<(string, int, int), Pixels> { [("map.one", 14, 1)] = map },
            new Dictionary<(string, int, int), Pixels> { [("layout.grass", 14, 1)] = cut });
        Assert.AreSame(cut, resolver.Resolve("map.one", "layout.grass", 14, 1, () => throw new AssertFailedException()));
        Assert.AreSame(cut, resolver.Resolve("map.two", "layout.grass", 14, 1, () => original));
        Assert.AreSame(map, resolver.Resolve("map.one", "layout.indoor", 14, 1, () => original));
        Assert.AreSame(original, resolver.Resolve("map.two", "layout.indoor", 14, 1, () => original));
        Assert.AreSame(original, resolver.Resolve("map.one", "layout.grass", 14, 2, () => original));
    }

    [TestMethod]
    public void UnboundTilesUseOnlyTheSuppliedSourceAndMissingArtFails()
    {
        var resolver = new MapMetatileArtResolver<Pixels>(new Dictionary<(string, int, int), Pixels>(), new Dictionary<(string, int, int), Pixels>());
        var live = new Pixels("live-layout", 16, 32);
        var historical = new Pixels("historical-layout", 32, 48);
        Assert.AreSame(live, resolver.Resolve("map.tower", "layout.live", 42, 0, () => live));
        Assert.AreSame(historical, resolver.Resolve("map.tower", "layout.saved", 42, 0, () => historical));
        Assert.ThrowsExactly<InvalidDataException>(() => resolver.Resolve("map.other", "layout.other", 42, 0, () => null));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => resolver.Resolve("map.tower", "layout.saved", 1024, 0, () => live));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => resolver.Resolve("map.tower", "layout.saved", 42, 3, () => live));
    }

    [TestMethod]
    public void CompositionCopiesBindingsAndRejectsInvalidLayers()
    {
        var maps = new Dictionary<(string, int, int), Pixels> { [("map.one", 7, 0)] = new("original", 0, 0) };
        var resolver = new MapMetatileArtResolver<Pixels>(maps, new Dictionary<(string, int, int), Pixels>());
        maps[("map.one", 7, 0)] = new("mutated", 0, 0);
        Assert.AreEqual("original", resolver.Resolve("map.one", "layout", 7, 0, () => null).Asset);
        maps.Add(("map.one", 8, -1), new("invalid", 0, 0));
        Assert.ThrowsExactly<InvalidDataException>(() => new MapMetatileArtResolver<Pixels>(maps, new Dictionary<(string, int, int), Pixels>()));
    }
}
