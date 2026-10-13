using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>The source Sun Stone branch available to owned Gloom. Trade evolution and trainer-only Sunkern stay outside ownership.</summary>
public static class SixIslandItemEvolutionRules
{
    public const string SunStone = "ITEM_SUN_STONE";
    public static SpeciesId Gloom { get; } = new("pret:SPECIES_GLOOM");
    public static SpeciesId Bellossom { get; } = new("pret:SPECIES_BELLOSSOM");

    public static ItemEvolutionEligibility? Eligibility(CampaignPokemonContent content, PartyPokemon owned)
    {
        Validate(content, owned);
        if (owned.HeldItem == "ITEM_EVERSTONE" || owned.Pokemon.Species != Gloom) return null;
        var source = SourceEdge(SixIslandOwnedPokemonCatalog.For(content.Catalog)!);
        return new(owned.InstanceId, Gloom, Bellossom, owned.Pokemon.Level, SunStone, source);
    }
    public static SpeciesId? PartyMenuTarget(CampaignPokemonContent content, PartyPokemon owned)
    {
        Validate(content, owned);
        return owned.Pokemon.Species == Gloom ? Bellossom : null;
    }
    public static ItemEvolutionResult Apply(CampaignPokemonContent content, PartyPokemon owned, ItemEvolutionEligibility eligibility)
    {
        ArgumentNullException.ThrowIfNull(eligibility);
        var expected = Eligibility(content, owned);
        if (expected is null || expected != eligibility) throw new ArgumentException("six_island_item_evolution_stale");
        var evolved = LevelEvolutionRules.ApplySpeciesChange(content, owned, Bellossom);
        return new(evolved.Pokemon, evolved.PreviousSpecies, evolved.TargetSpecies, SunStone, evolved.NicknameChanged,
            evolved.LearnedMoves.Select(m => new ItemEvolutionLearnedMove(m.Level, m.Move, m.Slot)).ToImmutableArray(),
            evolved.PendingMoves.Select(m => new ItemEvolutionPendingMove(m.Level, m.Move)).ToImmutableArray());
    }
    public static PokemonSourceRef SourceEdge(SixIslandOwnedPokemonCatalog owned)
    {
        if (!ReferenceEquals(SixIslandOwnedPokemonCatalog.For(owned.Catalog), owned)) throw new ArgumentException("six_island_item_evolution_catalog");
        var matches = owned.Mechanics.SpeciesMechanics[Gloom].SourceEvolutions.Where(e => e.Method == "EVO_ITEM" &&
            e.SourceMethodId == 7 && e.Parameter == SunStone && e.ParameterValue == 93 && e.Target == Bellossom).ToArray();
        if (matches.Length != 1 || !owned.Catalog.Species.ContainsKey(Bellossom)) throw new ArgumentException("six_island_item_evolution_source");
        return matches[0].Source;
    }
    private static void Validate(CampaignPokemonContent content, PartyPokemon owned)
    {
        if (SixIslandOwnedPokemonCatalog.For(content.Catalog) is not {} proof) throw new NotSupportedException("six_island_item_evolution_catalog");
        _ = SourceEdge(proof);
        LevelEvolutionRules.Validate(content, owned);
    }
}
