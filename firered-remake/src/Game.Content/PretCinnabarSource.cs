namespace Game.Content;

/// <summary>Independent pinned source evidence, with unchanged predecessor helpers.</summary>
internal static class CinnabarSource
{
    internal static SilphSourcePackage Region(string root) => new(root, "cinnabar-source-preparation", PretCinnabarRegion.Profile,
        "content/sources/cinnabar-source-preparation.lock.json", PretCinnabarRegion.LockSha256, 74460,
        PretCinnabarRegion.ManifestSha256, 21041, 241, 122);
    internal static SilphSourcePackage Catalogs(string root) => new(root, "cinnabar-catalogs", "firered-cinnabar-catalogs-v1",
        "scripts/cinnabar-catalogs.lock.json", "c3c583fec333419d459fef92b1b77ab9d8028f3ac450371e9a6202838cd92171", 7513,
        "da358852c02d2883c24e2aa17163120f28655712ea33c5cee6968e9631f7f007", 882, 25, 2);
}
