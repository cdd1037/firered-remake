using System.Collections.Immutable;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class PokemonAdmissionPolicyTests
{
    private static readonly SpeciesId First = new("pret:SPECIES_BULBASAUR");
    private static readonly SpeciesId Second = new("pret:SPECIES_IVYSAUR");
    private static readonly SpeciesId Excluded = new("pret:SPECIES_MEW");
    private static SourceMechanicsCatalog Definitions()
    {
        PokemonSpeciesDefinition Species(SpeciesId id, int number) => new(id, number,
            new(45, 49, 49, 45, 65, 65), "TYPE_GRASS", "TYPE_POISON", 45, 64, SixStats.Zero,
            GrowthRate.MediumSlow, 31, "ABILITY_OVERGROW", "ABILITY_NONE", 70, [], []);
        var species = new[] { Species(First, 1), Species(Second, 2), Species(Excluded, 151) }.ToImmutableDictionary(s => s.Id);
        var rows = species.Keys.ToImmutableDictionary(id => id, id => new SourceSpeciesMechanics(id, 69,
            id == First ? [new("EVO_LEVEL", 4, "16", 16, Second)] : []));
        return SourceMechanicsCatalog.FromDefinitions(new(PokemonFactory.SourceCommit, species,
            ImmutableDictionary<MoveId, PokemonMoveDefinition>.Empty)
        {
            MechanicsData = new(rows, ImmutableDictionary<string, SourceAbilityIdentity>.Empty, Excluded, [])
        });
    }
    private static PokemonCatalog Scope(SourceMechanicsCatalog definitions, params SpeciesId[] species) =>
        definitions.Catalog with { Species = definitions.Catalog.Species.Where(p => species.Contains(p.Key)).ToImmutableDictionary() };

    [TestMethod]
    public void ExplicitRosterDoesNotAdmitEveryCanonicalDefinition()
    {
        var definitions = Definitions();
        var game = GamePokemonCatalog.Create(definitions, Scope(definitions, First), Scope(definitions, First, Second), Scope(definitions, Second));
        Assert.IsFalse(game.Catalog.Species.ContainsKey(Second));
        Assert.IsTrue(game.Opponents.Catalog.Species.ContainsKey(Second));
        Assert.IsFalse(game.Opponents.Catalog.Species.ContainsKey(Excluded));
        Assert.IsTrue(game.Mechanics.Catalog.Species.ContainsKey(Excluded));
    }

    [TestMethod]
    public void OwnedRosterMustBeContainedInOrdinaryOpponents()
    {
        var definitions = Definitions();
        Assert.Throws<ArgumentException>(() => GamePokemonCatalog.Create(definitions,
            Scope(definitions, First), Scope(definitions, Second), Scope(definitions, Second)));
    }

    [TestMethod]
    public void EvolutionProjectionMustMatchCanonicalMethodAndRemainInsideRoster()
    {
        var definitions = Definitions();
        var scope = Scope(definitions, First, Second);
        var valid = scope with { Species = scope.Species.SetItem(First, scope.Species[First] with { Evolutions = [new(16, Second)] }) };
        var equivalentCopy = valid with { Species = valid.Species.ToImmutableDictionary() };
        var game = GamePokemonCatalog.Create(definitions, equivalentCopy, equivalentCopy, scope);
        Assert.AreEqual(16, game.Catalog.Species[First].Evolutions.Single().Level);
        var changed = valid with { Species = valid.Species.SetItem(First, valid.Species[First] with { Evolutions = [new(15, Second)] }) };
        Assert.Throws<ArgumentException>(() => GamePokemonCatalog.Create(definitions, changed, changed, scope));
        var outside = valid with { Species = valid.Species.Remove(Second) };
        Assert.Throws<ArgumentException>(() => GamePokemonCatalog.Create(definitions, outside, valid, scope));
    }

    [TestMethod]
    public void UnspecifiedFormStatsCannotChangeCanonicalDefinition()
    {
        var definitions = Definitions();
        var scope = Scope(definitions, First);
        var changed = scope with { Species = scope.Species.SetItem(First, scope.Species[First] with { BaseStats = new(1, 1, 1, 1, 1, 1) }) };
        Assert.Throws<ArgumentException>(() => GamePokemonCatalog.Create(definitions, changed, scope, scope));
    }
}
