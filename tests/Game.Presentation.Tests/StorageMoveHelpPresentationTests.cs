using Game.Content;
using FireRed.Tests;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class StorageMoveHelpPresentationTests
{
    [TestMethod]
    [DataRow(false, 0, false, false, "Empty", "ENTER: EMPTY")]
    [DataRow(false, 0, false, true, "Menu", "ENTER: MENU")]
    [DataRow(false, 0, true, false, "Menu", "ENTER: MENU")]
    [DataRow(false, 0, true, true, "Menu", "ENTER: MENU")]
    [DataRow(false, 6, false, true, "Menu", "ENTER: MENU")]
    [DataRow(true, 5, false, false, "Empty", "ENTER: EMPTY")]
    [DataRow(true, 5, true, false, "Menu", "ENTER: MENU")]
    [DataRow(true, 6, false, false, "Cancel", "ENTER: CANCEL")]
    [DataRow(true, 6, true, false, "Cancel", "ENTER: CANCEL")]
    public void PhysicalPartyRowAndHeldOccupancySelectExistingEnglishHint(bool partyOpen, int cursor,
        bool holding, bool occupied, string expectedKind, string expectedText)
    {
        var kind = StorageMoveHelpPresentation.SelectSlot(partyOpen, cursor, holding, occupied);
        Assert.AreEqual(expectedKind, kind.ToString());
        Assert.AreEqual(expectedText, StorageMoveHelpPresentation.Text(kind, PresentationTextCatalog.English("storage"), "CANCEL"));
    }

    [TestMethod]
    [DataRow("EMPTY")]
    [DataRow("CANCEL")]
    [DataRow("MENU")]
    public void CollidingDisplayLabelsCannotChangePhysicalSlotHint(string label)
    {
        var text = PresentationTextCatalog.Parse(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["help.enter-menu"] = label, ["help.enter-empty"] = label, ["help.enter"] = "{action}"
        }));
        foreach (var (partyOpen, cursor, holding, occupied, expected) in new[]
        {
            (false, 0, false, false, StorageMoveHelpKind.Empty),
            (false, 0, false, true, StorageMoveHelpKind.Menu),
            (false, 0, true, false, StorageMoveHelpKind.Menu),
            (false, 0, true, true, StorageMoveHelpKind.Menu),
            (true, 6, false, false, StorageMoveHelpKind.Cancel),
            (true, 6, true, false, StorageMoveHelpKind.Cancel)
        })
        {
            var kind = StorageMoveHelpPresentation.SelectSlot(partyOpen, cursor, holding, occupied);
            Assert.AreEqual(expected, kind);
            Assert.AreEqual(label, StorageMoveHelpPresentation.Text(kind, text, label));
            Assert.AreEqual(expected, StorageMoveHelpPresentation.SelectSlot(partyOpen, cursor, holding, occupied));
        }
    }

    [TestMethod]
    public void StorageAuthoredNoticesAndDesktopHintsKeepExistingEnglish()
    {
        var text = PresentationTextCatalog.English("storage");
        (string Id, string Expected)[] entries =
        [
            ("notice.busy", "The PC is busy.\nPlease try again."),
            ("notice.pokemon-changed", "The selected POKéMON has changed.\nPlease choose its current slot."),
            ("notice.group-occupied", "That space is occupied.\nChoose empty slots for the group."),
            ("notice.slot-occupied", "That slot is occupied.\nChoose the POKéMON to shift."),
            ("notice.option-unavailable", "This PC option is not available yet.\nChoose another option or go back."),
            ("notice.empty-slot", "There is no POKéMON in this slot."),
            ("notice.action-failed", "This action could not be completed.\nChoose another option or go back."),
            ("notice.box-changed", "The selected BOX has changed.\nPlease choose it again."),
            ("notice.name-characters", "Use up to 8 source keyboard characters."),
            ("notice.selection-changed", "The selection has changed.\nPlease choose it again."),
            ("notice.deposit-withdraw-menu", "Use DEPOSIT or WITHDRAW from\nthe storage menu for this option."),
            ("notice.item-selection-changed", "The selected POKéMON or item changed.\nPlease choose again."),
            ("notice.egg-item", "An EGG can't hold an item."),
            ("notice.item-transfer-failed", "The item couldn't be transferred.\nPlease choose again."),
            ("notice.item-description-unavailable", "No item information is available."),
            ("label.holding", "HOLDING"),
            ("label.holding-item", "HOLDING ITEM"),
            ("label.no-item", "NO ITEM"),
            ("heading.give-item", "BAG / GIVE ITEM"),
            ("help.home-box", "Home: BOX"),
            ("help.enter-empty", "ENTER: EMPTY"),
            ("help.enter-menu", "ENTER: MENU"),
            ("help.release-enter", "RELEASE ENTER"),
            ("help.enter-place", "ENTER: PLACE"),
            ("help.hold-enter", "HOLD ENTER"),
            ("help.space-multi", "SPACE: MULTI"),
            ("help.space-single", "SPACE: SINGLE"),
            ("help.item-back", "ENTER / B: BACK")
        ];
        foreach (var (id, expected) in entries) Assert.AreEqual(expected, text.Get(id), id);
        Assert.AreEqual("ENTER: CANCEL", text.Get("help.enter", ("action", "CANCEL")));
        Assert.AreEqual("This item can't be held.", PresentationTextCatalog.English("inventory").Get("notice.not-holdable"));
    }

    [TestMethod]
    public void PartialStoragePackChangesHintsAndNoticesWithEnglishFallbackAndLiteralParameters()
    {
        using var directory = TestAssets.TemporaryOutput();
        File.WriteAllText(Path.Combine(directory.Path, "storage.json"),
            """{"notice.busy":"PC unavailable.","help.enter":"{action}: Return","help.enter-menu":"OPEN MENU"}""");
        var text = PresentationTextCatalog.Load("storage", directory.Path);
        Assert.AreEqual("PC unavailable.", text.Get("notice.busy"));
        Assert.AreEqual("OPEN MENU", StorageMoveHelpPresentation.Text(StorageMoveHelpKind.Menu, text, "EMPTY"));
        Assert.AreEqual("{action}: Return", StorageMoveHelpPresentation.Text(StorageMoveHelpKind.Cancel, text, "{action}"));
        Assert.AreEqual("ENTER: EMPTY", StorageMoveHelpPresentation.Text(StorageMoveHelpKind.Empty, text, "MENU"));
        Assert.AreEqual("HOLD ENTER", text.Get("help.hold-enter"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => StorageMoveHelpPresentation.Text((StorageMoveHelpKind)99, text, "CANCEL"));
    }

    [TestMethod]
    [DataRow("gPCText_Cancel", "CANCEL$")]
    [DataRow("gPCText_Move", "MOVE$")]
    [DataRow("gText_Yes", "YES$")]
    [DataRow("gText_No", "NO$")]
    public void ReusedStorageLabelsAlreadyExistInCanonicalStoryText(string symbol, string expected)
        => Assert.AreEqual(expected, TestAssets.Cached(() => PretStoryRuntime.Load(TestAssets.Root)).TextBank[symbol]);
}
