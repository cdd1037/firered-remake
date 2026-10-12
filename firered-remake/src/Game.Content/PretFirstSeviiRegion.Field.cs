using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;
public sealed partial class PretFirstSeviiRegion
{
    private ImmutableDictionary<MapId, (byte[] Primary, byte[] Secondary)> _attributes = ImmutableDictionary<MapId, (byte[], byte[])>.Empty;
    private void InitializeField(SilphSourcePackage source)
    {
        using var layoutsJson = JsonDocument.Parse(source.Raw["data/layouts/layouts.json"]);
        var layouts = layoutsJson.RootElement.GetProperty("layouts").EnumerateArray().Where(l => l.TryGetProperty("id", out _)).ToDictionary(l => l.GetProperty("id").GetString()!, StringComparer.Ordinal);
        _attributes = NewMaps.ToImmutableDictionary(p => p.Key, p =>
        {
            var layout = layouts[p.Value.Events.GetProperty("layout").GetString()!];
            byte[] Bytes(string key) => source.Raw["data/tilesets/" + Catalog.GetProperty("tilesets").GetProperty(layout.GetProperty(key).GetString()!).GetString() + "/metatile_attributes.bin"];
            return (Bytes("primary_tileset"), Bytes("secondary_tileset"));
        });
    }
    /// <summary>Reads the complete authenticated tileset, including original guarded network-machine tiles absent from initial blocks.</summary>
    public FieldMetatileDefinition? MetatileDefinition(MapId map, int metatile)
    {
        if (!_attributes.TryGetValue(map, out var pair) || metatile is < 0 or > 1023) return null;
        var bytes = metatile < 640 ? pair.Primary : pair.Secondary; int offset = (metatile < 640 ? metatile : metatile - 640) * 4;
        if (offset + 4 > bytes.Length) return null;
        var a = PretWorldDecoder.DecodeAttributes(BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4)));
        return new(metatile, a.BehaviorId, a.Terrain, a.EncounterType, a.LayerType);
    }
}
