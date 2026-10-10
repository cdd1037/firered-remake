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
/// Pinned FireRed Moon Stone use in the authenticated MtMoon catalog. Pure eligibility/species
/// changes only: no RNG, inventory consumption, scene state, Pokédex changes, UI or IO.
/// </summary>
public static class ItemEvolutionRules
{
    public const string SourceCommit = PokemonFactory.SourceCommit;
    public const string RulesVersion = "firered-moon-stone-evolution-v1";
    public const string MoonStone = "ITEM_MOON_STONE";

    /// <summary>
    /// EVO_MODE_ITEM_USE: the first matching item method wins, at any current level and without a
    /// battle or level gain. Everstone prevents actual use even though the party menu can show a
    /// compatible target. The caller must own one separate bag item and consume it only on success.
    /// </summary>
    public static ItemEvolutionEligibility? Eligibility(CampaignPokemonContent content, PartyPokemon owned,
        string item = MoonStone)
    {
        Validate(content, owned, item);
        if (owned.HeldItem == "ITEM_EVERSTONE") return null;
        var evolution = MatchingEvolution(content, owned, item);
        return evolution is null ? null : new(owned.InstanceId, owned.Pokemon.Species, evolution.Target,
            owned.Pokemon.Level, item, evolution.Source);
    }

    /// <summary>
    /// EVO_MODE_ITEM_CHECK, for the source party-menu compatibility label only. This ignores an
    /// Everstone; always call Eligibility again before actual use. Eggs remain unsupported.
    /// </summary>
    public static SpeciesId? PartyMenuTarget(CampaignPokemonContent content, PartyPokemon owned,
        string item = MoonStone)
    {
        Validate(content, owned, item);
        return MatchingEvolution(content, owned, item)?.Target;
    }

    /// <summary>
    /// Applies a current actual-use proposal through the same species/stat/name/exact-level-move
    /// implementation as level evolution. The source item scene cannot be cancelled. Existing
    /// held items are preserved; they are never the bag stone being consumed. Retain PendingMoves
    /// for explicit move decisions, including Body Slam/Thrash only at exactly level 22.
    /// </summary>
    public static ItemEvolutionResult Apply(CampaignPokemonContent content, PartyPokemon owned,
        ItemEvolutionEligibility eligibility)
    {
        ArgumentNullException.ThrowIfNull(eligibility);
        var expected = Eligibility(content, owned, eligibility.Item);
        if (expected is null || expected != eligibility)
            throw new ArgumentException("Item evolution proposal is stale, ineligible or belongs to another Pokemon.", nameof(eligibility));
        var evolved = LevelEvolutionRules.ApplySpeciesChange(content, owned, eligibility.TargetSpecies);
        return new(evolved.Pokemon, evolved.PreviousSpecies, evolved.TargetSpecies, eligibility.Item, evolved.NicknameChanged,
            evolved.LearnedMoves.Select(m => new ItemEvolutionLearnedMove(m.Level, m.Move, m.Slot)).ToImmutableArray(),
            evolved.PendingMoves.Select(m => new ItemEvolutionPendingMove(m.Level, m.Move)).ToImmutableArray());
    }

    // ValidateParty validates the exact four admitted Moon Stone families. Their targets are
    // Kanto species, so both the source party-menu and scene National Dex gates always pass.
    private static PokemonEvolution? MatchingEvolution(CampaignPokemonContent content, PartyPokemon owned, string item) =>
        content.Catalog.Species[owned.Pokemon.Species].Evolutions.FirstOrDefault(e =>
            e.Method == PokemonEvolutionMethod.Item && e.Parameter == item);

    private static void Validate(CampaignPokemonContent content, PartyPokemon owned, string item)
    {
        LevelEvolutionRules.Validate(content, owned);
        if (content.Catalog.Species.Count is not (56 or 65 or 68 or 70 or 73 or 79))
            throw new NotSupportedException("Moon Stone evolution requires the verified MtMoon or Cerulean catalog.");
        if (item != MoonStone)
            throw new NotSupportedException("Only Moon Stone item evolution is implemented by this profile.");
    }
}
