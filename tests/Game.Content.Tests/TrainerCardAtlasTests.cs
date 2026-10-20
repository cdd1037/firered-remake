using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class TrainerCardAtlasTests
{
    private const string Package = "content/generated/ui/trainer-card";
    private const string Document = Package + "/trainer-card.json";

    [TestMethod]
    public void AllLogicalAssetsUseBoundedSharedPagesWithoutPaletteOrPlacementChanges()
    {
        var card = PretTrainerCard.Load(TestAssets.Root);
        Assert.AreEqual(59, card.Assets.Length);
        Assert.AreEqual(2, card.Pages.Length);
        Assert.AreEqual(46, card.Assets.Select(a => (a.TexturePath, a.Region)).Distinct().Count());
        Assert.IsTrue(card.Pages.All(p => p.Width <= 1024 && p.Height <= 1024));
        Assert.AreEqual(card.Asset("panel-front-male-2").Region, card.Asset("panel-front-female-2").Region);
        Assert.AreEqual(card.Asset("panel-front-male-2").TexturePath, card.Asset("panel-front-female-2").TexturePath);
        Assert.AreNotEqual(card.Asset("front-male-1").Region, card.Asset("front-female-1").Region);
        Assert.AreEqual((165, 44, 59, 64), (card.Asset("portrait-red").X, card.Asset("portrait-red").Y,
            card.Asset("portrait-red").Width, card.Asset("portrait-red").Height));
        var page = card.Pages[0].TexturePath;
        var expected = card.ReadTexturePage(page);
        var mutated = card.ReadTexturePage(page);
        mutated[0] ^= 0xff;
        CollectionAssert.AreEqual(expected, card.ReadTexturePage(page));
        Assert.ThrowsExactly<ArgumentException>(() => card.ReadTexturePage("badge-5.png"));
    }

    [TestMethod]
    [DataRow("missing-asset")]
    [DataRow("duplicate-asset")]
    [DataRow("missing-page")]
    [DataRow("duplicate-page")]
    [DataRow("out-of-page")]
    [DataRow("negative-region")]
    [DataRow("region-size")]
    [DataRow("missing-region")]
    [DataRow("oversized-page")]
    [DataRow("unknown-texture")]
    public void InvalidAtlasBindingsAreRejectedAtLoad(string corruption)
    {
        using var temporary = TestAssets.TemporaryOutput();
        TestAssets.CopyFiles(temporary.Path,
            Directory.EnumerateFiles(Path.Combine(TestAssets.Root, Package))
                .Select(path => Package + "/" + Path.GetFileName(path)));
        TestAssets.EditJson(temporary.Path, Document, document =>
        {
            var assets = document["assets"]!.AsArray();
            var pages = document["pages"]!.AsArray();
            var asset = assets[0]!;
            switch (corruption)
            {
                case "missing-asset": assets.RemoveAt(0); break;
                case "duplicate-asset": assets[1] = asset.DeepClone(); break;
                case "missing-page": pages.RemoveAt(0); break;
                case "duplicate-page": pages.Add(pages[0]!.DeepClone()); break;
                case "out-of-page": asset["region"]!["x"] = int.MaxValue; break;
                case "negative-region": asset["region"]!["y"] = -1; break;
                case "region-size": asset["region"]!["width"] = 1; break;
                case "missing-region": asset.AsObject().Remove("region"); break;
                case "oversized-page": pages[0]!["width"] = 1025; break;
                case "unknown-texture": asset["texturePath"] = "unknown.png"; break;
            }
        });
        void Load() => PretTrainerCard.Load(temporary.Path, PretBattleText.Load(TestAssets.Root));
        if (corruption == "missing-region")
        {
            var error = Assert.ThrowsExactly<ContentValidationException>(Load);
            Assert.AreEqual("json_schema", error.Diagnostics.Single().Code);
            Assert.AreEqual("$[0]", error.Diagnostics.Single().Path);
        }
        else
            Assert.ThrowsExactly<InvalidDataException>(Load);
    }
}
