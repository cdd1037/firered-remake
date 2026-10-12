namespace Game.Content;

/// <summary>Independent pinned source package; unchanged inherited packages remain prerequisites.</summary>
internal static class SaffronGymSource
{
    internal static SilphSourcePackage Catalogs(string root) => new(root, "saffron-gym-catalogs", "firered-saffron-gym-catalogs-v1",
        "scripts/saffron-gym-catalogs.lock.json", "1ca3411bb87ffbd7d0129124dc85b87d17d5b725807df1383cb85811d2428b6c", 9187,
        "5ac8f7f86a150fff44d9a9e1ee2bcb145afd941e0844fceac1a34637f081aa47", 1345, 31, 5);
}
