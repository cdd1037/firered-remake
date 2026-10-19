using System.Text.Json.Nodes;
using Game.Content;
using System.Text;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedCaveTransitionArtTests
{
    private static string Generated => Path.Combine(TestAssets.Root, "content/generated");

    [TestMethod]
    public void GeneratedOnlyIndicesAndPalettesRemainImmutableAfterInputDeletion()
    {
        string root = Prepare();
        try
        {
            var art = GeneratedCaveTransitionArt.Load(root);
            File.Delete(Path.Combine(root, "maps/cave-transition.json"));
            Assert.AreEqual(256, art.Width);
            Assert.AreEqual(256, art.Height);
            Assert.AreEqual(65536, art.Indices.Length);
            Assert.IsTrue(art.Indices.All(index => index <= 15));
            Assert.AreEqual(16, art.Indices.Distinct().Count());
            CollectionAssert.AreEqual(new ushort[] { 22290, 2114, 4228, 6342, 8456, 10570, 12684, 14798,
                16912, 19026, 21140, 23254, 25368, 27482, 29596, 32767 }, art.TransitionPalette.ToArray());
            Assert.IsTrue(art.WhitePalette.All(color => color == 32767));
            Assert.IsTrue(art.BlackPalette.All(color => color == 0));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow("schema")]
    [DataRow("width")]
    [DataRow("height")]
    [DataRow("short")]
    [DataRow("encoding")]
    [DataRow("paletteLength")]
    [DataRow("paletteColor")]
    public void RejectsMalformedArtAtLoad(string mutation)
    {
        string root = Prepare();
        try
        {
            string path = Path.Combine(root, "maps/cave-transition.json");
            var document = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
            switch (mutation)
            {
                case "schema": document["schemaVersion"] = 2; break;
                case "width": document["width"] = 255; break;
                case "height": document["height"] = 0; break;
                case "short": document["indices4Bpp"] = "AA=="; break;
                case "encoding": document["indices4Bpp"] = new string('!', document["indices4Bpp"]!.GetValue<string>().Length); break;
                case "paletteLength": document["whitePalette"]!.AsArray().RemoveAt(0); break;
                case "paletteColor": document["transitionPalette"]![0] = 32768; break;
                default: Assert.Fail("Unexercised mutation"); break;
            }
            File.WriteAllText(path, document.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => GeneratedCaveTransitionArt.Load(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void SourceDerivedPreviewMetadataDistinguishesCaveForestAndOrdinaryRegions()
    {
        var maps = PretRuntimeMapCatalog.Load(Path.Combine(Generated, "maps/world.json")).Maps.Values;
        Assert.IsTrue(maps.Single(map => map.SourceMapName == "MtMoon_1F").HasCavePreview);
        Assert.IsFalse(maps.Single(map => map.SourceMapName == "ViridianForest").HasCavePreview);
        Assert.IsFalse(maps.Single(map => map.SourceMapName == "Route4").HasCavePreview);
    }

    [TestMethod]
    public void MissingPreviewMetadataFailsInsteadOfAdmittingAnUnknownRegion()
    {
        var manifest = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Generated, "maps/world.json")))!;
        var entry = manifest["maps"]!.AsArray().First(row => row!["sourceMapSymbol"]!.GetValue<string>() == "MAP_MT_MOON_1F")!;
        var map = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Generated, entry["path"]!.GetValue<string>())))!.AsObject();
        string stableId = map["stableId"]!.GetValue<string>();
        Assert.IsTrue(map.Remove("cavePreviewVisitFlag"));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            PretRuntimeMapData.Decode(Encoding.UTF8.GetBytes(map.ToJsonString()), stableId));
    }

    private static string Prepare()
    {
        string root = Path.Combine(Path.GetTempPath(), "cave-transition-art-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "maps"));
        File.Copy(Path.Combine(Generated, "maps/cave-transition.json"), Path.Combine(root, "maps/cave-transition.json"));
        return root;
    }
}
