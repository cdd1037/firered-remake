using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Game.Domain.Ids;
using Game.Domain.World;

namespace Game.Content;
public sealed partial class PretSilphRegion
{
    public ImmutableArray<SilphWarpPad> WarpPads { get; private set; }
    public ImmutableArray<SilphDoorDefinition> Doors { get; private set; }
    public ImmutableArray<CampaignSilphElevatorFloor> ElevatorFloors { get; private set; }
    public ImmutableArray<CampaignSilphElevatorEntrance> ElevatorEntrances { get; private set; }
    public ImmutableArray<CampaignSilphElevatorEntrance> Entrances => ElevatorEntrances;
    public ImmutableArray<CampaignSilphDynamicExit> DynamicExits { get; private set; }
    private GeneratedFieldTilesets _fieldTilesets = null!;
    private void InitializeField(SilphSourcePackage source)
    {
        WarpPads = Contracts.GetProperty("pads").EnumerateArray().Select(p => new SilphWarpPad(new(p.GetProperty("map").GetString()!),
            p.GetProperty("warpIndex").GetInt32(), p.GetProperty("x").GetInt32(), p.GetProperty("y").GetInt32(), p.GetProperty("elevation").GetInt32(),
            MapIdFromSourceSymbol(p.GetProperty("destinationMap").GetString()!), p.GetProperty("destinationWarp").GetInt32())).ToImmutableArray();
        Doors = Contracts.GetProperty("doors").EnumerateArray().Select(d =>
        {
            ImmutableArray<SilphDoorCell> Cells(string property) => d.GetProperty(property).EnumerateArray().Select(c =>
                new SilphDoorCell(c.GetProperty("x").GetInt32(), c.GetProperty("y").GetInt32(), c.GetProperty("metatileId").GetInt32(), c.GetProperty("collision").GetInt32())).ToImmutableArray();
            return new SilphDoorDefinition(new(d.GetProperty("map").GetString()!), d.GetProperty("script").GetString()!, d.GetProperty("doorIndex").GetInt32(),
                d.GetProperty("flag").GetString()!, d.GetProperty("closedSource").GetProperty("label").GetString()!, d.GetProperty("openSource").GetProperty("label").GetString()!, Cells("closedPatches"), Cells("openPatches"));
        }).ToImmutableArray();
        ElevatorFloors = Contracts.GetProperty("elevatorFloors").EnumerateArray().Select(f =>
        {
            var map = MapIdFromSourceSymbol(f.GetProperty("destinationMap").GetString()!); int x = f.GetProperty("x").GetInt32(), y = f.GetProperty("y").GetInt32();
            string label = f.GetProperty("floor").GetInt32().ToString(CultureInfo.InvariantCulture) + "F";
            return new CampaignSilphElevatorFloor(f.GetProperty("menuIndex").GetInt32(), label, f.GetProperty("sourceFloorId").GetInt32(), map, x, y,
                NewMaps[map].GetSourceCell(x,y)!.Elevation, f.GetProperty("destinationWarp").GetInt32());
        }).OrderBy(f => f.MenuIndex).ToImmutableArray();
        var elevator = new MapId("map.silph-co-elevator");
        ElevatorEntrances = NewMaps.Values.SelectMany(m => m.FieldWarps.Where(w => w.DestinationMapId == elevator).Select(w =>
            new CampaignSilphElevatorEntrance(m.Id, w.Index, w.DestinationWarpId, w.X, w.Y, m.GetSourceCell(w.X,w.Y)!.Elevation))).OrderBy(e => e.SourceMapId.Value, StringComparer.Ordinal).ToImmutableArray();
        DynamicExits = NewMaps[elevator].FieldWarps.Select(w => new CampaignSilphDynamicExit(w.Index, w.X,w.Y,w.Elevation,w.DestinationWarpId)).ToImmutableArray();
        Need(WarpPads.Length == 34 && Doors.Length == 20 && ElevatorFloors.Length == 11 && ElevatorEntrances.Length == 11 && DynamicExits.Length == 1 &&
            Doors.Select(d => d.DoorIndex).Order().SequenceEqual(Enumerable.Range(1,20)), "Silph typed field contracts differ.");
        _fieldTilesets = GeneratedFieldTilesets.ForPackage(source.Directory);
        foreach (var d in Doors) foreach (var c in d.ClosedCells.Concat(d.OpenCells))
            Need(NewMaps[d.MapId].GetSourceCell(c.X,c.Y) is not null && MetatileDefinition(d.MapId,c.SourceMetatileId) is not null, "Silph door patch cell/metatile differs.");
    }
    public FieldMetatileDefinition? MetatileDefinition(MapId map, int metatile)
        => NewMaps.TryGetValue(map, out var field)
            ? _fieldTilesets.Find(field.Events.GetProperty("layout").GetString()!, metatile) : null;
}
