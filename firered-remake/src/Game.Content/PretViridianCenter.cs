using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
    public ImmutableArray<PretSourceScriptFile> Scripts { get; }
    public ImmutableDictionary<string, string> TextSources { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public ImmutableArray<PretWorldSourceFile> SourceFiles { get; }
    public ImmutableDictionary<string, string> SourceUiTexts { get; }
    public string GeneratedDirectory { get; }
    public string NpcSourcePath => Path.Combine(GeneratedDirectory, "npc-source.json");
    public string ServiceSourcePath => Path.Combine(GeneratedDirectory, "service.json");

    private PretViridianCenter(string directory, PretWorldMap map, ImmutableArray<PretSourceScriptFile> scripts,
        ImmutableDictionary<string, string> texts, ImmutableDictionary<string, int> constants,
        ImmutableArray<PretWorldSourceFile> files, ImmutableDictionary<string, string> uiTexts)
    {
        GeneratedDirectory = directory; Map = map; Scripts = scripts; TextSources = texts;
        Constants = constants; SourceFiles = files; SourceUiTexts = uiTexts;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => PretViridianRegion.MapIdFromSourceSymbol(symbol);

    public static PretViridianCenter Load(string projectRoot, PretRuntimeMapCatalog? runtimeMaps = null)
    {
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/viridian-center");
        var rawDirectory = Path.Combine(project, PretSourcePaths.FireRedRawDirectory);
        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        using var manifest = JsonDocument.Parse(ReadPinned(directory,"manifest.json"));
        using var sourceLock = JsonDocument.Parse(ReadPinned(project,"scripts/viridian-center.lock.json"));
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 &&
            m.GetProperty("unresolvedLabelCount").GetInt32() == 0, "Center manifest identity differs.");
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var output in m.GetProperty("outputs").EnumerateArray())
        {
            var path = output.GetProperty("path").GetString()!;
            if(runtimeMaps is not null && PretRuntimeMapIntegration.Replaces(runtimeMaps,"viridian-center/"+path))continue;
            Require(outputs.TryAdd(path, ReadPinned(directory,path)), "Duplicate center output.");
        }
        using var catalog = JsonDocument.Parse(outputs["center-catalog.json"]);
        var files = ImmutableArray.CreateBuilder<PretWorldSourceFile>();
        var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in sourceLock.RootElement.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
            var bytes = ReadPinned(rawDirectory,path);
            Require(raw.TryAdd(path, bytes), "Duplicate center source.");
            files.Add(new(path,$"https://raw.githubusercontent.com/pret/pokefirered/{PretWorldMap.SourceCommit}/{path}"));
        }
        var inventory = files.ToImmutable();
        var byPath = inventory.ToDictionary(f => f.Path, StringComparer.Ordinal);
        var metadata = new PretWorldMetadata("pret/pokefirered", PretWorldMap.SourceCommit,             "Local source evidence; asset redistribution rights are not inferred.",
            "Authenticated Viridian Pokemon Center 1F; runtime certification is separate.", Profile, inventory);
        var spec = catalog.RootElement.GetProperty("map");
        var relative = spec.GetProperty("directory").GetString()!;
        PretWorldMap map;
        if(runtimeMaps is not null)
        {
            var compact=runtimeMaps.Maps[new MapId(spec.GetProperty("stableId").GetString()!)];
            map=PretWorldMap.FromRuntime(compact,metadata,SafeFile(directory,relative),SafeFile(directory,"npc/red-normal.png"),MapIdFromSourceSymbol);
        }
        else
        {
            map = PretWorldMap.LoadRegion(SafeFile(directory, relative), spec.GetProperty("stableId").GetString()!,
                metadata, SafeFile(directory, "npc/red-normal.png"), MapIdFromSourceSymbol);
        }
        using var scriptInventory = JsonDocument.Parse(outputs["scripts.json"]);
        Require(scriptInventory.RootElement.GetProperty("unresolvedLabelReferences").GetArrayLength() == 0,
            "Center script reference closure is incomplete.");
        var scripts = ImmutableArray.CreateBuilder<PretSourceScriptFile>();
        var texts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var property in scriptInventory.RootElement.GetProperty("filePreambles").EnumerateObject())
        {
            var path = property.Name;
            var text = Encoding.UTF8.GetString(raw[path]);
            if (path.EndsWith("/text.inc", StringComparison.Ordinal) || path.StartsWith("data/text/", StringComparison.Ordinal)) texts.Add(path, text);
            else
            {
                var symbol = Regex.Match(text, @"^(\w+)::?", RegexOptions.Multiline | RegexOptions.CultureInvariant).Groups[1].Value;
                Require(symbol.Length > 0, "Center script has no original source label: " + path);
                scripts.Add(new(path, text, new(SourceKind.Pret,path,symbol,"pret/pokefirered",PretWorldMap.SourceCommit)));
            }
        }
        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["HEAL_LOCATION_VIRIDIAN_CITY"] == 2 && constants["LOCALID_VIRIDIAN_NURSE"] == 1 &&
            constants["FLDEFF_POKECENTER_HEAL"] == 25 && constants["SCR_MENU_CANCEL"] == 127, "Center required source constants differ.");
        var uiTexts = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var centerText = Regex.Match(Encoding.UTF8.GetString(raw["src/strings.c"]),
            "const u8 gText_PlayerScurriedToCenter\\[\\] = _\\(\"((?:\\\\.|[^\"\\\\])*)\"\\);", RegexOptions.CultureInvariant);
        Require(centerText.Success, "Missing source center whiteout message.");
        uiTexts.Add("gText_PlayerScurriedToCenter", centerText.Groups[1].Value + "$");
        return new(directory, map, scripts.ToImmutable(), texts.ToImmutable(), constants.ToImmutable(), inventory, uiTexts.ToImmutable());
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
