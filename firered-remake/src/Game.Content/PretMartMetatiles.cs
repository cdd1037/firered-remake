using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Content;

/// <summary>Original tileset attributes and three separate BG layers; collision/elevation belong to map blocks.</summary>
public sealed record PretMartMetatileDefinition(
    int RawMetatileId, uint RawAttributes, int Behavior, string BehaviorName, int Terrain,
    int EncounterType, int LayerType, ImmutableArray<ushort> TileEntries,
    string BottomTexturePath, string MiddleTexturePath, string TopTexturePath);

/// <summary>Source OnLoad setmetatile operands; the application preserves the current cell's elevation.</summary>
public sealed record PretMartMetatilePatch(int X, int Y, int RawMetatileId, bool IsImpassable,
    int Collision, ushort SourceRawBlock, int SourceElevation, ushort ResultingRawBlock);

/// <summary>Only the two source Mart counter replacements. Does not mutate or replace source maps.</summary>
public sealed class PretMartMetatiles
{
    public const string Profile = "firered-mart-metatiles-v1";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string MapId = "map.viridian-mart";
    public ImmutableDictionary<int, PretMartMetatileDefinition> Definitions { get; }
    public ImmutableArray<PretMartMetatilePatch> OnLoadPatches { get; }
    public string GeneratedDirectory { get; }

    private PretMartMetatiles(string directory, ImmutableDictionary<int, PretMartMetatileDefinition> definitions,
        ImmutableArray<PretMartMetatilePatch> patches)
    { GeneratedDirectory = directory; Definitions = definitions; OnLoadPatches = patches; }

