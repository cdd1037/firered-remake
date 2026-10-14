using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Domain.World;

public enum FieldFlyRegion { Kanto = 0, Sevii123 = 1, Sevii45 = 2, Sevii67 = 3 }
public enum FieldFlyGiftResult { AlreadyReceived, NoRoom, GiveHm02 }
public sealed record FieldFlySectionRegion(string SectionId, FieldFlyRegion Region);
public sealed record FieldFlyDestination(string SectionId, FieldFlyRegion Region, string MapSymbol, MapId MapId,
    string HealLocation, int HealLocationId, int X, int Y, string VisitedFlag, int VisitedFlagId,
    string VisitMapSymbol, OpeningItemSourceRef VisitSource);
public sealed record FieldFlySourceDocument(string SourceCommit, ImmutableArray<FieldFlyDestination> Destinations,
    ImmutableArray<FieldFlySectionRegion> SectionRegions);

/// <summary>Original party-menu predicates only. HP, status, PP, held item and HM possession are irrelevant.</summary>
public static class FieldFlySourceRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string FlyMove = "pret:MOVE_FLY";
    public const string BadgeFlag = "FLAG_BADGE03_GET";
    public const string GiftFlag = "FLAG_GOT_HM02";
    public const string GiftMap = "map.route-16-house";
    public const string GiftScript = "Route16_House_EventScript_Woman";
    public const int GiftActorLocalId = 1, GiftFlagId = 0x238, FieldUsePpCost = 0, WarpId = -1;
    public const Direction ArrivalFacing = Direction.South;

    // Overworld_ResetStateAfterFly. Normal destination map-entry scripts still execute.
    public static ImmutableArray<string> ClearedFlags { get; } = ["FLAG_SYS_ON_CYCLING_ROAD", "FLAG_SYS_CRUISE_MODE",
        "FLAG_SYS_SAFARI_MODE", "FLAG_SYS_USE_STRENGTH", "FLAG_SYS_FLASH_ACTIVE", "FLAG_SYS_QL_DEPARTED"];
    public static ImmutableArray<string> ResetVariables { get; } = ["VAR_MAP_SCENE_ROUTE16",
        "VAR_MAP_SCENE_FUCHSIA_CITY_SAFARI_ZONE_ENTRANCE", "VAR_QL_ENTRANCE"];

    public static bool KnowsFly(PartyPokemon member)
    {
        ArgumentNullException.ThrowIfNull(member);
        return !member.IsEgg && member.Pokemon.Moves.Any(slot => slot.Move.Value == FlyMove);
    }
    public static bool MapTypeAllowsFly(string mapType) => mapType is
        "MAP_TYPE_TOWN" or "MAP_TYPE_CITY" or "MAP_TYPE_ROUTE" or "MAP_TYPE_OCEAN_ROUTE";
    public static bool CanStart(PartyPokemon member, bool hasThunderBadge, string mapType,
        bool linkActive = false, bool inUnionRoom = false) =>
        KnowsFly(member) && !linkActive && !inUnionRoom && hasThunderBadge && MapTypeAllowsFly(mapType);
    public static FieldFlyGiftResult Gift(bool alreadyReceived, bool hasItemSpace) => alreadyReceived
        ? FieldFlyGiftResult.AlreadyReceived : hasItemSpace ? FieldFlyGiftResult.GiveHm02 : FieldFlyGiftResult.NoRoom;
}

/// <summary>Original destination/visited-flag and region tables, without a second map UI or warp executor.</summary>
public sealed class FieldFlyCatalog
{
    public const string SourceSha256 = "5f876ca42acad49755449692e33f7e9a73ed4a093c6b8c5b0b08f3a1e77c7608";
    public ImmutableArray<FieldFlyDestination> Destinations { get; }
    private readonly ImmutableDictionary<string, FieldFlyRegion> _regions;
    private FieldFlyCatalog(FieldFlySourceDocument source)
    { Destinations = source.Destinations; _regions = source.SectionRegions.ToImmutableDictionary(row => row.SectionId, row => row.Region, StringComparer.Ordinal); }
    public static FieldFlyCatalog Authenticate(FieldFlySourceDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.SourceCommit != FieldFlySourceRules.SourceCommit || source.Destinations.IsDefault || source.Destinations.Length != 20 ||
            source.SectionRegions.IsDefault || CampaignSourceFingerprint.Compute(source) != SourceSha256)
            throw new ArgumentException("field_fly_source");
        return new(source);
    }
    public FieldFlyRegion? RegionForSection(string section) => _regions.TryGetValue(section, out var region) ? region : null;
    public FieldFlyDestination? DestinationForSection(string section) => Destinations.SingleOrDefault(row => row.SectionId == section);
    // Fly mode has no map-switch button. A visited flag alone cannot cross region-map groups.
    public bool CanTravel(string currentSection, string destinationSection, IReadOnlyDictionary<string, bool> flags)
    {
        ArgumentNullException.ThrowIfNull(flags);
        return DestinationForSection(destinationSection) is { } destination && RegionForSection(currentSection) == destination.Region &&
            flags.TryGetValue(destination.VisitedFlag, out bool visited) && visited;
    }
    public ImmutableArray<FieldFlyDestination> AvailableDestinations(string currentSection, IReadOnlyDictionary<string, bool> flags) =>
        Destinations.Where(row => CanTravel(currentSection, row.SectionId, flags)).ToImmutableArray();
}
