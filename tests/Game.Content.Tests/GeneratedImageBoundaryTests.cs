using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedImageBoundaryTests
{
    [TestMethod]
    public void PlayerAndCycleArtLoadFromGeneratedFilesWithoutRawInputsOrLocks()
    {
        var root = Path.Combine(Path.GetTempPath(), "firered-images-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var package in new[] { "ui/player-gender-art", "ui/city-cycle-art" })
            {
                var relative = "content/generated/" + package;
                var destination = Path.Combine(root, relative);
                Directory.CreateDirectory(destination);
                foreach (var source in Directory.EnumerateFiles(Path.Combine(TestAssets.Root, relative)))
                    File.Copy(source, Path.Combine(destination, Path.GetFileName(source)));
            }
            var player = PretPlayerGenderArt.Load(root);
            var cycle = PretCityCycleArt.Load(root);
            foreach (var gender in new[] { 0, 1 })
            {
                var sprite = player.Sprite(gender);
                Assert.AreEqual((144, 32, 9), (sprite.Width, sprite.Height, sprite.FrameCount));
                CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(player.GeneratedDirectory, sprite.TexturePath)), player.ReadTexture(gender));
                var bike = cycle.Sprite(gender);
                Assert.AreEqual((288, 32, 9), (bike.Width, bike.Height, bike.FrameCount));
                CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(cycle.GeneratedDirectory, bike.TexturePath)), cycle.ReadTexture(gender));
                var fishing = cycle.FishingSprite(gender);
                Assert.AreEqual((384, 32, 12), (fishing.Width, fishing.Height, fishing.FrameCount));
                CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(cycle.GeneratedDirectory, fishing.TexturePath)), cycle.ReadFishingTexture(gender));
            }
            var original = player.ReadTexture(0);
            var changed = player.ReadTexture(0);
            changed[0] ^= 0xff;
            CollectionAssert.AreEqual(original, player.ReadTexture(0));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    [TestMethod]
    public void Route11ArtReturnsEncodedPngPayloads()
    {
        var art = PretRoute11Art.Load(TestAssets.Root);
        Assert.AreEqual(1, art.Portraits.Length);
        Assert.AreEqual(3, art.StatusAssets.Length);
        foreach (var payload in art.Portraits.Select(p => p.Png).Concat(art.StatusAssets.Select(p => p.Png)))
            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, payload.Take(8).ToArray());
    }

}
