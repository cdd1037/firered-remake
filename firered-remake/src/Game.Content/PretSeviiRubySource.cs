namespace Game.Content;

/// <summary>Immutable local evidence for Kindle Road and the original Mt. Ember Ruby route.</summary>
internal static class SeviiRubySource
{
    internal static SilphSourcePackage Region(string root) => new(root, "sevii-ruby-region", PretSeviiRubyRegion.Profile,
        "content/sources/sevii-ruby-region.lock.json", PretSeviiRubyRegion.LockSha256, 62397,
        PretSeviiRubyRegion.ManifestSha256, 22386, 200, 125);
}
