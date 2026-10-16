using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;

namespace Game.Content;

/// <summary>
/// Authenticated Vermilion Gym map, actors, two-switch puzzle, barrier metatiles, Surge and field Cut source contracts. This independent content profile registers
/// no executable handlers and does not certify battles, services, native gameplay or audio.
/// </summary>
public sealed class PretVermilionGymRegion
{
    public const string Profile = "firered-vermilion-gym-region-import-v1";
    public ImmutableDictionary<MapId, PretWorldMap> NewMaps { get; }
    public ImmutableDictionary<string, int> Constants { get; }
    public JsonElement BeamStates { get; }
    public ImmutableDictionary<int, PretMartMetatileDefinition> BeamDefinitions { get; }

    public JsonElement ExternalVermilionEvents { get; }
    public string GeneratedDirectory { get; }

    private PretVermilionGymRegion(string directory, ImmutableDictionary<MapId, PretWorldMap> maps, ImmutableDictionary<string, int> constants,
        JsonElement externalVermilionEvents, JsonElement beamStates,
        ImmutableDictionary<int, PretMartMetatileDefinition> beamDefinitions)
    {
        GeneratedDirectory = directory; NewMaps = maps;
        Constants = constants;
         ExternalVermilionEvents = externalVermilionEvents;
        BeamStates = beamStates; BeamDefinitions = beamDefinitions;
    }

    public static MapId MapIdFromSourceSymbol(string symbol) => symbol switch
    {
        "MAP_VERMILION_CITY_GYM" => new("map.vermilion-city-gym"),
        _ => PretVermilionRegion.MapIdFromSourceSymbol(symbol)
    };



    public static PretVermilionGymRegion Load(PretCurrentWorld world)
    {
        var projectRoot = world.ProjectRoot;
        var project = Path.GetFullPath(projectRoot);
        var directory = Path.Combine(project, "content/generated/maps/regions/vermilion-gym");

        byte[] ReadPinned(string root,string path)
        {
            var bytes = File.ReadAllBytes(SafeFile(root, path));
            return bytes;
        }
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["beam-metatiles.json"] = ReadPinned(directory, "beam-metatiles.json"),
        };
        var maps = world.MapsInDirectory(directory);

