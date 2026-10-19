using Game.Content;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SummaryPartyMenuPresentationTests
{
    private static Dictionary<string, string> Source() =>
        TestAssets.Cached(() => PretBattleText.Load(TestAssets.Root))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static SummaryPartyMenuEntry[] Menu(bool dayCare, int partyCount, bool egg, bool sendOut, SourceSummaryText text)
        => dayCare ? SummaryInteractionPresentationAssets.DayCarePartyActions(egg, text.Label)
            : SummaryInteractionPresentationAssets.BattlePartyActions(partyCount, egg, sendOut, text.Label);

    [TestMethod]
    public void SharedSummaryLabelsUseSourceTextAndAuthoredControlsRemainReplaceable()
    {
        var source = Source();
        source["gText_PokeSum_PageName_PokemonInfo"] = "CREATURE INFO";
        source["gText_Next"] = "{A_BUTTON}FORWARD";
        source["gText_PokeSum_Controls_Pick"] = "{DPAD_UPDOWN}CHOOSE";
        var text = new SourceSummaryText(source);
        Assert.AreEqual("CREATURE INFO", text.PageTitle(0, false));
        Assert.AreEqual("CREATURE INFO", text.PageTitle(2, true));
        Assert.AreEqual("POKéMON SKILLS", text.PageTitle(1, false));
        Assert.AreEqual("KNOWN MOVES", text.PageTitle(2, false));
        Assert.AreEqual("FORWARD", text.Next);
        Assert.AreEqual("CHOOSE", text.Control(new(SummaryInteractionMode.Replace, 0)));
        Assert.AreEqual("EXP. POINTS", text.Experience);
        Assert.AreEqual("NEXT LV.", text.NextLevel);
        Assert.AreEqual("EGG", text.Egg);
        using var directory = TestAssets.TemporaryOutput();
        File.WriteAllText(Path.Combine(directory.Path, "summary.json"), """{"control.details":"INSPECT"}""");
        var authored = PresentationTextCatalog.Load("summary", directory.Path);
        Assert.AreEqual("INSPECT", authored.Get("control.details"));
        Assert.AreEqual("BACK", authored.Get("control.back"));
        Assert.AreEqual("The move order could not be\nchanged here.", authored.Get("notice.order-failed"));
    }

    [TestMethod]
    [DataRow("gText_Next", "NEXT")]
    [DataRow("gText_Next", "{A_BUTTON}")]
    [DataRow("gText_Next", "{A_BUTTON}{UNKNOWN}NEXT")]
    [DataRow("gText_PokeSum_Controls_Pick", "{DPAD_LEFT}PICK")]
    [DataRow("gText_PokeSum_PageName_KnownMoves", "{UNKNOWN}")]
    public void SummaryLabelsRejectWrongOrUnconsumedSourceControls(string symbol, string value)
    {
        var source = Source(); source[symbol] = value;
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceSummaryText(source));
    }

    [TestMethod]
    [DataRow(false, 1, false, false, "Summary,Cancel", "SUMMARY,CANCEL")]
    [DataRow(false, 6, true, true, "Summary,Cancel", "SUMMARY,CANCEL")]
    [DataRow(false, 6, false, false, "Shift,Summary,Cancel", "SHIFT,SUMMARY,CANCEL")]
    [DataRow(false, 6, false, true, "SendOut,Summary,Cancel", "SEND OUT,SUMMARY,CANCEL")]
    [DataRow(true, 6, true, false, "Summary,Cancel", "SUMMARY,CANCEL")]
    [DataRow(true, 6, false, false, "Store,Summary,Cancel", "STORE,SUMMARY,CANCEL")]
    public void SourceEnglishKeepsOrderAndEggOrSinglePartyGates(bool dayCare, int partyCount, bool egg, bool sendOut,
        string expectedActions, string expectedLabels)
    {
        var text = new SourceSummaryText(Source());
        var menu = Menu(dayCare, partyCount, egg, sendOut, text);
        CollectionAssert.AreEqual(expectedActions.Split(',').Select(Enum.Parse<SummaryPartyAction>).ToArray(), menu.Select(entry => entry.Id).ToArray());
        CollectionAssert.AreEqual(expectedLabels.Split(','), menu.Select(entry => entry.Label).ToArray());
        Assert.AreEqual("Choose a POKéMON.", text.ChoosePrompt);
        Assert.AreEqual("Do what with this POKéMON?", text.ActionPrompt);
    }

    [TestMethod]
    [DataRow(false, 1, false, false, "Summary,Cancel", "SUMMARY,CANCEL")]
    [DataRow(false, 6, true, true, "Summary,Cancel", "SUMMARY,CANCEL")]
    [DataRow(false, 6, false, false, "Shift,Summary,Cancel", "SHIFT,SUMMARY,CANCEL")]
    [DataRow(false, 6, false, true, "SendOut,Summary,Cancel", "SEND OUT,SUMMARY,CANCEL")]
    [DataRow(true, 6, true, false, "Summary,Cancel", "SUMMARY,CANCEL")]
    [DataRow(true, 6, false, false, "Store,Summary,Cancel", "STORE,SUMMARY,CANCEL")]
    public void CollidingLabelsDoNotChangeActionDispatchAndBackAlwaysCancels(bool dayCare, int partyCount, bool egg,
        bool sendOut, string expectedActions, string expectedLabels)
    {
        var source = Source();
        foreach (string symbol in new[] { "gText_Summary5", "gFameCheckerText_Cancel", "gText_Shift", "gText_SendOut", "gText_Store" })
            source[symbol] = "CANCEL";
        var menu = Menu(dayCare, partyCount, egg, sendOut, new SourceSummaryText(source));
        var expected = expectedActions.Split(',').Select(Enum.Parse<SummaryPartyAction>).ToArray();
        Assert.AreEqual(expectedLabels.Split(',').Length, menu.Length);
        for (int cursor = 0; cursor < menu.Length; cursor++)
        {
            Assert.AreEqual("CANCEL", menu[cursor].Label);
            Assert.AreEqual(expected[cursor], SummaryInteractionPresentationAssets.SelectedPartyAction(menu, cursor, confirm: true, cancel: false));
            Assert.AreEqual(SummaryPartyAction.Cancel, SummaryInteractionPresentationAssets.SelectedPartyAction(menu, cursor, confirm: false, cancel: true));
            Assert.AreEqual(SummaryPartyAction.Cancel, SummaryInteractionPresentationAssets.SelectedPartyAction(menu, cursor, confirm: true, cancel: true));
            Assert.IsNull(SummaryInteractionPresentationAssets.SelectedPartyAction(menu, cursor, confirm: false, cancel: false));
        }
    }

    [TestMethod]
    public void ChangedSourceLabelsAndPromptsAreCapturedAtComposition()
    {
        var source = Source();
        source["gText_Store"] = "LEAVE HERE";
        source["gText_ChoosePokemon2"] = "Choose a companion.";
        source["gText_DoWhatWithPokemon"] = "Action for {PKMN}?";
        var text = new SourceSummaryText(source);
        source["gText_Store"] = "CHANGED";
        source["gText_DoWhatWithPokemon"] = "{UNKNOWN}";
        Assert.AreEqual("LEAVE HERE", text.Label(SummaryPartyAction.Store));
        Assert.AreEqual("Choose a companion.", text.ChoosePrompt);
        Assert.AreEqual("Action for POKéMON?", text.ActionPrompt);
    }

    [TestMethod]
    [DataRow("gText_Summary5")]
    [DataRow("gFameCheckerText_Cancel")]
    [DataRow("gText_Shift")]
    [DataRow("gText_SendOut")]
    [DataRow("gText_Store")]
    [DataRow("gText_ChoosePokemon2")]
    [DataRow("gText_DoWhatWithPokemon")]
    public void MissingRequiredSourceLabelFailsAtComposition(string symbol)
    {
        var source = Source();
        Assert.IsTrue(source.Remove(symbol));
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceSummaryText(source));
    }

    [TestMethod]
    [DataRow("gText_Store", "")]
    [DataRow("gText_Summary5", "   ")]
    [DataRow("gText_Shift", "SHIFT{UNKNOWN}")]
    [DataRow("gText_SendOut", "SEND\\nOUT")]
    [DataRow("gFameCheckerText_Cancel", "CANCEL\n")]
    [DataRow("gText_ChoosePokemon2", "{PKMN}")]
    [DataRow("gText_DoWhatWithPokemon", "Do what with {UNKNOWN}?")]
    [DataRow("gText_DoWhatWithPokemon", "{PKMN}\\p")]
    public void UnsupportedControlsAndEmptyLabelsFailAtComposition(string symbol, string value)
    {
        var source = Source();
        source[symbol] = value;
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceSummaryText(source));
    }

    [TestMethod]
    [DataRow(0, "AlmostReady")]
    [DataRow(5, "AlmostReady")]
    [DataRow(6, "Soon")]
    [DataRow(10, "Soon")]
    [DataRow(11, "SomeTime")]
    [DataRow(40, "SomeTime")]
    [DataRow(41, "LongTime")]
    [DataRow(255, "LongTime")]
    public void EggHatchLinesKeepSourceFriendshipThresholds(int friendship, string suffix)
    {
        var source = Source(); string symbol = "gText_PokeSum_EggHatch_" + suffix;
        Assert.AreEqual(source[symbol].Replace("\\n", "\n", StringComparison.Ordinal),
            string.Join('\n', new SourceSummaryText(source).EggHatchLines(friendship)));
        source[symbol] = "ONE\\nTWO\\nTHREE";
        CollectionAssert.AreEqual(new[] { "ONE", "TWO", "THREE" }, new SourceSummaryText(source).EggHatchLines(friendship).ToArray());
    }

    [TestMethod]
    [DataRow(true, false, 1, true, "NicePlace")]
    [DataRow(true, true, 4, false, "NicePlace")]
    [DataRow(false, false, 4, true, "Trade")]
    [DataRow(false, true, 3, true, "Trade")]
    [DataRow(false, true, 4, true, "TravelingMan")]
    [DataRow(false, true, 5, true, "TravelingMan")]
    [DataRow(false, true, 4, false, "DayCare")]
    [DataRow(false, true, 5, false, "DayCare")]
    public void EggOriginsKeepSourcePrecedenceAndAllowDisplayReplacement(bool fateful, bool currentOwner,
        int metGame, bool giftEgg, string suffix)
    {
        var source = Source(); string symbol = "gText_PokeSum_EggOrigin_" + suffix;
        Assert.AreEqual(source[symbol].Replace("\\n", "\n", StringComparison.Ordinal),
            string.Join('\n', new SourceSummaryText(source).EggOriginLines(fateful, currentOwner, metGame, giftEgg)));
        source[symbol] = "ORIGIN\\nDETAIL";
        CollectionAssert.AreEqual(new[] { "ORIGIN", "DETAIL" },
            new SourceSummaryText(source).EggOriginLines(fateful, currentOwner, metGame, giftEgg).ToArray());
    }

    [TestMethod]
    [DataRow("gText_PokeSum_EggHatch_AlmostReady", "ONE\\nTWO")]
    [DataRow("gText_PokeSum_EggOrigin_DayCare", "ONE\\nTWO\\nTHREE")]
    [DataRow("gText_PokeSum_EggOrigin_Trade", "ONE\\pTWO")]
    [DataRow("gText_PokeSum_EggOrigin_NicePlace", "{UNKNOWN}\\nTWO")]
    public void EggTextRejectsWrongLineStructureAndUnknownControls(string symbol, string replacement)
    {
        var source = Source(); source[symbol] = replacement;
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceSummaryText(source));
    }

}
