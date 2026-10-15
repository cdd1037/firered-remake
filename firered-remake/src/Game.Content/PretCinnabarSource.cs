namespace Game.Content;

/// <summary>Independent pinned source evidence, with unchanged predecessor helpers.</summary>
internal static class CinnabarSource
{
    internal static SilphSourcePackage Region(string root) => new(root,"cinnabar-source-preparation",        "content/sources/cinnabar-source-preparation.lock.json");
    internal static SilphSourcePackage Catalogs(string root) => new(root,"cinnabar-catalogs",        "scripts/cinnabar-catalogs.lock.json");
}
