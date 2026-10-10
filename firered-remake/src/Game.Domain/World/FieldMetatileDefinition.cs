namespace Game.Domain.World;
/// <summary>Authenticated source metatile attributes; placement retains the map's original elevation.</summary>
public sealed record FieldMetatileDefinition(int SourceMetatileId,int Behavior,int Terrain,int EncounterType,int LayerType);
public sealed record FieldMetatileEdit(int X,int Y,int SourceMetatileId,int Collision,int Elevation);
