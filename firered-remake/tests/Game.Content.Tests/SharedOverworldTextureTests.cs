using Game.Content;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SharedOverworldTextureTests
{
    private static string Package => Path.Combine(Path.GetTempPath(), "overworld-path-tests", "content", "generated", "pewter-region");

    [TestMethod]
    public void Shared_sheet_resolves_to_one_generated_file()
    {
        string expected = Path.Combine(Path.GetDirectoryName(Package)!, "shared-overworld", "red-normal.png");
        Assert.AreEqual(expected, PretOverworldTextures.Resolve(Package, "../shared-overworld/red-normal.png"));
        Assert.AreEqual(expected, PretOverworldTextures.Resolve(
            Path.Combine(Path.GetDirectoryName(Package)!, "silph-region"), "../shared-overworld/red-normal.png"));
    }

    [TestMethod]
    public void Shared_sheet_rejects_other_paths_and_traversal()
    {
        foreach (string path in new[] { "npc/red-normal.png", "../other/red-normal.png", "../../shared-overworld/red-normal.png",
            "../shared-overworld/../red-normal.png", "../shared-overworld/sub/red-normal.png", "../shared-overworld/sub\\red-normal.png",
            "../shared-overworld/red-normal.png:stream", "../shared-overworld/.png", "../shared-overworld/red-normal.PNG",
            "/shared-overworld/red-normal.png", "../shared-overworld/red.normal.png" })
            Assert.Throws<InvalidDataException>(() => PretOverworldTextures.Resolve(Package, path), path);
    }

    [TestMethod]
    public void Shared_sheet_requires_a_direct_generated_package()
    {
        foreach (string directory in new[] { Path.GetTempPath(), Path.Combine(Package, "maps"), Path.GetDirectoryName(Package)! })
            Assert.Throws<InvalidDataException>(() => PretOverworldTextures.Resolve(directory, "../shared-overworld/red-normal.png"));
    }
    [TestMethod]
    public void Canonical_catalog_preserves_animation_geometry_and_region_ids()
    {
        string generated = Path.Combine(TestAssets.Root, "content/generated");
        var catalog = PretOverworldTextures.Load(generated);
        Assert.AreEqual(119, catalog.Count);
        Assert.AreEqual(7, catalog.Values.Select(s => s.TexturePath).Distinct().Count());
        foreach (var sprite in catalog.Values)
        {
            Assert.AreEqual(sprite.FrameWidth * sprite.FrameCount, sprite.Region.Width);
            Assert.AreEqual(sprite.FrameHeight, sprite.Region.Height);
            Assert.AreEqual(sprite.FrameWidth / 2, sprite.AnchorX);
            Assert.AreEqual(sprite.FrameHeight - 8, sprite.AnchorY);
            Assert.IsTrue(File.Exists(Path.Combine(generated, "shared-overworld", sprite.TexturePath)));
        }
        foreach (var path in Directory.EnumerateFiles(generated, "npc-source.json", SearchOption.AllDirectories))
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(path));
            foreach (var graphic in doc.RootElement.GetProperty("graphics").EnumerateArray())
            {
                Assert.AreEqual(1, graphic.EnumerateObject().Count());
                Assert.IsTrue(catalog.ContainsKey(graphic.GetProperty("graphicsId").GetString()!));
            }
        }
    }

}
