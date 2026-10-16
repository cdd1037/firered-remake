using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass, TestCategory("Game")]
public sealed class MuseumPicturesTests
{
    [TestMethod]
    public void GeneratedOnlyLoaderResolvesNamedFossilsAndRejectsUnknownPictures()
    {
        var directory = Path.Combine(Path.GetTempPath(), "firered-museum-generated-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(directory, PretMuseumPictures.Base);
        Directory.CreateDirectory(output);
        try {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(TestAssets.Root, PretMuseumPictures.Base)))
                File.Copy(file, Path.Combine(output, Path.GetFileName(file)));
            Assert.IsFalse(Directory.Exists(Path.Combine(directory, "content/raw")));
            var pictures = PretMuseumPictures.Load(directory);
            Assert.AreEqual(142, pictures.Asset("aerodactyl-fossil").SourceSpeciesId);
            Assert.AreEqual(141, pictures.Asset("kabutops-fossil").SourceSpeciesId);
            foreach (var asset in pictures.Assets.Values) {
                Assert.AreEqual(64, asset.Width);
                Assert.AreEqual(64, asset.Height);
                Assert.IsTrue(pictures.ReadTexture(asset.Id).Length > 0);
            }
            Assert.ThrowsExactly<InvalidDataException>(() => pictures.Asset("ordinary-aerodactyl"));
            Assert.ThrowsExactly<InvalidDataException>(() => pictures.ReadTexture("ordinary-aerodactyl"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [TestMethod]
    [DataRow("empty")][DataRow("duplicate")][DataRow("dimensions")][DataRow("path")][DataRow("species")]
    public void MalformedGeneratedPictureCatalogIsRejected(string mutation)
    {
        var directory = Path.Combine(Path.GetTempPath(), "firered-museum-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(directory, PretMuseumPictures.Base);
        Directory.CreateDirectory(output);
        try {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(TestAssets.Root, PretMuseumPictures.Base)))
                File.Copy(file, Path.Combine(output, Path.GetFileName(file)));
            var path = Path.Combine(output, "pictures.json");
            var document = JsonNode.Parse(File.ReadAllText(path))!;
            var assets = document["assets"]!.AsArray();
            switch (mutation) {
                case "empty": assets.Clear(); break;
                case "duplicate": assets.Add(assets[0]!.DeepClone()); break;
                case "dimensions": assets[0]!["width"] = 32; break;
                case "path": assets[0]!["texturePath"] = "../outside.png"; break;
                case "species": assets[0]!["sourceSpeciesId"] = 0; break;
            }
            File.WriteAllText(path, document.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => PretMuseumPictures.Load(directory));
        }
        finally { Directory.Delete(directory, true); }
    }
}
