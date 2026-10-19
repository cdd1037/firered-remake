using Game.Domain.Events;
using System.Text.Json;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class SourceTextPackWiringTests
{
    private static PretStoryRuntime Story => TestAssets.Cached(() => PretStoryRuntime.Load(TestAssets.Root));
    private static IReadOnlyDictionary<string, string> Battle => TestAssets.Cached(() =>
        PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(null)));

    private static void WriteGroup(string directory, string group, IReadOnlyDictionary<string, string> values)
    {
        string source = Path.Combine(directory, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, group + ".json"), JsonSerializer.Serialize(values));
    }

    private static void AssertOverlay(IReadOnlyDictionary<string, string> original,
        IReadOnlyDictionary<string, string> actual, IReadOnlyDictionary<string, string> overrides)
    {
        Assert.AreEqual(original.Count, actual.Count, "An overlay cannot add or remove source identities.");
        foreach (var (id, text) in original)
            Assert.AreEqual(overrides.TryGetValue(id, out var replacement) ? replacement : text, actual[id], id);
    }

    [TestMethod]
    public void BattleLoadUsesSelectedPackAndPagesBindValuesLiterally()
    {
        using var output = TestAssets.TemporaryOutput();
        const string symbol = "sText_AttackerUsedX";
        var replacements = new Dictionary<string, string>
        {
            [symbol] = "{B_ATK_NAME_WITH_PREFIX} 使用了\\n{B_BUFF2}"
        };
        WriteGroup(output.Path, "battle", replacements);
        var translated = PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(output.Path));
        AssertOverlay(Battle, translated, replacements);
        Assert.AreNotEqual(replacements[symbol], Battle[symbol], "The selected source must actually change.");
        var values = new Dictionary<string, string>
        {
            ["B_ATK_NAME_WITH_PREFIX"] = "名字{B_BUFF2}\\p",
            ["B_BUFF2"] = "招式{WAIT_SE}"
        };
        Assert.AreSequenceEqual(new[] { "名字{B_BUFF2}\\p 使用了\n招式{WAIT_SE}" },
            SourceBattleText.Pages(translated, symbol, values));
        Assert.IsFalse(Directory.Exists(Path.Combine(output.Path, "content")), "Only the small override pack is written.");
    }

    [TestMethod]
    public void StoryBankOverlaysOnlySpecifiedSymbolsAndRetainsGeneratedFallback()
    {
        using var output = TestAssets.TemporaryOutput();
        const string symbol = "gText_PlayerUsedVar2";
        string original = Story.TextBank[symbol];
        var replacements = new Dictionary<string, string>
        {
            [symbol] = "{PLAYER} 使用了\\n{STR_VAR_2}。$"
        };
        WriteGroup(output.Path, "story", replacements);
        var translated = SourceTextPack.Load(output.Path).Apply("story", Story.TextBank, preserveControls: true);
        AssertOverlay(Story.TextBank, translated, replacements);
        Assert.AreNotEqual(original, translated[symbol]);
        Assert.AreEqual(original, Story.TextBank[symbol], "The shared generated runtime remains immutable.");
        Assert.AreEqual(Story.TextBank["gText_Close"], translated["gText_Close"]);
    }

    [TestMethod]
    public void MenuOverlayChangesLabelsAtTheirOriginalPositionsWithoutChangingLayoutOrIdentity()
    {
        using var output = TestAssets.TemporaryOutput();
        var replacements = new Dictionary<string, string>
        {
            ["gText_Yes"] = "是",
            ["gText_Energypowder_50"] = "能量粉{CLEAR_TO 0x74}{FONT_SMALL}50"
        };
        WriteGroup(output.Path, "story-menus", replacements);
        var original = Story.Compilation;
        var translated = SourceTextPack.Load(output.Path).ApplyMenus(original);
        Assert.AreEqual(original, translated with { Menus = original.Menus }, "Only the menu dictionary may change.");
        Assert.AreEqual(original.Menus.Count, translated.Menus.Count);
        Assert.IsTrue(original.Menus.Values.SelectMany(menu => menu.Options).Count(option => option.TextSymbol == "gText_Yes") > 1,
            "Exercise a label shared by multiple real menus.");
        Assert.IsNotNull(original.Menus["LISTMENU_BERRY_POWDER"].ListLayout);
        int changed = 0;
        foreach (var (id, menu) in original.Menus)
        {
            var actual = translated.Menus[id];
            Assert.AreEqual(menu, actual with { Options = menu.Options }, "Menu identity and every layout field stay unchanged: " + id);
            Assert.AreEqual(menu.Options.Length, actual.Options.Length);
            for (int index = 0; index < menu.Options.Length; index++)
            {
                var option = menu.Options[index];
                string expected = replacements.TryGetValue(option.TextSymbol, out var text) ? text : option.Text;
                Assert.AreEqual(option with { Text = expected }, actual.Options[index], id + ":" + index);
                if (expected != option.Text) changed++;
            }
        }
        Assert.IsTrue(changed > replacements.Count, "Shared labels change at every occurrence without reordering choices.");
    }

    [TestMethod]
    [DataRow("opening", "First\fSecond", "One\fTwo", true)]
    [DataRow("opening", "First\fSecond", "One\nTwo", false)]
    [DataRow("opening", "First\fSecond", "OneTwo", false)]
    [DataRow("opening", "First\fSecond", "\fOneTwo", false)]
    [DataRow("help", "First\fSecond", "One\fTwo", false)]
    [DataRow("battle", "First\fSecond", "One\fTwo", false)]
    public void OpeningPageBreakIsPreservedAndNotAdmittedInOtherSourceGroups(
        string group, string original, string replacement, bool allowed)
    {
        using var output = TestAssets.TemporaryOutput();
        WriteGroup(output.Path, group, new Dictionary<string, string> { ["page"] = replacement });
        var pack = SourceTextPack.Load(output.Path);
        var source = new Dictionary<string, string> { ["page"] = original };
        if (allowed)
            Assert.AreEqual(replacement, pack.Apply(group, source, preserveControls: true)["page"]);
        else
            Assert.ThrowsExactly<InvalidDataException>(() => pack.Apply(group, source, preserveControls: true));
    }

    [TestMethod]
    public void UnselectedPackAndExistingDirectoryWithoutSourceKeepEnglishFallback()
    {
        using var output = TestAssets.TemporaryOutput();
        foreach (var pack in new[] { SourceTextPack.Load(null), SourceTextPack.Load(output.Path) })
            AssertOverlay(Story.TextBank, pack.Apply("story", Story.TextBank, preserveControls: true),
                new Dictionary<string, string>());
        Assert.IsFalse(Directory.Exists(Path.Combine(output.Path, "source")));
    }

    [TestMethod]
    public void ExplicitMissingPackDirectoryDoesNotSilentlyFallBack()
    {
        using var output = TestAssets.TemporaryOutput();
        string missing = Path.Combine(output.Path, "missing-pack");
        Assert.IsFalse(Directory.Exists(missing));
        var error = Assert.ThrowsExactly<DirectoryNotFoundException>(() => SourceTextPack.Load(missing));
        StringAssert.Contains(error.Message, missing);
    }

    [TestMethod]
    public void UnknownSourceGroupFailsAtPackLoad()
    {
        using var output = TestAssets.TemporaryOutput();
        WriteGroup(output.Path, "unknown-source-group", new Dictionary<string, string> { ["unknown"] = "text" });
        var error = Assert.ThrowsExactly<InvalidDataException>(() => SourceTextPack.Load(output.Path));
        StringAssert.Contains(error.Message, "unknown-source-group");
    }

    [TestMethod]
    [DataRow("story")]
    [DataRow("story-menus")]
    [DataRow("battle")]
    public void UnknownSymbolsFailAtTheirRealOverlayBoundary(string group)
    {
        using var output = TestAssets.TemporaryOutput();
        const string id = "Unknown_Test_Text";
        WriteGroup(output.Path, group, new Dictionary<string, string> { [id] = "text" });
        var pack = SourceTextPack.Load(output.Path);
        Action apply = group switch
        {
            "battle" => () => { _ = PretBattleText.Load(TestAssets.Root, pack); },
            "story-menus" => () => { _ = pack.ApplyMenus(Story.Compilation); },
            _ => () => { _ = pack.Apply("story", Story.TextBank, preserveControls: true); }
        };
        var error = Assert.ThrowsExactly<InvalidDataException>(apply);
        StringAssert.Contains(error.Message, group + "/" + id);
    }
}
