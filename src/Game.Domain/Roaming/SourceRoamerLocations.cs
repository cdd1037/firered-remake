using System.Collections.Immutable;

namespace Game.Domain.Roaming;

// Source src/roamer.c:36-62 at 037335f4c725d7c9aecdac87066f2002b4bd7e14.
// Blank columns deliberately remain255: rejection draws are part of the algorithm.
internal static class SourceRoamerLocations
{
    internal static ImmutableArray<ImmutableArray<int>> Rows { get; } =
    [
        [19, 20, 39, 41, 255, 255, 255], // MAP_ROUTE1
        [20, 19, 21, 41, 255, 255, 255], // MAP_ROUTE2
        [21, 20, 22, 255, 255, 255, 255], // MAP_ROUTE3
        [22, 21, 23, 27, 43, 255, 255], // MAP_ROUTE4
        [23, 22, 24, 25, 26, 27, 43], // MAP_ROUTE5
        [24, 23, 25, 26, 29, 255, 255], // MAP_ROUTE6
        [25, 23, 24, 26, 34, 255, 255], // MAP_ROUTE7
        [26, 23, 24, 25, 28, 30, 255], // MAP_ROUTE8
        [27, 22, 23, 28, 43, 255, 255], // MAP_ROUTE9
        [28, 26, 27, 30, 255, 255, 255], // MAP_ROUTE10
        [29, 24, 30, 255, 255, 255, 255], // MAP_ROUTE11
        [30, 28, 29, 31, 255, 255, 255], // MAP_ROUTE12
        [31, 30, 32, 255, 255, 255, 255], // MAP_ROUTE13
        [32, 31, 33, 255, 255, 255, 255], // MAP_ROUTE14
        [33, 32, 36, 37, 255, 255, 255], // MAP_ROUTE15
        [34, 25, 35, 255, 255, 255, 255], // MAP_ROUTE16
        [35, 34, 36, 255, 255, 255, 255], // MAP_ROUTE17
        [36, 33, 35, 37, 255, 255, 255], // MAP_ROUTE18
        [37, 33, 36, 38, 255, 255, 255], // MAP_ROUTE19
        [38, 37, 39, 255, 255, 255, 255], // MAP_ROUTE20
        [39, 19, 38, 255, 255, 255, 255], // MAP_ROUTE21_NORTH
        [41, 19, 20, 42, 255, 255, 255], // MAP_ROUTE22
        [42, 41, 20, 255, 255, 255, 255], // MAP_ROUTE23
        [43, 22, 23, 27, 255, 255, 255], // MAP_ROUTE24
        [44, 43, 27, 255, 255, 255, 255], // MAP_ROUTE25
    ];
}
