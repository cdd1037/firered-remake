using Game.Content;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SourceBattleSelectionTextTests
{
    private static Dictionary<string, string> Source() =>
        TestAssets.Cached(() => PretBattleText.Load(TestAssets.Root))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static string Menu(string first, string second, string third, string fourth) =>
        "{PALETTE 5}{COLOR_HIGHLIGHT_SHADOW 13 14 15}" + first + "{CLEAR_TO 56}" + second +
        "\\n" + third + "{CLEAR_TO 56}" + fourth;

    [TestMethod]
    public void ImportedEnglishSelectionTextKeepsSourceOrderAndSpacing()
    {
        var text = new SourceBattleSelectionText(Source());
        CollectionAssert.AreEqual(new[] { "FIGHT", "BAG", "POKéMON", "RUN" }, text.ActionLabels.ToArray());
        Assert.AreEqual("What will\nPIKACHU do?", text.ActionPrompt("PIKACHU", "RED"));
        Assert.AreEqual("Foe ", text.FoePrefix);
        Assert.AreEqual("Wild ", text.WildPrefix);
        Assert.AreEqual("PP ", text.PpLabel);
        Assert.AreEqual("TYPE/", text.TypeLabel);
    }

    [TestMethod]
    public void ChangedAndCollidingLabelsRemainFourSourceOrderedCells()
    {
        var source = Source();
        foreach (var labels in new[] { new[] { "RUN", "FIGHT", "RUN", "BAG" }, new[] { "SAME", "SAME", "SAME", "SAME" } })
        {
            source["gText_BattleMenu"] = Menu(labels[0], labels[1], labels[2], labels[3]);
            var text = new SourceBattleSelectionText(source);
            CollectionAssert.AreEqual(labels, text.ActionLabels.ToArray());
            Assert.AreEqual(4, text.ActionLabels.Length);
        }
    }

    [TestMethod]
    public void PrefixesAndMoveLabelsComeFromSourceWithoutTrimming()
    {
        var source = Source();
        source["sText_FoePkmnPrefix"] = "Opponent  ";
        source["sText_WildPkmnPrefix"] = "";
        source["gText_MoveInterfacePP"] = "Uses ";
        source["gText_MoveInterfaceType"] = "Kind/";
        var text = new SourceBattleSelectionText(source);
        Assert.AreEqual("Opponent  ", text.FoePrefix);
        Assert.AreEqual("", text.WildPrefix);
        Assert.AreEqual("Uses ", text.PpLabel);
        Assert.AreEqual("Kind/", text.TypeLabel);
    }

    [TestMethod]
    public void ReorderedRepeatedPromptParametersPreserveInsertedNamesLiterally()
    {
        var source = Source();
        source["gText_WhatWillPkmnDo"] = "For {PLAYER}: {B_ACTIVE_NAME_WITH_PREFIX}\\n{B_PLAYER_NAME}/{B_ACTIVE_NAME_WITH_PREFIX}";
        var text = new SourceBattleSelectionText(source);
        const string nickname = @"{PLAYER}\n{B_PLAYER_NAME}\p{PALETTE 5}";
        const string player = @"{B_ACTIVE_NAME_WITH_PREFIX}\l""RED""";
        Assert.AreEqual("For " + player + ": " + nickname + "\n" + player + "/" + nickname,
            text.ActionPrompt(nickname, player));
        Assert.AreEqual("For BLUE: EEVEE\nBLUE/EEVEE", text.ActionPrompt("EEVEE", "BLUE"));
    }

    [TestMethod]
    public void SupportedLiteralEscapesDoNotBecomeMenuSeparatorsOrPromptControls()
    {
        var source = Source();
        source["gText_BattleMenu"] = Menu(@"A\\n", "\\\"B\\\"", "C", "D");
        source["gText_WhatWillPkmnDo"] = "\\\"{B_ACTIVE_NAME_WITH_PREFIX}\\\"\\n\\\\n";
        var text = new SourceBattleSelectionText(source);
        CollectionAssert.AreEqual(new[] { @"A\n", "\"B\"", "C", "D" }, text.ActionLabels.ToArray());
        Assert.AreEqual("\"PIKACHU\"\n\\n", text.ActionPrompt("PIKACHU", "RED"));
    }

    [TestMethod]
    [DataRow("{PALETTE 5}", "")]
    [DataRow("{PALETTE 5}", "{PALETTE 6}")]
    [DataRow("{COLOR_HIGHLIGHT_SHADOW 13 14 15}", "{COLOR_HIGHLIGHT_SHADOW 13 15 14}")]
    [DataRow("{CLEAR_TO 56}", "{CLEAR_TO 57}")]
    [DataRow("{CLEAR_TO 56}", "{CLEAR_TO 0x38}")]
    [DataRow("{CLEAR_TO 56}", "")]
    [DataRow("\\n", "\\p")]
    [DataRow("\\n", "\n")]
    [DataRow("FIGHT", "")]
    [DataRow("FIGHT", "   ")]
    [DataRow("FIGHT", "FIGHT{FONT_SMALL}")]
    [DataRow("FIGHT", "FIGHT{BROKEN")]
    [DataRow("RUN", "RUN}")]
    [DataRow("RUN", "RUN\\")]
    [DataRow("RUN", "RUN\\nEXTRA{CLEAR_TO 56}CELL")]
    public void UnknownMalformedOrDifferentMenuLayoutIsRejected(string original, string replacement)
    {
        var source = Source();
        string changed = source["gText_BattleMenu"].Replace(original, replacement, StringComparison.Ordinal);
        Assert.AreNotEqual(source["gText_BattleMenu"], changed);
        source["gText_BattleMenu"] = changed;
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceBattleSelectionText(source));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("{UNKNOWN}")]
    [DataRow("{B_ACTIVE_NAME_WITH_PREFIX")]
    [DataRow("{{PLAYER}}")]
    [DataRow("PLAYER}")]
    [DataRow("{PAUSE 60}")]
    [DataRow("{WAIT_SE}")]
    [DataRow("Choose\\p{B_ACTIVE_NAME_WITH_PREFIX}")]
    [DataRow("Choose\\l{B_ACTIVE_NAME_WITH_PREFIX}")]
    [DataRow("Choose\\t{B_ACTIVE_NAME_WITH_PREFIX}")]
    [DataRow("Choose\\")]
    [DataRow("Choose\n{B_ACTIVE_NAME_WITH_PREFIX}")]
    public void UnknownPromptParametersAndPageControlsFailAtLoad(string prompt)
    {
        var source = Source();
        source["gText_WhatWillPkmnDo"] = prompt;
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceBattleSelectionText(source));
    }

    [TestMethod]
    [DataRow("sText_FoePkmnPrefix", "{PLAYER} ")]
    [DataRow("sText_WildPkmnPrefix", "Wild\\n")]
    [DataRow("gText_MoveInterfacePP", "PP{COLOR 1}")]
    [DataRow("gText_MoveInterfaceType", "TYPE/}")]
    public void PlainLabelsDoNotSilentlyDiscardSourceControls(string symbol, string value)
    {
        var source = Source();
        source[symbol] = value;
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceBattleSelectionText(source));
    }

    [TestMethod]
    [DataRow("gText_WhatWillPkmnDo")]
    [DataRow("gText_BattleMenu")]
    [DataRow("sText_FoePkmnPrefix")]
    [DataRow("sText_WildPkmnPrefix")]
    [DataRow("gText_MoveInterfacePP")]
    [DataRow("gText_MoveInterfaceType")]
    public void MissingSourceSymbolFailsAtLoad(string symbol)
    {
        var source = Source();
        Assert.IsTrue(source.Remove(symbol));
        Assert.ThrowsExactly<InvalidDataException>(() => new SourceBattleSelectionText(source));
    }

    [TestMethod]
    public void RequiredPromptValuesCannotBeNull()
    {
        var text = new SourceBattleSelectionText(Source());
        Assert.ThrowsExactly<ArgumentNullException>(() => text.ActionPrompt(null!, "RED"));
        Assert.ThrowsExactly<ArgumentNullException>(() => text.ActionPrompt("PIKACHU", null!));
    }

    [TestMethod]
    public void ParsedSelectionTextDoesNotRetainMutableCatalogState()
    {
        var source = Source();
        var text = new SourceBattleSelectionText(source);
        source["gText_BattleMenu"] = "{UNKNOWN}";
        source["gText_WhatWillPkmnDo"] = "{UNKNOWN}";
        source["gText_MoveInterfaceType"] = "CHANGED";
        Assert.AreEqual("FIGHT", text.ActionLabels[0]);
        Assert.AreEqual("What will\nEEVEE do?", text.ActionPrompt("EEVEE", "RED"));
        Assert.AreEqual("TYPE/", text.TypeLabel);
    }
}
