using System.Text.Json;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class RuntimeBufferSourceTextTests
{
    [TestMethod]
    [DataRow("gText_Coins_2", "COINS$", "代币$")]
    [DataRow("gText_PokemonCenter", "POKéMON CENTER$", "宝可梦中心$")]
    public void ApplicationBufferLabelsUseRegeneratedStorySymbolsAndSparseOverrides(string symbol, string english, string translated)
    {
        var story = PretStoryRuntime.Load(TestAssets.Root);
        Assert.AreEqual(english, story.TextBank[symbol]);
        using var pack = TestAssets.TemporaryOutput();
        Directory.CreateDirectory(Path.Combine(pack.Path, "source"));
        File.WriteAllText(Path.Combine(pack.Path, "source/story.json"),
            JsonSerializer.Serialize(new Dictionary<string, string> { [symbol] = translated }));
        var changed = SourceTextPack.Load(pack.Path).Apply("story", story.TextBank, preserveControls: true);
        Assert.AreEqual(translated, changed[symbol]);
        Assert.AreEqual(story.TextBank["gText_Yes"], changed["gText_Yes"]);
        Assert.AreEqual(story.TextBank.Count, changed.Count);
    }
}
