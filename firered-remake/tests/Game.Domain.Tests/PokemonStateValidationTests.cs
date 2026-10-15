using System.Collections.Immutable;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Domain;

[TestClass, TestCategory("Game")]
public sealed class PokemonStateValidationTests
{
    private static readonly SpeciesId Species = new("pret:SPECIES_BULBASAUR");
    private static readonly MoveId Move = new("pret:MOVE_TACKLE");
    private static PokemonCatalog Catalog()
    {
        var species = new PokemonSpeciesDefinition(Species, 1, new(45, 49, 49, 45, 65, 65),
            "TYPE_GRASS", "TYPE_POISON", 45, 64, SixStats.Zero, GrowthRate.MediumSlow,
            31, "ABILITY_OVERGROW", "ABILITY_NONE", 70, [new(1, Move)], []);
        var move = new PokemonMoveDefinition(Move, 33, 35, 35, "TYPE_NORMAL", 95, "EFFECT_HIT", 0,
            "MOVE_TARGET_SELECTED", 0, [], false);
        var catalog = new PokemonCatalog(PokemonFactory.SourceCommit,
            ImmutableDictionary<SpeciesId, PokemonSpeciesDefinition>.Empty.Add(Species, species),
            ImmutableDictionary<MoveId, PokemonMoveDefinition>.Empty.Add(Move, move));
        PokemonFactory.ValidateDefinitions(catalog);
        return catalog;
    }
    private static PokemonInstance Pokemon(PokemonCatalog catalog) => PokemonFactory.Create(catalog,
        new(Species, 30, 42, IvPolicy.Fixed(20), PersonalityPolicy.Fixed(123)), new(0)).Pokemon;
    private static SixStats Set(SixStats stats, int index, int value) => index switch
    {
        0 => stats with { Hp = value }, 1 => stats with { Attack = value },
        2 => stats with { Defense = value }, 3 => stats with { Speed = value },
        4 => stats with { SpecialAttack = value }, 5 => stats with { SpecialDefense = value },
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
    public static IEnumerable<object[]> InvalidStats()
    {
        for (int i = 0; i < 6; i++)
        {
            yield return [i, -1, true]; yield return [i, 32, true];
            yield return [i, -1, false]; yield return [i, 256, false];
        }
    }
    [TestMethod, DynamicData(nameof(InvalidStats))]
    public void EveryIvAndEvFieldRetainsItsRangeCheck(int index, int value, bool iv)
    {
        var catalog = Catalog(); var pokemon = Pokemon(catalog);
        var changed = iv ? pokemon with { Ivs = Set(pokemon.Ivs, index, value) }
            : pokemon with { Evs = Set(pokemon.Evs, index, value) };
        Assert.AreEqual(iv ? "pokemon_ivs" : "pokemon_evs", PokemonStateValidator.Validate(catalog, changed));
        Assert.IsNull(PokemonStateValidator.Validate(catalog, pokemon));
    }
    [TestMethod]
    public void TotalEvLimitAndStaleStatBasisRemainStrict()
    {
        var catalog = Catalog(); var pokemon = Pokemon(catalog);
        var limit = new SixStats(85, 85, 85, 85, 85, 85);
        var valid = pokemon with { Evs = limit, StatCalculationEvs = SixStats.Zero };
        Assert.IsNull(PokemonStateValidator.Validate(catalog, valid));
        for (int i = 0; i < 6; i++)
        {
            Assert.AreEqual("pokemon_evs", PokemonStateValidator.Validate(catalog,
                valid with { Evs = Set(limit, i, 86) }));
            Assert.AreEqual("pokemon_stat_calculation_evs", PokemonStateValidator.Validate(catalog,
                valid with { StatCalculationEvs = Set(SixStats.Zero, i, 86) }));
            Assert.AreEqual("pokemon_stat_calculation_evs", PokemonStateValidator.Validate(catalog,
                valid with { StatCalculationEvs = Set(SixStats.Zero, i, -1) }));
            Assert.AreEqual("pokemon_stat_calculation_evs", PokemonStateValidator.Validate(catalog,
                valid with { StatCalculationEvs = Set(SixStats.Zero, i, 256) }));
        }
        Assert.AreEqual("pokemon_stats", PokemonStateValidator.Validate(catalog,
            valid with { StatCalculationEvs = new(1, 1, 1, 84, 1, 1) }));
    }
}
