using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedPokedexCatalogTests
{
    [TestMethod]
    public void CatalogNeedsNoSourceProofManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "firered-dex-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(root, "content/generated/sprites/pokemon/pokedex-menu");
        Directory.CreateDirectory(output);
        try
        {
            File.Copy(Path.Combine(TestAssets.Root, "content/generated/sprites/pokemon/pokedex-menu/pokedex-menu.json"), Path.Combine(output, "pokedex-menu.json"));
            var menu = PretPokedexMenu.Load(root);
            Assert.AreEqual(386, menu.Entries.Count);
            Assert.AreEqual("BULBASAUR", menu.Entries[1].Name);
            Assert.IsFalse(menu.CanOpen(new(false), new[] { 1 }));
            Assert.IsTrue(menu.CanOpen(new(true), new[] { 1 }));
            var page = menu.BuildPage(1, new[] { 1 }, Array.Empty<int>(), new(true));
            Assert.IsFalse(page.Caught);
        }
        finally { Directory.Delete(root, true); }
    }
}
