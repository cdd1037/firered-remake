using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.World;

/// <summary>Authenticated source declarations only. Application commands own puzzle and pickup state.</summary>
public sealed record SevenIslandFieldSource(string Path, string Symbol, string Sha256);
public sealed record SevenIslandPickup(MapId Map, string MapSymbol, int X, int Y, int Elevation,
    ItemId Item, string ItemSymbol, int SourceItemId, int Quantity, string Flag, int SourceFlagId,
    bool Hidden, bool Underfoot, int? LocalId, string? Script, bool Renewable,
    string? RenewalRarity, int? RenewalIndex, SevenIslandFieldSource Source);
public sealed record SevenIslandStrengthSwitch(int Index, int X, int Y, int Elevation,
    string TemporaryVariable, int PressedValue, string Script);
public sealed record SevenIslandStrengthBoulder(int LocalId, string SourceLocalId, int X, int Y,
    int Elevation, int SolvedX, int SolvedY);
public sealed record SevenIslandStrengthPuzzle(MapId Map, string SolvedFlag, int SourceSolvedFlagId,
    string CountVariable, int RequiredCount, int StrengthBehaviorId,
    ImmutableArray<SevenIslandStrengthSwitch> Switches, ImmutableArray<SevenIslandStrengthBoulder> Boulders);
public sealed record SevenIslandFerryLanding(MapId Map, int X, int Y, Direction Facing);
public sealed record SevenIslandHealLocation(int SourceId, MapId OutdoorMap, int X, int Y,
    MapId RespawnMap, string NurseLocalId, int BlackoutX, int BlackoutY, Direction BlackoutFacing);
public sealed record SevenIslandCloneActor(MapId Map, int LocalId, int X, int Y,
    MapId TargetMap, int TargetLocalId, string TargetLocalSymbol);
public sealed record SevenIslandFieldData(ImmutableArray<SevenIslandPickup> Pickups,
    SevenIslandStrengthPuzzle TanobyKey, SevenIslandFerryLanding FerryLanding,
    SevenIslandHealLocation HealLocation, ImmutableArray<SevenIslandCloneActor> Clones);
