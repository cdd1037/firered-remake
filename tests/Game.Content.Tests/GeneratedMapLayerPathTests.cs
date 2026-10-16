using System.Text.Json;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class GeneratedMapLayerPathTests
{
    [TestMethod]
    public void EveryPublishedMapLayerResolvesItsOwnAtlasKey()
    {
        var generated = Path.Combine(TestAssets.Root, "content/generated");
        using var atlas = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(generated, "maps/atlases/catalog.json")));
        var images = atlas.RootElement.GetProperty("images");
        var maps = 0;
        foreach (var path in Directory.EnumerateFiles(Path.Combine(generated, "maps/regions"), "*.json", SearchOption.AllDirectories))
        {
            var bytes = File.ReadAllBytes(path);
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object || !document.RootElement.TryGetProperty("blocks", out _)) continue;
            var data = PretRuntimeMapData.Decode(bytes, document.RootElement.GetProperty("stableId").GetString()!);
            var map = PretWorldMap.FromRuntime(data, path);
            void Check(string texture)
            {
                var key = Path.GetRelativePath(generated, texture).Replace(Path.DirectorySeparatorChar, '/');
                Assert.IsTrue(images.TryGetProperty(key, out _), $"Map {path} has missing layer {key}");
                Assert.IsTrue(map.Artifacts.Any(a => Path.Combine(map.DirectoryPath, a.Path) == texture), $"Layer is not declared by {path}: {texture}");
            }
            Check(map.TexturePath);
            Check(map.BottomTexturePath);
            Check(map.MiddleTexturePath);
            Check(map.TopTexturePath);
            if (map.Artifacts.Any(a => a.Path.EndsWith("border-bottom.png", StringComparison.Ordinal)))
            {
                Check(map.BorderBottomTexturePath);
                Check(map.BorderMiddleTexturePath);
                Check(map.BorderTopTexturePath);
            }
            maps++;
        }
        Assert.IsTrue(maps > 0, "No published maps were checked.");
    }
}
