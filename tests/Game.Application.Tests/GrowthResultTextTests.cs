using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Content;
using Game.Domain.Events;

namespace FireRed.Tests.Application;

internal static class GrowthResultTextFixture
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    // The same authored controls, placeholder order and page endings as generated English.
    private static readonly Dictionary<string, string> Translations = new(StringComparer.Ordinal)
    {
        ["gText_PkmnIsEvolving"] = "Now?\\n{STR_VAR_1} is changing!",
        ["gText_CongratsPkmnEvolved"] = "Success! Your {STR_VAR_1}\\nbecame {STR_VAR_2}!{WAIT_SE}\\p",
        ["gText_PkmnStoppedEvolving"] = "Wait! {STR_VAR_1}\\nstopped changing!\\p",
        ["sText_PkmnGrewToLv"] = "{B_BUFF1} advanced to\\nLV. {B_BUFF2}!{WAIT_SE}\\p",
        ["sText_PkmnLearnedMove"] = "{B_BUFF1} mastered\\n{B_BUFF2}!{WAIT_SE}\\p",
        ["sText_DidNotLearnMove"] = "{B_BUFF1} did not master\\n{B_BUFF2}.\\p"
    };

    internal static PretCampaignCatalog WithTranslations(PretCampaignCatalog original, bool translated)
    {
        if (!translated) return original;
        // Exercise the actual pack validator and importer without changing process-wide selection
        // or reloading a second complete campaign. All other fixture catalogs remain immutable.
        using var output = TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(output.Path, "source"));
        File.WriteAllText(Path.Combine(output.Path, "source", "battle.json"), JsonSerializer.Serialize(Translations));
        var texts = PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(output.Path));
        var copy = (PretCampaignCatalog)typeof(object).GetMethod("MemberwiseClone", Private)!.Invoke(original, null)!;
        typeof(PretCampaignCatalog).GetField("<BattleContent>k__BackingField", Private)!.SetValue(copy,
            original.BattleContent with
            {
                Texts = texts,
                // Display names are bound literal values, not another source template.
                MoveNames = original.BattleContent.MoveNames.ToImmutableSortedDictionary(
                    pair => pair.Key, pair => pair.Value + "{B_BUFF1}\\p{WAIT_SE}", StringComparer.Ordinal)
            });
        Assert.AreNotSame(original.BattleContent, copy.BattleContent);
        Assert.AreNotEqual(original.BattleContent.Texts["sText_PkmnLearnedMove"], copy.BattleContent.Texts["sText_PkmnLearnedMove"]);
        return copy;
    }

    internal static ImmutableArray<string> Pages(PretCampaignCatalog catalog, string symbol,
        params (string Key, string Value)[] values) => SourceBattleText.Pages(catalog.BattleContent.Texts,
            symbol, values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));

    internal static void AssertPages(PretCampaignCatalog catalog, IEnumerable<string> actual,
        string symbol, params (string Key, string Value)[] values) =>
        Assert.AreSequenceEqual(Pages(catalog, symbol, values), actual, symbol);
}

[TestClass, TestCategory("Game")]
public sealed class GrowthResultTextTests
{
    [TestMethod]
    public void GrowthSourceSymbolsPreserveEnglishPagesAndLiteralValues()
    {
        var catalog = CurrentCampaignFixture.Catalog;
        const string name = "Name{B_BUFF2}\\p{WAIT_SE}";
        const string value = "Value{STR_VAR_1}\\n";
        var samples = new[]
        {
            ("gText_PkmnIsEvolving", "STR_VAR_1", "STR_VAR_2", $"What?\n{name} is evolving!"),
            ("gText_CongratsPkmnEvolved", "STR_VAR_1", "STR_VAR_2", $"Congratulations! Your {name}\nevolved into {value}!"),
            ("gText_PkmnStoppedEvolving", "STR_VAR_1", "STR_VAR_2", $"Huh? {name}\nstopped evolving!"),
            ("sText_PkmnGrewToLv", "B_BUFF1", "B_BUFF2", $"{name} grew to\nLV. {value}!"),
            ("sText_PkmnLearnedMove", "B_BUFF1", "B_BUFF2", $"{name} learned\n{value}!"),
            ("sText_DidNotLearnMove", "B_BUFF1", "B_BUFF2", $"{name} did not learn\n{value}.")
        };
        foreach (var (symbol, first, second, expected) in samples)
            Assert.AreSequenceEqual(new[] { expected }, GrowthResultTextFixture.Pages(catalog, symbol,
                (first, name), (second, value)), symbol + " retains exactly one original page");
        var translated = GrowthResultTextFixture.WithTranslations(catalog, true);
        Assert.AreSequenceEqual(new[] { $"{name} mastered\n{value}!" },
            GrowthResultTextFixture.Pages(translated, "sText_PkmnLearnedMove", ("B_BUFF1", name), ("B_BUFF2", value)));
        Assert.AreEqual("{B_BUFF1} learned\\n{B_BUFF2}!{WAIT_SE}\\p", catalog.BattleContent.Texts["sText_PkmnLearnedMove"],
            "The shared default catalog was not mutated.");
    }
}
