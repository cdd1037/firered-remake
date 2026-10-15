using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class ImportedPresentationAtlasTests
{
    [TestMethod]
    public void FlyAndFameLoadOnlyTheirGeneratedPackages()
    {
        string root = Path.Combine(Path.GetTempPath(), "firered-atlas-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var package in new[] { "ui/fly-map", "ui/fame-checker-presentation" })
            {
                string relative = "content/generated/" + package;
                Directory.CreateDirectory(Path.Combine(root, relative));
                foreach (var source in Directory.EnumerateFiles(Path.Combine(TestAssets.Root, relative)))
                    File.Copy(source, Path.Combine(root, relative, Path.GetFileName(source)));
            }
            var fly = PretFlyMap.Load(root);
            Assert.AreEqual(12, fly.Backgrounds.Length);
            foreach (var row in fly.Backgrounds)
            {
                Assert.AreEqual((240, 160), (row.Region.Width, row.Region.Height));
                Assert.IsTrue(row.Region.X >= 1 && row.Region.Y >= 1 && row.Region.X + 240 < 484 && row.Region.Y + 160 < 972);
            }
            Assert.IsTrue(fly.Background(2, new HashSet<string>()).Hidden);
            Assert.IsFalse(fly.Background(2, new HashSet<string> { "FLAG_WORLD_MAP_NAVEL_ROCK_EXTERIOR" }).Hidden);
            Assert.IsTrue(fly.Background(3, new HashSet<string> { "FLAG_WORLD_MAP_NAVEL_ROCK_EXTERIOR" }).Hidden);
            Assert.IsFalse(fly.Background(3, new HashSet<string> { "FLAG_WORLD_MAP_BIRTH_ISLAND_EXTERIOR" }).Hidden);

            var fame = PretFameCheckerPresentation.Load(root);
            Assert.AreEqual(3, fame.Textures.Values.Select(t => t.TexturePath).Distinct().Count());
            Assert.AreEqual("base-restored", fame.BaseBackground(false, true).Id);
            Assert.AreEqual("base-pick", fame.BaseBackground(true).Id);
            for (int state = 0; state < 6; state++)
                Assert.AreEqual((512, 160), (fame.Overlay(state).Width, fame.Overlay(state).Height));
            foreach (var icon in fame.ObjectIcons.Keys)
            {
                var normal = fame.FactIcon(icon);
                var dimmed = fame.FactIcon(icon, true);
                Assert.AreEqual((normal.Width, normal.Height), (dimmed.Width, dimmed.Height));
                Assert.AreNotEqual(normal.Region, dimmed.Region);
            }
            foreach (var image in fame.Textures.Values)
            {
                Assert.IsTrue(File.Exists(Path.Combine(root, image.TexturePath)));
                Assert.AreEqual((image.Width, image.Height), (image.Region.Width, image.Region.Height));
            }
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
