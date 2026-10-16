using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Battle.Encounters;

[TestClass]
public sealed class RegionalWildDefinitionTests
{
    private static PretCanonicalPokemonCatalog Canonical => TestAssets.Cached(() => PretCanonicalPokemonCatalog.Load(TestAssets.Root, TestAssets.Root));
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    internal static GamePokemonCatalog Owned => TestAssets.Cached(() => PretGamePokemonCatalog.LoadContent(
        File.ReadAllText(Path.Combine(TestAssets.Root, PretGamePokemonCatalog.RelativePath)), Canonical, Mechanics));

    [TestMethod]
    [DataRow("victory-road")]
    [DataRow("sevii-ruby")]
    [DataRow("four-island")]
    [DataRow("six-island")]
    [DataRow("five-island")]
    public void RegionalSourceOriginsPreserveOrdinaryGenerationAndExactTrace(string region)
    {
        var tables = region switch {
            "victory-road" => VictoryRoadEncounterTables.Land,
            "sevii-ruby" => SeviiRubyEncounterTables.Land,
            "four-island" => FourIslandEncounterTables.Land,
            "six-island" => SixIslandEncounterTables.Land,
            _ => FiveIslandEncounterTables.Land };
        Assert.IsFalse(tables.IsEmpty);
        foreach (var table in tables)
        foreach (var slot in table.Slots)
        foreach (byte level in new[] { slot.MinimumLevel, slot.MaximumLevel }.Distinct())
        {
            var rng = new RngState(42, 19);
            var actual = region switch {
                "victory-road" => VictoryRoadWildPokemonFactory.GenerateSourceSlot(Owned, new(table.Map, VictoryRoadEncounterKind.Land, slot.Index), level, rng, 42),
                "sevii-ruby" => SeviiRubyWildPokemonFactory.GenerateSourceSlot(Owned, new(table.Map, SeviiRubyEncounterKind.Land, slot.Index), level, rng, 42),
                "four-island" => FourIslandWildPokemonFactory.GenerateSourceSlot(Owned, new(table.Map, FourIslandEncounterKind.Land, slot.Index), level, rng, 42),
                "six-island" => SixIslandWildPokemonFactory.GenerateSourceSlot(Owned, new(table.Map, SixIslandEncounterKind.Land, slot.Index), level, rng, 42),
                _ => FiveIslandWildPokemonFactory.GenerateSourceSlot(Owned, new(table.Map, FiveIslandEncounterKind.Land, slot.Index), level, rng, 42) };
            var expected = GenerateOrdinaryFixture(slot.Species, level, rng);
            Assert.AreEqual(expected.Pokemon with { Moves = actual.Pokemon.Moves }, actual.Pokemon);
            CollectionAssert.AreEqual(expected.Pokemon.Moves.ToArray(), actual.Pokemon.Moves.ToArray());
            Assert.AreEqual(expected.GlobalRng, actual.GlobalRng);
            CollectionAssert.AreEqual(expected.RandomTrace.ToArray(), actual.RandomTrace.ToArray());
            Assert.IsTrue(CanonicalWildPokemonFactory.IsInitialInstance(Owned, actual.Pokemon));
            Assert.IsFalse(CanonicalWildPokemonFactory.IsInitialInstance(Owned, actual.Pokemon with { CurrentHp = 0 }));
        }
    }

    [TestMethod]
    [DataRow("BULBASAUR", "ITEM_NONE", "ITEM_NONE")]
    [DataRow("PARAS", "ITEM_TINY_MUSHROOM", "ITEM_BIG_MUSHROOM")]
    // FireRed species_info.h: Pikachu holds neither item (unlike other versions).
    [DataRow("PIKACHU", "ITEM_NONE", "ITEM_NONE")]
    [DataRow("SENTRET", "ITEM_NONE", "ITEM_ORAN_BERRY")]
    [DataRow("SNORLAX", "ITEM_CHESTO_BERRY", "ITEM_CHESTO_BERRY")]
    public void HeldItemBoundariesUseCanonicalPairAndAlwaysConsumeOneDraw(string speciesName, string common, string rare)
    {
        var species = new SpeciesId("pret:SPECIES_" + speciesName);
        foreach (int roll in new[] { 0, 44, 45, 94, 95, 99 })
            Assert.AreEqual(common == rare ? common : roll < 45 ? "ITEM_NONE" : roll < 95 ? common : rare,
                CanonicalWildPokemonFactory.HeldItem(Owned, species, roll));
        var rng = new RngState(42, 19);
        var expected = Gen3Rng.Next(rng, "wild-battle.held-item", 100);
        var actual = CanonicalWildPokemonFactory.AssignHeldItem(Owned, species, rng);
        Assert.AreEqual(expected.State, actual.GlobalRng);
        CollectionAssert.AreEqual(new[] { expected.Trace }, actual.RandomTrace.ToArray());
        Assert.Throws<ArgumentOutOfRangeException>(() => CanonicalWildPokemonFactory.HeldItem(Owned, species, 100));
    }

    [TestMethod]
    public void OrdinarySurfBindingRejectsChangedOriginAndInitialState()
    {
        var table = CinnabarWaterEncounterCatalog.SourceAdditions[0] with { Map = new("map.supplied-surf") };
        var tables = ImmutableDictionary<MapId, WaterEncounterTable>.Empty.Add(table.Map, table);
        var slot = table.Slots[0];
        var candidate = new WaterEncounterCandidate(table.Map, 0, slot.Species, slot.MinimumLevel);
        var binding = WaterEncounterCatalog.BindBattle(tables, Owned, candidate);
        var pokemon = GenerateOrdinaryFixture(slot.Species, slot.MinimumLevel, new(42)).Pokemon;
        Assert.IsTrue(binding.SourceInitialInstance(Owned, pokemon));
        Assert.IsFalse(binding.Matches(pokemon with { Level = pokemon.Level + 1 }));
        Assert.IsFalse(binding.SourceInitialInstance(Owned, pokemon with { CurrentHp = 0 }));
        Assert.Throws<ArgumentException>(() => WaterEncounterCatalog.BindBattle(tables, Owned, candidate with { Slot = 5 }));
        Assert.Throws<ArgumentException>(() => WaterEncounterCatalog.BindBattle(tables, Owned, candidate with { Species = new("pret:SPECIES_MEW") }));
        Assert.Throws<ArgumentException>(() => WaterEncounterCatalog.BindBattle(tables, Owned, candidate with { Map = new("map.missing") }));
    }
    private static Route1WildPokemonResult GenerateOrdinaryFixture(SpeciesId species, byte level, RngState rng)
    {
        var map = new MapId("map.test-supplied-water");
        var table = new WaterEncounterTable(map, 10, WaterEncounterRules.SlotWeights.Select((weight, index) =>
            new WaterEncounterSlot(index, species, level, level, weight)).ToImmutableArray());
        var catalog = WaterEncounterCatalog.Authenticate([table]);
        var pending = new WaterEncounterState(new(0), Pending: new(new(map, 0, species, level), 0));
        return WaterEncounterRules.Generate(Owned, catalog.Tables, pending, table, rng, 42);
    }
}
