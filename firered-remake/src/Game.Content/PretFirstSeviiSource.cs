namespace Game.Content;

/// <summary>Immutable local evidence for the original pre-League One/Two/Three Island visit.</summary>
internal static class FirstSeviiSource
{
    internal static SilphSourcePackage Region(string root) => new(root, "first-sevii-region", PretFirstSeviiRegion.Profile,
        "content/sources/first-sevii-region.lock.json", PretFirstSeviiRegion.LockSha256, 123609,
        PretFirstSeviiRegion.ManifestSha256, 28752, 387, 166);
}
