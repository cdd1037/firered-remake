using Game.Domain.Pokemon;

namespace FireRed.Tests.Presentation;

[TestClass]
public sealed class EasyChatEditorNavigationTests
{
    private static SourceEasyChatTemplate Template(int rows, int words) => new("test", 0, 2, rows, words, 0, null, [], [], []);
    [TestMethod]
    public void ProfileFieldsWrapAndFooterKeepsItsThreeActions()
    {
        var t = Template(2, 4);
        Assert.AreEqual((2, 1), EasyChatEditorNavigation.Move(t, 0, 1, 0, -1));
        Assert.AreEqual((0, 1), EasyChatEditorNavigation.Move(t, 2, 2, 0, 1));
        Assert.AreEqual((1, 1), EasyChatEditorNavigation.Move(t, 2, 2, 0, -1));
        Assert.AreEqual((2, 2), EasyChatEditorNavigation.Move(t, 2, 0, -1, 0));
        Assert.AreEqual((2, 0), EasyChatEditorNavigation.Move(t, 2, 2, 1, 0));
        Assert.AreEqual((0, 1), EasyChatEditorNavigation.Move(t, 0, 0, -1, 0));
    }
    [TestMethod]
    public void MailPartialLastRowCannotSelectAnAbsentTenthWord()
    {
        var t = Template(5, 9);
        Assert.AreEqual((4, 0), EasyChatEditorNavigation.Move(t, 3, 1, 0, 1));
        Assert.AreEqual((4, 0), EasyChatEditorNavigation.Move(t, 5, 2, 0, -1));
        Assert.AreEqual((4, 0), EasyChatEditorNavigation.Move(t, 4, 0, 1, 0));
        Assert.AreEqual((4, 0), EasyChatEditorNavigation.Move(t, 4, 0, -1, 0));
        Assert.AreEqual((5, 0), EasyChatEditorNavigation.Move(t, 4, 0, 0, 1));
    }
}
