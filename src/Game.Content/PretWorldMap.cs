using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed record PretWorldWarp(int Index, int X, int Y, int Elevation, MapId DestinationMapId, int DestinationWarpId);

public sealed record PretWorldArtifact(string Path, int Width, int Height);
public sealed record PretWorldSpawn(int X, int Y, int Elevation, string Reason);
public sealed record PretWorldAttributes(int BehaviorId, int Terrain, int Attribute2, int Attribute3,
    int EncounterType, int Attribute5, int LayerType, int Attribute7);
public sealed record PretWorldCell(int X, int Y, ushort RawBlock, int MetatileId, int Collision, int Elevation,
    uint RawAttributes, int BehaviorId, string BehaviorName, int Terrain, int Attribute2, int Attribute3,
    int EncounterType, int Attribute5, int LayerType, int Attribute7, string Movement);
/// <summary>Pure little-endian FireRed field-map bit decoding; unknown behaviors remain unsupported.</summary>
public static class PretWorldDecoder
{
    public static (int MetatileId, int Collision, int Elevation) DecodeBlock(ushort raw)
        => (raw & 0x03ff, (raw >> 10) & 3, raw >> 12);
    public static PretWorldAttributes DecodeAttributes(uint raw)
        => new((int)(raw & 0x1ff), (int)((raw >> 9) & 31), (int)((raw >> 14) & 15),
            (int)((raw >> 18) & 63), (int)((raw >> 24) & 7), (int)((raw >> 27) & 3),
            (int)((raw >> 29) & 3), (int)(raw >> 31));
    public static string Movement(int behavior) => behavior switch
    {
        0x00 => "normal", 0x02 => "grassUnsupported", >= 0x38 and <= 0x3b => "ledgeUnsupported",
        0x69 => "doorUnsupported", >= 0x62 and <= 0x65 => "arrowWarpUnsupported",
        >= 0x6c and <= 0x6f => "stairWarpUnsupported",
        0x60 or 0x61 or 0x66 or 0x67 or 0x68 or 0x71 => "warpUnsupported", 0x10 or 0x11 or 0x12 or 0x13 or 0x15 => "waterUnsupported",
        0x84 => "signBlocked", _ => "unsupported"
    };
    public static TileBehavior Behavior(int behavior) => behavior switch
    {
        0x00 => TileBehavior.Floor, 0x02 => TileBehavior.Grass,
        >= 0x38 and <= 0x3b => TileBehavior.Ledge, >= 0x62 and <= 0x65 => TileBehavior.ArrowWarp, >= 0x6c and <= 0x6f => TileBehavior.StairWarp, >= 0x60 and <= 0x69 or 0x71 => TileBehavior.Warp, 0x84 => TileBehavior.Sign,
        _ => TileBehavior.Unsupported
    };
}

/// <summary>Converted map data and render-resource paths.</summary>
public sealed class PretWorldMap : IFieldMap
{
    // Existing reference-view save content ID; retained without changing historical saves.
    public const string FullSourceHash = "8795cbf31a2b799b33428fa6f88207a6f33a46574e5a4195bb9f79018ef088d4";
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    private readonly PretRuntimeMapData _data;
    private readonly Func<string, MapId> _mapIdFromSymbol;
    private readonly string? _playerTexturePath;
    public MapId Id => _data.Id;
    public int? SourceEncounterTypeAt(int x, int y) => GetSourceCell(x, y)?.EncounterType;
    public int? SourceBehaviorAt(int x, int y) => _data.SourceBehaviorAt(x, y);
    public ImmutableArray<FieldWarp> FieldWarps => Warps.Select(w => new FieldWarp(w.Index, w.X, w.Y, w.Elevation, w.DestinationMapId, w.DestinationWarpId)).ToImmutableArray();
    public int Width => _data.Width;
    public int Height => _data.Height;
    public int TileSize => _data.TileSize;
    public int BorderWidth => _data.BorderWidth;
    public int BorderHeight => _data.BorderHeight;
    public string SourceMapName => _data.SourceMapName;
    public PretWorldSpawn TechnicalSpawn => _data.TechnicalSpawn ?? throw new InvalidDataException("No technical spawn is declared for this region map.");
    public ImmutableArray<PretWorldCell> Cells => _data.ExpandCells();
    internal ImmutableArray<ConnectedFieldCell> CreateConnectedCells() => _data.CreateConnectedCells();
    public ImmutableArray<PretWorldCell> BorderCells => _data.ExpandBorderCells();
    public ImmutableArray<PretWorldArtifact> Artifacts => _data.Artifacts;
    public JsonElement Events => _data.Events;
    public string? CavePreviewVisitFlag => _data.CavePreviewVisitFlag;
    public bool HasCavePreview => CavePreviewVisitFlag is not null;
    public string SourceMapSymbol => _data.SourceMapSymbol;
    public ImmutableArray<PretWorldWarp> Warps => Events.GetProperty("warp_events").EnumerateArray().Select((e, index) =>
        new PretWorldWarp(index, e.GetProperty("x").GetInt32(), e.GetProperty("y").GetInt32(), e.GetProperty("elevation").GetInt32(),
            _mapIdFromSymbol(e.GetProperty("dest_map").GetString()!),
            PretRocketHideoutRegion.ParseWarpId(SourceMapSymbol, e))).ToImmutableArray();
    public string MapPath { get; }
    public bool UsesLocalBorder { get; }
    public string DirectoryPath => Path.GetDirectoryName(MapPath)!;
    public string TexturePath => Path.Combine(DirectoryPath, _data.TexturePath);
    public string BottomTexturePath => LayerTexturePath("bottom");
    public string MiddleTexturePath => LayerTexturePath("middle");
    public string TopTexturePath => LayerTexturePath("top");
    public string BorderBottomTexturePath => LayerTexturePath("border-bottom");
    public string BorderMiddleTexturePath => LayerTexturePath("border-middle");
    public string BorderTopTexturePath => LayerTexturePath("border-top");
    public string PlayerTexturePath => _playerTexturePath ?? Path.Combine(PretRuntimeMapValidation.GeneratedRoot(MapPath), "sprites/overworld/opening-player.png");
    private string LayerTexturePath(string layer) => Path.Combine(DirectoryPath, _data.TexturePath[..^"map.png".Length] + layer + ".png");
    private PretWorldMap(PretRuntimeMapData data, string directory, string? playerTexturePath, Func<string, MapId>? resolver, bool usesLocalBorder)
    {
        _data = data; MapPath = directory; UsesLocalBorder = usesLocalBorder; _playerTexturePath = playerTexturePath;
        _mapIdFromSymbol = resolver ?? data.ResolveMapSymbol;
    }
    internal static PretWorldMap FromRuntime(PretRuntimeMapData data, string directory, string? playerTexturePath = null, Func<string, MapId>? resolver = null, bool usesLocalBorder = true)
        => new(data, directory, playerTexturePath, resolver, usesLocalBorder);
    internal MapId ResolveMapSymbol(string symbol) => _mapIdFromSymbol(symbol);
    public PretWorldCell? GetSourceCell(int x, int y) => _data.GetSourceCell(x, y);
    public TileCell? GetTile(int x, int y) => _data.GetTile(x, y);
}
