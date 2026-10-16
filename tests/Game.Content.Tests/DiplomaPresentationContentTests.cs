using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class DiplomaPresentationContentTests
{
    [TestMethod]
    public void OriginalTextUsesSavedNameAndOneLeftAlignedCenteredBlock()
    {
        var content = PretDiplomaPresentation.Load(TestAssets.Root);
        var lines = content.TextLines("LEAF", true, text => text.Length * 6);
        Assert.AreEqual("PLAYER: LEAF", lines[0].Text);
        Assert.AreEqual(120 - "PLAYER: LEAF".Length * 3, lines[0].X);
        Assert.AreEqual(20, lines[0].Y);
        CollectionAssert.AreEqual(new[] { 46, 60, 74, 88 }, lines.Skip(1).Take(4).Select(l => l.Y).ToArray());
        Assert.AreEqual(1, lines.Skip(1).Take(4).Select(l => l.X).Distinct().Count());
        Assert.AreEqual(120 - lines.Skip(1).Take(4).Max(l => l.Text.Length) * 3, lines[1].X);
        Assert.AreEqual("the NATIONAL POKéDEX.", lines[4].Text);
        Assert.AreEqual(new DiplomaTextLine("GAME FREAK", 120, 121), lines[5]);
        Assert.AreEqual("the KANTO POKéDEX.", content.TextLines("RED", false, t => t.Length)[4].Text);
        Assert.AreEqual("MUS_OBTAIN_BADGE", content.Document.Fanfare);
        Assert.AreEqual(340, content.Document.FanfareCounterFrames);
        Assert.IsFalse(content.ReadTexture(false).SequenceEqual(content.ReadTexture(true)));
    }

    [TestMethod]
    public void GeneratedOnlyLoadingRejectsMissingInvalidAndUnsafeArtifacts()
    {
        string root = Path.Combine(Path.GetTempPath(), "firered-diploma-content-" + Guid.NewGuid().ToString("N"));
        string directory = Path.Combine(root, PretDiplomaPresentation.Base);
        try {
            Directory.CreateDirectory(directory);
            foreach (string path in Directory.GetFiles(Path.Combine(TestAssets.Root, PretDiplomaPresentation.Base)))
                File.Copy(path, Path.Combine(directory, Path.GetFileName(path)));
            Assert.IsNotNull(PretDiplomaPresentation.Load(root));
            string documentPath = Path.Combine(directory, "diploma.json");
            string original = File.ReadAllText(documentPath);
            void Reject(Action<JsonNode> change) {
                var node = JsonNode.Parse(original)!; change(node); File.WriteAllText(documentPath, node.ToJsonString());
                Assert.ThrowsExactly<InvalidDataException>(() => PretDiplomaPresentation.Load(root));
                File.WriteAllText(documentPath, original);
            }
            Reject(n => n["assets"]![0]!["texturePath"] = "../kanto.png");
            Reject(n => n["assets"]![0]!["width"] = 256);
            Reject(n => n["bodyTemplate"] = "unfinished");
            Reject(n => n["playerTemplate"] = "PLAYER: {UNRECOGNIZED}");
            Reject(n => n["fanfareCounterFrames"] = 0);
            File.WriteAllBytes(Path.Combine(directory, "kanto.png"), [1, 2, 3]);
            Assert.ThrowsExactly<InvalidDataException>(() => PretDiplomaPresentation.Load(root));
            File.Delete(Path.Combine(directory, "kanto.png"));
            Assert.ThrowsExactly<FileNotFoundException>(() => PretDiplomaPresentation.Load(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
