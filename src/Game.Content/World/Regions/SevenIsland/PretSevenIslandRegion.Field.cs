using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;
public sealed partial class PretSevenIslandRegion
{
    private GeneratedFieldTilesets _fieldTilesets = null!;
    private void InitializeField(SilphSourcePackage source)
    {
        _fieldTilesets = GeneratedFieldTilesets.ForPackage(source.Directory);
    }
    /// <summary>Reads the complete authenticated tileset, for field behavior and future source-driven tile changes.</summary>
    public FieldMetatileDefinition? MetatileDefinition(MapId map, int metatile)
        => NewMaps.TryGetValue(map, out var field)
            ? _fieldTilesets.Find(field.Events.GetProperty("layout").GetString()!, metatile) : null;
}
