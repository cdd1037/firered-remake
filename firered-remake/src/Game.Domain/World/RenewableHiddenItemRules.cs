using System.Collections.Immutable;
using Game.Domain.Randomness;

namespace Game.Domain.World;

/// <summary>The original table order determines global RNG consumption.</summary>
public sealed record RenewableHiddenItemGroup(string MapSymbol, ImmutableArray<string> Rare,
    ImmutableArray<string> Uncommon, ImmutableArray<string> Common);

public sealed record RenewableHiddenItemResult(int StepCounter, ImmutableSortedDictionary<string, bool> Flags,
    RngState Rng, bool Regenerated, ImmutableArray<RandomTrace> Trace);

/// <summary>
/// Exact renewable_hidden_items.c behavior, including global sampling of maps not currently loaded.
/// Flags set to true are unavailable; false flags are available for the ordinary hidden-item pickup script.
/// The caller supplies only completed, unpreempted source tookStep events and actual map entry events.
/// </summary>
public static class RenewableHiddenItemRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourcePath = "src/renewable_hidden_items.c";
    public const string CounterVariable = "VAR_RENEWABLE_ITEM_STEP_COUNTER";
    public const int StepThreshold = 1500;

    // NO_ITEM entries need no state, but nonempty entries retain original group and item order.
    public static ImmutableArray<RenewableHiddenItemGroup> Groups { get; } =
    [
        new("MAP_ROUTE20",
            [],
            ["FLAG_HIDDEN_ITEM_ROUTE20_STARDUST"],
            []),
        new("MAP_ROUTE21_NORTH",
            [],
            ["FLAG_HIDDEN_ITEM_ROUTE21_NORTH_PEARL"],
            []),
        new("MAP_UNDERGROUND_PATH_NORTH_SOUTH_TUNNEL",
            ["FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_NORTH_SOUTH_TUNNEL_ETHER"],
            ["FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_NORTH_SOUTH_TUNNEL_POTION", "FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_NORTH_SOUTH_TUNNEL_ANTIDOTE", "FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_NORTH_SOUTH_TUNNEL_PARALYZE_HEAL", "FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_NORTH_SOUTH_TUNNEL_AWAKENING", "FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_NORTH_SOUTH_TUNNEL_BURN_HEAL", "FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_NORTH_SOUTH_TUNNEL_ICE_HEAL"],
            []),
        new("MAP_UNDERGROUND_PATH_EAST_WEST_TUNNEL",
            ["FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_EAST_WEST_TUNNEL_ETHER"],
            ["FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_EAST_WEST_TUNNEL_POTION", "FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_EAST_WEST_TUNNEL_ANTIDOTE", "FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_EAST_WEST_TUNNEL_PARALYZE_HEAL", "FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_EAST_WEST_TUNNEL_AWAKENING", "FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_EAST_WEST_TUNNEL_BURN_HEAL", "FLAG_HIDDEN_ITEM_UNDERGROUND_PATH_EAST_WEST_TUNNEL_ICE_HEAL"],
            []),
        new("MAP_SEVEN_ISLAND_TANOBY_RUINS",
            ["FLAG_HIDDEN_ITEM_SEVEN_ISLAND_TANOBY_RUINS_HEART_SCALE_4", "FLAG_HIDDEN_ITEM_SEVEN_ISLAND_TANOBY_RUINS_HEART_SCALE", "FLAG_HIDDEN_ITEM_SEVEN_ISLAND_TANOBY_RUINS_HEART_SCALE_2", "FLAG_HIDDEN_ITEM_SEVEN_ISLAND_TANOBY_RUINS_HEART_SCALE_3"],
            [],
            []),
        new("MAP_MT_MOON_B1F",
            ["FLAG_HIDDEN_ITEM_MT_MOON_B1F_TINY_MUSHROOM", "FLAG_HIDDEN_ITEM_MT_MOON_B1F_TINY_MUSHROOM_2", "FLAG_HIDDEN_ITEM_MT_MOON_B1F_TINY_MUSHROOM_3", "FLAG_HIDDEN_ITEM_MT_MOON_B1F_BIG_MUSHROOM", "FLAG_HIDDEN_ITEM_MT_MOON_B1F_BIG_MUSHROOM_2", "FLAG_HIDDEN_ITEM_MT_MOON_B1F_BIG_MUSHROOM_3"],
            ["FLAG_HIDDEN_ITEM_MT_MOON_B1F_TINY_MUSHROOM", "FLAG_HIDDEN_ITEM_MT_MOON_B1F_TINY_MUSHROOM_2", "FLAG_HIDDEN_ITEM_MT_MOON_B1F_TINY_MUSHROOM_3"],
            []),
        new("MAP_THREE_ISLAND_BERRY_FOREST",
            ["FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_BLUK_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_WEPEAR_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_ORAN_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_CHERI_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_ASPEAR_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_PERSIM_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_PINAP_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_LUM_BERRY"],
            ["FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_BLUK_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_WEPEAR_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_ORAN_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_CHERI_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_ASPEAR_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_PERSIM_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_PINAP_BERRY"],
            ["FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_RAZZ_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_NANAB_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_CHESTO_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_PECHA_BERRY", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BERRY_FOREST_RAWST_BERRY"]),
        new("MAP_ONE_ISLAND_TREASURE_BEACH",
            ["FLAG_HIDDEN_ITEM_ONE_ISLAND_TREASURE_BEACH_ULTRA_BALL", "FLAG_HIDDEN_ITEM_ONE_ISLAND_TREASURE_BEACH_ULTRA_BALL_2", "FLAG_HIDDEN_ITEM_ONE_ISLAND_TREASURE_BEACH_STAR_PIECE", "FLAG_HIDDEN_ITEM_ONE_ISLAND_TREASURE_BEACH_BIG_PEARL"],
            ["FLAG_HIDDEN_ITEM_ONE_ISLAND_TREASURE_BEACH_STARDUST", "FLAG_HIDDEN_ITEM_ONE_ISLAND_TREASURE_BEACH_STARDUST_2", "FLAG_HIDDEN_ITEM_ONE_ISLAND_TREASURE_BEACH_PEARL", "FLAG_HIDDEN_ITEM_ONE_ISLAND_TREASURE_BEACH_PEARL_2", "FLAG_HIDDEN_ITEM_ONE_ISLAND_TREASURE_BEACH_ULTRA_BALL", "FLAG_HIDDEN_ITEM_ONE_ISLAND_TREASURE_BEACH_ULTRA_BALL_2"],
            ["FLAG_HIDDEN_ITEM_ONE_ISLAND_TREASURE_BEACH_ULTRA_BALL", "FLAG_HIDDEN_ITEM_ONE_ISLAND_TREASURE_BEACH_ULTRA_BALL_2"]),
        new("MAP_THREE_ISLAND_BOND_BRIDGE",
            [],
            ["FLAG_HIDDEN_ITEM_THREE_ISLAND_BOND_BRIDGE_PEARL", "FLAG_HIDDEN_ITEM_THREE_ISLAND_BOND_BRIDGE_STARDUST"],
            []),
        new("MAP_FOUR_ISLAND",
            [],
            ["FLAG_HIDDEN_ITEM_FOUR_ISLAND_PEARL"],
            ["FLAG_HIDDEN_ITEM_FOUR_ISLAND_ULTRA_BALL"]),
        new("MAP_FIVE_ISLAND_MEMORIAL_PILLAR",
            ["FLAG_HIDDEN_ITEM_FIVE_ISLAND_MEMORIAL_PILLAR_BIG_PEARL"],
            [],
            []),
        new("MAP_FIVE_ISLAND_RESORT_GORGEOUS",
            ["FLAG_HIDDEN_ITEM_FIVE_ISLAND_RESORT_GORGEOUS_NEST_BALL", "FLAG_HIDDEN_ITEM_FIVE_ISLAND_RESORT_GORGEOUS_STAR_PIECE"],
            ["FLAG_HIDDEN_ITEM_FIVE_ISLAND_RESORT_GORGEOUS_STARDUST", "FLAG_HIDDEN_ITEM_FIVE_ISLAND_RESORT_GORGEOUS_STARDUST_2"],
            []),
        new("MAP_SIX_ISLAND_OUTCAST_ISLAND",
            ["FLAG_HIDDEN_ITEM_SIX_ISLAND_OUTCAST_ISLAND_STAR_PIECE", "FLAG_HIDDEN_ITEM_SIX_ISLAND_OUTCAST_ISLAND_NET_BALL"],
            [],
            []),
        new("MAP_SIX_ISLAND_GREEN_PATH",
            [],
            [],
            ["FLAG_HIDDEN_ITEM_SIX_ISLAND_GREEN_PATH_ULTRA_BALL"]),
        new("MAP_SEVEN_ISLAND_TRAINER_TOWER",
            ["FLAG_HIDDEN_ITEM_SEVEN_ISLAND_TRAINER_TOWER_BIG_PEARL"],
            ["FLAG_HIDDEN_ITEM_SEVEN_ISLAND_TRAINER_TOWER_PEARL"],
            []),
    ];

    public static ImmutableArray<string> AllFlags { get; } = Groups
        .SelectMany(group => group.Rare.Concat(group.Uncommon).Concat(group.Common))
        .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToImmutableArray();

    public static bool IsRenewableMap(string sourceMapSymbol) =>
        Groups.Any(group => string.Equals(group.MapSymbol, sourceMapSymbol, StringComparison.Ordinal));

    /// <summary>IncrementRenewableHiddenItemStepCounter: saturates, and consumes no RNG.</summary>
    public static int IncrementStepCounter(int stepCounter)
    {
        ValidateSourceCounter(stepCounter);
        return stepCounter < StepThreshold ? stepCounter + 1 : stepCounter;
    }

    /// <summary>SetAllRenewableItemFlags, also called by original NewGameInitData; consumes no RNG.</summary>
    public static ImmutableSortedDictionary<string, bool> SetAllRenewableItemFlags(
        ImmutableSortedDictionary<string, bool> flags)
    {
        ArgumentNullException.ThrowIfNull(flags);
        var next = flags.ToBuilder();
        foreach (string flag in AllFlags) next[flag] = true;
        return next.ToImmutable();
    }

    /// <summary>
    /// Call after OnTransition only on a true warp/camera map entry, never on save restore or battle return.
    /// A capped counter on an unrelated map stays capped. A qualifying entry resamples all 15 groups once.
    /// </summary>
    public static RenewableHiddenItemResult TryRegenerate(string sourceMapSymbol, int stepCounter,
        ImmutableSortedDictionary<string, bool> flags, RngState rng)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceMapSymbol);
        ArgumentNullException.ThrowIfNull(flags);
        ValidateSourceCounter(stepCounter);
        if (!IsRenewableMap(sourceMapSymbol) || stepCounter < StepThreshold)
            return new(stepCounter, flags, rng, false, []);

        var next = SetAllRenewableItemFlags(flags).ToBuilder();
        var trace = ImmutableArray.CreateBuilder<RandomTrace>(Groups.Length);
        foreach (var group in Groups)
        {
            var draw = Gen3Rng.Next(rng, "renewable-hidden-items:" + group.MapSymbol, 100);
            rng = draw.State;
            trace.Add(draw.Trace);
            int rval = draw.Value % 100;
            var sampled = rval >= 90 ? group.Rare : rval >= 60 ? group.Uncommon : group.Common;
            foreach (string flag in sampled) next[flag] = false;
        }
        return new(0, next.ToImmutable(), rng, true, trace.MoveToImmutable());
    }

    private static void ValidateSourceCounter(int stepCounter)
    {
        // The source variable is u16. Ordinary gameplay only writes 0..1500, but source
        // TryRegenerate explicitly accepts >=1500 and Increment leaves such values alone.
        if (stepCounter is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(stepCounter));
    }
}
