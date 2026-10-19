using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class LeagueTextProjectionTests
{
    private static IReadOnlyDictionary<string, string> English => TestAssets.Cached(() =>
        PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(null)));
    private static PokemonLeaguePresentationData Original => TestAssets.Cached(() =>
        PokemonLeaguePresentationAssets.Load(TestAssets.Root, English));

    [TestMethod]
    [DataRow("gText_LeagueChamp", "LEAGUE", "HALL")]
    [DataRow("gString_PokemonFireRed_Staff", "Staff", "Team")]
    [DataRow("gCreditsString_Director", "Director", "Lead")]
    [DataRow("gCreditsString_Junichi_Masuda", "Junichi", "J.")]
    public void SparseSourceChangesReachCreditsAndHallWithoutChangingSequenceOrArt(string symbol, string before, string after)
    {
        using var output = TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(output.Path, "source"));
        string replacement = English[symbol].Replace(before, after, StringComparison.Ordinal);
        Assert.AreNotEqual(English[symbol], replacement);
        File.WriteAllText(Path.Combine(output.Path, "source/battle.json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [symbol] = replacement }));
        var source = PretBattleText.Load(TestAssets.Root, SourceTextPack.Load(output.Path));
        var actual = PokemonLeaguePresentationAssets.Load(TestAssets.Root, source);
        var expected = JsonNode.Parse(Original.Manifest.GetRawText())!;
        string display = replacement.Replace("\\n", "\n", StringComparison.Ordinal);
        int changed = 0;
        if (expected["texts"]!.AsObject().ContainsKey(symbol)) { expected["texts"]![symbol] = display; changed++; }
        var credits = expected["credits"]!;
        if (credits["titleSymbol"]!.GetValue<string>() == symbol) { credits["title"] = display; changed++; }
        foreach (var row in credits["texts"]!.AsObject())
        {
            var block = row.Value!;
            foreach (string field in new[] { "title", "names" })
                if (block[field + "Symbol"]?.GetValue<string>() == symbol) { block[field] = display; changed++; }
        }
        Assert.IsTrue(changed > 0, "The source override must reach a real projected field.");
        Assert.IsTrue(JsonNode.DeepEquals(expected, JsonNode.Parse(actual.Manifest.GetRawText())),
            "Commands, times, map references, source symbol bindings and unrelated display text stay unchanged.");
        Assert.AreEqual(Original.Files.Count, actual.Files.Count);
        foreach (var (path, bytes) in Original.Files) CollectionAssert.AreEqual(bytes, actual.Files[path], path);
    }
}
