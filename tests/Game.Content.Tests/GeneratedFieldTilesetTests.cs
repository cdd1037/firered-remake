using System.Text.Json;
using Game.Content;
using FireRed.Tests;

namespace FireRed.Tests.Content;
[TestClass]
public sealed class GeneratedFieldTilesetTests
{
    [TestMethod]
    public void FieldDefinitionsLoadWithoutOriginalSourcesOrImportRecipes()
    {
        string root = Path.Combine(Path.GetTempPath(), "field-runtime-" + Guid.NewGuid());
        string generated = Path.Combine(root, "content", "generated");
        Directory.CreateDirectory(Path.Combine(generated, "maps/regions/silph"));
        try
        {
            File.Copy(Path.Combine(TestAssets.Root, "content/generated/maps/tilesets.json"), Path.Combine(generated, "maps/tilesets.json"));
            var table = GeneratedFieldTilesets.ForPackage(Path.Combine(generated, "maps/regions/silph"));
            using var data = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(generated, "maps/tilesets.json")));
            int checkedRows = 0;
            foreach (var layout in data.RootElement.GetProperty("layouts").EnumerateObject())
                for (int bank = 0; bank < 2; bank++)
                {
                    var rows = data.RootElement.GetProperty("tilesets").GetProperty(layout.Value[bank].GetString()!);
                    for (int i = 0; i < rows.GetArrayLength(); i++)
                    {
                        int tile = bank == 0 ? i : i + 640;
                        if (tile > 1023) continue;
                        var actual = table.Find(layout.Name, tile)!;
                        Assert.AreEqual(rows[i][0].GetInt32(), actual.Behavior);
                        Assert.AreEqual(rows[i][1].GetInt32(), actual.Terrain);
                        Assert.AreEqual(rows[i][2].GetInt32(), actual.EncounterType);
                        Assert.AreEqual(rows[i][3].GetInt32(), actual.LayerType);
                        checkedRows++;
                    }
                }
            Assert.IsTrue(checkedRows > 10000);
            Assert.IsNull(table.Find("unknown", 0));
            Assert.IsNull(table.Find("unknown", -1));
        }
        finally { Directory.Delete(root, true); }
    }
}
