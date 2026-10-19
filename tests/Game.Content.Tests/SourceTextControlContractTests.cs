using System.Text.Json;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SourceTextControlContractTests
{
    private static string Apply(string original, string replacement)
    {
        string directory = Path.Combine(Path.GetTempPath(), "firered-source-text-controls-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(directory, "source"));
            File.WriteAllText(Path.Combine(directory, "source", "story.json"),
                JsonSerializer.Serialize(new Dictionary<string, string> { ["fixture"] = replacement }));
            return SourceTextPack.Load(directory).Apply("story",
                new Dictionary<string, string> { ["fixture"] = original }, preserveControls: true)["fixture"];
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [TestMethod]
    [DataRow("Hello {PLAYER}\\nNext$", "你好 {PLAYER}\\n下一页$")]
    [DataRow("Hello\nNext$", "你好\n下一页$")]
    [DataRow("Hello{PLAY_BGM}{MUS_LEVEL_UP}$", "你好{PLAY_BGM}{MUS_LEVEL_UP}$")]
    [DataRow("ITEM{CLEAR_TO 0x65}{FONT_SMALL}1,000", "物品{CLEAR_TO 0x65}{FONT_SMALL}1,000")]
    public void TranslatedProseRetainsSourceControlStructure(string original, string replacement) =>
        Assert.AreEqual(replacement, Apply(original, replacement));

    [TestMethod]
    [DataRow("{PLAYER} and {RIVAL}$", "{RIVAL} 与 {PLAYER}$")]
    [DataRow("First\\pSecond\\nThird$", "一\\n二\\p三$")]
    [DataRow("First\\lSecond$", "一\\n二$")]
    [DataRow("First\nSecond$", "一\\n二$")]
    [DataRow("Hello{PLAY_BGM}{MUS_LEVEL_UP}$", "你好{PLAY_BGM}{MUS_LEVEL_UP}再见$")]
    [DataRow("Hello{PLAY_BGM}{MUS_LEVEL_UP}$", "你好{PLAY_BGM} {MUS_LEVEL_UP}$")]
    [DataRow("Hello{PAUSE 60}$", "你好{PAUSE 61}$")]
    [DataRow("Hello$", "你好$再见")]
    [DataRow("Hello$", "你好$$")]
    [DataRow("Hello$", "你好\\q$")]
    [DataRow("Hello$", "你好{")]
    [DataRow("Hello$", "你好}$")]
    [DataRow("Hello$", "你好\r$")]
    public void ChangedControlsAndAnchorsAreRejected(string original, string replacement) =>
        Assert.ThrowsExactly<InvalidDataException>(() => Apply(original, replacement));
}
