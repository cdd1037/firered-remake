using System.Text;
using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedMapPreviewCatalogTests
{
    private static string Generated => Path.Combine(TestAssets.Root, "content/generated");

    [TestMethod]
    public void GeneratedOnlyArtNamesAndVisitFlagsRemainImmutableAfterInputDeletion()
    {
        string root = Prepare();
        try
        {
            var catalog = GeneratedMapPreviewCatalog.Load(root);
            File.Delete(Path.Combine(root, "maps/map-previews.json"));
            Assert.IsTrue(catalog.Entries.Length > 0);
            var moon = catalog.Find("MAPSEC_MT_MOON");
            Assert.IsNotNull(moon);
            Assert.AreEqual("MT. MOON", moon.Name);
            Assert.AreEqual("FLAG_WORLD_MAP_MT_MOON_1F", moon.VisitFlag);
            Assert.IsNull(catalog.Find("MAPSEC_VIRIDIAN_FOREST"));
            Assert.IsNull(catalog.Find("MAPSEC_ROUTE_4"));
            foreach (var entry in catalog.Entries)
            {
                Assert.AreEqual(256, entry.Art.Width);
                Assert.AreEqual(160, entry.Art.Height);
                Assert.AreEqual(40960, entry.Art.Indices.Length);
                Assert.IsTrue(entry.Art.Indices.All(index => index < 32));
                Assert.IsTrue(entry.Art.Indices.Any(index => (index & 15) == 0));
                Assert.IsTrue(entry.Art.Indices.Any(index => (index & 15) != 0));
                Assert.AreEqual(32, entry.Art.Palette.Length);
                CollectionAssert.AreEqual(new ushort[] { 32734, 0, 27482 },
                    new[] { entry.Art.Palette[17], entry.Art.Palette[20], entry.Art.Palette[19] });
            }
            var monean = catalog.Find("MAPSEC_MONEAN_CHAMBER")!;
            var liptoo = catalog.Find("MAPSEC_LIPTOO_CHAMBER")!;
            Assert.AreNotEqual(monean.Name, liptoo.Name);
            Assert.AreEqual(monean.VisitFlag, liptoo.VisitFlag);
            Assert.AreSame(monean.Art, liptoo.Art);
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void EveryRuntimeMapUsesTheSameRegistryVisitFlagAsItsPreviewCatalog()
    {
        var previews = GeneratedMapPreviewCatalog.Load(Generated);
        var maps = PretRuntimeMapCatalog.Load(Path.Combine(Generated, "maps/world.json")).MapsByPath.Values;
        Assert.IsTrue(maps.Any());
        foreach (var map in maps)
        {
            var entry = previews.Find(map.Events.GetProperty("region_map_section").GetString()!);
            Assert.AreEqual(entry?.VisitFlag, map.CavePreviewVisitFlag, map.SourceMapName);
            Assert.AreEqual(entry is not null, map.HasCavePreview, map.SourceMapName);
        }
    }

    [TestMethod]
    [DataRow("schema")]
    [DataRow("emptyArt")]
    [DataRow("emptyEntries")]
    [DataRow("duplicate")]
    [DataRow("region")]
    [DataRow("flag")]
    [DataRow("name")]
    [DataRow("reference")]
    [DataRow("width")]
    [DataRow("height")]
    [DataRow("short")]
    [DataRow("encoding")]
    [DataRow("index")]
    [DataRow("paletteLength")]
    [DataRow("paletteColor")]
    [DataRow("unused")]
    public void RejectsMalformedCatalogAtLoad(string mutation)
    {
        string root = Prepare();
        try
        {
            string path = Path.Combine(root, "maps/map-previews.json");
            var doc = JsonNode.Parse(File.ReadAllBytes(path))!;
            var art = doc["art"]![0]!;
            var entry = doc["entries"]![0]!;
            switch (mutation)
            {
                case "schema": doc["schemaVersion"] = 2; break;
                case "emptyArt": doc["art"]!.AsArray().Clear(); break;
                case "emptyEntries": doc["entries"]!.AsArray().Clear(); break;
                case "duplicate": doc["entries"]!.AsArray().Add(entry.DeepClone()); break;
                case "region": entry["regionSection"] = ""; break;
                case "flag": entry["visitFlag"] = "FLAG_"; break;
                case "name": entry["name"] = "\n"; break;
                case "reference": entry["art"] = -1; break;
                case "width": art["width"] = 240; break;
                case "height": art["height"] = 256; break;
                case "short": art["indices8Bpp"] = "AA=="; break;
                case "encoding": art["indices8Bpp"] = new string('!', art["indices8Bpp"]!.GetValue<string>().Length); break;
                case "index":
                    var indices = Convert.FromBase64String(art["indices8Bpp"]!.GetValue<string>());
                    indices[0] = 32;
                    art["indices8Bpp"] = Convert.ToBase64String(indices);
                    break;
                case "paletteLength": art["palette"]!.AsArray().RemoveAt(0); break;
                case "paletteColor": art["palette"]![0] = 32768; break;
                case "unused": doc["art"]!.AsArray().Add(art.DeepClone()); break;
                default: Assert.Fail("Unexercised mutation"); break;
            }
            File.WriteAllText(path, doc.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => GeneratedMapPreviewCatalog.Load(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("FLAG_")]
    [DataRow("FLAG_not_valid")]
    public void InvalidMapPreviewVisitFlagFails(string flag)
    {
        var index = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Generated, "maps/world.json")))!;
        var entry = index["maps"]!.AsArray().First(row => row!["sourceMapSymbol"]!.GetValue<string>() == "MAP_MT_MOON_1F")!;
        var map = JsonNode.Parse(File.ReadAllBytes(Path.Combine(Generated, entry["path"]!.GetValue<string>())))!;
        map["cavePreviewVisitFlag"] = flag;
        Assert.ThrowsExactly<InvalidDataException>(() => PretRuntimeMapData.Decode(
            Encoding.UTF8.GetBytes(map.ToJsonString()), map["stableId"]!.GetValue<string>()));
    }

    private static string Prepare()
    {
        string root = Path.Combine(Path.GetTempPath(), "map-preview-art-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "maps"));
        File.Copy(Path.Combine(Generated, "maps/map-previews.json"), Path.Combine(root, "maps/map-previews.json"));
        return root;
    }
}
