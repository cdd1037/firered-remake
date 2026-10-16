using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class PokedexSearchOrderTests
{
    private static readonly Lazy<PretPokedexMenu> Catalog = new(() => PretPokedexMenu.Load(TestAssets.Root));
    private static readonly PokedexMenuAccess Kanto = new(true);
    private static readonly PokedexMenuAccess National = new(true, true, 0xB9, 0x6258);

    [TestMethod]
    public void MenuChoicesAndLabelsRetainOriginalOrderAndFullNationalGate()
    {
        var menu = Catalog.Value;
        var regionalOrders = new[] { PokedexMenuOrder.NumericalKanto, PokedexMenuOrder.Alphabetical,
            PokedexMenuOrder.Type, PokedexMenuOrder.Lightest, PokedexMenuOrder.Smallest };
        foreach (var access in new[] { Kanto, new(true, true), new(true, true, 0xB9), new(true, true, 0, 0x6258) })
        {
            CollectionAssert.AreEqual(regionalOrders, menu.AvailableOrders(access).ToArray());
            Assert.ThrowsExactly<InvalidDataException>(() => menu.BuildOrderedList([1], [1], access, PokedexMenuOrder.NumericalNational));
        }
        CollectionAssert.AreEqual(new[] { PokedexMenuOrder.NumericalKanto, PokedexMenuOrder.NumericalNational,
            PokedexMenuOrder.Alphabetical, PokedexMenuOrder.Type, PokedexMenuOrder.Lightest, PokedexMenuOrder.Smallest },
            menu.AvailableOrders(National).ToArray());
        Assert.AreEqual(0, menu.AvailableOrders(new(false, true, 0xB9, 0x6258)).Length);
        Assert.AreEqual("NUMERICAL MODE", menu.GetOrderLabel(PokedexMenuOrder.NumericalKanto, Kanto));
        Assert.AreEqual("A TO Z MODE", menu.GetOrderLabel(PokedexMenuOrder.Alphabetical, Kanto));
        Assert.AreEqual("TYPE MODE", menu.GetOrderLabel(PokedexMenuOrder.Type, Kanto));
        Assert.AreEqual("LIGHTEST MODE", menu.GetOrderLabel(PokedexMenuOrder.Lightest, Kanto));
        Assert.AreEqual("SMALLEST MODE", menu.GetOrderLabel(PokedexMenuOrder.Smallest, Kanto));
        Assert.AreEqual("SEARCH", menu.UiTexts["gText_SearchNoColor"]);
    }

    [TestMethod]
    public void AlphabeticalKeepsSeenNamesAndHidesUncaughtTypes()
    {
        // Independently transcribed relative order from gPokedexOrder_Alphabetical.
        var list = Catalog.Value.BuildOrderedList([25, 7, 6, 5, 4, 3, 2, 1, 63, 277], [1, 25, 277],
            Kanto, PokedexMenuOrder.Alphabetical);
        CollectionAssert.AreEqual(new[] { 63, 1, 6, 4, 5, 2, 25, 7, 3 }, Species(list));
        Assert.IsFalse(list.NationalOrder);
        Assert.AreEqual(9, list.SeenCount);
        Assert.AreEqual(2, list.CaughtCount);
        Assert.IsTrue(list.Rows.All(row => row.Seen && row.CanOpen && row.DisplayName != "-----"));
        var bulbasaur = list.Rows.Single(row => row.SourceSpeciesId == 1);
        Assert.AreEqual("TYPE_GRASS", bulbasaur.Type1);
        Assert.AreEqual("TYPE_POISON", bulbasaur.Type2);
        Assert.AreEqual(1 | 1 << 16 | 1 << 17, bulbasaur.SourcePackedIndex);
        var pikachu = list.Rows.Single(row => row.SourceSpeciesId == 25);
        Assert.AreEqual("TYPE_ELECTRIC", pikachu.Type1);
        Assert.IsNull(pikachu.Type2);
        var abra = list.Rows[0];
        Assert.IsFalse(abra.Caught);
        Assert.IsNull(abra.Type1);
        Assert.IsNull(abra.Type2);
        Assert.AreEqual(63 | 1 << 16, abra.SourcePackedIndex);
    }

    [TestMethod]
    public void TypeUsesSourcePrimarySecondaryOrderAndCaughtGate()
    {
        // Normal -> normal/flying -> steel/ground -> fire -> fire/flying -> water -> grass/poison -> electric -> electric/steel.
        // Steelix is not sorted next to Onix by name or national number; no Fairy type is added to FireRed.
        int[] seen = [81, 25, 1, 7, 6, 4, 208, 16, 19, 35];
        int[] caught = [81, 25, 1, 7, 6, 4, 208, 16, 19];
        var national = Catalog.Value.BuildOrderedList(seen, caught, National, PokedexMenuOrder.Type);
        CollectionAssert.AreEqual(new[] { 19, 16, 208, 4, 6, 7, 1, 25, 81 }, Species(national));
        Assert.IsTrue(national.Rows.All(row => row.Caught));
        Assert.AreEqual(10, national.SeenCount);
        Assert.AreEqual(9, national.CaughtCount);
        var regional = Catalog.Value.BuildOrderedList(seen, caught, Kanto, PokedexMenuOrder.Type);
        CollectionAssert.AreEqual(new[] { 19, 16, 4, 6, 7, 1, 25, 81 }, Species(regional));
        Assert.AreEqual(9, regional.SeenCount);
        Assert.AreEqual(8, regional.CaughtCount);
    }

    [TestMethod]
    public void LightestKeepsOriginalWeightTiesAndRejectsSeenOnlyRows()
    {
        // Gastly and Haunter both weigh 0.1 kg; these explicit expectations do not sort catalog metadata.
        int[] seen = [7, 1, 25, 19, 16, 385, 50, 187, 93, 92];
        int[] caught = [1, 25, 19, 16, 385, 50, 187, 93, 92];
        var national = Catalog.Value.BuildOrderedList(seen, caught, National, PokedexMenuOrder.Lightest);
        CollectionAssert.AreEqual(new[] { 92, 93, 187, 50, 385, 16, 19, 25, 1 }, Species(national));
        var regional = Catalog.Value.BuildOrderedList(seen, caught, Kanto, PokedexMenuOrder.Lightest);
        CollectionAssert.AreEqual(new[] { 92, 93, 50, 16, 19, 25, 1 }, Species(regional));
        Assert.IsTrue(regional.Rows.All(row => row.Caught));
    }

    [TestMethod]
    public void SmallestPreservesCustomTiesRatherThanNumericOrAlphabeticalTieBreaks()
    {
        // These 0.2/0.3-metre entries have deliberate table ordering: Azurill before Natu,
        // Weedle before Caterpie, Spearow before Pidgey, Shellder before Rattata.
        int[] caught = [46, 81, 19, 90, 16, 21, 133, 132, 10, 13, 177, 350, 50];
        var national = Catalog.Value.BuildOrderedList(caught, caught, National, PokedexMenuOrder.Smallest);
        CollectionAssert.AreEqual(new[] { 50, 350, 177, 13, 10, 132, 133, 21, 16, 90, 19, 81, 46 }, Species(national));
        var regional = Catalog.Value.BuildOrderedList(caught, caught, Kanto, PokedexMenuOrder.Smallest);
        CollectionAssert.AreEqual(new[] { 50, 13, 10, 132, 133, 21, 16, 90, 19, 81, 46 }, Species(regional));
    }

    [TestMethod]
    public void SearchUsesNationalNumbersNotSourceIdsOrPreviousNumericalChoice()
    {
        var menu = Catalog.Value;
        int[] seen = [410, 392, 277, 151]; // Deoxys, Ralts, Treecko, Mew.
        var beforeUnlock = menu.BuildOrderedList(seen, seen, Kanto, PokedexMenuOrder.Alphabetical);
        CollectionAssert.AreEqual(new[] { 151 }, Species(beforeUnlock));
        var kantoNumerical = menu.BuildOrderedList(seen, seen, National, PokedexMenuOrder.NumericalKanto);
        Assert.AreEqual(151, kantoNumerical.Rows.Length);
        var afterUnlock = menu.BuildOrderedList(seen, seen, National, PokedexMenuOrder.Alphabetical);
        CollectionAssert.AreEqual(new[] { 410, 151, 392, 277 }, Species(afterUnlock));
        CollectionAssert.AreEqual(new[] { 386, 151, 280, 252 }, afterUnlock.Rows.Select(row => row.NationalNumber).ToArray());
        Assert.IsTrue(afterUnlock.NationalOrder);
        Assert.AreEqual(4, afterUnlock.SeenCount);
        Assert.AreEqual(4, afterUnlock.CaughtCount);
    }

    [TestMethod]
    public void NumericalDispatchRetainsUnseenHolesAndHighestSeenLength()
    {
        var menu = Catalog.Value;
        var list = menu.BuildOrderedList([1, 4, 277], [1], Kanto, PokedexMenuOrder.NumericalKanto);
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, Species(list));
        CollectionAssert.AreEqual(new[] { "BULBASAUR", "-----", "-----", "CHARMANDER" }, list.Rows.Select(row => row.DisplayName).ToArray());
        CollectionAssert.AreEqual(menu.BuildNumericalList([1, 4, 277], [1], Kanto).Rows.ToArray(), list.Rows.ToArray());
        var national = menu.BuildOrderedList([1, 4, 277], [1], National, PokedexMenuOrder.NumericalNational);
        Assert.AreEqual(252, national.Rows.Length);
        Assert.AreEqual(277, national.Rows[^1].SourceSpeciesId);
    }

    [TestMethod]
    public void EmptyAndSeenOnlySearchesAreWellDefinedAndDoNotMutateInputs()
    {
        var menu = Catalog.Value;
        foreach (var order in menu.AvailableOrders(National))
        {
            var empty = menu.BuildOrderedList([], [], National, order);
            Assert.AreEqual(0, empty.Rows.Length);
            Assert.AreEqual(0, empty.SeenCount);
            Assert.AreEqual(0, empty.CaughtCount);
            Assert.AreEqual(new PokedexMenuCursor(0, 0), menu.MoveList(empty, new(0, 0), 1));
        }
        int[] seen = [25, 1, 25, 7];
        int[] caught = [1, 1];
        foreach (var order in menu.AvailableOrders(National)) menu.BuildOrderedList(seen, caught, National, order);
        CollectionAssert.AreEqual(new[] { 25, 1, 25, 7 }, seen);
        CollectionAssert.AreEqual(new[] { 1, 1 }, caught);
        foreach (var order in new[] { PokedexMenuOrder.Type, PokedexMenuOrder.Lightest, PokedexMenuOrder.Smallest })
        {
            var list = menu.BuildOrderedList(seen, [], Kanto, order);
            Assert.AreEqual(0, list.Rows.Length);
            Assert.AreEqual(3, list.SeenCount);
        }
    }

    [TestMethod]
    public void SearchRejectsInvalidSpeciesCaughtStateAndOrder()
    {
        var menu = Catalog.Value;
        foreach (var order in menu.AvailableOrders(National))
        {
            Assert.ThrowsExactly<InvalidDataException>(() => menu.BuildOrderedList([252], [], National, order)); // Old Unown, not a canonical species.
            Assert.ThrowsExactly<InvalidDataException>(() => menu.BuildOrderedList([1], [4], National, order));
            Assert.ThrowsExactly<InvalidDataException>(() => menu.BuildOrderedList([1], [1], new(false), order));
        }
        Assert.ThrowsExactly<InvalidDataException>(() => menu.BuildOrderedList([1], [1], National, (PokedexMenuOrder)99));
    }

    [TestMethod]
    public void EverySearchTableIsACanonicalPermutationWithoutUnusedUnownSlots()
    {
        var menu = Catalog.Value;
        int[] all = menu.Entries.Keys.ToArray();
        foreach (var order in new[] { PokedexMenuOrder.Alphabetical, PokedexMenuOrder.Type, PokedexMenuOrder.Lightest, PokedexMenuOrder.Smallest })
        {
            var list = menu.BuildOrderedList(all, all, National, order);
            CollectionAssert.AreEquivalent(all, Species(list));
            Assert.IsTrue(list.Rows.All(row => row.SourceSpeciesId is < 252 or > 276));
            Assert.AreEqual(386, list.Rows.Length);
        }
    }

    [TestMethod]
    [DataRow("alphabetical")]
    [DataRow("type")]
    [DataRow("lightest")]
    [DataRow("smallest")]
    public void LoaderRejectsDuplicateUnknownAndMissingOrderEntries(string order)
    {
        WithDocument(document =>
        {
            var entries = document["orders"]![order]!.AsArray();
            entries[0] = entries[1]!.GetValue<int>();
        }, root => Assert.ThrowsExactly<InvalidDataException>(() => PretPokedexMenu.Load(root)));
        WithDocument(document => document["orders"]![order]![0] = 252,
            root => Assert.ThrowsExactly<InvalidDataException>(() => PretPokedexMenu.Load(root)));
        WithDocument(document => document["orders"]![order]!.AsArray().RemoveAt(0),
            root => Assert.ThrowsExactly<InvalidDataException>(() => PretPokedexMenu.Load(root)));
    }

    [TestMethod]
    public void LoaderRequiresOrderDataRatherThanInventingSortsFromNamesOrMeasures()
    {
        WithDocument(document => document.AsObject().Remove("orders"),
            root => Assert.ThrowsExactly<ContentValidationException>(() => PretPokedexMenu.Load(root)));
    }

    [TestMethod]
    public void LoaderValidatesSearchLabelsBeforeAnyMenuIsOpened()
    {
        WithDocument(document => document["uiTexts"]!.AsObject().Remove("gText_TypeMode"),
            root => Assert.ThrowsExactly<InvalidDataException>(() => PretPokedexMenu.Load(root)));
    }

    private static int[] Species(PokedexMenuList list) => list.Rows.Select(row => row.SourceSpeciesId).ToArray();

    private static void WithDocument(Action<JsonNode> mutate, Action<string> check)
    {
        const string relative = "content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json";
        string root = Path.Combine(Path.GetTempPath(), "firered-dex-orders-" + Guid.NewGuid().ToString("N"));
        try
        {
            var document = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, relative)))!;
            mutate(document);
            string target = Path.Combine(root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(target, document.ToJsonString());
            check(root);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
