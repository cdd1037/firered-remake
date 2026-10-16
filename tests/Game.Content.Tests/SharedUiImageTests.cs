using System.Text.Json;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SharedUiImageTests
{
    [TestMethod]
    public void OptionFramesKeepAllLogicalVariantsWithSharedDefaultTextures()
    {
        var frames = PretOptionFrames.Load(TestAssets.Root);
        Assert.AreEqual(40, frames.Assets.Length);
        Assert.AreEqual(5, frames.Assets.Select(a => a.TexturePath).Distinct().Count());
        Assert.AreEqual(1, Directory.EnumerateFiles(frames.GeneratedDirectory, "*.png", SearchOption.AllDirectories).Count());
        foreach (var asset in frames.Assets)
        {
            Assert.IsTrue(asset.Width > 0 && asset.Height > 0);
            Assert.IsTrue(File.Exists(Path.Combine(TestAssets.Root, asset.TexturePath)));
            Assert.AreEqual((0, asset.Kind == "frame" ? 0 : 112), (asset.X, asset.Y));
            if (asset.FrameIndex == 0 || asset.Kind == "battle-message")
            {
                var name = asset.Kind == "frame" ? "frame1" : asset.Kind;
                Assert.AreEqual($"content/generated/ui/lab-battle-ui/{name}.png", asset.TexturePath);
                Assert.IsNull(asset.Region);
                Assert.IsFalse(File.Exists(Path.Combine(frames.GeneratedDirectory, $"type{asset.FrameIndex + 1}/{asset.Kind}.png")));
            }
            else
            {
                Assert.AreEqual("content/generated/ui/option-frames/frames.png", asset.TexturePath);
                Assert.IsNotNull(asset.Region);
                Assert.AreEqual((asset.Width, asset.Height), (asset.Region.Width, asset.Region.Height));
            }
        }
    }

    [TestMethod]
    public void BattleStatusContextsSharePixelsAndKeepTheirPlacement()
    {
        var document = PretForestStatusUi.Load(TestAssets.Root);
        Assert.AreEqual(11, document.Assets.Length);
        Assert.AreEqual(8, document.Assets.Select(a => a.TexturePath).Distinct().Count());
        foreach (var status in new[] { "poison", "paralysis", "burn" })
        {
            var player = document.Assets.Single(a => a.Id == "battle-player-" + status);
            var opponent = document.Assets.Single(a => a.Id == "battle-opponent-" + status);
            Assert.AreEqual("battle-" + status + ".png", player.TexturePath);
            Assert.AreEqual(player.TexturePath, opponent.TexturePath);
            Assert.AreEqual((142, 96), (player.X, player.Y));
            Assert.AreEqual((20, 30), (opponent.X, opponent.Y));
        }
    }

    [TestMethod]
    public void DoubleBattleUsesTheSameHealthBarPixels()
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root, "content/generated/ui/double-battle-ui/double-battle-ui.json")));
        var shared = 0;
        foreach (var asset in document.RootElement.GetProperty("assets").EnumerateArray())
        {
            var id = asset.GetProperty("id").GetString()!;
            var path = asset.GetProperty("path").GetString()!;
            var width = asset.GetProperty("width").GetInt32();
            var height = asset.GetProperty("height").GetInt32();
            Assert.IsTrue(width > 0 && height > 0);
            Assert.IsTrue(path.EndsWith(".png", StringComparison.Ordinal));
            Assert.IsTrue(File.Exists(Path.Combine(TestAssets.Root, path)));
            if (id is "hp-label" or "hp-green" or "hp-yellow" or "hp-red")
            {
                shared++;
                Assert.AreEqual($"content/generated/ui/lab-battle-ui/{id}.png", path);
                Assert.IsFalse(File.Exists(Path.Combine(TestAssets.Root, $"content/generated/ui/double-battle-ui/{id}.png")));
            }
        }
        Assert.AreEqual(4, shared);
    }
}