        var constants = PretSharedConstants.ForPackage(directory).ToBuilder();
        Require(constants["FLAG_TEMP_1"] == 1 && constants["FLAG_TEMP_12"] == 18 &&
            constants["FOUND_FIRST_SWITCH"] == 1 && constants["SWITCH1_ID"] == 0x8004 &&
            constants["SWITCH2_ID"] == 0x8005 && constants["TRASH_CAN_ID"] == 0x8008 &&
            constants["VAR_TEMP_0"] == 0x4000 && constants["VAR_TEMP_1"] == 0x4001,
            "Vermilion Gym puzzle/Cut constants differ.");
        using var beams = JsonDocument.Parse(outputs["beam-metatiles.json"]);
        var beamDefinitions = ReadBeams(beams.RootElement, outputs, directory);
        using var cityEvents = JsonDocument.Parse(world.ReadEvents("data/maps/VermilionCity/map.json"));
        return new(directory, maps, constants.ToImmutable(), cityEvents.RootElement.Clone(),
            beams.RootElement.GetProperty("states").Clone(), beamDefinitions);
    }

    private static ImmutableDictionary<int, PretMartMetatileDefinition> ReadBeams(JsonElement beams,
        Dictionary<string, byte[]> outputs, string directory)
    {
        Require(beams.GetProperty("schemaVersion").GetInt32() == 1 &&
            beams.GetProperty("mapId").GetString() == "map.vermilion-city-gym" && beams.GetProperty("preserveSourceElevation").GetBoolean(),
            "Vermilion Gym beam identity differs.");
        var definitions = ImmutableDictionary.CreateBuilder<int, PretMartMetatileDefinition>();
        foreach (var tile in beams.GetProperty("definitions").EnumerateArray())
        {
            var id = tile.GetProperty("rawMetatileId").GetInt32();
            var attribute = tile.GetProperty("rawAttributes").GetUInt32();
            var behavior = (int)(attribute & 511);
            var terrain = (int)((attribute >> 9) & 31);
            var encounter = (int)((attribute >> 24) & 7);
            var layer = (int)((attribute >> 29) & 3);
            Require(tile.GetProperty("rawAttributes").GetUInt32() == attribute && tile.GetProperty("behavior").GetInt32() == behavior &&
                tile.GetProperty("terrain").GetInt32() == terrain && tile.GetProperty("encounterType").GetInt32() == encounter &&
                tile.GetProperty("layerType").GetInt32() == layer && layer is >= 0 and <= 2, "Vermilion Gym beam attributes differ.");
            var values = tile.GetProperty("tileEntries").EnumerateArray().Select(e => e.GetUInt16()).ToImmutableArray();
            Require(values.Length == 8, "Vermilion Gym beam tile count differs.");
            string Texture(string name)
            {
                var path = tile.GetProperty(name + "TexturePath").GetString()!;
                Require(path == $"beam-metatiles/{id:x3}-{name}.png", "Vermilion Gym beam image path differs.");
                var png = File.ReadAllBytes(SafeFile(directory, path));
                Require(png.Length > 33 && png.AsSpan(0, 8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }) &&
                    BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)) == 16 && BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)) == 16, "Vermilion Gym beam image format differs.");
                return SafeFile(directory, path);
            }
            definitions.Add(id, new(id, attribute, behavior, behavior == 0 ? "MB_NORMAL" : "MB_" + behavior, terrain, encounter, layer, values,
                Texture("bottom"), Texture("middle"), Texture("top")));
        }
        Require(definitions.Count == 29, "Vermilion Gym beam inventory differs.");
        var states = beams.GetProperty("states").EnumerateArray().ToArray();
        Require(states.Select(s => s.GetProperty("state").GetString()).SequenceEqual(["on", "half", "off"]), "Vermilion Gym beam state order differs.");
        foreach (var state in states)
        {
            var patches = state.GetProperty("patches").EnumerateArray().ToArray();
            Require(patches.Length == 10, "Vermilion Gym beam state must replace ten cells.");
            var coordinates = new HashSet<(int, int)>();
            foreach (var patch in patches)
            {
                int x = patch.GetProperty("x").GetInt32(), y = patch.GetProperty("y").GetInt32();
                int id = patch.GetProperty("rawMetatileId").GetInt32(), collision = patch.GetProperty("collision").GetInt32();
                Require(x is >= 3 and <= 7 && y is 6 or 7 && coordinates.Add((x, y)) && definitions.ContainsKey(id), "Vermilion Gym beam coordinates differ.");
                var block = patch.GetProperty("sourceRawBlock").GetUInt16();
                Require(collision == (patch.GetProperty("isImpassable").GetBoolean() ? 3 : 0) &&
                    patch.GetProperty("sourceRawBlock").GetUInt16() == block && patch.GetProperty("sourceElevation").GetInt32() == block >> 12 &&
                    patch.GetProperty("resultingRawBlock").GetUInt16() == ((block & 0xf000) | id | (collision << 10)), "Vermilion Gym beam block semantics differ.");
                Require((state.GetProperty("state").GetString() == "off" && x is >= 4 and <= 6) ? collision == 0 : collision == 3,
                    "Vermilion Gym barrier opening differs.");
            }
        }
        return definitions.ToImmutable();
    }
    private static void Require(bool value, string error) { if (!value) throw new InvalidDataException(error); }
    private static string SafeFile(string root, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path) || path.Contains('\\') || path.Split('/').Any(x => x is "" or "." or ".."))
            throw new InvalidDataException("Unsafe Vermilion Gym path: " + path);
        for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
            if (cursor.LinkTarget is not null) throw new InvalidDataException("Vermilion Gym directory symlink is unsupported.");
        var result = root;
        foreach (var segment in path.Split('/'))
        {
            result = Path.Combine(result, segment);
            if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                throw new InvalidDataException("Vermilion Gym file symlink is unsupported: " + path);
        }
        return result;
    }
}
