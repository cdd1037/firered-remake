using Game.Content;

namespace FireRed.Tests.Presentation;

[TestClass]
public sealed class PokedexDetailPresentationTests
{
    private static readonly Lazy<PretPokedexMenu> Menu = new(() => PretPokedexMenu.Load(TestAssets.Root));

    [TestMethod]
    public void CryUsesSeenEntryWithoutRequiringCaughtOrRevealingHiddenData()
    {
        var menu = Menu.Value;
        var list = menu.BuildNumericalList([1, 4], [4], new(true));
        Assert.AreEqual(1, PokedexDetailPresentation.CrySpecies(list, new(0, 0)));
        Assert.IsNull(PokedexDetailPresentation.CrySpecies(list, new(0, 1)));
        Assert.AreEqual(4, PokedexDetailPresentation.CrySpecies(list, new(0, 3)));
        var page = menu.BuildPage(1, [1, 4], [4], new(true));
        Assert.IsFalse(page.Caught);
        Assert.AreEqual(string.Empty, page.Description);
        Assert.IsNull(page.Footprint);
    }

    [TestMethod]
    public void CryUsesSourceSpeciesAndHonorsNationalListGate()
    {
        var menu = Menu.Value;
        var regional = menu.BuildNumericalList([1, 277, 392, 410], [], new(true));
        Assert.AreEqual(1, regional.Rows.Length);
        var national = menu.BuildNumericalList([1, 277, 392, 410], [], new(true, true, 0xb9, 0x6258), true);
        foreach (int species in new[] { 277, 392, 410 })
        {
            int number = menu.Entries[species].NationalNumber;
            Assert.AreNotEqual(species, number);
            Assert.AreEqual(species, PokedexDetailPresentation.CrySpecies(national,
                PretPokedexMenu.CenterDetailCursor(national.Rows.Length, number - 1)));
        }
    }

    [TestMethod]
    public void DetailMovementSkipsUnseenWithoutWrappingAndKeepsSourceIdentity()
    {
        var menu = Menu.Value;
        var list = menu.BuildNumericalList([1, 4, 25], [], new(true));
        var cursor = new PokedexMenuCursor(0, 0);
        Assert.AreEqual(cursor, menu.MoveDetail(list, cursor, -1));
        cursor = menu.MoveDetail(list, cursor, 1);
        Assert.AreEqual(4, PokedexDetailPresentation.CrySpecies(list, cursor));
        cursor = menu.MoveDetail(list, cursor, 1);
        Assert.AreEqual(25, PokedexDetailPresentation.CrySpecies(list, cursor));
        Assert.AreEqual(cursor, menu.MoveDetail(list, cursor, 1));
        Assert.AreEqual(4, PokedexDetailPresentation.CrySpecies(list, menu.MoveDetail(list, cursor, -1)));
    }

    [TestMethod]
    public void EmptyAndOutOfRangeSelectionCannotProduceCry()
    {
        var empty = Menu.Value.BuildNumericalList([], [], new(true));
        Assert.IsNull(PokedexDetailPresentation.CrySpecies(empty, new(0, 0)));
        var list = Menu.Value.BuildNumericalList([1], [], new(true));
        Assert.IsNull(PokedexDetailPresentation.CrySpecies(list, new(0, -1)));
        Assert.IsNull(PokedexDetailPresentation.CrySpecies(list, new(0, 1)));
    }

    [TestMethod]
    public void FooterButtonsMatchDrawnRegionsWithoutOverlapping()
    {
        foreach (var button in PokedexDetailPresentation.Footer)
        {
            Assert.AreEqual(button.Action, PokedexDetailPresentation.HitTest(button.X, 144));
            Assert.AreEqual(button.Action, PokedexDetailPresentation.HitTest(button.X + button.Width - .01, 159.99));
            Assert.AreEqual(PokedexDetailPresentation.Action.None, PokedexDetailPresentation.HitTest(button.X + 8, 143.99));
            Assert.AreEqual(PokedexDetailPresentation.Action.None, PokedexDetailPresentation.HitTest(button.X + 8, 160));
        }
        Assert.AreEqual(PokedexDetailPresentation.Action.None, PokedexDetailPresentation.HitTest(-.01, 150));
        Assert.AreEqual(PokedexDetailPresentation.Action.None, PokedexDetailPresentation.HitTest(240, 150));
        Assert.AreEqual(PokedexDetailPresentation.Action.Cry, PokedexDetailPresentation.HitTest(130, 150));
    }
}
