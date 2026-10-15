namespace Game.Content;

/// <summary>Independent pinned source evidence, with unchanged predecessor helpers.</summary>
internal static class CinnabarSource
{
    internal static SilphSourcePackage Region(string root) => new(root,"cinnabar-source-preparation");
    internal static SilphSourcePackage Catalogs(string root) => new(root,"cinnabar-catalogs");
}
