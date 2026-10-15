using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;
public sealed partial class PretCinnabarRegion
{
    private GeneratedFieldTilesets _fieldTilesets = null!;
    private void InitializeField(SilphSourcePackage source)
    {
        _fieldTilesets = GeneratedFieldTilesets.ForPackage(source.Directory);
        foreach (var patch in Contracts.GetProperty("tilePatches").EnumerateArray())
        {
            string label = patch.GetProperty("label").GetString()!;
            var mapId = label.StartsWith("PokemonMansion_", StringComparison.Ordinal)
                ? new MapId("map.pokemon-mansion-" + label[(label.LastIndexOf('_') + 1)..].ToLowerInvariant())
                : new MapId("map.cinnabar-island-gym");
            foreach (var cell in patch.GetProperty("changes").EnumerateArray())
                Need(NewMaps[mapId].GetSourceCell(cell.GetProperty("x").GetInt32(), cell.GetProperty("y").GetInt32()) is not null &&
                    Constants.TryGetValue(cell.GetProperty("metatile").GetString()!, out var metatile) && MetatileDefinition(mapId, metatile) is not null,
                    "Cinnabar source metatile patch differs.");
        }
    }
    /// <summary>Reads the complete authenticated tileset, including switch tiles absent from initial blocks.</summary>
    public FieldMetatileDefinition? MetatileDefinition(MapId map, int metatile)
        => NewMaps.TryGetValue(map, out var field)
            ? _fieldTilesets.Find(field.Events.GetProperty("layout").GetString()!, metatile) : null;
}
