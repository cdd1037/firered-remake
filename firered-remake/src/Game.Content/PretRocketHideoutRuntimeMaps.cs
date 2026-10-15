using System.Collections.Immutable;
using System.Text.Json;
namespace Game.Content;

/// <summary>120 pinned maps; all prior 114 entries, payloads and source registries remain exact.</summary>
public static class PretRocketHideoutRuntimeMaps
{
    public const string DirectoryName = "content/generated/rocket-hideout-runtime-maps";
    public static PretRuntimeMapCatalog Load(string projectRoot)
    {
        var directory=Path.Combine(Path.GetFullPath(projectRoot),DirectoryName);RequireNoLinks(directory,"manifest.json");
        return PretRuntimeMapCatalog.Load(directory);
    }
    internal static void RequireNoLinks(string directory,string relative)
    {
        var path=PretRuntimeMapValidation.SafeFile(directory,relative);
        for(string? current=path;current is not null;current=Path.GetDirectoryName(current))
            PretRuntimeMapValidation.Require((File.GetAttributes(current)&FileAttributes.ReparsePoint)==0,"Rocket runtime symlink rejected.");
    }
}
