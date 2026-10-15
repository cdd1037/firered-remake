namespace Game.Content;

/// <summary>Independent pinned source evidence, with unchanged predecessor helpers.</summary>
internal static class CinnabarSource
{
    internal static SilphSourcePackage Region(string root) => new(root,"maps/regions/cinnabar");
    internal static SilphSourcePackage Catalogs(string root) => new(root,"data/regions/cinnabar");
}
