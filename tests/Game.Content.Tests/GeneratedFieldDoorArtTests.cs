using System.Text.Json.Nodes;
using Game.Content;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class GeneratedFieldDoorArtTests
{
    private static string Generated => Path.Combine(TestAssets.Root, "content/generated");

    [TestMethod]
    public void SharedLayoutBindingHasSourceSoundAndThreeReplacementLayersPerOpenStage()
    {
        var art = GeneratedFieldDoorArt.Load(Generated);
        var door = art.Find("LAYOUT_PALLET_TOWN", 0x2a3);
        Assert.IsNotNull(door);
        Assert.AreEqual("SE_DOOR", door.SoundEffect);
        Assert.AreEqual("maps/door-animations.png", art.TexturePath);
        for (int frame = 0; frame < 3; frame++)
            for (int layer = 0; layer < 3; layer++)
                Assert.AreEqual(new ImageRegion(layer * 16, door.AtlasY + frame * 16, 16, 16), door.FrameRegion(frame, layer));
        Assert.IsNull(art.Find("LAYOUT_PALLET_TOWN_PLAYERS_HOUSE_1F", 0x2a3));
        Assert.IsNull(art.Find("LAYOUT_PALLET_TOWN", 0x2ac));
        Assert.IsNull(art.Find("unknown", 0x2a3));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => door.FrameRegion(-1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => door.FrameRegion(3, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => door.FrameRegion(0, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => door.FrameRegion(0, 3));
    }

    [TestMethod]
    public void LoadsOnlyGeneratedAssetsAndOwnsImmutableBindings()
    {
        string root = Prepare();
        try
        {
            var art = GeneratedFieldDoorArt.Load(root);
            var expected = art.Find("LAYOUT_PALLET_TOWN", 0x2a3);
            Assert.IsNotNull(expected);
            File.WriteAllText(Path.Combine(root, "maps/door-animations.json"), "{}");
            Assert.AreEqual(expected, art.Find("LAYOUT_PALLET_TOWN", 0x2a3));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow("duplicate")]
    [DataRow("path")]
    [DataRow("frameSize")]
    [DataRow("outsideAtlas")]
    [DataRow("unalignedAtlas")]
    [DataRow("metatile")]
    [DataRow("sound")]
    [DataRow("nullEntry")]
    public void RejectsInvalidBindingsBeforeRendering(string mutation)
    {
        string root = Prepare();
        try
        {
            string path = Path.Combine(root, "maps/door-animations.json");
            var document = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
            var entries = document["entries"]!.AsArray();
            var entry = entries[0]!.AsObject();
            switch (mutation)
            {
                case "duplicate": entries.Add(entry.DeepClone()); break;
                case "path": document["texturePath"] = "../door.png"; break;
                case "frameSize": entry["height"] = 32; break;
                case "outsideAtlas": entry["atlasY"] = document["height"]!.GetValue<int>(); break;
                case "unalignedAtlas": entry["atlasY"] = 1; break;
                case "metatile": entry["metatileId"] = 1024; break;
                case "sound": entry["soundEffect"] = "unknown"; break;
                case "nullEntry": entries[0] = null; break;
                default: Assert.Fail("Unexercised mutation"); break;
            }
            File.WriteAllText(path, document.ToJsonString());
            Assert.ThrowsExactly<InvalidDataException>(() => GeneratedFieldDoorArt.Load(root));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void RejectsMissingAndMismatchedPng()
    {
        string root = Prepare();
        try
        {
            string path = Path.Combine(root, "maps/door-animations.png");
            byte[] png = File.ReadAllBytes(path);
            png[19] = 47;
            File.WriteAllBytes(path, png);
            Assert.ThrowsExactly<InvalidDataException>(() => GeneratedFieldDoorArt.Load(root));
            File.Delete(path);
            Assert.ThrowsExactly<FileNotFoundException>(() => GeneratedFieldDoorArt.Load(root));
        }
        finally { Directory.Delete(root, true); }
    }

    private static string Prepare()
    {
        string root = Path.Combine(Path.GetTempPath(), "field-door-art-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "maps"));
        foreach (string name in new[] { "door-animations.json", "door-animations.png" })
            File.Copy(Path.Combine(Generated, "maps", name), Path.Combine(root, "maps", name));
        return root;
    }
}
