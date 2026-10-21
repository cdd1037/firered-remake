using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;

public sealed partial class PretCampaignCatalog
{
    private ImmutableDictionary<(MapId Map, string Layout), PretWorldMap> _mapLayouts = ImmutableDictionary<(MapId, string), PretWorldMap>.Empty;
    private ImmutableDictionary<(MapId Map, string Layout), ConnectedFieldMap> _connectedMapLayouts = ImmutableDictionary<(MapId, string), ConnectedFieldMap>.Empty;
    private ImmutableHashSet<MapId> _layoutMaps = ImmutableHashSet<MapId>.Empty;

    private void InitializeMapLayouts(IEnumerable<KeyValuePair<string, PretWorldMap>> layouts)
    {
        _mapLayouts = layouts.ToImmutableDictionary(p => (p.Value.Id, p.Key), p => p.Value);
        foreach (var ((map, _), variant) in _mapLayouts)
        {
            var original = SourceMaps[map];
            if (!variant.FieldWarps.SequenceEqual(original.FieldWarps) || variant.Width != original.Width || variant.Height != original.Height)
                throw new InvalidDataException("map_layout_header_mismatch");
        }
        _connectedMapLayouts = _mapLayouts.ToImmutableDictionary(p => p.Key, p => ConnectedFieldMaps![p.Key.Map] with
            { Cells = p.Value.CreateConnectedCells(), Width = p.Value.Width, Height = p.Value.Height });
        _layoutMaps = _mapLayouts.Keys.Select(k => k.Map).ToImmutableHashSet();
    }
    public bool HasMapLayouts(MapId map) => _layoutMaps.Contains(map);
    public IFieldMap? MapLayout(MapId map, string layout) => _mapLayouts.GetValueOrDefault((map, layout));
    public ConnectedFieldMap? ConnectedMapLayout(MapId map, string layout) => _connectedMapLayouts.GetValueOrDefault((map, layout));
}
