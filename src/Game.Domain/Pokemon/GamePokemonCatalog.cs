using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.Pokemon;

/// <summary>Current game Pokemon definitions and explicitly admitted campaign rosters.
/// Canonical mechanics alone never grant permanent ownership or encounter admission.</summary>
public sealed class GamePokemonCatalog
{
    public const string Profile = "firered-campaign-pokemon-v1";
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics { get; }
    public GameOpponentPokemonCatalog Opponents { get; }
    public GameOpponentPokemonCatalog TrainerTowerOpponents { get; }
    public string RulesVersion => Profile;

    private GamePokemonCatalog(SourceMechanicsCatalog mechanics, PokemonCatalog owned,
        PokemonCatalog opponents, PokemonCatalog trainerTower)
    {
        Mechanics = mechanics;
        Catalog = owned;
        Opponents = new(this, opponents, GameOpponentPokemonCatalog.Profile);
        TrainerTowerOpponents = new(this, trainerTower, GameOpponentPokemonCatalog.TrainerTowerProfile);
    }

    public static GamePokemonCatalog Create(SourceMechanicsCatalog mechanics, PokemonCatalog owned,
        PokemonCatalog opponents, PokemonCatalog trainerTower)
    {
        ArgumentNullException.ThrowIfNull(mechanics);
        ValidateDefinitions(mechanics, owned);
        ValidateDefinitions(mechanics, opponents);
        ValidateDefinitions(mechanics, trainerTower);
        if (owned.Species.IsEmpty || opponents.Species.IsEmpty || trainerTower.Species.IsEmpty ||
            !owned.Species.Keys.All(opponents.Species.ContainsKey))
            throw new ArgumentException("campaign_pokemon_roster");
        return new(mechanics, owned, opponents, trainerTower);
    }

    // Unbound legacy grass/water entry keeps its reviewed roster. Catalog-bound ordinary land
    // and special encounters supply validated encounter contracts to the shared battle policy.
    internal bool AdmitsWildOpponent(PokemonInstance pokemon) =>
        Catalog.Species.ContainsKey(pokemon.Species) &&
        (PokemonFactory.FuchsiaSupportedSpecies.Contains(pokemon.Species) ||
         pokemon.Species.Value == "pret:SPECIES_PSYDUCK" ||
         Game.Domain.Encounters.CinnabarWildPokemonFactory.WildSpecies.Contains(pokemon.Species));

    internal bool SupportsOrdinaryWildOpponent(PokemonInstance pokemon) => AdmitsWildOpponent(pokemon) &&
        (Game.Domain.WildBattle.FuchsiaBattleScope.SupportsWildOpponent(pokemon.SourceSpeciesId) ||
         pokemon.SourceSpeciesId is 54 or 72 or 109 ||
         Game.Domain.Encounters.CinnabarWildPokemonFactory.WildSpecies.Contains(pokemon.Species));

