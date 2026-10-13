namespace Game.Content;

/// <summary>Immutable local source evidence for Four Island and Icefall Cave.</summary>
internal static class FourIslandSource
{
    internal static SilphSourcePackage Region(string root) => new(root, "four-island-region", PretFourIslandRegion.Profile,
        "content/sources/four-island-region.lock.json", PretFourIslandRegion.LockSha256, 74234,
        PretFourIslandRegion.ManifestSha256, 18023, 239, 101);
}
