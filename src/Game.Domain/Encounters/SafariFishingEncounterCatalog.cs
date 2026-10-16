using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Encounters;

public sealed record SafariFishingTable(FishingRod Rod, OldRodEncounterTable Table);
public sealed record SafariFishingHeldItem(SpeciesId Species, string Common, string Rare);

/// <summary>Safari context and held-item definitions; ordinary and Safari rods share the same immutable task provider.</summary>
public sealed class SafariFishingEncounterCatalog
{
    public const string RulesVersion = "firered-safari-fishing-v1";
    public FishingTaskProfile TaskProfile { get; }
    public ImmutableDictionary<(FishingRod Rod, MapId Map), OldRodEncounterTable> Tables => TaskProfile.Tables;
    public ImmutableDictionary<SpeciesId, FishingHeldItem> HeldItems => TaskProfile.HeldItems;

    public SafariFishingEncounterCatalog(ImmutableArray<SafariFishingTable> tables, ImmutableArray<SafariFishingHeldItem> heldItems)
    {
        if (tables.IsDefaultOrEmpty || heldItems.IsDefaultOrEmpty || tables.Any(row => row?.Table is null) || heldItems.Any(row => row is null))
            throw new ArgumentException("safari_fishing_missing_definitions");
        TaskProfile = new(RulesVersion, tables.Select(row => new FishingRodTable(row.Rod, row.Table)).ToImmutableArray(), [],
            heldItems.Select(row => new FishingHeldItem(row.Species, row.Common, row.Rare)).ToImmutableArray());
        if (Tables.Keys.Any(key => !key.Map.Value.StartsWith("map.safari-zone-", StringComparison.Ordinal)) ||
            Tables.Keys.Select(key => key.Map).Distinct().Any(map => Enum.GetValues<FishingRod>().Any(rod => !HasTable(rod, map))))
            throw new ArgumentException("safari_fishing_map_or_rod_coverage");

    }

    public bool HasTable(FishingRod rod, MapId map) => TaskProfile.HasTable(rod, map);
    public OldRodEncounterTable Table(FishingRod rod, MapId map) => TaskProfile.Table(rod, map);
    public OldRodEncounterSlot? SourceSlot(FishingEncounterOrigin? origin) => TaskProfile.SourceSlot(origin);
    public OldRodFishingResult Start(OldRodCastContext context, RngState rng, bool cancelBeforeCast = false) => TaskProfile.Start(context, rng, cancelBeforeCast);
    public OldRodFishingResult Advance(OldRodFishingState state, OldRodFrameInput input, RngState rng) => TaskProfile.Advance(state, input, rng);
    public void ValidateState(OldRodFishingState state) => TaskProfile.ValidateState(state);
    public OldRodEncounterResult GenerateCandidate(OldRodFishingState fishing, RngState rng) => TaskProfile.GenerateCandidate(fishing, rng);
    public Route1WildPokemonResult GeneratePokemon(GamePokemonCatalog owned, FishingEncounterOrigin origin, RngState rng, uint trainerId) =>
        TaskProfile.GeneratePokemon(owned, origin, rng, trainerId);

    public OldRodHeldItemResult AssignHeldItem(FishingEncounterOrigin origin, RngState rng)
    {
        if (SourceSlot(origin) is null) throw new ArgumentException("safari_fishing_candidate");
        // SetWildMonHeldItem consumes its draw even for identical or NONE/NONE items.
        var draw = Gen3Rng.Next(rng, "safari.fishing.held-item", 100);
        return new(TaskProfile.HeldItem(origin, draw.Trace.Result), draw.State, [draw.Trace]);
    }
}
