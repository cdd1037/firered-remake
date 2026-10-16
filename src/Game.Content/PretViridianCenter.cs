using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>
/// A separately authenticated Pokemon Center 1F source increment. Source availability does not
/// certify script handlers, healing animation, audio, PC storage, upstairs or link play.
/// </summary>
public sealed class PretViridianCenter
{
    public const string Profile = "firered-viridian-center-import-v1";
    public const string StableMapId = "map.viridian-pokemon-center-1f";
    public PretWorldMap Map { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableDictionary<string, string> SourceUiTexts { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string ServiceSourcePath => Path.Combine(GeneratedDirectory, "service.json");

    private PretViridianCenter(string directory, PretWorldMap map, ImmutableDictionary<string, int> constants,
        ImmutableDictionary<string, string> uiTexts)
    {
        GeneratedDirectory = directory; Map = map;
        Constants = constants; SourceUiTexts = uiTexts;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => PretViridianRegion.MapIdFromSourceSymbol(symbol);

    public static PretViridianCenter Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/viridian-center");
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1, "Center manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if(runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps,"maps/regions/viridian-center/"+path))continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate center output.");
        }
        using var catalog = JsonDocument.Parse(outputs["center-catalog.json"]);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Viridian Pokemon Center 1F; runtime certification is separate.", Profile);
        var spec = catalog.RootElement.GetProperty("map");
        var relative = spec.GetProperty("mapPath").GetString()!;
        PretWorldMap map;
        if(runtimeMaps is not null)
        {
            var compact=runtimeMaps.Maps[new MapId(spec.GetProperty("stableId").GetString()!)];
            map=PretWorldMap.FromRuntime(compact,metadata,SafeFile(directory,relative),PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"),MapIdFromSourceSymbol);
        }
        else
        {
            map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, PretOverworldTextures.Resolve(directory, "sprites/overworld/red-normal.png"), MapIdFromSourceSymbol);
        }
        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["HEAL_LOCATION_VIRIDIAN_CITY"] == 2 && constants["LOCALID_VIRIDIAN_NURSE"] == 1 &&
            constants["FLDEFF_POKECENTER_HEAL"] == 25 && constants["SCR_MENU_CANCEL"] == 127, "Center required source constants differ.");
        var storyTexts = PretStoryRuntime.Load(projectRoot).TextBank;
        var uiTexts = ImmutableDictionary<string, string>.Empty.Add("gText_PlayerScurriedToCenter", storyTexts["gText_PlayerScurriedToCenter"]);
        return new(directory, map, constants.ToImmutable(), uiTexts);
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe center path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Center directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Center file symlink is unsupported: " + path);
        }
        return result;
    }
}
