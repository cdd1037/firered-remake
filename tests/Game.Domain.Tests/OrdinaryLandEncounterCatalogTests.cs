using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Growth;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.World;

namespace FireRed.Tests.Domain;

[TestClass]
public sealed class OrdinaryLandEncounterCatalogTests
{
    [TestMethod]
    public void SuppliedMapAndAttributesAdmitANewOrdinaryMapWithoutANameWhitelist()
    {
        var table = EarlyLandEncounterRules.Route1Table with { Map = new("map.custom-grass"), RawEncounterRate = 255 };
        var definition = Bind(table, 0xd1);
        var state = new EarlyLandEncounterState(new(0), 0xd1, RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion);
        var check = new EarlyLandEncounterCheck(table.Map, EncounterCheckTrigger.WalkingStepCompleted, 0xd1, EncounterTileType.Land);
        var result = EarlyLandEncounterRules.Check(state, definition, check, new(RulesVersion: state.RulesVersion), new(0, 19));
        Assert.AreEqual(EncounterCheckOutcome.CandidateReady, result.Outcome);
        Assert.AreSame(table, definition.Table);
        definition.ValidateState(result.State, table);
        var complete = EarlyLandEncounterRules.CompleteCandidate(result.State, table, definition);
        Assert.IsNull(complete.Pending);
        Assert.AreEqual((ushort)0xd1, complete.PreviousMetatileBehavior);
        Assert.AreEqual((ulong)21, result.GlobalRng.Cursor);
        CollectionAssert.AreEqual(new[] { "encounter.rate", "encounter.land-slot", "encounter.level" },
            result.RandomTrace.Select(t => t.Draw.Purpose).ToArray());
        Assert.Throws<NotSupportedException>(() => EarlyLandEncounterRules.ValidateTable(table));
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.Check(state, definition,
            check with { Map = new("map.not-loaded") }, new(RulesVersion: state.RulesVersion), new(0)));
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.Check(state, definition,
            check with { MetatileBehavior = 2 }, new(RulesVersion: state.RulesVersion), new(0)));
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.Check(state, definition,
            check with { EncounterType = EncounterTileType.None }, new(RulesVersion: state.RulesVersion), new(0)));
        Assert.Throws<ArgumentException>(() => definition.ValidateState(result.State, table with { RawEncounterRate = 1 }));
        Assert.Throws<ArgumentException>(() => definition.ValidateState(result.State with
            { Pending = result.State.Pending! with { MetatileBehavior = 0xd0 } }, table));
    }

    [TestMethod]
    public void LoadRejectsMissingMapsSpeciesMalformedTablesAndUnsupportedAttributes()
    {
        var table = EarlyLandEncounterRules.Route1Table with { Map = new("map.custom-grass") };
        var tables = new Dictionary<MapId, EarlyLandEncounterTable> { [table.Map] = table };
        var map = new TestMap(table.Map, 0xd1);
        var maps = new Dictionary<MapId, IFieldMap> { [table.Map] = map };
        var pokemon = Pokemon(table);
        Assert.Throws<NotSupportedException>(() => new OrdinaryLandEncounterCatalog(tables,
            new Dictionary<MapId, IFieldMap>(), pokemon));
        Assert.Throws<ArgumentException>(() => new OrdinaryLandEncounterCatalog(tables, maps,
            pokemon with { Species = ImmutableDictionary<SpeciesId, PokemonSpeciesDefinition>.Empty }));
        maps[table.Map] = map with { LandAttribute = 3 };
        Assert.Throws<NotSupportedException>(() => new OrdinaryLandEncounterCatalog(tables, maps, pokemon));
        maps[table.Map] = map;
        tables[table.Map] = table with { RawEncounterRate = 0 };
        Assert.Throws<ArgumentException>(() => new OrdinaryLandEncounterCatalog(tables, maps, pokemon));
        tables[table.Map] = table with { Slots = table.Slots.SetItem(0, table.Slots[0] with { Weight = 19 }) };
        Assert.Throws<ArgumentException>(() => new OrdinaryLandEncounterCatalog(tables, maps, pokemon));
    }

    [TestMethod]
    public void UnusedTableCannotCreateLandEncountersWithoutLandAttributedTiles()
    {
        var table = EarlyLandEncounterRules.Route1Table with { Map = new("map.unused-land-table") };
        var definition = new OrdinaryLandEncounterCatalog(
            new Dictionary<MapId, EarlyLandEncounterTable> { [table.Map] = table },
            new Dictionary<MapId, IFieldMap> { [table.Map] = new TestMap(table.Map, 0, LandAttribute: 0) },
            Pokemon(table))[table.Map];
        var state = new EarlyLandEncounterState(new(0), RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion);
        var check = new EarlyLandEncounterCheck(table.Map, EncounterCheckTrigger.WalkingStepCompleted, 0, EncounterTileType.None);
        var result = EarlyLandEncounterRules.Check(state, definition, check, new(RulesVersion: state.RulesVersion), new(42, 19));
        Assert.AreEqual(EncounterCheckOutcome.NoEncounterTerrain, result.Outcome);
        Assert.IsTrue(result.RandomTrace.IsEmpty);
        Assert.AreEqual(new Game.Domain.Randomness.RngState(42, 19), result.GlobalRng);
        Assert.AreEqual(state, result.State);
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.Check(state, definition,
            check with { EncounterType = EncounterTileType.Land }, new(RulesVersion: state.RulesVersion), new(42)));
        var slot = table.Slots[0];
        Assert.Throws<ArgumentException>(() => definition.ValidateState(state with
            { Pending = new(new(table.Map, slot.Index, slot.Species, slot.MinimumLevel), 0) }, table));
    }

    [TestMethod]
    public void LegacySpecialTablesDoNotBecomeOrdinaryThroughCatalogRegistration()
    {
        var table = SafariLandEncounterTables.All[0];
        var maps = new Dictionary<MapId, IFieldMap> { [table.Map] = new TestMap(table.Map, 2) };
        var tables = new Dictionary<MapId, EarlyLandEncounterTable> { [table.Map] = table };
        var catalog = new OrdinaryLandEncounterCatalog(tables, maps, Pokemon(table));
        Assert.IsFalse(catalog.TryGetValue(table.Map, out _));
        tables[table.Map] = table with { RawEncounterRate = table.RawEncounterRate + 1 };
        Assert.Throws<ArgumentException>(() => new OrdinaryLandEncounterCatalog(tables, maps, Pokemon(table)));
    }

    [TestMethod]
    public void CatalogBoundProfilesAuthenticateFullLeadIdentifiersBeforeUsingFieldEffects()
    {
        var table = EarlyLandEncounterRules.Route1Table with { Map = new("map.loaded-grass") };
        var pokemon = Pokemon(table);
        pokemon = pokemon with { Species = pokemon.Species.ToImmutableDictionary(pair => pair.Key,
            pair => pair.Value with { Ability1 = "ABILITY_RUN_AWAY" }) };
        var definition = new OrdinaryLandEncounterCatalog(
            new Dictionary<MapId, EarlyLandEncounterTable> { [table.Map] = table },
            new Dictionary<MapId, IFieldMap> { [table.Map] = new TestMap(table.Map, 2) }, pokemon)[table.Map];
        var state = new EarlyLandEncounterState(new(0), 2, RulesVersion: EarlyLandEncounterRules.FiveIslandRulesVersion);
        var check = new EarlyLandEncounterCheck(table.Map, EncounterCheckTrigger.WalkingStepCompleted, 2, EncounterTileType.Land);
        var profile = new EarlyLandEncounterProfile(RulesVersion: state.RulesVersion, LeadAbility: "ABILITY_RUN_AWAY");
        var actual = EarlyLandEncounterRules.Check(state, definition, check, profile, new(42));
        var neutral = EarlyLandEncounterRules.Check(state, definition, check, profile with { LeadAbility = "ABILITY_NONE" }, new(42));
        Assert.AreEqual(neutral.GlobalRng, actual.GlobalRng);
        Assert.AreEqual(neutral.State, actual.State);
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.Check(state, definition, check,
            profile with { LeadAbility = "ABILITY_NOT_LOADED" }, new(42)));
        Assert.Throws<ArgumentException>(() => EarlyLandEncounterRules.Check(state, definition, check,
            profile with { LeadHeldItem = "ITEM_NOT_LOADED" }, new(42)));
    }

    internal static OrdinaryLandEncounterDefinition Bind(EarlyLandEncounterTable table, byte behavior) =>
        new OrdinaryLandEncounterCatalog(new Dictionary<MapId, EarlyLandEncounterTable> { [table.Map] = table },
            new Dictionary<MapId, IFieldMap> { [table.Map] = new TestMap(table.Map, behavior) }, Pokemon(table))[table.Map];

    private static PokemonCatalog Pokemon(EarlyLandEncounterTable table) => new("test",
        table.Slots.Select(slot => slot.Species).Distinct().Select((id, index) => new PokemonSpeciesDefinition(id,
            index + 1, new(40, 40, 40, 40, 40, 40), "TYPE_NORMAL", "TYPE_NORMAL", 255, 50, SixStats.Zero,
            GrowthRate.MediumFast, 127, "ABILITY_NONE", "ABILITY_NONE", 70, [], [])).ToImmutableDictionary(s => s.Id),
        ImmutableDictionary<MoveId, PokemonMoveDefinition>.Empty);

    private sealed record TestMap(MapId Id, byte Behavior, int LandAttribute = 1) : IFieldMap
    {
        public int Width => 3;
        public int Height => 1;
        public ImmutableArray<FieldWarp> FieldWarps => [];
        public TileCell? GetTile(int x, int y) => x is >= 0 and < 3 && y == 0 ? new(false) : null;
        public int? SourceBehaviorAt(int x, int y) => x == 0 ? Behavior : x == 1 ? 0xd0 : 0;
        public int? SourceEncounterTypeAt(int x, int y) => x == 0 ? LandAttribute : 0;
    }
}
