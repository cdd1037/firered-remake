namespace Game.Content;

/// <summary>Immutable local source evidence for Five Island and Rocket Warehouse.</summary>
internal static class FiveIslandSource
{
    internal static SilphSourcePackage Region(string root) => new(root, "five-island-region", PretFiveIslandRegion.Profile,
        "content/sources/five-island-region.lock.json", PretFiveIslandRegion.LockSha256, 64506,
        PretFiveIslandRegion.ManifestSha256, 11677, 208, 65);
}
