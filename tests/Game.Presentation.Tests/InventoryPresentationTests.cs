using FireRed.Tests;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class InventoryPresentationTests
{
    private static readonly int[] Shift = [0,3,6,8,11,13,14,15,16,15,14,13,11,8,6,3,0];
    private static void Step(StorageHandPresentation hand, int frames) { for(int i=0;i<frames;i++) hand.AdvanceFrame(); }

    [TestMethod]
    [DataRow(0,40,47)][DataRow(49,28,53)][DataRow(50,41,46)][DataRow(57,40,46)][DataRow(-1,27,54)]
    public void DiscOrderMatchesSourceTmAndHmPositions(int index,int x,int y)
        => Assert.AreEqual((x,y),MachineDiscPresentation.Position(index));

    [TestMethod]
    public void DiscLowersBeforeChangingTypeThenRaisesAndHidesForClose()
    {
        var disc=new MachineDiscPresentation();disc.Select(0);
        Assert.AreEqual(0,disc.OffsetY);Assert.IsFalse(disc.Moving);
        disc.Select(50);disc.AdvanceFrame();Assert.AreEqual(10,disc.OffsetY);Assert.AreEqual(0,disc.Displayed);
        disc.AdvanceFrame();Assert.AreEqual(20,disc.OffsetY);Assert.AreEqual(0,disc.Displayed);
        disc.AdvanceFrame();Assert.AreEqual(50,disc.Displayed);Assert.AreEqual(20,disc.OffsetY);
        disc.AdvanceFrame();Assert.AreEqual(10,disc.OffsetY);disc.AdvanceFrame();Assert.AreEqual(0,disc.OffsetY);
        disc.AdvanceFrame();Assert.IsFalse(disc.Moving);
        disc.Select(-1);for(int n=0;n<3;n++)disc.AdvanceFrame();Assert.AreEqual(-1,disc.Displayed);Assert.IsFalse(disc.Moving);
    }

    [TestMethod]
    public void RapidDiscReselectionAndScreenResetNeverExposeWrongDisc()
    {
        var disc=new MachineDiscPresentation();disc.Select(0);disc.Select(10);disc.AdvanceFrame();disc.Select(50);
        disc.AdvanceFrame();Assert.AreEqual(0,disc.Displayed);disc.AdvanceFrame();Assert.AreEqual(50,disc.Displayed);
        disc.Select(-1);disc.AdvanceFrame();Assert.AreEqual(-1,disc.Displayed);
        disc.Reset();disc.Select(49);Assert.AreEqual(49,disc.Displayed);Assert.AreEqual(0,disc.OffsetY);Assert.IsFalse(disc.Moving);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>disc.Select(58));
    }

    [TestMethod]
    public void GrabKeepsIconOnSlotUntilOpenHandContactsThenRaisesFistAndIcon()
    {
        var hand=new StorageHandPresentation();hand.Begin(StorageHandAction.Grab);
        Assert.AreEqual(2,hand.Pose(Shift).HandFrame);Step(hand,9);
        Assert.AreEqual(8,hand.Pose(Shift).HandY);Assert.AreEqual(0,hand.Pose(Shift).MovingY);Assert.IsFalse(hand.Pose(Shift).AtContact);
        hand.AdvanceFrame();Assert.IsTrue(hand.Pose(Shift).AtContact);Assert.AreEqual(3,hand.Pose(Shift).HandFrame);
        Step(hand,8);Assert.AreEqual(0,hand.Pose(Shift).HandY);Assert.AreEqual(-8,hand.Pose(Shift).MovingY);
        Step(hand,2);Assert.IsFalse(hand.Active);
    }

    [TestMethod]
    [DataRow(2)][DataRow(5)]
    public void PlaceLowersHeldIconOpensHandThenLeavesIconOnDestination(int value)
    {
        var action=(StorageHandAction)value;
        var hand=new StorageHandPresentation();hand.Begin(action);
        Assert.AreEqual(-8,hand.Pose(Shift).MovingY);Assert.AreEqual(3,hand.Pose(Shift).HandFrame);
        Step(hand,9);Assert.AreEqual(2,hand.Pose(Shift).HandFrame);Assert.AreEqual(0,hand.Pose(Shift).MovingY);
        Step(hand,8);Assert.AreEqual(0,hand.Pose(Shift).HandY);Assert.AreEqual(0,hand.Pose(Shift).MovingY);
        hand.AdvanceFrame();Assert.AreEqual(0,hand.Pose(Shift).HandFrame);
        hand.AdvanceFrame();Assert.IsFalse(hand.Active);
    }

    [TestMethod]
    public void ShiftUsesSourceSixteenStepArcAndReversesDrawOrderHalfway()
    {
        var hand=new StorageHandPresentation();hand.Begin(StorageHandAction.Shift);
        for(int step=0;step<=16;step++)
        {
            var pose=hand.Pose(Shift);
            Assert.AreEqual(-Shift[step],pose.MovingX);Assert.AreEqual(Shift[step],pose.TargetX);
            Assert.AreEqual(-8+(step+1)/2,pose.MovingY);Assert.AreEqual(-(step+1)/2,pose.TargetY);
            Assert.AreEqual(step>=8,pose.TargetOnTop);hand.AdvanceFrame();
        }
        Assert.AreEqual(3,hand.Pose(Shift).HandFrame);hand.AdvanceFrame();Assert.IsFalse(hand.Active);
    }

    [TestMethod]
    public void ResetAtEveryFrameClearsActionAndAllowsDifferentNextScreenFlow()
    {
        foreach(var action in Enum.GetValues<StorageHandAction>().Where(a=>a!=StorageHandAction.None))
        for(int frame=0;frame<20;frame++)
        {
            var hand=new StorageHandPresentation();hand.Begin(action);Step(hand,frame);hand.Reset();
            Assert.IsFalse(hand.Active);Assert.AreEqual(0,hand.Frame);
            hand.AdvanceFrame();Assert.IsFalse(hand.Active);
            hand.Begin(StorageHandAction.Place);Assert.AreEqual(-8,hand.Pose(Shift).MovingY);
        }
    }

    [TestMethod]
    public void FiveRowSharedHitAreaIncludesCloseForEmptyAndScrollingLists()
    {
        Assert.AreEqual((80,10,152,16),(MachineCaseLayout.ListX,MachineCaseLayout.ListY,MachineCaseLayout.ListWidth,MachineCaseLayout.RowHeight));
        foreach(int count in new[]{0,1,4,5,57})
        for(int cursor=0;cursor<=count;cursor++)
        {
            int first=MachineCaseLayout.KeepCursorVisible(cursor,0,count),visible=MachineCaseLayout.VisibleRows(count,first);
            Assert.IsTrue(visible is >=1 and <=5);Assert.IsTrue(cursor>=first&&cursor<first+visible);
            Assert.IsTrue(MachineCaseLayout.ListY+(visible-1)*16<88);
        }
    }

    [TestMethod]
    public void ScrolledHoverAndClickKeepTheSameVisibleMachineAndPopupOrigin()
    {
        const int count = 9;
        int first = 0;
        for (int cursor = 0; cursor <= count; cursor++)
            first = MachineCaseLayout.KeepCursorVisible(cursor, first, count);
        Assert.AreEqual(5, first); // TM10, TM15, TM17, TM39, CLOSE.
        // Native report: viewport origin (110,86), scale 4, click (661,349).
        double y = (349 - 86) / 4d;
        int row = (int)((y - MachineCaseLayout.ListY) / MachineCaseLayout.RowHeight);
        Assert.AreEqual(3, row);
        int hovered = first + row;
        Assert.AreEqual(8, hovered); // TM39.
        for (int eventIndex = 0; eventIndex < 4; eventIndex++)
        {
            first = MachineCaseLayout.KeepCursorVisible(hovered, first, count);
            Assert.AreEqual(5, first); // Repeated hover, redraw, click, action popup.
            Assert.AreEqual(hovered, first + row);
        }
        Assert.AreEqual(5, MachineCaseLayout.KeepCursorVisible(5, first, count));
        Assert.AreEqual(4, MachineCaseLayout.KeepCursorVisible(4, first, count));
        Assert.AreEqual(0, MachineCaseLayout.KeepCursorVisible(0, first, 0));
        Assert.AreEqual(4, MachineCaseLayout.KeepCursorVisible(8, first, 8)); // Consumed final stack.
        Assert.AreEqual(0, MachineCaseLayout.KeepCursorVisible(0, 0, count)); // Reopen.
    }

    [TestMethod]
    public void GeneratedInventoryPackageHasBothGendersAllDiscTypesAndSourceShiftSamples()
    {
        var assets=InventoryPresentationAssets.Load(TestAssets.Root);
        CollectionAssert.AreEqual(Shift,assets.ShiftOffsets);
        Assert.AreEqual(17,assets.DiscTypes.Length);Assert.AreEqual(32,assets.ArrowOffsets.Length);
        foreach(string id in new[]{"case-male","case-female","case-male-front","case-female-front","hand-single","hand-multi","hand-shadow","discs","hm","number","move-labels","scroll-up","scroll-down"})
            Assert.IsTrue(assets.Assets.Any(a=>a.Id==id));
    }

    private static string InventoryLabel(InventoryMenuAction action)
    {
        if (action == InventoryMenuAction.Deposit)
            return Game.Content.PresentationTextCatalog.English("inventory").Get("action.deposit");
        string symbol = InventoryMenuPresentation.SourceSymbol(action);
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/ui/item-ui/item-ui.json")));
        if (document.RootElement.GetProperty("texts").TryGetProperty(symbol, out var text)) return text.GetString()!;
        return InventoryStory()[symbol].TrimEnd('$');
    }

    private static IReadOnlyDictionary<string, string> InventoryStory() =>
        TestAssets.Cached(() => Game.Content.PretStoryRuntime.Load(TestAssets.Root)).TextBank;

    [TestMethod]
    [DataRow("withdraw", false, false, false, false, "Withdraw,Give,Cancel", "WITHDRAW|GIVE|CANCEL")]
    [DataRow("deposit", false, false, false, false, "Deposit,Cancel", "DEPOSIT|CANCEL")]
    [DataRow("bag", true, false, false, false, "Use,Give,Toss,Cancel", "USE|GIVE|TOSS|CANCEL")]
    [DataRow("bag", true, false, true, true, "Check,Give,Toss,Cancel", "CHECK|GIVE|TOSS|CANCEL")]
    [DataRow("berries", true, false, true, false, "Use,Give,Toss,Exit", "USE|GIVE|TOSS|EXIT")]
    [DataRow("bag", true, true, true, false, "Give,Cancel", "GIVE|CANCEL")]
    [DataRow("bag", true, true, false, false, "Cancel", "CANCEL")]
    [DataRow("other", false, false, true, false, "Use,Give,Cancel", "USE|GIVE|CANCEL")]
    [DataRow("other", false, false, false, false, "Use,Cancel", "USE|CANCEL")]
    public void InventoryActionsPreserveOriginGatesOrderAndSourceEnglish(string view, bool field, bool target,
        bool holdable, bool mail, string expectedIds, string expectedLabels)
    {
        var actions = InventoryMenuPresentation.ItemActions(view, field, target, holdable, mail, InventoryLabel);
        Assert.AreEqual(expectedIds, string.Join(",", actions.Select(action => action.Id)));
        Assert.AreEqual(expectedLabels, string.Join('|', actions.Select(action => action.Label)));
    }

    [TestMethod]
    public void InventoryDisplayCollisionsCannotIdentifyGiveTossOrCancel()
    {
        foreach (string view in new[] { "withdraw", "deposit", "bag", "berries", "other" })
        foreach (bool field in new[] { false, true })
        foreach (bool target in new[] { false, true })
        foreach (bool holdable in new[] { false, true })
        foreach (bool mail in new[] { false, true })
        {
            var original = InventoryMenuPresentation.ItemActions(view, field, target, holdable, mail, id => id.ToString());
            var changed = InventoryMenuPresentation.ItemActions(view, field, target, holdable, mail, _ => "TOSS");
            CollectionAssert.AreEqual(original.Select(action => action.Id).ToArray(), changed.Select(action => action.Id).ToArray());
            Assert.IsTrue(changed.All(action => action.Label == "TOSS"));
        }
    }

    [TestMethod]
    [DataRow(true, false, true, false, "Use,Register,Cancel", "USE|REGISTER|CANCEL")]
    [DataRow(true, false, true, true, "Use,Deselect,Cancel", "USE|DESELECT|CANCEL")]
    [DataRow(true, false, false, false, "Use,Cancel", "USE|CANCEL")]
    [DataRow(true, true, true, false, "Use,Register,Cancel", "USE|REGISTER|CANCEL")]
    [DataRow(false, false, false, false, "Give,Toss,Cancel", "GIVE|TOSS|CANCEL")]
    [DataRow(false, true, false, false, "Give,Cancel", "GIVE|CANCEL")]
    public void QuestActionsPreserveRegistrationAndHeldTargetGates(bool keys, bool target, bool canRegister,
        bool registered, string expectedIds, string expectedLabels)
    {
        var actions = InventoryMenuPresentation.QuestActions(keys, target, canRegister, registered, InventoryLabel);
        Assert.AreEqual(expectedIds, string.Join(",", actions.Select(action => action.Id)));
        Assert.AreEqual(expectedLabels, string.Join('|', actions.Select(action => action.Label)));
        var collision = InventoryMenuPresentation.QuestActions(keys, target, canRegister, registered, _ => "TOSS");
        CollectionAssert.AreEqual(actions.Select(action => action.Id).ToArray(), collision.Select(action => action.Id).ToArray());
    }

    [TestMethod]
    public void InventorySourcePromptsKeepTheirSymbolsNewlinesAndPlaceholders()
    {
        var source = InventoryStory();
        (string Symbol, string Expected)[] prompts =
        [
            ("gOtherText_WhereShouldTheStrVar1BePlaced", "Where should the {STR_VAR_1}\\nbe placed?$"),
            ("gText_TossOutHowManyStrVar1s", "Toss out how many\\n{STR_VAR_1}(s)?$"),
            ("gText_ThrowAwayStrVar2OfThisItemQM", "Throw away {STR_VAR_2} of\\nthis item?$"),
            ("gText_ThrewAwayStrVar2StrVar1s", "Threw away {STR_VAR_2}\\n{STR_VAR_1}(s).$"),
            ("gText_GiveToWhichPokemon", "Give to which POKéMON?$")
        ];
        foreach (var (symbol, expected) in prompts) Assert.AreEqual(expected, source[symbol], symbol);
        Assert.AreEqual("Toss out how many\nPOTION(s)?", InventoryMenuPresentation.BindSource(
            source["gText_TossOutHowManyStrVar1s"].TrimEnd('$').Replace("\\n", "\n", StringComparison.Ordinal), "POTION"));
        Assert.AreEqual("Throw away 3 of\nthis item?", InventoryMenuPresentation.BindSource(
            source["gText_ThrowAwayStrVar2OfThisItemQM"].TrimEnd('$').Replace("\\n", "\n", StringComparison.Ordinal), strVar2: "3"));
        Assert.AreEqual("Threw away 3\nPOTION(s).", InventoryMenuPresentation.BindSource(
            source["gText_ThrewAwayStrVar2StrVar1s"].TrimEnd('$').Replace("\\n", "\n", StringComparison.Ordinal), "POTION", "3"));
    }

    [TestMethod]
    public void InventorySourceBindingTreatsInsertedTokensAsLiteralAndRejectsUnknownControls()
    {
        Assert.AreEqual("{STR_VAR_2}\\p 3 {STR_VAR_2}\\p", InventoryMenuPresentation.BindSource(
            "{STR_VAR_1} {STR_VAR_2} {STR_VAR_1}", "{STR_VAR_2}\\p", "3"));
        Assert.ThrowsExactly<ArgumentException>(() => InventoryMenuPresentation.BindSource("{STR_VAR_1}"));
        foreach (string bad in new[] { "{PLAYER}", "{PAUSE 60}", "{STR_VAR_3}", "{", "}" })
            Assert.ThrowsExactly<InvalidDataException>(() => InventoryMenuPresentation.BindSource(bad, "POTION", "3"));
    }

    [TestMethod]
    public void InventoryAuthoredMessagesKeepEnglishAndAcceptPartialOverrides()
    {
        var english = Game.Content.PresentationTextCatalog.English("inventory");
        (string Id, string Expected)[] notices =
        [
            ("notice.use-unavailable", "This item's use is not\nimplemented yet."),
            ("notice.not-holdable", "This item can't be held."),
            ("notice.target-changed", "The selected POKéMON changed."),
            ("notice.nothing-held", "It isn't holding anything."),
            ("notice.take-bag-full", "The BAG is full. The item\ncouldn't be taken."),
            ("notice.mail-egg-transfer-unavailable", "Mail and Eggs aren't supported\nfor item transfers here."),
            ("notice.transfer-failed", "The item couldn't be transferred."),
            ("notice.registered", "Registered. Press R in the field."),
            ("notice.fame-checker-unavailable", "FAME CHECKER viewing is not\nimplemented yet."),
            ("notice.teachy-tv-unavailable", "TEACHY TV playback is not\nimplemented yet."),
            ("notice.action-unavailable", "This action isn't available."),
            ("notice.order-failed", "The item order couldn't be changed."),
            ("notice.not-tossable", "That item can't be tossed.")
        ];
        foreach (var (id, expected) in notices) Assert.AreEqual(expected, english.Get(id), id);
        Assert.AreEqual("Received POTION.", english.Get("notice.received", ("item", "POTION")));
        Assert.AreEqual("Gave POTION\nto RED.", english.Get("notice.gave", ("item", "POTION"), ("pokemon", "RED")));
        Assert.AreEqual("HELD ITEM / NONE", english.Get("held.header", ("item", "NONE")));
        Assert.AreEqual("Switch its held item for\nPOTION?", english.Get("prompt.exchange", ("item", "POTION")));
        using var directory = TestAssets.TemporaryOutput();
        File.WriteAllText(Path.Combine(directory.Path, "inventory.json"), """{"notice.gave":"{pokemon}: {item}; {item}","action.deposit":"STORE"}""");
        var translated = Game.Content.PresentationTextCatalog.Load("inventory", directory.Path);
        Assert.AreEqual("STORE", translated.Get("action.deposit"));
        Assert.AreEqual("{item}: {pokemon}; {pokemon}", translated.Get("notice.gave", ("pokemon", "{item}"), ("item", "{pokemon}")));
        Assert.AreEqual(english.Get("notice.not-holdable"), translated.Get("notice.not-holdable"));
    }

}
