using System.Collections.Immutable;
using System.Text.Json;
namespace Game.Content;

/// <summary>120 pinned maps; all prior 114 entries, payloads and source registries remain exact.</summary>
public static class PretRocketHideoutRuntimeMaps
{
    public const string DirectoryName = "content/generated/rocket-hideout-runtime-maps";
    public const string ManifestSha256 = "ee99ed635f7355c82f26373274caf3d1bc2706e7329f4d17383d3fc6b614c56b";
    public const string ConverterSha256 = "be4fcdc7cb9551557596109335edd918cd40a1498012f47111d14b968dbb7bff";
    public static PretRuntimeMapCatalog Load(string projectRoot)
    {
        var directory=Path.Combine(Path.GetFullPath(projectRoot),DirectoryName);RequireNoLinks(directory,"manifest.json");
        return PretRuntimeMapCatalog.Load(directory,ManifestSha256);
    }
    internal static void ValidatePackage(string directory,ImmutableArray<PretRuntimeMapEntry> entries)
    {
        PretCeladonGymRuntimeMaps.ValidatePackage(directory,entries.Where(e=>e.FullSourceHash!=PretRocketHideoutRegion.FullSourceHash).ToImmutableArray());
        foreach(var e in entries)RequireNoLinks(directory,e.Path);
        RequireNoLinks(directory,"rocket-hideout-region-source-lock.json");
        var b=PretRuntimeMapValidation.ReadBounded(Path.Combine(directory,"rocket-hideout-region-source-lock.json"),256*1024);
        PretRuntimeMapValidation.Require(PretRuntimeMapValidation.Hash(b)==PretRocketHideoutRegion.LockSha256,"Rocket runtime source lock differs.");
        using var source=JsonDocument.Parse(b);
        PretRuntimeMapValidation.Require(source.RootElement.GetProperty("commit").GetString()==PretWorldMap.SourceCommit &&
            source.RootElement.GetProperty("tree").GetString()==PretCeladonApproachSource.Tree && source.RootElement.GetProperty("files").GetArrayLength()==175,"Rocket source registry differs.");
        var regional=entries.Where(e=>e.FullSourceHash==PretRocketHideoutRegion.FullSourceHash).ToArray();
        var expected=new[]{"map.celadon-city-game-corner","map.rocket-hideout-b1f","map.rocket-hideout-b2f","map.rocket-hideout-b3f","map.rocket-hideout-b4f","map.rocket-hideout-elevator"};
        PretRuntimeMapValidation.Require(entries.Length==120 && regional.Length==6 && regional.Select(e=>e.StableId).ToHashSet(StringComparer.Ordinal).SetEquals(expected) &&
            regional.All(e=>e.Path=="rocket-hideout-region/"+e.StableId[4..]+"/runtime-map.json"),"Rocket runtime map registry differs.");
    }
    internal static void RequireNoLinks(string directory,string relative)
    {
        var path=PretRuntimeMapValidation.SafeFile(directory,relative);
        for(string? current=path;current is not null;current=Path.GetDirectoryName(current))
            PretRuntimeMapValidation.Require((File.GetAttributes(current)&FileAttributes.ReparsePoint)==0,"Rocket runtime symlink rejected.");
    }
}
