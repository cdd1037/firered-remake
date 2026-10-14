using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.World;

public sealed record SeafoamBoulder(MapId Map, int LocalId, int X, int Y, int Elevation,
    int MovementRange, string HideFlag, string? RevealFlag, bool Interactable);
public sealed record SeafoamHole(MapId Map, int WarpId, int X, int Y, MapId DestinationMap,
    int DestinationWarpId, int LandingX, int LandingY, int LandingElevation);
public sealed record SeafoamMetatileChange(int X, int Y, int MetatileId, int Behavior, int Collision, int Elevation);
public sealed record SeafoamCurrentScript(string Symbol, ImmutableArray<string> Actions, TilePosition End,
    TilePosition? WarpAfter = null, bool DismountNorthAfter = false);

/// <summary>Pinned source event data. The campaign's existing flags and live actors remain authoritative;
/// no second puzzle-progress counter is introduced. Coordinates exclude the seven-cell source border.</summary>
public static class SeafoamFieldRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public static readonly MapId FirstFloor = new("map.seafoam-islands-1f");
    public static readonly MapId B1F = new("map.seafoam-islands-b1f");
    public static readonly MapId B2F = new("map.seafoam-islands-b2f");
    public static readonly MapId B3F = new("map.seafoam-islands-b3f");
    public static readonly MapId B4F = new("map.seafoam-islands-b4f");
    public static readonly MapId Route20 = new("map.route-20");
    public const string B3FStopped = "FLAG_STOPPED_SEAFOAM_B3F_CURRENT";
    public const string B4FStopped = "FLAG_STOPPED_SEAFOAM_B4F_CURRENT";
    public const string FallingVariable = "VAR_TEMP_1";
    public const string B4FSceneVariable = "VAR_MAP_SCENE_SEAFOAM_ISLANDS_B4F";
    public const int FallBehavior = 0x66, CalmWaterMetatile = 0x12B;
    public static ImmutableArray<SeafoamBoulder> Boulders { get; } = [
        new(FirstFloor,1,22,12,3,0,"FLAG_HIDE_SEAFOAM_1F_BOULDER_1","FLAG_HIDE_SEAFOAM_B1F_BOULDER_1",true),
        new(FirstFloor,2,32,9,3,0,"FLAG_HIDE_SEAFOAM_1F_BOULDER_2","FLAG_HIDE_SEAFOAM_B1F_BOULDER_2",true),
        new(B1F,1,22,8,3,0,"FLAG_HIDE_SEAFOAM_B1F_BOULDER_1","FLAG_HIDE_SEAFOAM_B2F_BOULDER_1",true),
        new(B1F,2,30,8,3,0,"FLAG_HIDE_SEAFOAM_B1F_BOULDER_2","FLAG_HIDE_SEAFOAM_B2F_BOULDER_2",true),
        new(B2F,1,22,8,3,0,"FLAG_HIDE_SEAFOAM_B2F_BOULDER_1","FLAG_HIDE_SEAFOAM_B3F_BOULDER_1",true),
        new(B2F,2,30,8,3,0,"FLAG_HIDE_SEAFOAM_B2F_BOULDER_2","FLAG_HIDE_SEAFOAM_B3F_BOULDER_2",true),
        new(B3F,1,23,8,1,1,"FLAG_HIDE_SEAFOAM_B3F_BOULDER_1",null,false),
        new(B3F,2,24,8,1,1,"FLAG_HIDE_SEAFOAM_B3F_BOULDER_2",null,false),
        new(B3F,3,12,16,3,0,"FLAG_HIDE_SEAFOAM_B3F_BOULDER_5","FLAG_HIDE_SEAFOAM_B4F_BOULDER_2",true),
        new(B3F,4,13,16,3,0,"FLAG_HIDE_SEAFOAM_B3F_BOULDER_6",null,true),
        new(B3F,5,9,16,3,0,"FLAG_HIDE_SEAFOAM_B3F_BOULDER_4",null,true),
        new(B3F,6,6,17,3,0,"FLAG_HIDE_SEAFOAM_B3F_BOULDER_3","FLAG_HIDE_SEAFOAM_B4F_BOULDER_1",true),
        new(B4F,1,8,18,1,1,"FLAG_HIDE_SEAFOAM_B4F_BOULDER_1",null,false),
        new(B4F,2,9,18,1,1,"FLAG_HIDE_SEAFOAM_B4F_BOULDER_2",null,false)
    ];
    public static ImmutableArray<SeafoamHole> Holes { get; } = [
        new(FirstFloor,5,21,8,B1F,9,21,8,3), new(FirstFloor,6,30,8,B1F,10,29,8,3),
        new(B1F,7,23,8,B2F,7,22,7,3), new(B1F,8,28,8,B2F,8,29,8,3),
        new(B2F,9,24,8,B3F,5,23,9,1), new(B2F,10,27,8,B3F,6,24,9,1),
        new(B3F,7,6,18,B4F,2,8,17,1), new(B3F,8,9,18,B4F,3,9,17,1)
    ];
    public static bool IsCave(MapId map) => map == FirstFloor || map == B1F || map == B2F || map == B3F || map == B4F;
    public static bool ValidDimensions(IFieldMap map) => IsCave(map.Id) && map.Width == 38 && map.Height == (map.Id == B1F ? 23 : 24);
    public static bool BothBouldersPresent(MapId map, IReadOnlyDictionary<string,bool> flags)
    {
        if (map != B3F && map != B4F) throw new ArgumentException("Only the two current floors count blocking boulders.",nameof(map));
        return Boulders.Where(b => b.Map == map && b.LocalId <= 2).All(b => !flags.GetValueOrDefault(b.HideFlag));
    }

    /// <summary>Run on actual map transition, never on stable Continue. Route20 resets each unfinished chain independently.
    /// Actor coordinates and temporary field/Strength flags are reset by the existing common map-entry owner.</summary>
    public static ImmutableSortedDictionary<string,bool> MapEntryFlags(MapId map, ImmutableSortedDictionary<string,bool> flags)
    {
        if (map == Route20)
        {
            if (!flags.GetValueOrDefault(B3FStopped))
                foreach (var b in Boulders.Where(b => b.Map == FirstFloor || b.Map == B1F || b.Map == B2F || b.Map == B3F && b.LocalId <= 2))
                    flags = flags.SetItem(b.HideFlag,b.Map != FirstFloor);
            if (!flags.GetValueOrDefault(B4FStopped))
                foreach (var b in Boulders.Where(b => b.Map == B3F && b.LocalId > 2 || b.Map == B4F))
                    flags = flags.SetItem(b.HideFlag,b.Map == B4F);
        }
        if (map == B3F && BothBouldersPresent(map,flags)) flags = flags.SetItem(B3FStopped,true);
        if (map == B4F)
        {
            if (!flags.GetValueOrDefault("FLAG_FOUGHT_ARTICUNO")) flags = flags.SetItem("FLAG_HIDE_ARTICUNO",false);
            if (BothBouldersPresent(map,flags)) flags = flags.SetItem(B4FStopped,true);
        }
        if (map == FirstFloor) flags = flags.SetItem("FLAG_WORLD_MAP_SEAFOAM_ISLANDS_1F",true);
        return flags;
    }

    /// <summary>After the authenticated shared Strength push completes, remove that live actor. Its own hide flag is
    /// set, and its trainer_type reveal flag is cleared regardless of which original hole was used. Flag zero is a no-op.</summary>
    public static ImmutableSortedDictionary<string,bool> CompleteBoulderFall(MapId map, int localId, int x, int y,
        ImmutableSortedDictionary<string,bool> flags)
    {
        var b = Boulders.SingleOrDefault(b => b.Map == map && b.LocalId == localId);
        if (b is null || !b.Interactable || flags.GetValueOrDefault(b.HideFlag) || !Holes.Any(h => h.Map == map && h.X == x && h.Y == y))
            throw new ArgumentException("An active source boulder on an original hole is required.");
        flags = flags.SetItem(b.HideFlag,true);
        return b.RevealFlag is null ? flags : flags.SetItem(b.RevealFlag,false);
    }

    public static string Layout(MapId map,IReadOnlyDictionary<string,bool> flags)
    {
        if (!IsCave(map)) throw new ArgumentException("Not a Seafoam floor.",nameof(map));
        string floor = map == FirstFloor ? "1F" : map == B1F ? "B1F" : map == B2F ? "B2F" : map == B3F ? "B3F" : "B4F";
        return "LAYOUT_SEAFOAM_ISLANDS_" + floor + ((map == B3F && flags.GetValueOrDefault(B3FStopped) || map == B4F && flags.GetValueOrDefault(B4FStopped)) ? "_CURRENT_STOPPED" : "");
    }
    public static ImmutableArray<SeafoamMetatileChange> OnLoadChanges(MapId map,IReadOnlyDictionary<string,bool> flags) =>
        map == B4F && BothBouldersPresent(map,flags) ? [new(12,14,CalmWaterMetatile,0x15,0,1),new(13,14,CalmWaterMetatile,0x15,0,1)] : [];
    public static Direction? CurrentDirection(int behavior) => behavior switch
    { 0x50 => Direction.East, 0x51 => Direction.West, 0x52 => Direction.North, 0x53 or 0x13 => Direction.South, _ => null };
    public static bool Surfable(int behavior) => behavior is >=0x10 and <=0x13 or 0x15 or 0x1A or 0x1B or >=0x50 and <=0x53;
    public static bool IsWaterFloor(MapId map) => map == B3F || map == B4F;
    public static bool FallStartsSurf(TilePosition landing,int behavior) => IsWaterFloor(landing.MapId) && Surfable(behavior);

    /// <summary>Exact held-movement source scripts. The story interpreter owns their lock, movement waits, scene/temp vars,
    /// warp and Surf specials. These vectors authenticate those operations and must not also run automatic terrain currents.</summary>
    public static SeafoamCurrentScript FallingCurrentScript(MapId map,int x,int y)
    {
        if (map == B3F && y == 9 && x is 23 or 24)
        {
            var actions = Enumerable.Repeat("walk_fast_down",4).Concat(Enumerable.Repeat("walk_fast_right",x == 23 ? 3 : 2)).Concat(Enumerable.Repeat("walk_fast_down",6)).ToImmutableArray();
            return new("SeafoamIslands_B3F_Movement_RideCurrent" + (x == 23 ? "Far" : "Close"),actions,new(map,26,19,1,Direction.South),new(B4F,27,21,1,Direction.North));
        }
        if (map == B4F && y == 17 && x is 8 or 9)
        {
            var actions = Enumerable.Repeat("walk_up",3).Concat(Enumerable.Repeat("walk_right",x == 8 ? 4 : 3)).Append("walk_up").ToImmutableArray();
            return new("SeafoamIslands_B4F_Movement_RideCurrent" + (x == 8 ? "Far" : "Close"),actions,new(map,12,13,1,Direction.North),null,true);
        }
        throw new ArgumentException("Not a source water-fall landing.");
    }
}
