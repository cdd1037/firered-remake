using System.Collections.Immutable;
using Game.Content;
using Game.Domain.Battle;
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
            // This is the current Application fallback for these exact regional tables,
            // not the retired per-region factories that production never called.
            var pending = new EarlyLandEncounterState(new(123),
                Pending: new(new(table.Map, slot.Index, slot.Species, level), EarlyLandEncounterRules.TallGrassBehavior),
                RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion);
            var actual = SevenIslandWildPokemonFactory.Generate(Owned, table, pending, rng, 42);
            var expected = GenerateOrdinaryFixture(slot.Species, level, rng);
            Assert.AreEqual(expected.Pokemon with { Moves = actual.Pokemon.Moves }, actual.Pokemon);
            CollectionAssert.AreEqual(expected.Pokemon.Moves.ToArray(), actual.Pokemon.Moves.ToArray());
            Assert.AreEqual(expected.GlobalRng, actual.GlobalRng);
            CollectionAssert.AreEqual(expected.RandomTrace.ToArray(), actual.RandomTrace.ToArray());
            Assert.IsTrue(CanonicalWildPokemonFactory.IsInitialInstance(Owned, actual.Pokemon));
            Assert.IsFalse(CanonicalWildPokemonFactory.IsInitialInstance(Owned, actual.Pokemon with { CurrentHp = 0 }));
            AssertRegionalEntry(region, table.Map, slot.Index, 0, actual.Pokemon, actual.GlobalRng);


        }
    }

    [TestMethod]
    [DataRow("victory-road")]
    [DataRow("sevii-ruby")]
    [DataRow("four-island")]
    [DataRow("six-island")]
    [DataRow("five-island")]
    public void RegionalSurfOriginsUseCurrentWaterGenerationAndStrictEntry(string region)
    {
        var tables = region switch {
            "victory-road" => VictoryRoadEncounterTables.Water,
            "sevii-ruby" => SeviiRubyEncounterTables.Water,
            "four-island" => FourIslandEncounterTables.Water,
            "six-island" => SixIslandEncounterTables.Water,
            _ => FiveIslandEncounterTables.Water };
        Assert.IsFalse(tables.IsEmpty);
        var catalog = WaterEncounterCatalog.Authenticate(tables);
        foreach (var table in tables)
        foreach (var slot in table.Slots)
        foreach (byte level in new[] { slot.MinimumLevel, slot.MaximumLevel }.Distinct())
        {
            var pending = new WaterEncounterState(new(123), Pending: new(new(table.Map, slot.Index, slot.Species, level), 0));
            var actual = WaterEncounterRules.Generate(Owned, catalog.Tables, pending, table, new(1234, 19), 42);
            AssertRegionalEntry(region, table.Map, slot.Index, 1, actual.Pokemon, actual.GlobalRng);
        }
    }

    private static void AssertRegionalEntry(string region, MapId map, int slot, int kind, PokemonInstance pokemon, RngState rng)
    {
        var context = RegionalContext(region, map, slot, kind);
        var entry = SourceWildBattlePolicy.StartOwned(Owned, [new(pokemon)], 0, new(pokemon), rng, context);
        Assert.IsTrue(entry.Succeeded, entry.Fault);
        void Reject(WildBattleCombatant opponent, SourceWildEncounterContext origin) =>
            Assert.IsFalse(SourceWildBattlePolicy.StartOwned(Owned, [new(pokemon)], 0, opponent, rng, origin).Succeeded);
        Reject(new(pokemon with { CurrentHp = pokemon.CurrentHp - 1 }), context);
        Reject(new(pokemon, HeldItem: "ITEM_ORAN_BERRY"), context);
        Reject(new(pokemon, Status: 1), context);
        Reject(new(pokemon), RegionalContext(region, new("map.missing"), slot, kind));
        Reject(new(pokemon), RegionalContext(region, map, 12, kind));
        Reject(new(pokemon), RegionalContext(region, map, -1, kind));
        Reject(new(pokemon), RegionalContext(region, map, slot, 99));
        Reject(new(GenerateOrdinaryFixture(new("pret:SPECIES_BULBASAUR"), 5, new(42)).Pokemon), context);
        Reject(new(GenerateOrdinaryFixture(pokemon.Species, 100, new(42)).Pokemon), context);
        Reject(new(pokemon with { Evs = new(1, 0, 0, 0, 0, 0) }), context);
        Reject(new(pokemon with { Moves = pokemon.Moves.SetItem(0, pokemon.Moves[0] with { CurrentPp = 0 }) }), context);
        var entryRng = rng;
        for (int i = 0; i < 4; i++)
        {
            var draw = Gen3Rng.Next(entryRng, "wild-battle.entry.ai-setup.slot-" + i, 16);
            Assert.AreEqual(draw.Trace, entry.Trace[i]);
            entryRng = draw.State;
        }
        var held = Gen3Rng.Next(entryRng, "wild-battle.held-item", 100);
        Assert.AreEqual(held.Trace, entry.Trace[4]);
        Assert.AreEqual(CanonicalWildPokemonFactory.HeldItem(Owned, pokemon.Species, held.Trace.Result),
            entry.State!.Get(new(1)).Body!.HeldItem);
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
    [TestMethod]
    public void SpecialUnownFormAndArticunoGenerationRemainDistinctFromOrdinaryEntry()
    {
        var origin = new SevenIslandWildOrigin(new("map.seven-island-tanoby-ruins-monean-chamber"), SevenIslandEncounterKind.Land, 0);
        var unown = SevenIslandWildPokemonFactory.GenerateSourceSlot(Owned, origin, 25, new(42, 19), 42);
        Assert.AreEqual(UnownFormRules.SourceSlotForm(origin.Map, origin.Slot), UnownFormRules.Form(unown.Pokemon.Personality));
        Assert.AreEqual("wild.unown.personality.1.high16", unown.RandomTrace[0].Purpose);
        Assert.IsFalse(unown.RandomTrace.Any(draw => draw.Purpose == "wild.nature"));
        var unownEntry = SourceWildBattlePolicy.StartOwned(Owned, [new(unown.Pokemon)], 0,
            new(unown.Pokemon), unown.GlobalRng, new() { SevenIsland = origin });
        Assert.IsTrue(unownEntry.Succeeded, unownEntry.Fault);
        Assert.AreEqual(1, unownEntry.Trace.Count(draw => draw.Purpose == "wild-battle.held-item"));

        var articuno = SeafoamPokemonFactory.GenerateArticuno(Owned, new(42, 19), 42);
        Assert.AreEqual(4, articuno.RandomTrace.Length);
        var legendary = SourceWildBattlePolicy.StartOwned(Owned, [new(unown.Pokemon)], 0,
            new(articuno.Pokemon), articuno.GlobalRng, new() { Seafoam = SeafoamWildBattleContract.ArticunoOrigin });
        Assert.IsTrue(legendary.Succeeded, legendary.Fault);
        Assert.IsFalse(legendary.Trace.Any(draw => draw.Purpose == "wild-battle.held-item"));
        Assert.AreEqual("ITEM_NONE", legendary.State!.Get(new(1)).Body!.HeldItem);
        Assert.IsFalse(SourceWildBattlePolicy.StartOwned(Owned, [new(unown.Pokemon)], 0,
            new(articuno.Pokemon), articuno.GlobalRng,
            RegionalContext("four-island", origin.Map, 0, 0)).Succeeded);
    }

    private static SourceWildEncounterContext RegionalContext(string region, MapId map, int slot, int kind) => region switch
    {
        "victory-road" => new() { VictoryRoad = new(map, (VictoryRoadEncounterKind)kind, slot) },
        "sevii-ruby" => new() { SeviiRuby = new(map, (SeviiRubyEncounterKind)kind, slot) },
        "four-island" => new() { FourIsland = new(map, (FourIslandEncounterKind)kind, slot) },
        "six-island" => new() { SixIsland = new(map, (SixIslandEncounterKind)kind, slot) },
        "five-island" => new() { FiveIsland = new(map, (FiveIslandEncounterKind)kind, slot) },
        _ => throw new ArgumentException("Unknown regional fixture.")
    };

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
