using System.Collections.Immutable;
using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class PokedexAreaTests
{
    private static readonly Lazy<PretPokedexMenu> Catalog = new(() => PretPokedexMenu.Load(TestAssets.Root));
    private static readonly PokedexMenuAccess National = new(true, true, 0xB9, 0x6258);
    private static PokedexAreaContext Context(int cave = 0, int starter = 0, string? roamer = null, params string[] islands)
        => new(islands.ToImmutableDictionary(i => "FLAG_WORLD_MAP_" + i + "_ISLAND", _ => true), cave, starter, roamer);
    private static PokedexAreaPage Page(int species, PokedexAreaContext? context = null, bool caught = false)
        => Catalog.Value.BuildAreaPage(species, [species], caught ? [species] : [], National, context ?? Context());
    private static string[] Markers(PokedexAreaPage page) => page.Markers.Select(m => m.Id).ToArray();

    [TestMethod]
    public void SourceAreaGeometryAndSingleAtlasAreRetained()
    {
        var areas = Catalog.Value.Document.Areas;
        var route1 = areas.Markers.Single(m => m.Id == "DEX_AREA_ROUTE_1");
        Assert.AreEqual(158, route1.X);
        Assert.AreEqual(64, route1.Y);
        Assert.AreEqual(8, route1.Sprite.Width);
        Assert.AreEqual(16, route1.Sprite.Height);
        var route25 = areas.Markers.Single(m => m.Id == "DEX_AREA_ROUTE_25");
        Assert.AreEqual(194, route25.X);
        Assert.AreEqual(30, route25.Y);
        var sprites = areas.Regions.Select(r => r.Sprite).Concat(areas.Markers.Select(m => m.Sprite)).Append(areas.UnknownSprite).ToArray();
        Assert.AreEqual(1, sprites.Select(s => s.Texture).Distinct().Count());
        Assert.IsTrue(sprites.All(s => s.SourceX >= 0 && s.SourceY >= 0 && s.SourceX + s.Width <= s.Texture.Width && s.SourceY + s.Height <= s.Texture.Height));
        Assert.IsTrue(Catalog.Value.ReadTexture(sprites[0].Texture).Length > 0);
    }

    [TestMethod]
    public void WildAreasAreVisibleWhenSeenAndRepeatedFloorsBecomeOneMarker()
    {
        CollectionAssert.AreEqual(new[] { "DEX_AREA_VIRIDIAN_FOREST", "DEX_AREA_POWER_PLANT" }, Markers(Page(25)));
        CollectionAssert.AreEqual(Markers(Page(25)), Markers(Page(25, caught: true)));
        CollectionAssert.AreEqual(new[] { "DEX_AREA_MT_MOON" }, Markers(Page(35)));
        Assert.ThrowsExactly<InvalidDataException>(() => Catalog.Value.BuildAreaPage(25, [], [], National, Context()));
        Assert.ThrowsExactly<InvalidDataException>(() => Catalog.Value.BuildAreaPage(25, [25], [1], National, Context()));
        Assert.ThrowsExactly<InvalidDataException>(() => Catalog.Value.BuildAreaPage(25, [25], [], new(false), Context()));
    }

    [TestMethod]
    public void GiftFossilAndStaticLegendarySpeciesHaveUnknownWildAreas()
    {
        var all = Context(islands: ["ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN"]);
        foreach (int species in new[] { 1, 4, 7, 138, 140, 142, 144, 145, 146, 150, 151 })
            Assert.IsTrue(Page(species, all).Unknown, $"Static/gift source species {species} must not gain a made-up wild marker.");
    }

    [TestMethod]
    public void IslandsFollowIndividualWorldMapFlagsAndVerticalLayout()
    {
        Assert.AreEqual(1, Page(129).Regions.Length);
        Assert.AreEqual(32, Page(129).VerticalOffset);
        Assert.AreEqual(32, Page(129, Context(islands: ["ONE", "TWO", "THREE"])).VerticalOffset);
        var fourth = Page(129, Context(islands: ["FOUR"]));
        CollectionAssert.AreEqual(new[] { "Kanto", "FourIsland" }, fourth.Regions.Select(r => r.Id).ToArray());
        Assert.AreEqual(0, fourth.VerticalOffset);
        Assert.IsFalse(fourth.Markers.Any(m => m.Id == "DEX_AREA_KINDLE_ROAD"));
        Assert.IsTrue(Page(201).Unknown, "National access alone does not reveal an island.");
        CollectionAssert.AreEqual(new[] { "DEX_AREA_TANOBY_CHAMBER" }, Markers(Page(201, Context(islands: ["SEVEN"]))));
        Assert.ThrowsExactly<InvalidDataException>(() => Catalog.Value.BuildAreaPage(201, [201], [], new(true), Context(islands: ["SEVEN"])));
    }

    [TestMethod]
    public void AlteringCaveUsesOneCurrentTableAndInvalidValuesFallBackToZero()
    {
        Assert.IsTrue(Page(179, Context(1)).Unknown, "The cave is hidden until Six Island is unlocked.");
        Assert.IsTrue(Page(179, Context(islands: ["SIX"])).Unknown);
        CollectionAssert.AreEqual(new[] { "DEX_AREA_ALTERING_CAVE" }, Markers(Page(179, Context(1, islands: ["SIX"]))));
        Assert.IsTrue(Page(179, Context(9, islands: ["SIX"])).Unknown);
        Assert.IsTrue(Page(41, Context(ushort.MaxValue, islands: ["SIX"])).Markers.Any(m => m.Id == "DEX_AREA_ALTERING_CAVE"));
        Assert.IsFalse(Page(41, Context(1, islands: ["SIX"])).Markers.Any(m => m.Id == "DEX_AREA_ALTERING_CAVE"));
        CollectionAssert.AreEqual(new[] { "DEX_AREA_ALTERING_CAVE" }, Markers(Page(235, Context(8, islands: ["SIX"]))));
        Assert.ThrowsExactly<InvalidDataException>(() => Page(179, Context(-1)));
    }

    [TestMethod]
    public void StarterRoamerRequiresActiveLocationAndUsesCurrentMapSection()
    {
        foreach (var (species, starter) in new[] { (244, 0), (243, 1), (245, 2) })
        {
            Assert.IsTrue(Page(species, Context(starter: starter)).Unknown);
            CollectionAssert.AreEqual(new[] { "DEX_AREA_ROUTE_1" }, Markers(Page(species, Context(starter: starter, roamer: "MAPSEC_ROUTE_1"))));
            CollectionAssert.AreEqual(new[] { "DEX_AREA_ROUTE_25" }, Markers(Page(species, Context(starter: starter, roamer: "MAPSEC_ROUTE_25"))));
            Assert.IsTrue(Page(species, Context(starter: (starter + 1) % 3, roamer: "MAPSEC_ROUTE_1")).Unknown);
            Assert.IsTrue(Page(species, Context(starter: starter, roamer: "MAPSEC_ALTERING_CAVE", islands: ["SIX"])).Unknown);
        }
        CollectionAssert.AreEqual(new[] { "DEX_AREA_ROUTE_1" }, Markers(Page(244, Context(starter: ushort.MaxValue, roamer: "MAPSEC_ROUTE_1"))));
    }

    [TestMethod]
    public void FishingAreasIncludeSourceWaterWithoutInventingStaticEncounters()
    {
        var magikarp = Page(129);
        Assert.IsTrue(magikarp.Markers.Any(m => m.Id == "DEX_AREA_PALLET_TOWN"));
        Assert.IsTrue(magikarp.Markers.Any(m => m.Id == "DEX_AREA_VERMILION_CITY"));
        Assert.IsTrue(magikarp.Markers.Any(m => m.Id == "DEX_AREA_SAFARI_ZONE"));
        Assert.AreEqual(magikarp.Markers.Length, magikarp.Markers.Select(m => m.Id).Distinct().Count());
    }

    [TestMethod]
    public void InvalidAtlasCropIsRejectedAtComposition()
    {
        string root = Path.Combine(Path.GetTempPath(), "firered-area-invalid-" + Guid.NewGuid().ToString("N"));
        string file = Path.Combine(root, "content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try
        {
            var document = JsonNode.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root, "content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json")))!;
            document["areas"]!["markers"]![0]!["sprite"]!["sourceX"] = 999;
            File.WriteAllText(file, document.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => PretPokedexMenu.Load(root));
        }
        finally { Directory.Delete(root, true); }
    }
}