    public static PretMartMetatiles Load(string projectRoot)
    {
        var project = Path.GetFullPath(projectRoot);
        var rawDirectory = Path.Combine(project, PretSourcePaths.FireRedRawDirectory);
        var directory = Path.Combine(project, "content/generated/mart-metatiles");
        static void Require(bool value, string message)
        { if (!value) throw new InvalidDataException(message); }
        static string SafeFile(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains('\\') ||
                relative.Split('/').Any(x => x is "" or "." or ".."))
                throw new InvalidDataException("Unsafe Mart metatile path: " + relative);
            for (var cursor = new DirectoryInfo(root); cursor is not null; cursor = cursor.Parent)
                if (cursor.LinkTarget is not null) throw new InvalidDataException("Mart directory symlink is unsupported.");
            var result = root;
            foreach (var segment in relative.Split('/'))
            {
                result = Path.Combine(result, segment);
                if (new FileInfo(result).LinkTarget is not null || new DirectoryInfo(result).LinkTarget is not null)
                    throw new InvalidDataException("Mart file symlink is unsupported: " + relative);
            }
            return result;
        }
        static byte[] Pinned(string root, string path)
        {
            return File.ReadAllBytes(SafeFile(root, path));
        }
        using var sourceLock = JsonDocument.Parse(Pinned(project,"scripts/mart-metatiles.lock.json"));
        using var manifest = JsonDocument.Parse(Pinned(directory,"manifest.json"));
        var source = sourceLock.RootElement;
        var m = manifest.RootElement;
        Require(m.GetProperty("schemaVersion").GetInt32() == 1 , "Mart manifest identity differs.");
        var raw = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in source.GetProperty("files").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
                    var bytes = Pinned(rawDirectory,path);
            Require(raw.TryAdd(path, bytes), "Duplicate Mart source path.");
        }
        var outputs = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in m.GetProperty("outputs").EnumerateArray())
        {
            var path = entry.GetProperty("path").GetString()!;
                    Require(outputs.TryAdd(path, Pinned(directory,path)), "Duplicate Mart output path.");
        }
        using var metadata = JsonDocument.Parse(outputs["metatiles.json"]);
        var catalog = metadata.RootElement;
                Require(            catalog.GetProperty("mapId").GetString() == MapId && catalog.GetProperty("tileSize").GetInt32() == 16 &&
                    catalog.GetProperty("preserveSourceElevation").GetBoolean() && catalog.GetProperty("metatiles").GetArrayLength() == 2 , "Mart metadata differs.");
        var definitions = ImmutableDictionary.CreateBuilder<int, PretMartMetatileDefinition>();
        var attributes = raw["data/tilesets/secondary/mart/metatile_attributes.bin"];
        var sourceTiles = raw["data/tilesets/secondary/mart/metatiles.bin"];
        foreach (var tile in catalog.GetProperty("metatiles").EnumerateArray())
        {
            int id = tile.GetProperty("rawMetatileId").GetInt32();
            Require(id is 0x2bf or 0x2c0, "Metatile is outside the bounded Mart patch set.");
            int local = id - 640;
            uint attr = BinaryPrimitives.ReadUInt32LittleEndian(attributes.AsSpan(local * 4, 4));
            Require(tile.GetProperty("rawAttributes").GetUInt32() == attr, "Mart raw attributes differ.");
            int behavior = (int)(attr & 511), terrain = (int)((attr >> 9) & 31), encounter = (int)((attr >> 24) & 7), layer = (int)((attr >> 29) & 3);
            Require(tile.GetProperty("behavior").GetInt32() == behavior && tile.GetProperty("terrain").GetInt32() == terrain &&
                tile.GetProperty("encounterType").GetInt32() == encounter && tile.GetProperty("layerType").GetInt32() == layer,
                "Mart decoded rule attributes differ.");
            var tileEntries = ImmutableArray.CreateBuilder<ushort>();
            var values = tile.GetProperty("tileEntries").EnumerateArray().ToArray();
            Require(values.Length == 8, "Mart tile count differs.");
            for (int i = 0; i < 8; i++)
            {
                var value = BinaryPrimitives.ReadUInt16LittleEndian(sourceTiles.AsSpan(local * 16 + i * 2, 2));
                Require(values[i].GetInt32() == value, "Mart source tile entry differs.");
                tileEntries.Add(value);
            }
            string Texture(string name)
            {
                var path = tile.GetProperty(name + "TexturePath").GetString()!;
                Require(path == $"{id:x3}-{name}.png", "Unexpected Mart texture path.");
                var png = outputs[path];
                        Require(png.Length > 33 && png.AsSpan(0, 8).SequenceEqual(new byte[] { 137,80,78,71,13,10,26,10 }) &&
                    BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)) == 16 &&
                    BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)) == 16 && png[24] == 8 && png[25] == 6,
                    "Expected 16 by 16 RGBA Mart texture.");
                return SafeFile(directory, path);
            }
            definitions.Add(id, new(id, attr, behavior, tile.GetProperty("behaviorName").GetString()!, terrain, encounter,
                layer, tileEntries.ToImmutable(), Texture("bottom"), Texture("middle"), Texture("top")));
        }
        Require(definitions.Count == 2, "Missing Mart metatile definition.");
        var patches = ImmutableArray.CreateBuilder<PretMartMetatilePatch>();
        var sourceBlocks = raw["data/layouts/Mart/map.bin"];
        foreach (var patch in catalog.GetProperty("onLoadPatches").EnumerateArray())
        {
            int x = patch.GetProperty("x").GetInt32(), y = patch.GetProperty("y").GetInt32(), id = patch.GetProperty("rawMetatileId").GetInt32();
            Require(x == 1 && ((y == 3 && id == 0x2bf) || (y == 4 && id == 0x2c0)), "Unexpected Mart patch coordinate.");
            var block = BinaryPrimitives.ReadUInt16LittleEndian(sourceBlocks.AsSpan((y * 11 + x) * 2, 2));
            var result = (ushort)((block & 0xf000) | id | 0xc00);
            Require(patch.GetProperty("sourceRawBlock").GetUInt16() == block && patch.GetProperty("sourceElevation").GetInt32() == block >> 12 &&
                patch.GetProperty("isImpassable").GetBoolean() && patch.GetProperty("collision").GetInt32() == 3 &&
                patch.GetProperty("resultingRawBlock").GetUInt16() == result, "Mart patch block semantics differ.");
            patches.Add(new(x, y, id, true, 3, block, block >> 12, result));
        }
        Require(patches.Count == 2 && patches.Select(x => x.RawMetatileId).Distinct().Count() == 2, "Mart patch set differs.");
        return new(directory, definitions.ToImmutable(), patches.ToImmutable());
    }
}