    private static void ValidateDefinitions(SourceMechanicsCatalog mechanics, PokemonCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.MechanicsData is not { } data || data.MaximumHpOneSpecies != mechanics.Data.MaximumHpOneSpecies ||
            !data.FormStatOverrides.SequenceEqual(mechanics.Data.FormStatOverrides) ||
            data.Abilities.Count != mechanics.Data.Abilities.Count || data.Abilities.Any(pair =>
                !mechanics.Data.Abilities.TryGetValue(pair.Key, out var expected) || pair.Value != expected) ||
            data.SpeciesMechanics.Count != mechanics.Data.SpeciesMechanics.Count || data.SpeciesMechanics.Any(pair =>
                !mechanics.Data.SpeciesMechanics.TryGetValue(pair.Key, out var expected) ||
                pair.Value.Species != expected.Species || pair.Value.WeightHectograms != expected.WeightHectograms ||
                pair.Value.ItemCommon != expected.ItemCommon || pair.Value.ItemRare != expected.ItemRare ||
                !pair.Value.SourceEvolutions.SequenceEqual(expected.SourceEvolutions)))
            throw new ArgumentException("campaign_pokemon_mechanics");
        if (catalog.Species is null || catalog.Moves is null || catalog.Moves.Count != mechanics.Catalog.Moves.Count ||
            catalog.Moves.Any(pair => !mechanics.Catalog.Moves.TryGetValue(pair.Key, out var canonical) || pair.Value != canonical))
            throw new ArgumentException("campaign_pokemon_moves");
        foreach (var (id, species) in catalog.Species)
        {
            if (!mechanics.Catalog.Species.TryGetValue(id, out var canonical) || species is null ||
                species.Evolutions.IsDefault || species.LevelUpMoves.IsDefault)
                throw new ArgumentException("campaign_pokemon_species");
            var form = mechanics.FormStatOverrides.FirstOrDefault(row => row.Species == id);
            var expectedStats = form?.BaseStats ?? canonical.BaseStats;
            if (!species.LevelUpMoves.SequenceEqual(canonical.LevelUpMoves) ||
                species with { LevelUpMoves = canonical.LevelUpMoves, Evolutions = canonical.Evolutions, BaseStats = canonical.BaseStats } != canonical ||
                species.BaseStats != canonical.BaseStats && species.BaseStats != expectedStats)
                throw new ArgumentException("campaign_pokemon_definition");
            foreach (var edge in species.Evolutions)
                if (!catalog.Species.ContainsKey(edge.Target) ||
                    !mechanics.SpeciesMechanics[id].SourceEvolutions.Any(row => MatchesEvolution(edge, row)))
                    throw new ArgumentException("campaign_pokemon_evolution");
        }
    }
    private static bool MatchesEvolution(PokemonEvolution edge, SourceMechanicsEvolution row) =>
        edge.Target == row.Target && edge.Method switch
        {
            PokemonEvolutionMethod.Level => row.Method == "EVO_LEVEL" && row.ParameterValue == edge.Level && edge.Parameter is null,
            PokemonEvolutionMethod.Item => row.Method == "EVO_ITEM" && edge.Level == 0 && row.Parameter == edge.Parameter,
            PokemonEvolutionMethod.Trade => row.Method == "EVO_TRADE" && edge.Level == 0 && edge.Parameter is null,
            PokemonEvolutionMethod.TradeItem => row.Method == "EVO_TRADE_ITEM" && edge.Level == 0 && row.Parameter == edge.Parameter,
            PokemonEvolutionMethod.Friendship => row.Method == "EVO_FRIENDSHIP" && edge.Level == 0 && edge.Parameter is null,
            PokemonEvolutionMethod.LevelAttackGreaterDefense => row.Method == "EVO_LEVEL_ATK_GT_DEF" && row.ParameterValue == edge.Level && edge.Parameter is null,
            PokemonEvolutionMethod.LevelAttackEqualDefense => row.Method == "EVO_LEVEL_ATK_EQ_DEF" && row.ParameterValue == edge.Level && edge.Parameter is null,
            PokemonEvolutionMethod.LevelAttackLessDefense => row.Method == "EVO_LEVEL_ATK_LT_DEF" && row.ParameterValue == edge.Level && edge.Parameter is null,
            _ => false
        };
}

/// <summary>An explicit opponent roster for ordinary encounters or the Trainer Tower.</summary>
public sealed class GameOpponentPokemonCatalog
{
    public const string Profile = "firered-campaign-opponents-v1";
    public const string TrainerTowerProfile = "firered-trainer-tower-opponents-v1";
    public GamePokemonCatalog Owned { get; }
    public PokemonCatalog Catalog { get; }
    public SourceMechanicsCatalog Mechanics => Owned.Mechanics;
    public string RulesVersion { get; }
    internal GameOpponentPokemonCatalog(GamePokemonCatalog owned, PokemonCatalog catalog, string rulesVersion)
    { Owned = owned; Catalog = catalog; RulesVersion = rulesVersion; }
}
