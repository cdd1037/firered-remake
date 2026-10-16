using System.Text.Json;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SharedTrainerTowerArtTests
{
    [TestMethod]
    public void TowerSharesEarlierArtworkWithoutDroppingLogicalAssets()
    {
        const string prefix = "content/generated/sprites/pokemon/trainer-tower/";
        var catalog = PretTrainerTowerPresentation.Load(TestAssets.Root);
        var shared = 0;
        foreach (var asset in catalog.GetProperty("assets").EnumerateArray())
        {
            var path = asset.GetProperty("texturePath").GetString()!;
            var width = asset.GetProperty("width").GetInt32();
            var height = asset.GetProperty("height").GetInt32();
            Assert.IsTrue(width > 0 && height > 0);
            Assert.IsTrue(path.EndsWith(".png", StringComparison.Ordinal));
            Assert.IsTrue(File.Exists(Path.Combine(TestAssets.Root, path)));
            if (!path.StartsWith(prefix, StringComparison.Ordinal))
            {
                shared++;
                Assert.IsFalse(File.Exists(Path.Combine(TestAssets.Root, prefix + asset.GetProperty("id").GetString() + ".png")));
            }
        }
        Assert.AreEqual(42, shared);
        Assert.AreEqual(80, catalog.GetProperty("sprites").GetArrayLength());
        Assert.AreEqual(37, catalog.GetProperty("trainerPortraits").GetArrayLength());
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root, prefix + "manifest.json")));
        Assert.AreEqual(91, manifest.RootElement.GetProperty("outputs").GetArrayLength());
        Assert.AreEqual(91, manifest.RootElement.GetProperty("outputCount").GetInt32());
    }

    [TestMethod]
    public void SharedMeowthAndSeakingKeepBothSidesAndPalettes()
    {
        var catalog = PretTrainerTowerPresentation.Load(TestAssets.Root);
        foreach (var (name, package) in new[] { ("meowth", "vermilion"), ("seaking", "rocktunnel") })
        foreach (var side in new[] { "front", "back" })
        foreach (var palette in new[] { "normal", "shiny" })
        {
            var id = $"{name}-{side}-{palette}";
            var asset = catalog.GetProperty("sprites").EnumerateArray().Single(a => a.GetProperty("id").GetString() == id);
            Assert.AreEqual($"content/generated/sprites/pokemon/{package}/{id}.png", asset.GetProperty("texturePath").GetString());
            Assert.AreEqual(side, asset.GetProperty("side").GetString());
            Assert.AreEqual(palette, asset.GetProperty("paletteVariant").GetString());
            Assert.AreEqual(side == "front" ? 144 : 40, asset.GetProperty("x").GetInt32());
        }
    }
}
