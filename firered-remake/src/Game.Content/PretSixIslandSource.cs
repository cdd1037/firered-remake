namespace Game.Content;

/// <summary>Immutable local source evidence for Six Island and Dotted Hole.</summary>
internal static class SixIslandSource
{
    internal static SilphSourcePackage Region(string root) => new(root,"six-island-region",        "content/sources/six-island-region.lock.json");
}
