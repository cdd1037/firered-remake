// Reconstructed and reverified after workspace recovery; see docs/recovery-moon-stone.md.
using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>An actual-use proposal, never a party-menu-only compatibility indication.</summary>
public sealed record ItemEvolutionEligibility(long InstanceId, SpeciesId PreviousSpecies, SpeciesId TargetSpecies,
    int Level, string Item, PokemonSourceRef Source)
{
    public bool CanCancel => false;
}
public sealed record ItemEvolutionLearnedMove(int Level, MoveId Move, int Slot);
public sealed record ItemEvolutionPendingMove(int Level, MoveId Move);
public sealed record ItemEvolutionResult(PartyPokemon Pokemon, SpeciesId PreviousSpecies, SpeciesId TargetSpecies,
    string Item, bool NicknameChanged, ImmutableArray<ItemEvolutionLearnedMove> LearnedMoves,
    ImmutableArray<ItemEvolutionPendingMove> PendingMoves);

/// <summary>
/// Pinned FireRed six-stone use in an exact authenticated owned campaign catalog. Pure eligibility/species
/// changes only: no RNG, inventory consumption, scene state, Pokédex changes, UI or IO.
/// </summary>
public static class ItemEvolutionRules
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-moon-stone-evolution-v1";
    public const string MoonStone = "ITEM_MOON_STONE";
    public const string SunStone = "ITEM_SUN_STONE";
    public const string ThunderStone = "ITEM_THUNDER_STONE";
    public const string FireStone = "ITEM_FIRE_STONE";
    public const string WaterStone = "ITEM_WATER_STONE";
    public const string LeafStone = "ITEM_LEAF_STONE";

    // Original include/constants/items.h identities. A held trade item is never an item-use stone.
    public static int? SourceItemId(string? item) => item switch
    {
        SunStone => 93, MoonStone => 94, FireStone => 95,
        ThunderStone => 96, WaterStone => 97, LeafStone => 98, _ => null
    };

    /// <summary>
    /// EVO_MODE_ITEM_USE: the first matching item method wins, at any current level and without a
    /// battle or level gain. Everstone prevents actual use even though the party menu can show a
    /// compatible target. The caller must own one separate bag item and consume it only on success.
    /// </summary>
    public static ItemEvolutionEligibility? Eligibility(CampaignPokemonContent content, OwnedBattlePokemonCatalog ownership,
        PartyPokemon owned, string item = MoonStone)
    {
        Validate(content, ownership, owned, item);
        if (owned.HeldItem == "ITEM_EVERSTONE") return null;
        var evolution = SourceEvolution(content, ownership, owned.Pokemon.Species, item);
        return evolution is null ? null : new(owned.InstanceId, owned.Pokemon.Species, evolution.Target,
            owned.Pokemon.Level, item, evolution.Source);
    }

    /// <summary>
    /// EVO_MODE_ITEM_CHECK, for the source party-menu compatibility label only. This ignores an
    /// Everstone; always call Eligibility again before actual use. Eggs remain unsupported.
    /// </summary>
    public static SpeciesId? PartyMenuTarget(CampaignPokemonContent content, OwnedBattlePokemonCatalog ownership,
        PartyPokemon owned, string item = MoonStone)
    {
        Validate(content, ownership, owned, item);
        return SourceEvolution(content, ownership, owned.Pokemon.Species, item)?.Target;
    }

    /// <summary>
    /// Applies a current actual-use proposal through the same species/stat/name/exact-level-move
    /// implementation as level evolution. The source item scene cannot be cancelled. Existing
    /// held items are preserved; they are never the bag stone being consumed. Retain PendingMoves
    /// for explicit move decisions, including Body Slam/Thrash only at exactly level 22.
    /// </summary>
    public static ItemEvolutionResult Apply(CampaignPokemonContent content, OwnedBattlePokemonCatalog ownership, PartyPokemon owned,
        ItemEvolutionEligibility eligibility)
    {
        ArgumentNullException.ThrowIfNull(eligibility);
        var expected = Eligibility(content, ownership, owned, eligibility.Item);
        if (expected is null || expected != eligibility)
            throw new ArgumentException("Item evolution proposal is stale, ineligible or belongs to another Pokemon.", nameof(eligibility));
        var evolved = LevelEvolutionRules.ApplySpeciesChange(content, owned, eligibility.TargetSpecies);
        return new(evolved.Pokemon, evolved.PreviousSpecies, evolved.TargetSpecies, eligibility.Item, evolved.NicknameChanged,
            evolved.LearnedMoves.Select(m => new ItemEvolutionLearnedMove(m.Level, m.Move, m.Slot)).ToImmutableArray(),
            evolved.PendingMoves.Select(m => new ItemEvolutionPendingMove(m.Level, m.Move)).ToImmutableArray());
    }

    /// <summary>Authenticated source edge shared by use, menu labels and saved lineage checks.
    /// Only the six source stones and targets admitted by the exact owned catalog are eligible.
    /// The full source table retains branches omitted from earlier regional catalog projections.</summary>
    public static SourceMechanicsEvolution? SourceEvolution(CampaignPokemonContent content,
        OwnedBattlePokemonCatalog ownership, SpeciesId species, string item)
    {
        ValidateCatalog(content, ownership, item);
        if (!content.Catalog.Species.ContainsKey(species))
            throw new ArgumentException("item_evolution_species_not_owned", nameof(species));
        int sourceItem = SourceItemId(item)!.Value;
        return ownership.Mechanics.SpeciesMechanics[species].SourceEvolutions.FirstOrDefault(e =>
            e.Method == "EVO_ITEM" && e.SourceMethodId == 7 && e.Parameter == item &&
            e.ParameterValue == sourceItem && content.Catalog.Species.ContainsKey(e.Target));
    }

    private static void Validate(CampaignPokemonContent content, OwnedBattlePokemonCatalog ownership,
        PartyPokemon owned, string item)
    {
        ValidateCatalog(content, ownership, item);
        LevelEvolutionRules.Validate(content, owned);
    }

    private static void ValidateCatalog(CampaignPokemonContent content, OwnedBattlePokemonCatalog ownership, string item)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(ownership);
        if (!ReferenceEquals(ownership.Catalog, content.Catalog) ||
            !ReferenceEquals(OwnedBattlePokemonCatalog.For(content.Catalog), ownership))
            throw new ArgumentException("item_evolution_owned_catalog_identity", nameof(ownership));
        if (SourceItemId(item) is null)
            throw new NotSupportedException("Only the six original evolution stones have an item-use evolution method.");
    }
}
