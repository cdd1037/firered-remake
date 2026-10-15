namespace Game.Content;

/// <summary>Independent pinned source package; unchanged inherited packages remain prerequisites.</summary>
internal static class SaffronGymSource
{
    internal static SilphSourcePackage Catalogs(string root) => new(root,"saffron-gym-catalogs",        "scripts/saffron-gym-catalogs.lock.json");
}
