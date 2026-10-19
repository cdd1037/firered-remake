using Game.Content;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class SharedOverworldTextureTests
{
    private static string Generated => Path.Combine(Path.GetTempPath(), "overworld-path-tests", "content", "generated");
    private static string Package => Path.Combine(Generated, "maps", "regions", "pewter");

    [TestMethod]
    public void Shared_sheet_resolves_to_one_generated_file()
    {
        string expected = Path.Combine(Generated, "sprites/overworld", "red-normal.png");
        Assert.AreEqual(expected, PretOverworldTextures.Resolve(Package, PretOverworldTextures.RelativePrefix + "red-normal.png"));
        Assert.AreEqual(expected, PretOverworldTextures.Resolve(
            Path.Combine(Generated, "maps", "regions", "silph"), PretOverworldTextures.RelativePrefix + "red-normal.png"));
    }

    [TestMethod]
    public void Shared_sheet_rejects_other_paths_and_traversal()
    {
        foreach (string path in new[] { "npc/red-normal.png", "../other/red-normal.png", "../sprites/overworld/red-normal.png",
            "sprites/overworld/../red-normal.png", "sprites/overworld/sub/red-normal.png", "sprites/overworld/sub\\red-normal.png",
            "sprites/overworld/red-normal.png:stream", "sprites/overworld/.png", "sprites/overworld/red-normal.PNG",
            "/sprites/overworld/red-normal.png", "sprites/overworld/red.normal.png", "../shared-overworld/red-normal.png" })
            Assert.Throws<InvalidDataException>(() => PretOverworldTextures.Resolve(Package, path), path);
    }

    [TestMethod]
    public void Shared_sheet_requires_a_content_generated_root()
    {
        foreach (string directory in new[] { Path.GetTempPath(), Path.GetDirectoryName(Generated)!,
            Path.Combine(Path.GetTempPath(), "overworld-path-tests", "generated", "maps") })
            Assert.Throws<InvalidDataException>(() => PretOverworldTextures.Resolve(directory, PretOverworldTextures.RelativePrefix + "red-normal.png"));
    }
    [TestMethod]
    public void Shadow_size_is_required_and_rejects_unsupported_values()
    {
        using var output = TestAssets.TemporaryOutput();
        string directory = Path.Combine(output.Path, "sprites/overworld");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "catalog.json");
        const string valid = """
            {"actor":{"texturePath":"actors.png","region":{"x":0,"y":0,"width":16,"height":32},"frameWidth":16,"frameHeight":32,"frameCount":1,"anchorX":8,"anchorY":24,"inanimate":false,"shadowSize":1}}
            """;
        File.WriteAllText(path, valid);
        Assert.AreEqual(1, PretOverworldTextures.Load(output.Path)["actor"].ShadowSize);
        File.WriteAllText(path, valid.Replace(",\"shadowSize\":1", "", StringComparison.Ordinal));
        Assert.Throws<ContentValidationException>(() => PretOverworldTextures.Load(output.Path));
        foreach (int invalid in new[] { -1, 4 })
        {
            File.WriteAllText(path, valid.Replace("\"shadowSize\":1", $"\"shadowSize\":{invalid}", StringComparison.Ordinal));
            Assert.Throws<InvalidDataException>(() => PretOverworldTextures.Load(output.Path));
        }
    }

    [TestMethod]
    public void Canonical_catalog_preserves_animation_geometry_and_current_world_graphics()
    {
        string generated = Path.Combine(TestAssets.Root, "content/generated");
        var catalog = PretOverworldTextures.Load(generated);
        Assert.AreEqual(7, catalog.Values.Select(s => s.TexturePath).Distinct().Count());
        foreach (var sprite in catalog.Values)
        {
            Assert.AreEqual(sprite.FrameWidth * sprite.FrameCount, sprite.Region.Width);
            Assert.AreEqual(sprite.FrameHeight, sprite.Region.Height);
            Assert.AreEqual(sprite.FrameWidth / 2, sprite.AnchorX);
            Assert.AreEqual(sprite.FrameHeight - 8, sprite.AnchorY);
            Assert.IsTrue(sprite.ShadowSize is >= 0 and <= 3);
            Assert.IsTrue(File.Exists(Path.Combine(generated, "sprites/overworld", sprite.TexturePath)));
        }
        Assert.AreEqual(1, catalog["OBJ_EVENT_GFX_RED_NORMAL"].ShadowSize);
        Assert.AreEqual(0, catalog["OBJ_EVENT_GFX_PUSHABLE_BOULDER"].ShadowSize);
        var world = PretCurrentWorld.Load(TestAssets.Root);
        int checkedGraphics = 0;
        foreach (var map in world.RuntimeMaps.MapsByPath.Values)
        {
            foreach (var actor in map.Events.GetProperty("object_events").EnumerateArray())
            {
                string graphicsId = actor.GetProperty("graphics_id").GetString()!;
                // Clone actors and variable graphics are resolved by gameplay at runtime.
                if (graphicsId == "0" || graphicsId.StartsWith("OBJ_EVENT_GFX_VAR_", StringComparison.Ordinal))
                    continue;
                Assert.IsTrue(catalog.ContainsKey(graphicsId), $"{map.Id}: {graphicsId}");
                checkedGraphics++;
            }
        }
        Assert.IsTrue(checkedGraphics > 0);

    }

}
