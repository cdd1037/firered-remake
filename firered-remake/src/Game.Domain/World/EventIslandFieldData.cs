using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.World;

/// <summary>Original event-island declarations. Application owns admission and persistent state.</summary>
public sealed record EventIslandFieldSource(string Path, string Symbol);
public sealed record EventIslandMapHeader(MapId Map, string Music, string Weather, string MapType,
    bool AllowCycling, bool AllowEscaping, bool AllowRunning, bool RequiresFlash, bool ShowMapName,
    int FloorNumber, string BattleScene, bool HasRandomEncounterHeader, EventIslandFieldSource Source);
public sealed record EventIslandHiddenPickup(MapId Map, int X, int Y, int Elevation, ItemId Item,
    string ItemSymbol, int SourceItemId, int Quantity, string Flag, int SourceFlagId,
    bool Underfoot, bool RequiresItemfinder, bool RuntimePickupAdmitted, EventIslandFieldSource Source);
public sealed record EventIslandCoordinateTrigger(MapId Map, int X, int Y, int Elevation,
    string Variable, int SourceVariableId, int RequiredValue, string Script, EventIslandFieldSource Source);
public sealed record EventIslandFerryLanding(MapId Map, int X, int Y, Direction Facing, EventIslandFieldSource Source);
public sealed record EventIslandDynamicGraphics(MapId Map, int LocalId, string SourceGraphics,
    string Variable, string GraphicsId, string Script);
public sealed record EventIslandFieldData(ImmutableArray<EventIslandMapHeader> MapHeaders,
    ImmutableArray<EventIslandHiddenPickup> HiddenPickups, ImmutableArray<EventIslandCoordinateTrigger> CoordinateTriggers,
    ImmutableArray<EventIslandFerryLanding> FerryLandings, ImmutableArray<EventIslandDynamicGraphics> DynamicGraphics,
    int NativeCameraLocalId);
