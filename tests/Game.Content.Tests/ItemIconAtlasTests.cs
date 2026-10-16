using Game.Content;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Game.Domain.Ids;

namespace FireRed.Tests.Content;

[TestClass]
public sealed class ItemIconAtlasTests
{
    private static ImmutableArray<ItemIcon> MachineIcons(string root) =>
        ItemIconAssets.LoadProject(root, "content/generated/data/machine-learning/pewter.json");

    [TestMethod]
    public void RequiredConsumerIconsRejectMissingOrWrongIdentityWithoutMachineCatalogs()
    {
        var packages = new Dictionary<string, string[]>
        {
            ["data/machine-learning/pewter.json"] = ["TM39", "TM_CASE"],
            ["data/machine-learning/bill.json"] = ["TM45", "TM43", "TM28"],
            ["data/machine-learning/cerulean.json"] = ["TM03"],
            ["data/machine-learning/route11.json"] = ["HM05"],
            ["data/items/rocktunnel/rocktunnel-machines.json"] = ["TM40"]
        };
        string root = TestAssets.Output();
        try
        {
            foreach (var (relative, symbols) in packages)
            {
                string path = Path.Combine(root, "icons.json");
                var source = JsonNode.Parse(File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated", relative)))!;
                var required = symbols.Select(symbol => new ItemId("pret:ITEM_" + symbol)).ToArray();
                File.WriteAllText(path, source.ToJsonString());
                CollectionAssert.AreEqual(required, ItemIconAssets.LoadProject(root, "icons.json", required).Select(icon => icon.Item).ToArray());
                var changed = source.DeepClone();
                changed["icons"]![0]!["item"]!["value"] = "pret:ITEM_POTION";
                File.WriteAllText(path, changed.ToJsonString());
                Assert.ThrowsExactly<InvalidDataException>(() => ItemIconAssets.LoadProject(root, "icons.json", required), relative);
                changed = source.DeepClone();
                changed["icons"]!.AsArray().RemoveAt(required.Length - 1);
                File.WriteAllText(path, changed.ToJsonString());
                Assert.ThrowsExactly<InvalidDataException>(() => ItemIconAssets.LoadProject(root, "icons.json", required), relative);
            }
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void TypedInventoryIconsLoadFromGeneratedMetadataAndOneNativePage()
    {
        string root = TestAssets.Output();
        try
        {
            foreach (string relative in new[]
            {
                "content/generated/data/services/early-shop/early-shop.json",
                "content/generated/data/items.json",
                "content/generated/data/machine-learning/pewter.json",
                "content/generated/data/services/silph-item-art.json",
                "content/generated/data/services/cinnabar-item-art/icons.json",
                "content/generated/data/services/cinnabar-item-art/manifest.json",
                "content/generated/sprites/items.png"
            })
            {
                string destination = Path.Combine(root, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(TestAssets.Root, relative), destination);
            }
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            var early = PretEarlyShop.Load(File.ReadAllText(Path.Combine(root, "content/generated/data/services/early-shop/early-shop.json")), SharedItemCatalog.LoadProject(root));
            var machines = MachineIcons(root);
            var silph = PretSilphItemArt.Load(root);
            var cinnabar = PretCinnabarItemArt.LoadProject(root);
            var paths = early.Icons.Select(i => (i.PngPath, i.Region))
                .Concat(machines.Concat(silph.Icons).Concat(cinnabar.Icons).Select(i => (i.PngPath, i.Region))).ToArray();
            Assert.AreEqual(37, paths.Length);
            Assert.AreEqual(1, paths.Select(i => i.PngPath).Distinct().Count());
            Assert.AreEqual("content/generated/sprites/items.png", paths[0].PngPath);
            Assert.IsTrue(File.Exists(Path.Combine(root, paths[0].PngPath)));
            foreach (var (_, region) in paths)
            {
                Assert.IsNotNull(region);
                Assert.AreEqual((24, 24), (region.Width, region.Height));
                Assert.IsTrue(region.X >= 1 && region.Y >= 1);
            }
            var ice = cinnabar.Icons.Single(i => i.Item.Value == "pret:ITEM_TM14");
            var fire = cinnabar.Icons.Single(i => i.Item.Value == "pret:ITEM_TM38");
            Assert.AreEqual(ice.PngPath, fire.PngPath);
            Assert.AreNotEqual(ice.Region, fire.Region);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public void MachineIconsAndGymAssetsLoadWithoutCanonicalDefinitionsOrHistoricalSelections()
    {
        string root = TestAssets.Output();
        try
        {
            var expected = new Dictionary<string, string[]>
            {
                ["data/machine-learning/pewter.json"] = ["TM39", "TM_CASE"],
                ["data/machine-learning/bill.json"] = ["TM45", "TM43", "TM28"],
                ["data/machine-learning/cerulean.json"] = ["TM03"],
                ["data/machine-learning/route11.json"] = ["HM05"],
                ["data/items/rocktunnel/rocktunnel-machines.json"] = ["TM40"]
            };
            string[] gymFiles = ["ui/vermilion-gym-assets/manifest.json", "ui/vermilion-gym-assets/assets.json",
                "ui/vermilion-gym-assets/engineer-front.png", "ui/vermilion-gym-assets/lt-surge-front.png"];
            foreach (string relative in expected.Keys.Concat(gymFiles).Append("sprites/items.png"))
            {
                string destination = Path.Combine(root, "content/generated", relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(Path.Combine(TestAssets.Root, "content/generated", relative), destination);
            }
            foreach (var (relative, items) in expected)
            {
                var icons = ItemIconAssets.LoadProject(root, "content/generated/" + relative);
                CollectionAssert.AreEqual(items.Select(item => "pret:ITEM_" + item).ToArray(),
                    icons.Select(icon => icon.Item.Value).ToArray(), relative);
                foreach (var icon in icons) AssertAtlasBounds(root, icon);
            }
            var gym = PretVermilionGymAssets.Load(root);
            Assert.AreEqual("pret:ITEM_TM34", gym.Document.Icon.Item.Value);
            AssertAtlasBounds(root, gym.Document.Icon);
            CollectionAssert.AreEqual(new[] { "engineer-front", "lt-surge-front" }, gym.Portraits.Select(p => p.Id).ToArray());
            foreach (var portrait in gym.Portraits)
                Assert.AreEqual((64, 64), PngDimensions(portrait.Png));
            Assert.IsFalse(File.Exists(Path.Combine(root, "content/generated/data/items.json")));
            Assert.IsFalse(File.Exists(Path.Combine(root, "content/generated/data/machines.json")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "content/raw")));
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "scripts")));
        }
        finally { Directory.Delete(root, true); }
    }

    private static (int Width, int Height) PngDimensions(byte[] png)
    {
        Assert.IsTrue(png.Length >= 24);
        Assert.IsTrue(png.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
        Assert.IsTrue(png.AsSpan(12, 4).SequenceEqual("IHDR"u8));
        return (BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4)),
            BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4)));
    }

    private static void AssertAtlasBounds(string root, ItemIcon icon)
    {
        Assert.AreEqual("content/generated/sprites/items.png", icon.PngPath);
        Assert.AreEqual((24, 24), (icon.Width, icon.Height));
        Assert.IsNotNull(icon.Region);
        Assert.AreEqual((24, 24), (icon.Region.Width, icon.Region.Height));
        var (width, height) = PngDimensions(GeneratedResourceFile.Read(root, icon.PngPath));
        Assert.IsTrue(icon.Region.X >= 0 && icon.Region.X + icon.Width <= width);
        Assert.IsTrue(icon.Region.Y >= 0 && icon.Region.Y + icon.Height <= height);
    }

    [TestMethod]
    public void IconDocumentsRejectMissingDuplicateAndRetiredMachineFields()
    {
        var icon = MachineIcons(TestAssets.Root)[0];
        string row = JsonSerializer.Serialize(icon, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        string valid = "{\"schemaVersion\":1,\"icons\":[" + row + "]}";
        Assert.AreEqual(icon, ItemIconAssets.Load(valid).Single());
        Assert.ThrowsExactly<InvalidDataException>(() => ItemIconAssets.Load("{\"schemaVersion\":1,\"icons\":[]}"));
        Assert.ThrowsExactly<InvalidDataException>(() => ItemIconAssets.Load(valid.Replace("\"schemaVersion\":1", "\"schemaVersion\":2")));
        Assert.ThrowsExactly<InvalidDataException>(() => ItemIconAssets.Load("{\"schemaVersion\":1,\"icons\":[" + row + "," + row + "]}"));
        Assert.ThrowsExactly<ContentValidationException>(() => ItemIconAssets.Load("{\"schemaVersion\":1}"));
        Assert.ThrowsExactly<ContentValidationException>(() => ItemIconAssets.Load(valid[..^1] + ",\"machines\":[]}"));
        Assert.ThrowsExactly<ContentValidationException>(() => ItemIconAssets.Load(valid[..^1] + ",\"icons\":[]}"));
    }

    [TestMethod]
    public void IconFilesRejectOversizedAndMalformedUtf8Input()
    {
        string root = TestAssets.Output();
        try
        {
            string path = Path.Combine(root, "icons.json");
            using (var stream = File.Create(path)) stream.SetLength(ContentJson.MaximumJsonBytes + 1L);
            Assert.ThrowsExactly<InvalidDataException>(() => ItemIconAssets.LoadProject(root, "icons.json"));
            File.WriteAllBytes(path, [0xff]);
            Assert.ThrowsExactly<DecoderFallbackException>(() => ItemIconAssets.LoadProject(root, "icons.json"));
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    [DataRow("/items.png")]
    [DataRow("../items.png")]
    [DataRow("sprites/../items.png")]
    [DataRow("sprites\\items.png")]
    [DataRow("sprites/items.png:stream")]
    [DataRow("sprites//items.png")]
    [DataRow("sprites/items.PNG")]
    public void UnsafeIconPathsAreRejected(string path) =>
        Assert.ThrowsExactly<InvalidDataException>(() => ItemIconAssets.Validate(MachineIcons(TestAssets.Root)[0] with { PngPath = path }));

    [TestMethod]
    public void InvalidLogicalRegionsAreRejectedBeforeTextureLoading()
    {
        var document = PretEarlyShop.Load(File.ReadAllText(Path.Combine(TestAssets.Root, "content/generated/data/services/early-shop/early-shop.json")), SharedItemCatalog.LoadProject(TestAssets.Root));
        Assert.ThrowsExactly<InvalidDataException>(() => PretEarlyShop.ValidateIcon(document.Icons[0] with { Region = new(1, 1, 23, 24) }));
        var machines = MachineIcons(TestAssets.Root);
        Assert.ThrowsExactly<InvalidDataException>(() => ItemIconAssets.Validate(machines[0] with { Region = new(-1, 1, 24, 24) }));
    }
}
