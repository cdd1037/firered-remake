using System.Collections.Immutable;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class HelpCatalogTests
{
    private static HelpProgress Progress(params string[] flags) => new(flags.ToImmutableHashSet(StringComparer.Ordinal), ImmutableHashSet<string>.Empty, 0, "RED");
    private static HelpEntry[] Entries(HelpCatalog catalog, string context, string topic, HelpProgress progress) =>
        catalog.Topics(context, progress).Single(t => t.Id == topic).Entries.ToArray();

    [TestMethod]
    public void CompleteCatalogLoadsFromGeneratedOnlyRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "firered-help-" + Guid.NewGuid().ToString("N"));
        var target = Path.Combine(root, HelpCatalog.ResourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        try
        {
            File.Copy(Path.Combine(TestAssets.Root, HelpCatalog.ResourcePath), target);
            var catalog = HelpCatalog.Load(root);
            Assert.IsTrue(catalog.Greeting.StartsWith("Greetings!", StringComparison.Ordinal));
            Assert.AreEqual(36, catalog.Contexts.Count());
            Assert.IsFalse(catalog.SupportsContext("HELPCONTEXT_UNUSED"));
            Assert.IsFalse(catalog.SupportsContext("invalid"));
            CollectionAssert.AreEqual(new[] { "TOPIC_ABOUT_GAME", "TOPIC_WHAT_TO_DO", "TOPIC_HOW_TO_DO", "TOPIC_TERMS", "TOPIC_EXIT" },
                catalog.Topics("HELPCONTEXT_POKECENTER", Progress()).Select(t => t.Id).ToArray());
            Assert.AreEqual(35, Entries(catalog, "HELPCONTEXT_GYM", "TOPIC_TYPE_MATCHUP", Progress()).Length);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void SourceMapsRespectDungeonRangesTanobyUnlockAndSurfPriority()
    {
        var catalog = HelpCatalog.Load(TestAssets.Root);
        var flags = ImmutableHashSet<string>.Empty;
        Assert.AreEqual("HELPCONTEXT_PLAYERS_HOUSE", catalog.ResolveMapContext("MAP_PALLET_TOWN_PLAYERS_HOUSE_2F", false, flags));
        Assert.AreEqual("HELPCONTEXT_OAKS_LAB", catalog.ResolveMapContext("MAP_PALLET_TOWN_PROFESSOR_OAKS_LAB", false, flags));
        Assert.AreEqual("HELPCONTEXT_MART", catalog.ResolveMapContext("MAP_CELADON_CITY_DEPARTMENT_STORE_ELEVATOR", false, flags));
        Assert.AreEqual("HELPCONTEXT_POKECENTER", catalog.ResolveMapContext("MAP_VIRIDIAN_CITY_POKEMON_CENTER_2F", false, flags));
        Assert.AreEqual("HELPCONTEXT_GYM", catalog.ResolveMapContext("MAP_VIRIDIAN_CITY_GYM", false, flags));
        Assert.AreEqual("HELPCONTEXT_DUNGEON", catalog.ResolveMapContext("MAP_VIRIDIAN_FOREST", false, flags));
        Assert.AreEqual("HELPCONTEXT_DUNGEON", catalog.ResolveMapContext("MAP_MT_MOON_B2F", false, flags));
        const string chamber = "MAP_SEVEN_ISLAND_TANOBY_RUINS_MONEAN_CHAMBER";
        Assert.AreNotEqual("HELPCONTEXT_DUNGEON", catalog.ResolveMapContext(chamber, false, flags));
        Assert.AreEqual("HELPCONTEXT_DUNGEON", catalog.ResolveMapContext(chamber, false, flags.Add("FLAG_SYS_UNLOCKED_TANOBY_RUINS")));
        Assert.AreEqual("HELPCONTEXT_SURFING", catalog.ResolveMapContext(chamber, true, flags));
    }

    [TestMethod]
    public void ProgressUsesRealFlagsTownMapPossessionAndKantoCaughtCount()
    {
        var catalog = HelpCatalog.Load(TestAssets.Root);
        var initial = Progress();
        var bag = Entries(catalog, "HELPCONTEXT_BAG", "TOPIC_HOW_TO_DO", initial);
        Assert.IsFalse(bag.Any(e => e.Id == "HELP_USING_TOWN_MAP"));
        Assert.IsTrue(Entries(catalog, "HELPCONTEXT_BAG", "TOPIC_HOW_TO_DO", initial with { ItemIds = ImmutableHashSet.Create("ITEM_TOWN_MAP") }).Any(e => e.Id == "HELP_USING_TOWN_MAP"));
        Assert.IsFalse(Entries(catalog, "HELPCONTEXT_PARTY_MENU", "TOPIC_HOW_TO_DO", initial with { KantoCaughtCount = 1 }).Any(e => e.Id == "HELP_USING_SWITCH"));
        Assert.IsTrue(Entries(catalog, "HELPCONTEXT_PARTY_MENU", "TOPIC_HOW_TO_DO", initial with { KantoCaughtCount = 2 }).Any(e => e.Id == "HELP_USING_SWITCH"));
        Assert.IsFalse(Entries(catalog, "HELPCONTEXT_BAG", "TOPIC_HOW_TO_DO", initial with { ItemIds = ImmutableHashSet.Create("ITEM_HM01") }).Any(e => e.Id == "HELP_USING_HM"));
        Assert.IsTrue(Entries(catalog, "HELPCONTEXT_BAG", "TOPIC_HOW_TO_DO", Progress("FLAG_GOT_HM01")).Any(e => e.Id == "HELP_USING_HM"));
    }

    [TestMethod]
    public void ReturningAfterBrockAppendsBasicTermsOnceInOriginalOrder()
    {
        var catalog = HelpCatalog.Load(TestAssets.Root);
        var before = Entries(catalog, "HELPCONTEXT_POKEMON_SKILLS", "TOPIC_TERMS", Progress("FLAG_SYS_POKEMON_GET"));
        var after = Entries(catalog, "HELPCONTEXT_POKEMON_SKILLS", "TOPIC_TERMS", Progress("FLAG_SYS_POKEMON_GET", "FLAG_DEFEATED_BROCK"));
        Assert.IsTrue(after.Length > before.Length);
        Assert.AreEqual(after.Length, after.Select(e => e.Id).Distinct().Count());
        Assert.AreEqual("HELP_TERM_LEVEL", after[^31].Id);
        Assert.AreEqual("HELP_TERM_BADGES", after[^1].Id);
        CollectionAssert.AreEqual(after.Select(e => e.Id).ToArray(), Entries(catalog, "HELPCONTEXT_POKEMON_SKILLS", "TOPIC_TERMS", Progress("FLAG_SYS_POKEMON_GET", "FLAG_DEFEATED_BROCK")).Select(e => e.Id).ToArray());
    }
}
