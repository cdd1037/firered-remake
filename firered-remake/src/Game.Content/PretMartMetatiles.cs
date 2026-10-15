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
        var directory = Path.Combine(projectRoot, "content/generated/maps/objects/mart-metatiles");
        using var metadata = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory, "metatiles.json")));
        var catalog = metadata.RootElement;
        var definitions = catalog.GetProperty("metatiles").EnumerateArray().Select(tile =>
        {
            string Texture(string name) => PretRuntimeMapValidation.SafeFile(directory, tile.GetProperty(name + "TexturePath").GetString()!);
            return new PretMartMetatileDefinition(tile.GetProperty("rawMetatileId").GetInt32(), tile.GetProperty("rawAttributes").GetUInt32(),
                tile.GetProperty("behavior").GetInt32(), tile.GetProperty("behaviorName").GetString()!, tile.GetProperty("terrain").GetInt32(),
                tile.GetProperty("encounterType").GetInt32(), tile.GetProperty("layerType").GetInt32(),
                tile.GetProperty("tileEntries").EnumerateArray().Select(v => v.GetUInt16()).ToImmutableArray(),
                Texture("bottom"), Texture("middle"), Texture("top"));
        }).ToImmutableDictionary(t => t.RawMetatileId);
        var patches = catalog.GetProperty("onLoadPatches").EnumerateArray().Select(patch => new PretMartMetatilePatch(
            patch.GetProperty("x").GetInt32(), patch.GetProperty("y").GetInt32(), patch.GetProperty("rawMetatileId").GetInt32(),
            patch.GetProperty("isImpassable").GetBoolean(), patch.GetProperty("collision").GetInt32(), patch.GetProperty("sourceRawBlock").GetUInt16(),
            patch.GetProperty("sourceElevation").GetInt32(), patch.GetProperty("resultingRawBlock").GetUInt16())).ToImmutableArray();
        return new(directory, definitions, patches);
    }
}
